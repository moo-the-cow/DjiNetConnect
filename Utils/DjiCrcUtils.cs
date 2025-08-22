using Serilog;

namespace djiconnect.Utils;
public static class DjiCrcUtils
{
    private static readonly Serilog.ILogger _logger = Log.Logger;
    // CRC8 implementation that matches Node.js crc-full library
    public static byte[] Crc16(byte[] data)
    {
        _logger.Information($"Data being hashed ({data.Length} bytes): {BitConverter.ToString(data)}");

        const ushort polynomial = 0x1021;
        ushort crc = 0x496C; // Initial value

        foreach (byte b in data)
        {
            // Reflect input (bit reversal)
            byte reflectedByte = ReflectByte(b);
            crc ^= (ushort)(reflectedByte << 8);

            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = (ushort)((crc << 1) ^ polynomial);
                }
                else
                {
                    crc <<= 1;
                }
            }
        }

        // Reflect output (bit reversal)
        crc = ReflectUshort(crc);

        byte[] result = new byte[] { (byte)(crc & 0xFF), (byte)((crc >> 8) & 0xFF) };
        _logger.Information($"CRC16 result: {BitConverter.ToString(result)}");

        return result;
    }

    // Alternative CRC16 implementation (CCITT-FALSE)
    public static byte[] Crc16Simple(byte[] data)
    {
        ushort crc = 0xFFFF;

        foreach (byte b in data)
        {
            crc ^= (ushort)(b << 8);

            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ 0x1021);
                else
                    crc = (ushort)(crc << 1);
            }
        }

        return new byte[] { (byte)(crc >> 8), (byte)(crc & 0xFF) };
    }

    // Bit reversal for a byte
    private static byte ReflectByte(byte value)
    {
        byte result = 0;
        for (int i = 0; i < 8; i++)
        {
            if ((value & (1 << i)) != 0)
            {
                result |= (byte)(1 << (7 - i));
            }
        }
        return result;
    }

    // Bit reversal for a ushort
    private static ushort ReflectUshort(ushort value)
    {
        ushort result = 0;
        for (int i = 0; i < 16; i++)
        {
            if ((value & (1 << i)) != 0)
            {
                result |= (ushort)(1 << (15 - i));
            }
        }
        return result;
    }

    public static byte Crc8(byte[] data)
    {
        const byte polynomial = 0x31;
        byte crc = 0xEE; // Initial value

        foreach (byte b in data)
        {
            // Reflect input
            byte reflectedByte = ReflectByte(b);
            crc ^= reflectedByte;

            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x80) != 0)
                {
                    crc = (byte)((crc << 1) ^ polynomial);
                }
                else
                {
                    crc <<= 1;
                }
            }
        }

        // Reflect output
        crc = ReflectByte(crc);
        // Final XOR value (0x00 in this case, so no change)

        return crc;
    }
}