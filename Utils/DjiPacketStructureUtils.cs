using Serilog;

namespace djiconnect.Utils;
public static class DjiPacketStructureUtils
{
    private static readonly Serilog.ILogger _logger = Log.Logger;
    //private static byte[] _currentSequence = new byte[] { 0x00, 0x00 };
    public static byte[] BuildDjiFrame(byte[] command, byte[] id, byte[] type, byte[] data)
    {
        if (command.Length != 2) throw new ArgumentException("Command must be 2 bytes");
        if (id.Length != 2) throw new ArgumentException("ID must be 2 bytes");
        if (type.Length != 3 && type.Length != 4) throw new ArgumentException("Type must be 3 or 4 bytes");

        // Create initial payload without CRC16
        List<byte> payload = new List<byte>
        {
            0x55, // Start byte
            0xFF, // Size placeholder
            0x04, // Spacer
            0xFF, // CRC8 placeholder
        };

        payload.AddRange(command);
        payload.AddRange(id);
        payload.AddRange(type);
        payload.AddRange(data);

        // Calculate and set size (includes the CRC16 that will be appended)
        int totalSize = payload.Count + 2;
        payload[1] = (byte)totalSize;

        // Calculate and set CRC8 (first 3 bytes)
        byte[] firstThreeBytes = new byte[] { payload[0], payload[1], payload[2] };
        payload[3] = DjiCrcUtils.Crc8(firstThreeBytes); // Now uses reflection

        // Calculate and append CRC16 with reflection
        byte[] crc16 = DjiCrcUtils.Crc16(payload.ToArray()); // Now uses reflection
        payload.AddRange(crc16);

        return payload.ToArray();
    }
    /*
    public static byte[] GetNextSequenceId()
    {
        if (_currentSequence[0] == 0xFF)
        {
            _currentSequence[0] = 0x00;
            _currentSequence[1]++;
        }
        else
        {
            _currentSequence[0]++;
        }

        return _currentSequence;
    }
    
    public static void ResetSequence()
    {
        _currentSequence = new byte[] { 0x00, 0x00 };
    }
    */
}