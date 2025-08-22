using Linux.Bluetooth;
using Serilog;

namespace djiconnect.Utils;
public static class DeviceUtils
{
    private static readonly Serilog.ILogger _logger = Log.Logger;
    public static async Task<string> GetDeviceDescriptionAsync(IDevice1 device)
    {
        var deviceProperties = await device.GetAllAsync();
        IDictionary<ushort, object> manufacturerData = new Dictionary<ushort, object>();
        if (deviceProperties.ManufacturerData != null)
        {
            manufacturerData = deviceProperties.ManufacturerData;
            foreach (var item in manufacturerData)
            {
                _logger.Information($"{item.Key}");
                byte[] realValue = (byte[])item.Value;
                string hexString = BitConverter.ToString(realValue);
                _logger.Information(hexString);
            }
        }
        return $"{deviceProperties.Alias} (Address: {deviceProperties.Address}, RSSI: {deviceProperties.RSSI})";
    }
}