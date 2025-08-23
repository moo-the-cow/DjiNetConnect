using Serilog;

namespace djiconnect.Utils;
public static class DjiUtils
{
    private static readonly Serilog.ILogger _logger = Log.Logger;
    public static void DebugCommand(byte[] command, string name)
    {
        _logger.Debug($"=== {name} ===");
        _logger.Debug($"Length: {command.Length} bytes");
        _logger.Debug($"Hex: {BitConverter.ToString(command)}");

        // Check if this is a Moblin-style command (starts with 55 AA)
        if (command.Length >= 2 && command[0] == 0x55 && command[1] == 0xAA)
        {
            // Moblin protocol: uses additive checksum
            byte calculatedChecksum = 0;
            for (int i = 0; i < command.Length - 1; i++)
            {
                calculatedChecksum += command[i];
            }
            
            bool checksumValid = (calculatedChecksum == command[command.Length - 1]);
            _logger.Debug($"Additive Checksum Valid: {checksumValid}");
            
            if (!checksumValid)
            {
                _logger.Debug($"Expected: 0x{calculatedChecksum:X2}");
                _logger.Debug($"Actual: 0x{command[command.Length - 1]:X2}");
            }
        }
        else
        {
            // Your legacy protocol: uses CRC16
            if (command.Length >= 4)
            {
                byte[] dataWithoutCrc = new byte[command.Length - 2];
                Array.Copy(command, 0, dataWithoutCrc, 0, command.Length - 2);

                byte[] calculatedCrc = DjiCrcUtils.Crc16(dataWithoutCrc);
                byte[] actualCrc = new byte[] { command[command.Length - 2], command[command.Length - 1] };

                bool crcValid = calculatedCrc[0] == actualCrc[0] && calculatedCrc[1] == actualCrc[1];
                _logger.Debug($"CRC16 Valid: {crcValid}");

                if (!crcValid)
                {
                    _logger.Debug($"Expected: {BitConverter.ToString(calculatedCrc)}");
                    _logger.Debug($"Actual: {BitConverter.ToString(actualCrc)}");
                }
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