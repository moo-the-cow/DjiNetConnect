public static class DjiCrc
{
    // CRC8 implementation that matches Node.js crc-full library
    public static byte[] Crc16(byte[] data)
    {
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
        // Final XOR value (0x0000 in this case, so no change)
        
        return new byte[] { (byte)(crc & 0xFF), (byte)((crc >> 8) & 0xFF) };
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