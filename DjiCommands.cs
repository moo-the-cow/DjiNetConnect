using System.Text;

public static class DjiCommands
{
    // Initialization command - CRUCIAL!
    public static byte[] CreateInitiateCommand()
    {
        return new byte[] { 0x55, 0x0e, 0x04, 0x66, 0x02, 0x08, 0x12, 0x8c, 0x40, 0x02, 0xe1, 0x1a, 0x11, 0xdf };
    }
    
    // WiFi configuration
    public static byte[] CreateWifiConfigCommand(string ssid, string password)
    {
        byte[] ssidBytes = Encoding.UTF8.GetBytes(ssid);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        
        List<byte> data = new List<byte>();
        data.Add((byte)ssidBytes.Length);
        data.AddRange(ssidBytes);
        data.Add((byte)passwordBytes.Length);
        data.AddRange(passwordBytes);
        
        return DjiPacketStructure.BuildDjiFrame(
            command: new byte[] { 0x02, 0x07 },
            id: new byte[] { 0xb2, 0xea },
            type: new byte[] { 0x40, 0x07, 0x47 },
            data: data.ToArray()
        );
    }
    
    // Enhanced RTMP configuration with all parameters
    public static byte[] CreateRtmpConfigCommand(string url, byte[] count, int bitrateKbps = 4000, int resolutionCode = 0x0a, 
                                           int fpsCode = 0x03, bool auto = true, int eisCode = 1)
    {
        // Base data template
        byte[] baseData = new byte[] { 0x27, 0x00, 0x0a, 0x70, 0x17, 0x02, 0x00, 0x03, 0x00, 0x00, 0x00, 0x1c, 0x00 };
        
        byte[] urlBytes = Encoding.UTF8.GetBytes(url);
        List<byte> data = new List<byte>(baseData);
        data.AddRange(urlBytes);
        
        // Set URL length - USE BITWISE AND like Node.js
        data[11] = (byte)(urlBytes.Length & 0xff);
        
        // Set bitrate (big endian) - 4000 = 0x0FA0
        data[4] = (byte)((bitrateKbps >> 8) & 0xFF);  // 0x0F
        data[5] = (byte)(bitrateKbps & 0xFF);         // 0xA0
        
        // Set other parameters
        data[7] = (byte)fpsCode;
        data[2] = (byte)resolutionCode;
        data[6] = (byte)(auto ? 0x01 : 0x00);

        // Build main RTMP config payload - USE FIXED ID like Node.js
        byte[] rtmpPayload = DjiPacketStructure.BuildDjiFrame(
            command: new byte[] { 0x02, 0x08 },
            id: new byte[] { 0xbe, 0xea }, // FIXED ID from Node.js
            type: new byte[] { 0x40, 0x08, 0x78, 0x00 },
            data: data.ToArray()
        );
        
        // Build EIS payload - USE THE count PARAMETER
        byte[] eisData = new byte[] { 0x01, 0x01, 0x08, 0x00, 0x01, (byte)eisCode, 0xf0, 0x72 };
        
        // Use the passed count parameter instead of sequence generator
        byte[] eisId = count;
        Console.WriteLine($"EIS Count ID: {BitConverter.ToString(eisId)}");
        
        byte[] eisPayload = DjiPacketStructure.BuildDjiFrame(
            command: new byte[] { 0x02, 0x01 },
            id: eisId, // Use the count parameter
            type: new byte[] { 0x40, 0x02, 0x8e },
            data: eisData
        );
        
        // Combine both payloads
        List<byte> combined = new List<byte>();
        combined.AddRange(rtmpPayload);
        combined.AddRange(eisPayload);
        
        return combined.ToArray();
    }
    
    // Start broadcast command
    public static byte[] CreateStartBroadcastCommand()
    {
        byte[] start = new byte[] { 0x55, 0x13, 0x04, 0x03, 0x02, 0x08, 0x6a, 0xc0, 0x40, 0x02, 0x8e, 0x01, 0x01, 0x1a, 0x00, 0x01, 0x01 };
        
        // Recalculate size and CRC8
        start[1] = (byte)(start.Length + 2);
        
        byte[] firstThreeBytes = new byte[] { start[0], start[1], start[2] };
        start[3] = DjiCrc.Crc8(firstThreeBytes);
        
        // Append CRC16
        byte[] crc16 = DjiCrc.Crc16(start);
        List<byte> complete = new List<byte>();
        complete.AddRange(start);
        complete.AddRange(crc16);
        
        return complete.ToArray();
    }
    
    // Authentication command
    public static byte[] CreateAuthCommand(string pin, byte[] count)
    {
        const string tokenAscii = "4ea4bfaa85f2e6052ac03bd41520f380";
        byte[] tokenBytes = Encoding.ASCII.GetBytes(tokenAscii);
        byte[] pinBytes = Encoding.ASCII.GetBytes(pin);
        
        List<byte> data = new List<byte>();
        data.Add(0x20); // tokenLen
        data.AddRange(tokenBytes);
        data.Add((byte)pinBytes.Length);
        data.AddRange(pinBytes);
        
        return DjiPacketStructure.BuildDjiFrame(
            command: new byte[] { 0x02, 0x07 },
            id: count, // Use the passed count
            type: new byte[] { 0x40, 0x07, 0x45 },
            data: data.ToArray()
        );
    }

    // Stop streaming command
    public static byte[] CreateStopStreamingCommand(byte[] count)
    {
        return DjiPacketStructure.BuildDjiFrame(
            command: new byte[] { 0x02, 0x07 },
            //id: count,
            id: new byte[] { 0xb2, 0xea },
            type: new byte[] { 0x40, 0x07, 0x43 },
            data: new byte[] { 0x00 }
        );
    }
}