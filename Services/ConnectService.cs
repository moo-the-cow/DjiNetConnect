using djiconnect.EventHandlers;
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
    private static IDisposable _propertiesWatcher;
    private readonly Serilog.ILogger _logger = Log.Logger;
    public async Task StartAsync(CancellationToken cancellationToken)
    {

        int scanSeconds = 10;
        IAdapter1 adapter;
        //if (args.Length > 1)
        //{
        //    adapter = await BlueZManager.GetAdapterAsync("hci0");
        //}
        //else
        {
            var adapters = await BlueZManager.GetAdaptersAsync();
            if (adapters.Count == 0)
            {
                throw new Exception("No Bluetooth adapters found.");
            }

            adapter = adapters.First();
        }

        var adapterPath = adapter.ObjectPath.ToString();
        var adapterName = adapterPath.Substring(adapterPath.LastIndexOf("/") + 1);
        _logger.Information($"Using Bluetooth adapter {adapterName}");
        _logger.Information($"Adapter's full path:    {adapterPath}");
        // Print out the devices we already know about.
        _logger.Information("Getting known devices...");
        string djiBlutoothAddress = string.Empty;
        string djiDeviceName = string.Empty;
        var devices = await adapter.GetDevicesAsync();
        foreach (var device in devices)
        {
            try
            {
                string deviceDescription = await DeviceUtils.GetDeviceDescriptionAsync(device);
                //_logger.Information($" - {deviceDescription}");
                string name = await device.GetNameAsync();
                if (name.StartsWith("Osmo"))
                {
                    djiDeviceName = name;
                    string address = await device.GetAddressAsync();
                    _logger.Information($"name: {name} ({address})");
                    djiBlutoothAddress = address;
                }
            }
            catch (Exception ex) { _logger.Information(ex.Message); }
        }

        _logger.Information($"Found {devices.Count} paired device(s).");

        // Scan for more devices.
        _logger.Information($"Scanning for {scanSeconds} seconds...");

        int newDevices = 0;
        using (await adapter.WatchDevicesAddedAsync(async device =>
        {
            newDevices++;
            // Write a message when we detect new devices during the scan.
            string deviceDescription = await DeviceUtils.GetDeviceDescriptionAsync(device);
            try
            {
                _logger.Information($"[NEW] {deviceDescription}");
                string name = await device.GetNameAsync();
                if (name.StartsWith("Osmo"))
                {
                    djiDeviceName = name;
                    string address = await device.GetAddressAsync();
                    _logger.Information($"name: {name} ({address})");
                    djiBlutoothAddress = address;
                }
            }
            catch (Exception ex) { _logger.Information(ex.Message); }
        }))
        {
            await adapter.StartDiscoveryAsync();
            await Task.Delay(TimeSpan.FromSeconds(scanSeconds));
            await adapter.StopDiscoveryAsync();
        }

        _logger.Information($"Scan complete. {newDevices} new device(s) found.");


        //the actual device after having it listed
        // Find the Bluetooth peripheral.
        _logger.Information(await adapter.GetNameAsync());
        var djiDevice = await adapter.GetDeviceAsync(djiBlutoothAddress); //TODO fetch via above and filtered Starts with OSMO
        if (djiDevice == null)
        {
            _logger.Information($"Bluetooth '{djiDeviceName}' with address '{djiBlutoothAddress}' not found. Use `bluetoothctl` or Bluetooth Manager to scan and possibly pair first.");
            return;
        }
        // Check if already paired
        //HINT: Bluetooth Low Energy (BLE) has no pairing via standard linux - its proprietary in case of DJI
        /*
        var paired = await djiDevice.GetPairedAsync();
        if (paired)
        {
          _logger.Information($"Device '{djiDeviceName} ({djiBlutoothAddress})' is already paired!");
          //await adapter.RemoveDeviceAsync(djiDevice.ObjectPath);
        }
        */
        //_logger.Information("Pairing device...");
        //await djiDevice.PairAsync();
        _logger.Information("Connecting...");
        await djiDevice.ConnectAsync();
        await djiDevice.WaitForPropertyValueAsync("Connected", value: true, timeout);
        var deviceObjPath = djiDevice.ObjectPath.ToString();
        _logger.Information($"Connected ({deviceObjPath}).");

        _logger.Information("Waiting for services to resolve...");
        await djiDevice.WaitForPropertyValueAsync("ServicesResolved", value: true, timeout);

        var servicesUUID = await djiDevice.GetUUIDsAsync();
        _logger.Information($"Device offers {servicesUUID.Length} service(s).");
        //var props = new Device1Properties();
        if (servicesUUID is not null)
        {
            var writeOptions = new Dictionary<string, object>
      {
          { "type", "command" }  // Write without response. type can also be "request" for a response
      };
            string pin = "1234"; // Change to your device PIN
            string rtmpUrl = "rtmp://10.0.1.20:1936/irlbox/live";
            string wifiSsid = "section-9_5G-1";
            string wifiPassword = "r11m!!oL0stiegl0";
            IGattCharacteristic1 writeCharacteristic = null;
            IGattCharacteristic1 notifyCharacteristic = null;
            foreach (var svc in servicesUUID)
            {
                _logger.Information($"- Uuid: {svc}");
                if (svc == "0000fff0-0000-1000-8000-00805f9b34fb") // DJI custom service
                {
                    // Get the GATT service
                    IGattService1 service = await djiDevice.GetServiceAsync(svc);
                    IReadOnlyList<IGattCharacteristic1> characteristics = await service.GetCharacteristicsAsync();
                    foreach (IGattCharacteristic1 characteristic in characteristics)
                    {
                        string characteristicUUID = await characteristic.GetUUIDAsync();
                        _logger.Information($"Charateristics Uuid: {characteristicUUID}");
                        IGattCharacteristic1 characteristicItem = await service.GetCharacteristicAsync(characteristicUUID);
                        _logger.Information(characteristicItem.ObjectPath.ToString());
                        GattCharacteristic1Properties properties = await characteristicItem.GetAllAsync();
                        //_logger.Information(BitConverter.ToString(properties.Value));
                        /*foreach (string flag in properties.Flags)
                        {
                          _logger.Information(flag);
                        }*/
                        if (properties.Flags.Contains("write") || properties.Flags.Contains("write-without-response"))
                        {
                            writeCharacteristic = characteristic;
                            _logger.Information($"Found write characteristic");
                            // Look for notify characteristic
                            if (properties.Flags.Contains("notify") || properties.Flags.Contains("indicate"))
                            {
                                notifyCharacteristic = characteristic;
                                _logger.Information($"Found notify characteristic");
                                byte[] initialValue = await notifyCharacteristic.ReadValueAsync(writeOptions);
                                if (initialValue != null && initialValue.Length > 0)
                                {
                                    _logger.Information($"INITIAL VALUE: {BitConverter.ToString(initialValue)}");
                                }
                                else
                                {
                                    _logger.Information("Initial value is null or empty");
                                }
                            }
                            break;
                        }
                    }
                    if (writeCharacteristic == null)
                    {
                        _logger.Information("No write characteristic found!");
                        await djiDevice.DisconnectAsync();
                        return;
                    }
                    // Set up notification handler if available
                    if (notifyCharacteristic != null)
                    {

                        // Enable notifications
                        await notifyCharacteristic.StartNotifyAsync();
                        //await notifyCharacteristic.ReadValueAsync(writeOptions);

                        // Subscribe to property changes using the correct API
                        _propertiesWatcher = await notifyCharacteristic.WatchPropertiesAsync(DjiEventHandler.OnPropertiesChanged);
                        _logger.Information("Notifications enabled");
                    }
                    else
                    {
                        _logger.Information("No notify characteristic found - will not receive responses");
                    }

                    //TEST
                    // Manual verification of initiate command
                    /*
                    byte[] initiatePacket = new byte[] { 0x55, 0x0e, 0x04, 0x66, 0x02, 0x08, 0x12, 0x8c, 0x40, 0x02, 0xe1, 0x1a };
                    byte[] calculatedCrc = DjiCrcUtils.Crc16(initiatePacket);
                    byte[] actualCrc = new byte[] { 0x11, 0xdf };

                    _logger.Information($"Initiate packet CRC:");
                    _logger.Information($"Calculated: {BitConverter.ToString(calculatedCrc)}");
                    _logger.Information($"Actual: {BitConverter.ToString(actualCrc)}");
                    _logger.Information($"Match: {calculatedCrc.SequenceEqual(actualCrc)}");
                    */
                    //TEST2
                    // Test with initiate packet data (without CRC)
                    /*
                    byte[] initiateData = new byte[] { 0x55, 0x0e, 0x04, 0x66, 0x02, 0x08, 0x12, 0x8c, 0x40, 0x02, 0xe1, 0x1a };
                    byte[] crc = DjiCrcUtils.Crc16(initiateData);
                    _logger.Information($"Fixed CRC16: {BitConverter.ToString(crc)}");
                    _logger.Information($"Should be: 11-DF");
                    _logger.Information($"Match: {crc[0] == 0x11 && crc[1] == 0xDF}");
                    */
                    {
                        // Test with a simple known value
                        byte[] testData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                        byte[] crc = DjiCrcUtils.Crc16(testData);
                        _logger.Information($"CRC16 test result: {BitConverter.ToString(crc)}");

                        // Expected value for CRC-16/CCITT-FALSE with this input is 0x0C13
                        // If you don't get this, your algorithm is wrong
                    }

                    // At the start of your communication sequence
                    //DjiPacketStructure.ResetSequence();
                    byte[] count = new byte[] { 0x00, 0x00 };

                    _logger.Information("Sending initialization command...");
                    byte[] initCommand = DjiCommandUtils.CreateInitiateCommand();
                    DjiUtils.DebugCommand(initCommand, "Send Init");
                    await writeCharacteristic.WriteValueAsync(initCommand, writeOptions);
                    await Task.Delay(TimeSpan.FromSeconds(1));

                    _logger.Information("Sending authentication...");
                    byte[] authCommand = DjiCommandUtils.CreateAuthCommand(pin, count);
                    DjiUtils.DebugCommand(authCommand, "Send authentication");
                    await writeCharacteristic.WriteValueAsync(authCommand, writeOptions);
                    count = DjiUtils.GetNextCount(count); // Increment count after auth
                    await Task.Delay(TimeSpan.FromSeconds(3));

                    //DjiPacketStructure.ResetSequence();

                    _logger.Information("Configuring WiFi...");
                    byte[] wifiCommand = DjiCommandUtils.CreateWifiConfigCommand(wifiSsid, wifiPassword);
                    DjiUtils.DebugCommand(wifiCommand, "Send Wifi Config");
                    await writeCharacteristic.WriteValueAsync(wifiCommand, writeOptions);
                    await Task.Delay(TimeSpan.FromSeconds(3));

                    // Test with known data from Node.js
                    /*
                    byte[] testData = new byte[] { 0x55, 0x3C, 0x04, 0x52, 0x02, 0x08, 0xBE, 0xEA, 0x40, 0x08, 0x78, 0x00, 0x27, 0x00, 0x0A, 0x70, 0x0F, 0xA0, 0x01, 0x03, 0x00, 0x00, 0x00, 0x21, 0x00, 0x72, 0x74, 0x6D, 0x70, 0x3A, 0x2F, 0x2F, 0x31, 0x30, 0x2E, 0x30, 0x2E, 0x31, 0x2E, 0x32, 0x30, 0x3A, 0x31, 0x39, 0x33, 0x36, 0x2F, 0x69, 0x72, 0x6C, 0x62, 0x6F, 0x78, 0x2F, 0x6C, 0x69, 0x76, 0x65 };
                    byte[] crc = DjiCrcUtils.Crc16(testData);
                    _logger.Information($"Calculated CRC: {BitConverter.ToString(crc)}");
                    */

                    // test 2
                    /*
                    byte[] eisTestData = new byte[] { 0x01, 0x01, 0x08, 0x00, 0x01, 0x01, 0xf0, 0x72 };
                    byte[] eisTestPayload = DjiPacketStructure.BuildDjiFrame(
                        command: new byte[] { 0x02, 0x01 },
                        id: new byte[] { 0x01, 0x00 }, // Use the exact ID from your hex output
                        type: new byte[] { 0x40, 0x02, 0x8e },
                        data: eisTestData
                    );
                    DjiUtils.DebugCommand(eisTestPayload, "EIS Test");
                    */
                    //DjiPacketStructure.ResetSequence();
                    _logger.Information("Setting RTMP configuration...");
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
                    count = DjiUtils.GetNextCount(count); // Increment count after auth
                    await Task.Delay(TimeSpan.FromSeconds(1));

                    //DjiPacketStructure.ResetSequence();

                    _logger.Information("Starting broadcast...");
                    byte[] startCommand = DjiCommandUtils.CreateStartBroadcastCommand();
                    DjiUtils.DebugCommand(startCommand, "Start Broadcast");
                    await writeCharacteristic.WriteValueAsync(startCommand, writeOptions);
                    await Task.Delay(TimeSpan.FromSeconds(1));

                    _logger.Information("Broadcast started successfully!");

                    // Keep connection open
                    _logger.Information("Press any key to stop broadcasting and disconnect...");
                    Console.ReadKey();

                    _logger.Information("Stopping broadcast...");
                    count = DjiUtils.GetNextCount(count); // Increment count after auth
                                                          //byte[] stopCommand = DjiCommandUtils.CreateStopStreamingCommand3(count);
                    byte[] stopCommand = DjiCommandUtils.CreateStopBroadcastCommandNew(count);
                    DjiUtils.DebugCommand(stopCommand, "Stop Broadcast");
                    await writeCharacteristic.WriteValueAsync(stopCommand, writeOptions);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Warning("Stopping Chathub Client setting IsChatHubClientStarted to false");
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

    /*
    private static async void OnCharacteristicValueChanged(IGattCharacteristic1 characteristic, GattCharacteristicValueEventArgs args)
    {
      try
      {
        byte[] data = args.Value.ToArray();
        var notification = DjiNotificationParser.ParseNotify(data);

        _logger.Information($"Received: {notification}");

        // Handle specific responses
        if (notification.AuthSuccess.HasValue)
        {
          if (notification.AuthSuccess.Value)
          {
            _logger.Information("✓ Device authenticated successfully!");
          }
          else
          {
            _logger.Information("✗ Authentication failed! Check your PIN.");
          }
        }

        if (notification.StreamingStatus.HasValue)
        {
          switch (notification.StreamingStatus.Value)
          {
            case 0x00:
              _logger.Information("✓ Streaming stopped");
              break;
            case 0x01:
              _logger.Information("✓ Streaming started successfully!");
              break;
            case 0x02:
              _logger.Information("⚠ Streaming in progress...");
              break;
            case 0x03:
              _logger.Information("⚠ Streaming preparing...");
              break;
            case 0x04:
              _logger.Information("✗ Streaming failed - check RTMP URL");
              break;
            default:
              _logger.Information($"Streaming status: 0x{notification.StreamingStatus.Value:X2}");
              break;
          }
        }
      }
      catch (Exception ex)
      {
        _logger.Information($"Error handling notification: {ex.Message}");
      }
    }
    */

    private void OnApplicationStopping()
    {
        _isShuttingDown = true;
    }

    /*
    public static byte[] CreateStopStreamingCommand()
    {
      return new byte[] {
          0x55, 0xAA, 0x04, 0x00, // Header
          0x03, 0x00, 0x00, 0x00  // Stop streaming command
      };
    }

    public static byte[] CreateStartStreamingCommand()
    {
      return new byte[] {
            0x55, 0xAA, 0x04, 0x00, // Header
            0x02, 0x00, 0x00, 0x00  // Start streaming command
        };
    }
      */
    // Set RTMP URL (from rtmp.js)
    /*
    public static byte[] CreateRtmpUrlCommand(string rtmpUrl)
    {
      // URL format: rtmp://your-server.com/live/stream-key
      byte[] urlBytes = Encoding.UTF8.GetBytes(rtmpUrl);

      // Command structure: [header] + [url bytes] + [null terminator]
      byte[] command = new byte[8 + urlBytes.Length + 1];

      // Header (example - actual values may vary based on protocol)
      command[0] = 0x55; // Command type
      command[1] = 0xAA; // 
      command[2] = (byte)(urlBytes.Length + 1); // Length
      command[3] = 0x01; // Config type (RTMP URL)

      // Copy URL bytes
      Buffer.BlockCopy(urlBytes, 0, command, 4, urlBytes.Length);

      // Null terminator
      command[4 + urlBytes.Length] = 0x00;

      return command;
    }
  */
    /*
 private static async Task AdapterDeviceFoundAsync(IAdapter1 sender, DeviceFoundEventArgs e)
{
    _logger.Information($"Device found: {e.Device.Name} ({e.Device.Address})");

    // Optionally connect to the device
    await e.Device.ConnectAsync();

    // Assuming the device supports a specific service, read a characteristic
    string serviceUUID = "0000180a-0000-1000-8000-00805f9b34fb"; // Example service UUID
    string characteristicUUID = "00002a24-0000-1000-8000-00805f9b34fb"; // Example characteristic UUID

    // Get the GATT service and characteristic
    IGattService1 service = await e.Device.GetServiceAsync(serviceUUID);
    IGattCharacteristic1 characteristic = await service.GetCharacteristicAsync(characteristicUUID);

    // Read the characteristic value
    byte[] value = await characteristic.ReadValueAsync();
    string modelName = Encoding.UTF8.GetString(value);
    _logger.Information($"Model Name: {modelName}");

    // Optionally disconnect after reading
    await e.Device.DisconnectAsync();
}
*/
}
