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
      var devices = await adapter.GetDevicesAsync();
      foreach (var device in devices)
      {
        string deviceDescription = await GetDeviceDescriptionAsync(device);
        Console.WriteLine($" - {deviceDescription}");
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
        Console.WriteLine($"[NEW] {deviceDescription}");
      }))
      {
        await adapter.StartDiscoveryAsync();
        await Task.Delay(TimeSpan.FromSeconds(scanSeconds));
        await adapter.StopDiscoveryAsync();
      }

      Console.WriteLine($"Scan complete. {newDevices} new device(s) found.");


      //the actual device after having it listed
      // Find the Bluetooth peripheral.
        var djiDevice = await adapter.GetDeviceAsync("E4:7A:2C:56:5F:59"); //TODO fetch via above and filtered Starts with OSMO
        if (djiDevice == null)
        {
        Console.WriteLine($"Bluetooth peripheral with address 'E4:7A:2C:56:5F:59' not found. Use `bluetoothctl` or Bluetooth Manager to scan and possibly pair first.");
        return;
        }
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
            foreach (var svc in servicesUUID)
            {
                Console.WriteLine($"- Uuid: {svc}");
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
                    foreach(string flag in properties.Flags)
                    {
                        Console.WriteLine(flag);
                    }
                    byte[] shutdownCommand = new byte[] { 0x01, 0x02 };  // Example command, replace with actual bytes
                    var writeOptions = new Dictionary<string, object>
                    {
                        { "type", "command" }  // Write without response. type can also be "request" for a response
                    };
                    // Write the shutdown command to the characteristic
                    await characteristic.WriteValueAsync(shutdownCommand, writeOptions);
                }
            }
        }
    }

    private static async Task<string> GetDeviceDescriptionAsync(IDevice1 device)
    {
      var deviceProperties = await device.GetAllAsync();
      IDictionary<ushort,object> manufacturerData = new Dictionary<ushort,object>();
      if(deviceProperties.ManufacturerData != null)
      {
        manufacturerData = deviceProperties.ManufacturerData;
        foreach(var item in manufacturerData)
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
