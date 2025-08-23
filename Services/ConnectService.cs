using djiconnect.Utils;
using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace djiconnect.Services;
public class ConnectService : IHostedService, IAsyncDisposable
{
  static TimeSpan timeout = TimeSpan.FromSeconds(15);
  private static bool _isShuttingDown;
  //private static IDisposable? _propertiesWatcher;
  private readonly Serilog.ILogger _logger = Log.Logger;
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    int scanMaxSeconds = 10;
    IAdapter1 adapter;
    //if (args.Length > 1)
    //{
    //    adapter = await BlueZManager.GetAdapterAsync("hci0");
    //}
    //else
    var adapters = await BlueZManager.GetAdaptersAsync();
    if (adapters.Count == 0)
    {
      throw new Exception("No Bluetooth adapters found.");
    }

    adapter = adapters.First();
    var discoveryFilter = new Dictionary<string, object>
    {
        { "Transport", "le" }  // This is the key for LE scanning
    };
    await adapter.SetDiscoveryFilterAsync(discoveryFilter);

    var adapterPath = adapter.ObjectPath.ToString();
    var adapterName = adapterPath.Substring(adapterPath.LastIndexOf("/") + 1);
    _logger.Debug($"Using Bluetooth adapter {adapterName}");
    _logger.Debug($"Adapter's full path:    {adapterPath}");
    // Print out the devices we already know about.
    _logger.Debug("Getting known devices...");
    string djiBlutoothAddress = string.Empty;
    string djiDeviceName = string.Empty;
    var devices = await adapter.GetDevicesAsync();
    foreach (var device in devices)
    {
      try
      {
        string deviceDescription = await DeviceUtils.GetDeviceDescriptionAsync(device);
        //_logger.Debug($" - {deviceDescription}");
        string name = await device.GetNameAsync();
        //TODO for now only OA4 but yeah we could do "Osmo"
        if (name.StartsWith("Osmo")) // can be OsmoPocket3 or OsmoAction4 or OsmoAction3 (also for more control)
        {
          djiDeviceName = name;
          string address = await device.GetAddressAsync();
          _logger.Debug($"name: {name} ({address})");
          djiBlutoothAddress = address;
        }
      }
      catch (Exception ex) { _logger.Debug(ex.Message); }
    }

    _logger.Debug($"Found {devices.Count} paired device(s).");

    // Scan for more devices.
    _logger.Debug($"Scanning for max. {scanMaxSeconds} seconds...");

    int newDevices = 0;
    //CancellationTokenSource deviceLoopupCancelTokenSource = new();
    using (await adapter.WatchDevicesAddedAsync(async device =>
    {
      newDevices++;
      // Write a message when we detect new devices during the scan.
      string deviceDescription = await DeviceUtils.GetDeviceDescriptionAsync(device);
      try
      {
        _logger.Debug($"[NEW] {deviceDescription}");
        string name = await device.GetNameAsync();
        //TODO for now only OA4 but yeah we could do "Osmo"
        if (name.StartsWith("Osmo")) // can be OsmoPocket3 or OsmoAction4 or OsmoAction3 (also for more control)
        {
          djiDeviceName = name;
          string address = await device.GetAddressAsync();
          _logger.Debug($"name: {name} ({address})");
          djiBlutoothAddress = address;
          //deviceLoopupCancelTokenSource.Cancel();
        }
      }
      catch (Exception ex) { _logger.Debug(ex.Message); }
    }))
    {
      await adapter.StartDiscoveryAsync();
      /*
      try
      {
        await Task.Delay(TimeSpan.FromMinutes(scanMaxMinutes), deviceLoopupCancelTokenSource.Token);
        _logger.Debug("Scan completed due to timeout");
      }
      catch (TaskCanceledException)
      {
        _logger.Debug("Scan cancelled - device found!");
      }
      finally
      {
        await adapter.StopDiscoveryAsync();
      }
      */
      await Task.Delay(TimeSpan.FromSeconds(scanMaxSeconds));
      await adapter.StopDiscoveryAsync();
    }

    _logger.Debug($"Scan complete. {newDevices} new device(s) found.");


    //the actual device after having it listed
    // Find the Bluetooth peripheral.
    _logger.Debug(await adapter.GetNameAsync());
    var djiDevice = await adapter.GetDeviceAsync(djiBlutoothAddress); //TODO fetch via above and filtered Starts with OSMO
    if (djiDevice == null)
    {
      _logger.Debug($"Bluetooth '{djiDeviceName}' with address '{djiBlutoothAddress}' not found. Use `bluetoothctl` or Bluetooth Manager to scan and possibly pair first.");
      return;
    }
    // Check if already paired
    //HINT: Bluetooth Low Energy (BLE) has no pairing via standard linux - its proprietary in case of DJI
    /*
    var paired = await djiDevice.GetPairedAsync();
    if (paired)
    {
    _logger.Debug($"Device '{djiDeviceName} ({djiBlutoothAddress})' is already paired!");
    //await adapter.RemoveDeviceAsync(djiDevice.ObjectPath);
    }
    */
    //_logger.Debug("Pairing device...");
    //await djiDevice.PairAsync();
    _logger.Debug("Connecting...");
    await djiDevice.ConnectAsync();
    await djiDevice.WaitForPropertyValueAsync("Connected", value: true, timeout);
    var deviceObjPath = djiDevice.ObjectPath.ToString();
    _logger.Debug($"Connected ({deviceObjPath}).");

