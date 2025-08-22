using Serilog;

namespace djiconnect.Utils;
public static class DjiUtils
{
    private static readonly Serilog.ILogger _logger = Log.Logger;
    public static void DebugCommand(byte[] command, string name)
    {
        _logger.Information($"=== {name} ===");
        _logger.Information($"Length: {command.Length} bytes");
        _logger.Information($"Hex: {BitConverter.ToString(command)}");

        // Check CRC16 of the entire packet (if it's a complete DJI frame)
        if (command.Length >= 4)
        {
            byte[] dataWithoutCrc = new byte[command.Length - 2];
            Array.Copy(command, 0, dataWithoutCrc, 0, command.Length - 2);

            byte[] calculatedCrc = DjiCrcUtils.Crc16(dataWithoutCrc);
            byte[] actualCrc = new byte[] { command[command.Length - 2], command[command.Length - 1] };

            bool crcValid = calculatedCrc[0] == actualCrc[0] && calculatedCrc[1] == actualCrc[1];
            _logger.Information($"CRC16 Valid: {crcValid}");

            if (!crcValid)
            {
                _logger.Information($"Expected: {BitConverter.ToString(calculatedCrc)}");
                _logger.Information($"Actual: {BitConverter.ToString(actualCrc)}");
            }
        }
    }

    public static byte[] GetNextCount(byte[] currentCount)
    {
        if (currentCount == null || currentCount.Length != 2)
            return new byte[] { 0x00, 0x00 };

        byte[] next = new byte[2];
        Array.Copy(currentCount, next, 2);

        // Exact match to Node.js logic
        if (next[0] == 0xFF)
        {
            next[0] = 0x00;
            next[1]++;
        }
        else
        {
            next[0]++;
        }

        return next;
    }
}