using System.Text;
using Serilog;

namespace djiconnect.Utils;

public static class DjiCommandUtils
{
    private static readonly ILogger _logger = Log.Logger;
    #region Init
    public static byte[] CreateInitiateCommand()
    {
        _logger.Debug("Sending Init Command");
        return [0x55, 0x0e, 0x04, 0x66, 0x02, 0x08, 0x12, 0x8c, 0x40, 0x02, 0xe1, 0x1a, 0x11, 0xdf];
    }
    #endregion

    #region Auth
    public static byte[] CreateAuthCommand(string pin, byte[] count)
    {
        _logger.Debug("Sending Aut Command");
        const string tokenAscii = "4ea4bfaa85f2e6052ac03bd41520f380";
        byte[] tokenBytes = Encoding.ASCII.GetBytes(tokenAscii);
        byte[] pinBytes = Encoding.ASCII.GetBytes(pin);

        List<byte> data =
        [
            0x20, // tokenLen
            .. tokenBytes,
            (byte)pinBytes.Length,
            .. pinBytes,
        ];

        return DjiPacketStructureUtils.BuildDjiFrame(
            command: [0x02, 0x07],
            id: count, // Use the passed count
            type: [0x40, 0x07, 0x45],
            data: [.. data]
        );
    }
    #endregion

    #region Wifi
    public static byte[] CreateWifiConfigCommand(string ssid, string password)
    {
        _logger.Debug("Sending Wifi Command");
        byte[] ssidBytes = Encoding.UTF8.GetBytes(ssid);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        List<byte> data = [(byte)ssidBytes.Length, .. ssidBytes, (byte)passwordBytes.Length, .. passwordBytes];

        return DjiPacketStructureUtils.BuildDjiFrame(
            command: [0x02, 0x07],
            id: [0xb2, 0xea],
            type: [0x40, 0x07, 0x47],
            data: [.. data]
        );
    }
    #endregion

    #region RTMP config
    public static byte[] CreateRtmpConfigCommand(string url, byte[] count, int bitrateKbps = 4000, int resolutionCode = 0x0a, int fpsCode = 0x03, bool auto = true, int eisCode = 1)
    {
        _logger.Debug("Sending RTMP Config Command");
        byte[] urlBytes = Encoding.UTF8.GetBytes(url);
        int urlLength = urlBytes.Length;

        // Reconstruct the base data template dynamically
        List<byte> data =
        [
            0x27, // Constant
            0x00, // Constant
            (byte)resolutionCode, // Resolution (0x0a for 1080p)
            0x70, // Constant
            // Bitrate (big endian) - 4000 = 0x0FA0
            (byte)((bitrateKbps >> 8) & 0xFF),  // 0x0F
            (byte)(bitrateKbps & 0xFF),         // 0xA0
            (byte)(auto ? 0x01 : 0x00), // Auto setting
            (byte)fpsCode, // FPS code
            0x00, // Constant
            0x00, // Constant
            0x00, // Constant
            (byte)urlLength, // URL length - CRITICAL FIX
            0x00, // Constant
            .. urlBytes, // Append the URL bytes
        ];

        // Build main RTMP config payload
        byte[] rtmpPayload = DjiPacketStructureUtils.BuildDjiFrame(
            command: [0x02, 0x08],
            id: [0xbe, 0xea], // FIXED ID from Node.js
            type: [0x40, 0x08, 0x78, 0x00],
            data: [.. data]
        );
        //HINT: uncomment if needed
        //DjiUtils.DebugCommand(rtmpPayload, "RTMP payload config");

        // Build EIS payload - USE THE count PARAMETER
        byte[] eisData = [0x01, 0x01, 0x08, 0x00, 0x01, (byte)eisCode, 0xf0, 0x72];

        byte[] eisPayload = DjiPacketStructureUtils.BuildDjiFrame(
            command: [0x02, 0x01],
            id: count, // Use the count parameter
            type: [0x40, 0x02, 0x8e],
            data: eisData
        );
        //HINT: uncomment if needed
        //DjiUtils.DebugCommand(eisPayload, "RTMP EIS payload config");

        // Combine both payloads
        List<byte> combined = [.. rtmpPayload, .. eisPayload];

        return [.. combined];
    }
    #endregion

    #region Start stream
    public static byte[] CreateStartBroadcastCommand()
    {
        _logger.Debug("Sending Start Stream Command");
        byte[] start = [0x55, 0x13, 0x04, 0x03, 0x02, 0x08, 0x6a, 0xc0, 0x40, 0x02, 0x8e, 0x01, 0x01, 0x1a, 0x00, 0x01, 0x01];

        // Recalculate size and CRC8
        start[1] = (byte)(start.Length + 2);

        byte[] firstThreeBytes = [start[0], start[1], start[2]];
        start[3] = DjiCrcUtils.Crc8(firstThreeBytes);

        // Append CRC16
        byte[] crc16 = DjiCrcUtils.Crc16(start);
        List<byte> complete = [.. start, .. crc16];

        return [.. complete];
    }
    #endregion

    #region Stop stream
    public static byte[] CreateStopBroadcastCommand()
    {
        _logger.Debug("Sending Stop Stream Command");
        // Start with the EXACT same structure as your working start command
        byte[] stop = [
            0x55, 0x13, 0x04, 0x03, 0x02, 0x08, 0x6a, 0xc0, 0x40, 0x02, 0x8e, 0x01, 0x01, 0x1a, 0x00, 0x01, 0x01
        ];

        // Change the last parameter byte from 0x01 (start) to 0x02 (stop)
        // This is the crucial difference based on the Swift code
        stop[16] = 0x02; // Change from 0x01 to 0x02 for STOP

        // Recalculate size (should stay the same: 17 bytes + 2 CRC16 = 19 = 0x13)
        stop[1] = (byte)(stop.Length + 2); // Still 0x13

        // Recalculate CRC8 for first 3 bytes (0x55, 0x13, 0x04)
        byte[] firstThreeBytes = [stop[0], stop[1], stop[2]];
        stop[3] = DjiCrcUtils.Crc8(firstThreeBytes);

        // Append CRC16 (recalculated because data changed)
        byte[] crc16 = DjiCrcUtils.Crc16(stop);
        List<byte> complete = [.. stop, .. crc16];

        return [.. complete];
    }
    #endregion
}