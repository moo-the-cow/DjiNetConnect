using Serilog;

namespace djiconnect.Utils;

public static class DjiParseUtils
{
    private static readonly Serilog.ILogger _logger = Log.Logger;
    // Known success response patterns
    public static readonly byte[] AuthSuccessResponse =
    [
        0x55, 0x0F, 0x04, 0xA2, 0x07, 0x02, 0x00, 0x00,
        0xC0, 0x07, 0x45, 0x00, 0x01, 0xA0, 0xD4
    ];

    public static readonly byte[] WifiSuccessResponse =
    [
        0x55, 0x16, 0x04, 0xFC, 0x08, 0x02, 0x00, 0x00, 
        0x80, 0xEE, 0x03, 0x03, 0x09, 0x00, 0x00, 0x00, 
        0x00, 0x00, 0x00, 0x20, 0xDA, 0xAC
    ];

    public static readonly byte[] RtmpConfigSuccessResponse =
    [
        0x55, 0x0E, 0x04, 0x66, 0x01, 0x02, 0x01, 0x00, 
        0xC0, 0x02, 0x8E, 0x00, 0x16, 0x7B
    ];

    public static readonly byte[] StreamStartSuccessResponse = new byte[]
    {
        0x55, 0x0E, 0x04, 0x66, 0x08, 0x02, 0x6A, 0xC0, 
        0xC0, 0x02, 0x8E, 0x00, 0xF6, 0x36
    };

    private static bool _authSuccessful = false;
    private static bool _wifiConfigured = false;
    private static bool _rtmpConfigured = false;
    private static bool _streamStartSuccessful = false;

    public static void ParseNotificationResponse(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        // Convert to hex for pattern matching
        string dataHex = BitConverter.ToString(data);

        // Check for authentication success
        if (!_authSuccessful && IsMatchingPattern(data, AuthSuccessResponse))
        {
            _authSuccessful = true;
            _logger.Debug("✅ AUTHENTICATION SUCCESSFUL! Device is now paired.");
            return;
        }

        // Check for auth success
        if (_authSuccessful && !_wifiConfigured && IsMatchingPattern(data, WifiSuccessResponse))
        {
            _wifiConfigured = true;
            _logger.Debug("✅ WiFi CONFIGURATION SUCCESSFUL!");
            return;
        }

        // Check for rtmp config success
        if (_authSuccessful && _wifiConfigured && !_rtmpConfigured && IsMatchingPattern(data, RtmpConfigSuccessResponse))
        {
            _rtmpConfigured = true;
            _logger.Debug("✅ RTMP CONFIGURATION SUCCESSFUL!");
            return;
        }

        // Check for stream start success
        if (_authSuccessful && _wifiConfigured && _rtmpConfigured && !_streamStartSuccessful && IsMatchingPattern(data, StreamStartSuccessResponse))
        {
            _streamStartSuccessful = true;
            _logger.Debug("✅ STREAM START SUCCESSFUL!");
            return;
        }

        // If no known patterns, use legacy parsing
        ParseLegacyResponse(data);
    }

    private static bool IsMatchingPattern(byte[] response, byte[] pattern)
    {
        if (response.Length < pattern.Length)
            return false;

        // Check if the beginning of the response matches the pattern
        for (int i = 0; i < pattern.Length; i++)
        {
            if (response[i] != pattern[i])
                return false;
        }

        return true;
    }

    private static void ParseLegacyResponse(byte[] data)
    {
        // Your existing legacy parsing logic here
        if (data.Length < 4)
            return;

        byte length = data[1];
        byte command = data[2];
        byte status = data[3];

        // Only log non-spam messages
        byte[] spamStatuses = { 0x2E, 0xFC, 0x92, 0xA8, 0x63, 0x33 };
        if (!spamStatuses.Contains(status))
        {
            _logger.Debug($"📨 Response - Cmd: 0x{command:X2}, Status: 0x{status:X2}, Len: {length}");
        }
    }

    public static void Reset()
    {
        _authSuccessful = false;
        _wifiConfigured = false;
        _rtmpConfigured = false;
        _streamStartSuccessful = false;
    }
    
    public static void AnalyzeResponse(byte[] data)
    {
        _logger.Debug($"🔍 response: {BitConverter.ToString(data)}");
        _logger.Debug($"Length: {data.Length} bytes");
        
        if (data.Length >= 4)
        {
            _logger.Debug($"Command: 0x{data[2]:X2}, Status: 0x{data[3]:X2}");
        }
        
        // Use this method after each operation to identify success patterns
    }
}