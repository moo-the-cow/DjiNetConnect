using System.Text;
using Serilog;

namespace djiconnect.Utils;
public static class DjiCommandUtils
{
    private static readonly Serilog.ILogger _logger = Log.Logger;
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

        return DjiPacketStructureUtils.BuildDjiFrame(
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
        byte[] urlBytes = Encoding.UTF8.GetBytes(url);
        int urlLength = urlBytes.Length;

        // Reconstruct the base data template dynamically
        List<byte> data = new List<byte>();
        data.Add(0x27); // Constant
        data.Add(0x00); // Constant
        data.Add((byte)resolutionCode); // Resolution (0x0a for 1080p)
        data.Add(0x70); // Constant
        // Bitrate (big endian) - 4000 = 0x0FA0
        data.Add((byte)((bitrateKbps >> 8) & 0xFF));  // 0x0F
        data.Add((byte)(bitrateKbps & 0xFF));         // 0xA0
        data.Add((byte)(auto ? 0x01 : 0x00)); // Auto setting
        data.Add((byte)fpsCode); // FPS code
        data.Add(0x00); // Constant
        data.Add(0x00); // Constant
        data.Add(0x00); // Constant
        data.Add((byte)urlLength); // URL length - CRITICAL FIX
        data.Add(0x00); // Constant

        data.AddRange(urlBytes); // Append the URL bytes

        // Build main RTMP config payload
        byte[] rtmpPayload = DjiPacketStructureUtils.BuildDjiFrame(
            command: new byte[] { 0x02, 0x08 },
            id: new byte[] { 0xbe, 0xea }, // FIXED ID from Node.js
            type: new byte[] { 0x40, 0x08, 0x78, 0x00 },
            data: data.ToArray()
        );
        DjiUtils.DebugCommand(rtmpPayload, "RTMP payload config");

        // Build EIS payload - USE THE count PARAMETER
        byte[] eisData = new byte[] { 0x01, 0x01, 0x08, 0x00, 0x01, (byte)eisCode, 0xf0, 0x72 };

        byte[] eisPayload = DjiPacketStructureUtils.BuildDjiFrame(
            command: new byte[] { 0x02, 0x01 },
            id: count, // Use the count parameter
            type: new byte[] { 0x40, 0x02, 0x8e },
            data: eisData
        );
        DjiUtils.DebugCommand(eisPayload, "RTMP EIS payload config");

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
        start[3] = DjiCrcUtils.Crc8(firstThreeBytes);

        // Append CRC16
        byte[] crc16 = DjiCrcUtils.Crc16(start);
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

        return DjiPacketStructureUtils.BuildDjiFrame(
            command: new byte[] { 0x02, 0x07 },
            id: count, // Use the passed count
            type: new byte[] { 0x40, 0x07, 0x45 },
            data: data.ToArray()
        );
    }

    // Stop streaming command
    public static byte[] CreateStopBroadcastCommandNew(byte[] currentSequenceId)
    {

        // Build the core packet - same as start but last data byte is 0x00
        byte[] stopData = new byte[] { 0x01, 0x01, 0x1a, 0x00, 0x01, 0x00 }; // Last byte 0x00 for STOP

        List<byte> packet = new List<byte>();
        packet.Add(0x55);
        packet.Add(0x00); // Length placeholder
        packet.Add(0x04);
        packet.Add(0x00); // CRC8 placeholder
        packet.AddRange(new byte[] { 0x02, 0x08 }); // Command
        packet.AddRange(currentSequenceId); // Sequence ID
        packet.AddRange(new byte[] { 0x40, 0x02, 0x8e }); // Your specific type
        packet.AddRange(stopData); // Stop data payload

        // Calculate and set length
        int lengthValue = (packet.Count - 2) + 2; // -2 (0x55+placeholder), +2 for CRC16
        packet[1] = (byte)lengthValue;

        // Calculate and set CRC8
        byte[] headerForCrc8 = { packet[0], packet[1], packet[2] };
        packet[3] = DjiCrcUtils.Crc8(headerForCrc8);

        // Calculate CRC16
        byte[] packetWithoutCrc16 = packet.ToArray();
        byte[] crc16 = DjiCrcUtils.Crc16(packetWithoutCrc16);

        // Final packet
        List<byte> completePacket = new List<byte>();
        completePacket.AddRange(packetWithoutCrc16);
        completePacket.AddRange(crc16);

        return completePacket.ToArray();
    }
    public static byte[] CreateStopStreamingCommand(byte[] count)
    {
        return DjiPacketStructureUtils.BuildDjiFrame(
            command: new byte[] { 0x02, 0x07 },
            id: count,
            type: new byte[] { 0x40, 0x07, 0x43 },
            data: new byte[] { 0x00 }  // 0x00 = stop, 0x01 = start
        );
    }
    public static byte[] CreateStopStreamingCommand2(byte[] count)
    {
        // Try different command types that might control streaming
        return DjiPacketStructureUtils.BuildDjiFrame(
            command: new byte[] { 0x02, 0x08 }, // Different command
            id: count,
            type: new byte[] { 0x40, 0x08, 0x43 }, // Different type
            data: new byte[] { 0x00 }
        );
    }
    public static byte[] CreateStopStreamingCommand3(byte[] count)
    {
        // This is the most common stop command structure found in DJI protocols
        return DjiPacketStructureUtils.BuildDjiFrame(
            command: new byte[] { 0x02, 0x07 },
            id: count,
            type: new byte[] { 0x40, 0x07, 0x43 },
            data: new byte[] { 0x00, 0x00, 0x00, 0x00 }  // Sometimes needs 4 bytes
        );
    }
    public static byte[] CreateStopStreamingCommand()
    {
        byte[] start = new byte[] { 0x55, 0x13, 0x04, 0x03, 0x02, 0x08, 0x6a, 0xc0, 0x40, 0x02, 0x8e, 0x01, 0x01, 0x1a, 0x00, 0x01, 0x00 }; // Last byte 0x00 instead of 0x01

        start[1] = (byte)(start.Length + 2);

        byte[] firstThreeBytes = new byte[] { start[0], start[1], start[2] };
        start[3] = DjiCrcUtils.Crc8(firstThreeBytes);

        byte[] crc16 = DjiCrcUtils.Crc16(start);
        List<byte> complete = new List<byte>();
        complete.AddRange(start);
        complete.AddRange(crc16);

        return complete.ToArray();
    }
}