    _logger.Debug("Waiting for services to resolve...");
    await djiDevice.WaitForPropertyValueAsync("ServicesResolved", value: true, timeout);

    var servicesUUID = await djiDevice.GetUUIDsAsync();
    _logger.Debug($"Device offers {servicesUUID.Length} service(s).");
    //var props = new Device1Properties();
    if (servicesUUID is not null)
    {
      /* types for write:
      "command": Fire-and-forget. Device doesn't acknowledge. Fastest.
      "request": Device must acknowledge receipt. More reliable.
      "reliable": For large data, ensures delivery.
      */
      var writeOptions = new Dictionary<string, object>
      {
        //{ "type", "command" }  // Write without response.  type can also be "request" for a response
      };
      var notifyOptions = new Dictionary<string, object>
      {
        // Usually EMPTY for simple reads
        // { "offset", 0 }  // Optional: read from specific byte offset
      };
      string pin = "1234"; // Change to your device PIN
      string rtmpUrl = "rtmp://10.0.1.20:1936/irlbox/live";
      string wifiSsid = "section-9_5G-1";
      string wifiPassword = "r11m!!oL0stiegl0";
      IGattCharacteristic1? writeCharacteristic = null;
      IGattCharacteristic1? notifyCharacteristic = null;
      //string djiNonGenericServiceUUID = "0000fff0-0000-1000-8000-00805f9b34fb";
      bool writeServiceFound = false;
      bool notifyServiceFound = false;
      foreach (string svc in servicesUUID)
      {
        _logger.Debug($"- Uuid: {svc}");
        if (svc.StartsWith("0000fff")) // DJI devices are known to use service FFF0 with characteristics FFF3, FFF4, FFF5
        {
          // Get the GATT service
          IGattService1 service = await djiDevice.GetServiceAsync(svc);
          IReadOnlyList<IGattCharacteristic1> characteristics = await service.GetCharacteristicsAsync();
          foreach (IGattCharacteristic1 characteristic in characteristics)
          {
            string characteristicUUID = await characteristic.GetUUIDAsync();
            _logger.Debug($"Charateristics Uuid: {characteristicUUID}");
            IGattCharacteristic1 characteristicItem = await service.GetCharacteristicAsync(characteristicUUID);
            _logger.Debug(characteristicItem.ObjectPath.ToString());
            GattCharacteristic1Properties properties = await characteristicItem.GetAllAsync();
            _logger.Debug(string.Join(",", properties.Flags));
            //read,write-without-response,write,notify,indicate
            /*
                FFF5: Write characteristic - for sending commands to the device
                FFF4: Notify characteristic - for receiving responses from the device
                FFF3: Unknown - possibly for additional functionality
            */
            if (characteristicUUID.StartsWith("0000fff5") && !writeServiceFound && (properties.Flags.Contains("write") || properties.Flags.Contains("write-without-response")))
            {
              writeServiceFound = true;
              writeCharacteristic = characteristic;
              _logger.Debug($"Found write characteristic");
            }
            // Look for notify characteristic
            if (characteristicUUID.StartsWith("0000fff4") && !notifyServiceFound && (properties.Flags.Contains("notify") || properties.Flags.Contains("indicate")))
            {
              notifyServiceFound = true;
              notifyCharacteristic = characteristic;
              _logger.Debug($"Found notify characteristic");
            }
          }
        }
      }
      if (writeCharacteristic == null)
      {
        _logger.Debug("❌No write characteristic found!");
        await djiDevice.DisconnectAsync();
        return;
      }
      _logger.Debug("✅Sending Commands enabled!");
      // Set up notification handler if available
      if (notifyCharacteristic == null)
      {
        _logger.Debug("❌No notification characteristic found!");
        await djiDevice.DisconnectAsync();
        return;
      }
      #region Notification
      await notifyCharacteristic.StartNotifyAsync();
      await notifyCharacteristic.WatchPropertiesAsync((changes) =>
      {
        foreach (var change in changes.Changed.Where(x => x.Key.Equals("Value") && x.Value is byte[] data))
        {
          if (change.Key == "Value" && change.Value is byte[] data)
          {
            //HINT: dont delete - use it to analyze notifications
            //DjiParseUtils.AnalyzeResponse(data);
            DjiParseUtils.ParseNotificationResponse(data);
          }
        }
      });
      _logger.Debug("✅Notifications enabled!");
      #endregion
      
      byte[] count = new byte[] { 0x00, 0x00 };
      #region Init
      _logger.Debug("Sending initialization command...");
      byte[] initCommand = DjiCommandUtils.CreateInitiateCommand();
      DjiUtils.DebugCommand(initCommand, "Send Init");
      await writeCharacteristic.WriteValueAsync(initCommand, writeOptions);
      DjiUtils.DebugCommand(initCommand, "..init done - waiting a bit");
      await Task.Delay(TimeSpan.FromSeconds(1));
      #endregion

      #region Auth
      _logger.Debug("Sending authentication...");
      byte[] authCommand = DjiCommandUtils.CreateAuthCommand(pin, count);
      DjiUtils.DebugCommand(authCommand, "Send authentication");
      await writeCharacteristic.WriteValueAsync(authCommand, writeOptions);
      DjiUtils.DebugCommand(initCommand, "..auth done - waiting a bit");
      count = DjiUtils.GetNextCount(count); // Increment count after auth
      await Task.Delay(TimeSpan.FromSeconds(3));
      #endregion

      #region Wifi
      _logger.Debug("Configuring WiFi...");
      byte[] wifiCommand = DjiCommandUtils.CreateWifiConfigCommand(wifiSsid, wifiPassword);
      DjiUtils.DebugCommand(wifiCommand, "Send Wifi Config");
      await writeCharacteristic.WriteValueAsync(wifiCommand, writeOptions);
      DjiUtils.DebugCommand(initCommand, "..wifi done - waiting a bit");
      await Task.Delay(TimeSpan.FromSeconds(5));
      #endregion

      #region RTMP Config
      _logger.Debug("Setting RTMP configuration...");
      byte[] rtmpCommand = DjiCommandUtils.CreateRtmpConfigCommand(
        url: rtmpUrl,
        count: count,
        bitrateKbps: 4000,    // 4 Mbps
        resolutionCode: 0x0a, // FHD (1080p)
        fpsCode: 0x03,        // 30 FPS
        auto: true,
        eisCode: 0x01         // EIS enabled
      );
      await writeCharacteristic.WriteValueAsync(rtmpCommand, writeOptions);
      DjiUtils.DebugCommand(initCommand, "..RTMP config done - waiting a bit");
      count = DjiUtils.GetNextCount(count); // Increment count after auth
      await Task.Delay(TimeSpan.FromSeconds(5));
      #endregion

      #region Start stream
      _logger.Debug("Starting broadcast...");
      byte[] startCommand = DjiCommandUtils.CreateStartBroadcastCommand();
      DjiUtils.DebugCommand(startCommand, "Start Broadcast");
      await writeCharacteristic.WriteValueAsync(startCommand, writeOptions);
      DjiUtils.DebugCommand(initCommand, "..broadcast done - waiting a bit");
      await Task.Delay(TimeSpan.FromSeconds(1));
      #endregion

      // Keep connection open
      _logger.Debug("Press any key to stop broadcasting and disconnect...");
      Console.ReadKey();
      #region Stop stream
      _logger.Debug("Stopping broadcast...");
      //count = DjiUtils.GetNextCount(count); // Increment count after auth
      //byte[] stopCommand = DjiCommandUtils.CreateStopStreamingCommand3(count);
      //byte[] stopCommand = DjiCommandUtils.CreateStopBroadcastCommandNew(count);
      /*
      byte[] stopCommand = DjiCommandUtils.CreateStopBroadcastCommand();
      DjiUtils.DebugCommand(stopCommand, "Stop Broadcast");
      await writeCharacteristic.WriteValueAsync(stopCommand, writeOptions);
      await Task.Delay(TimeSpan.FromSeconds(1));
      */
      //TESTS
      //byte[] stopCommand = DjiCommandUtils.CreateStopBroadcastCommand();
      byte[] stopCommand = DjiCommandUtils.CreateStopBroadcastCommand();
      DjiUtils.DebugCommand(stopCommand, "Stop Broadcast");
      await writeCharacteristic.WriteValueAsync(stopCommand, new Dictionary<string, object>());
      //byte[] moblinStopCommand = DjiCommandUtils.CreateMoblinStyleStopCommand();
      //await writeCharacteristic.WriteValueAsync(moblinStopCommand, new Dictionary<string, object>());

      // Create stop command
      /*
      var stopCommand = new byte[] { 0x55, 0xAA, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x53 };

      // Write to characteristic
      
      writeOptions = new Dictionary<string, object>
      {
        { "stopStreamingType", 0x8E0240 }  //stop stream type
      };
      await writeCharacteristic.WriteValueAsync(stopCommand, writeOptions);
      */
      #endregion
    }
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.Warning("Stopping Dji Application");
    await Task.CompletedTask;
  }

  public async ValueTask DisposeAsync()
  {
    _logger.Warning("Disposing Background Service");
    if (_isShuttingDown)
    {
      _logger.Warning("Application is shutting down...");
    }
    await Task.CompletedTask;
  }
  public void OnApplicationStopping()
  {
    _isShuttingDown = true;
  }
}
