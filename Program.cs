using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

class Program
{
  static TimeSpan timeout = TimeSpan.FromSeconds(15);
  private static IDisposable _propertiesWatcher;
  static async Task Main(string[] args)
  {
    int scanSeconds = 10;
    IAdapter1 adapter;
    if (args.Length > 1)
    {
      adapter = await BlueZManager.GetAdapterAsync("hci0");
    }
    else
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
    Console.WriteLine($"Using Bluetooth adapter {adapterName}");
    Console.WriteLine($"Adapter's full path:    {adapterPath}");
    // Print out the devices we already know about.
    Console.WriteLine();
    Console.WriteLine("Getting known devices...");
    string djiBlutoothAddress = string.Empty;
    string djiDeviceName = string.Empty;
    var devices = await adapter.GetDevicesAsync();
    foreach (var device in devices)
    {
      try
      {
        string deviceDescription = await GetDeviceDescriptionAsync(device);
        //Console.WriteLine($" - {deviceDescription}");
        string name = await device.GetNameAsync();
        if (name.StartsWith("Osmo"))
        {
          djiDeviceName = name;
          string address = await device.GetAddressAsync();
          Console.WriteLine($"name: {name} ({address})");
          djiBlutoothAddress = address;
        }
      }
      catch (Exception ex) { Console.WriteLine(ex.Message); }
    }

    Console.WriteLine($"Found {devices.Count} paired device(s).");
    Console.WriteLine();

    // Scan for more devices.
    Console.WriteLine($"Scanning for {scanSeconds} seconds...");

    int newDevices = 0;
    using (await adapter.WatchDevicesAddedAsync(async device =>
    {
      newDevices++;
      // Write a message when we detect new devices during the scan.
      string deviceDescription = await GetDeviceDescriptionAsync(device);
      try
      {
        Console.WriteLine($"[NEW] {deviceDescription}");
        string name = await device.GetNameAsync();
        if (name.StartsWith("Osmo"))
        {
          djiDeviceName = name;
          string address = await device.GetAddressAsync();
          Console.WriteLine($"name: {name} ({address})");
          djiBlutoothAddress = address;
        }
      }
      catch (Exception ex) { Console.WriteLine(ex.Message); }
    }))
    {
      await adapter.StartDiscoveryAsync();
      await Task.Delay(TimeSpan.FromSeconds(scanSeconds));
      await adapter.StopDiscoveryAsync();
    }

    Console.WriteLine($"Scan complete. {newDevices} new device(s) found.");


    //the actual device after having it listed
    // Find the Bluetooth peripheral.
    Console.WriteLine(await adapter.GetNameAsync());
    var djiDevice = await adapter.GetDeviceAsync(djiBlutoothAddress); //TODO fetch via above and filtered Starts with OSMO
    if (djiDevice == null)
    {
      Console.WriteLine($"Bluetooth '{djiDeviceName}' with address '{djiBlutoothAddress}' not found. Use `bluetoothctl` or Bluetooth Manager to scan and possibly pair first.");
      return;
    }
    // Check if already paired
    //HINT: Bluetooth Low Energy (BLE) has no pairing via standard linux - its proprietary in case of DJI
    /*
    var paired = await djiDevice.GetPairedAsync();
    if (paired)
    {
      Console.WriteLine($"Device '{djiDeviceName} ({djiBlutoothAddress})' is already paired!");
      //await adapter.RemoveDeviceAsync(djiDevice.ObjectPath);
    }
    */
    //Console.WriteLine("Pairing device...");
    //await djiDevice.PairAsync();
    Console.WriteLine("Connecting...");
    await djiDevice.ConnectAsync();
    await djiDevice.WaitForPropertyValueAsync("Connected", value: true, timeout);
    var deviceObjPath = djiDevice.ObjectPath.ToString();
    Console.WriteLine($"Connected ({deviceObjPath}).");

    Console.WriteLine("Waiting for services to resolve...");
    await djiDevice.WaitForPropertyValueAsync("ServicesResolved", value: true, timeout);

    var servicesUUID = await djiDevice.GetUUIDsAsync();
    Console.WriteLine($"Device offers {servicesUUID.Length} service(s).");
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
        Console.WriteLine($"- Uuid: {svc}");
        if (svc == "0000fff0-0000-1000-8000-00805f9b34fb") // DJI custom service
        {
          // Get the GATT service
          IGattService1 service = await djiDevice.GetServiceAsync(svc);
          IReadOnlyList<IGattCharacteristic1> characteristics = await service.GetCharacteristicsAsync();
          foreach (IGattCharacteristic1 characteristic in characteristics)
          {
            string characteristicUUID = await characteristic.GetUUIDAsync();
            Console.WriteLine($"Charateristics Uuid: {characteristicUUID}");
            IGattCharacteristic1 characteristicItem = await service.GetCharacteristicAsync(characteristicUUID);
            Console.WriteLine(characteristicItem.ObjectPath.ToString());
            GattCharacteristic1Properties properties = await characteristicItem.GetAllAsync();
            //Console.WriteLine(BitConverter.ToString(properties.Value));
            /*foreach (string flag in properties.Flags)
            {
              Console.WriteLine(flag);
            }*/
            if (properties.Flags.Contains("write") || properties.Flags.Contains("write-without-response"))
            {
              writeCharacteristic = characteristic;
              Console.WriteLine($"Found write characteristic");
              // Look for notify characteristic
              if (properties.Flags.Contains("notify") || properties.Flags.Contains("indicate"))
              {
                notifyCharacteristic = characteristic;
                Console.WriteLine($"Found notify characteristic");
              }
              break;
            }
          }
          if (writeCharacteristic == null)
          {
            Console.WriteLine("No write characteristic found!");
            await djiDevice.DisconnectAsync();
            return;
          }
          // Set up notification handler if available
          if (notifyCharacteristic != null)
          {

            // Enable notifications
            //await notifyCharacteristic.StartNotifyAsync();
            //await notifyCharacteristic.ReadValueAsync(writeOptions);

            // Subscribe to property changes using the correct API
            _propertiesWatcher = await notifyCharacteristic.WatchPropertiesAsync(EventHandlers.OnPropertiesChanged);
            Console.WriteLine("Notifications enabled");
          }
          else
          {
            Console.WriteLine("No notify characteristic found - will not receive responses");
          }

          //TEST
          // Manual verification of initiate command
          /*
          byte[] initiatePacket = new byte[] { 0x55, 0x0e, 0x04, 0x66, 0x02, 0x08, 0x12, 0x8c, 0x40, 0x02, 0xe1, 0x1a };
          byte[] calculatedCrc = DjiCrc.Crc16(initiatePacket);
          byte[] actualCrc = new byte[] { 0x11, 0xdf };

          Console.WriteLine($"Initiate packet CRC:");
          Console.WriteLine($"Calculated: {BitConverter.ToString(calculatedCrc)}");
          Console.WriteLine($"Actual: {BitConverter.ToString(actualCrc)}");
          Console.WriteLine($"Match: {calculatedCrc.SequenceEqual(actualCrc)}");
          */
          //TEST2
          // Test with initiate packet data (without CRC)
          /*
          byte[] initiateData = new byte[] { 0x55, 0x0e, 0x04, 0x66, 0x02, 0x08, 0x12, 0x8c, 0x40, 0x02, 0xe1, 0x1a };
          byte[] crc = DjiCrc.Crc16(initiateData);
          Console.WriteLine($"Fixed CRC16: {BitConverter.ToString(crc)}");
          Console.WriteLine($"Should be: 11-DF");
          Console.WriteLine($"Match: {crc[0] == 0x11 && crc[1] == 0xDF}");
          */

          // At the start of your communication sequence
          //DjiPacketStructure.ResetSequence();
          byte[] count = new byte[] { 0x00, 0x00 };

          Console.WriteLine("Sending initialization command...");
          byte[] initCommand = DjiCommands.CreateInitiateCommand();
          DjiUtils.DebugCommand(initCommand, "Send Init");
          await writeCharacteristic.WriteValueAsync(initCommand, writeOptions);
          await Task.Delay(5000);

          Console.WriteLine("Sending authentication...");
          byte[] authCommand = DjiCommands.CreateAuthCommand(pin, count);
          DjiUtils.DebugCommand(authCommand, "Send authentication");
          await writeCharacteristic.WriteValueAsync(authCommand, writeOptions);
          count = DjiUtils.GetNextCount(count); // Increment count after auth
          await Task.Delay(5000);

          //DjiPacketStructure.ResetSequence();

          Console.WriteLine("Configuring WiFi...");
          byte[] wifiCommand = DjiCommands.CreateWifiConfigCommand(wifiSsid, wifiPassword);
          DjiUtils.DebugCommand(wifiCommand, "Send Wifi Config");
          await writeCharacteristic.WriteValueAsync(wifiCommand, writeOptions);
          await Task.Delay(5000);

          // Test with known data from Node.js
          /*
          byte[] testData = new byte[] { 0x55, 0x3C, 0x04, 0x52, 0x02, 0x08, 0xBE, 0xEA, 0x40, 0x08, 0x78, 0x00, 0x27, 0x00, 0x0A, 0x70, 0x0F, 0xA0, 0x01, 0x03, 0x00, 0x00, 0x00, 0x21, 0x00, 0x72, 0x74, 0x6D, 0x70, 0x3A, 0x2F, 0x2F, 0x31, 0x30, 0x2E, 0x30, 0x2E, 0x31, 0x2E, 0x32, 0x30, 0x3A, 0x31, 0x39, 0x33, 0x36, 0x2F, 0x69, 0x72, 0x6C, 0x62, 0x6F, 0x78, 0x2F, 0x6C, 0x69, 0x76, 0x65 };
          byte[] crc = DjiCrc.Crc16(testData);
          Console.WriteLine($"Calculated CRC: {BitConverter.ToString(crc)}");
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
          Console.WriteLine("Setting RTMP configuration...");
          byte[] rtmpCommand = DjiCommands.CreateRtmpConfigCommand(
              url: rtmpUrl,
              count: count,
              bitrateKbps: 4000,    // 4 Mbps
              resolutionCode: 0x0a, // FHD (1080p)
              fpsCode: 0x03,        // 30 FPS
              auto: true,
              eisCode: 0x01         // EIS enabled
          );
          DjiUtils.DebugCommand(rtmpCommand, "RTMP config");
          await writeCharacteristic.WriteValueAsync(rtmpCommand, writeOptions);
          count = DjiUtils.GetNextCount(count); // Increment count after auth
          await Task.Delay(5000);

          //DjiPacketStructure.ResetSequence();

          Console.WriteLine("Starting broadcast...");
          byte[] startCommand = DjiCommands.CreateStartBroadcastCommand();
          DjiUtils.DebugCommand(startCommand, "Start Broadcast");
          await writeCharacteristic.WriteValueAsync(startCommand, writeOptions);
          await Task.Delay(5000);

          Console.WriteLine("Broadcast started successfully!");

          // Keep connection open
          Console.WriteLine("Press any key to stop broadcasting and disconnect...");
          Console.ReadKey();

          Console.WriteLine("Stopping broadcast...");
          byte[] stopCommand = DjiCommands.CreateStopStreamingCommand(count);
          await writeCharacteristic.WriteValueAsync(stopCommand, writeOptions);
          await Task.Delay(1000);
        }
      }
    }
  }

  private static async void OnCharacteristicValueChanged(IGattCharacteristic1 characteristic, GattCharacteristicValueEventArgs args)
  {
      try
      {
          byte[] data = args.Value.ToArray();
          var notification = DjiNotificationParser.ParseNotify(data);
          
          Console.WriteLine($"Received: {notification}");
          
          // Handle specific responses
          if (notification.AuthSuccess.HasValue)
          {
              if (notification.AuthSuccess.Value)
              {
                  Console.WriteLine("✓ Device authenticated successfully!");
              }
              else
              {
                  Console.WriteLine("✗ Authentication failed! Check your PIN.");
              }
          }
          
          if (notification.StreamingStatus.HasValue)
          {
              switch (notification.StreamingStatus.Value)
              {
                  case 0x00:
                      Console.WriteLine("✓ Streaming stopped");
                      break;
                  case 0x01:
                      Console.WriteLine("✓ Streaming started successfully!");
                      break;
                  case 0x02:
                      Console.WriteLine("⚠ Streaming in progress...");
                      break;
                  case 0x03:
                      Console.WriteLine("⚠ Streaming preparing...");
                      break;
                  case 0x04:
                      Console.WriteLine("✗ Streaming failed - check RTMP URL");
                      break;
                  default:
                      Console.WriteLine($"Streaming status: 0x{notification.StreamingStatus.Value:X2}");
                      break;
              }
          }
      }
      catch (Exception ex)
      {
          Console.WriteLine($"Error handling notification: {ex.Message}");
      }
  }

  private static async Task<string> GetDeviceDescriptionAsync(IDevice1 device)
  {
    var deviceProperties = await device.GetAllAsync();
    IDictionary<ushort, object> manufacturerData = new Dictionary<ushort, object>();
    if (deviceProperties.ManufacturerData != null)
    {
      manufacturerData = deviceProperties.ManufacturerData;
      foreach (var item in manufacturerData)
      {
        Console.WriteLine(item.Key);
        byte[] realValue = (byte[])item.Value;
        string hexString = BitConverter.ToString(realValue);
        Console.WriteLine(hexString);
      }
    }
    return $"{deviceProperties.Alias} (Address: {deviceProperties.Address}, RSSI: {deviceProperties.RSSI})";
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
    Console.WriteLine($"Device found: {e.Device.Name} ({e.Device.Address})");

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
    Console.WriteLine($"Model Name: {modelName}");

    // Optionally disconnect after reading
    await e.Device.DisconnectAsync();
}
*/
}
