using Serilog;

namespace djiconnect.Utils;

public static class DjiParseUtils
{
    private static readonly Serilog.ILogger _logger = Log.Logger;
    // Known success response patterns
    private static readonly byte[] AuthSuccessResponse =
    [
        0x55, 0x0F, 0x04, 0xA2, 0x07, 0x02, 0x00, 0x00,
        0xC0, 0x07, 0x45, 0x00, 0x01, 0xA0, 0xD4
    ];

    private static readonly byte[] WifiSuccessResponse =
    [
        0x55, 0x16, 0x04, 0xFC, 0x08, 0x02, 0x00, 0x00, 
        0x80, 0xEE, 0x03, 0x03, 0x09, 0x00, 0x00, 0x00, 
        0x00, 0x00, 0x00, 0x20, 0xDA, 0xAC
    ];

    private static readonly byte[] RtmpConfigSuccessResponse =
    [
        0x55, 0x0E, 0x04, 0x66, 0x01, 0x02, 0x01, 0x00, 
        0xC0, 0x02, 0x8E, 0x00, 0x16, 0x7B
    ];

    private static readonly byte[] StreamStartSuccessResponse = new byte[]
    {
        0x55, 0x0E, 0x04, 0x66, 0x08, 0x02, 0x6A, 0xC0, 
        0xC0, 0x02, 0x8E, 0x00, 0xF6, 0x36
    };

    private static bool _authSuccessful = false;
    private static bool _wifiConfigured = false;
    private static bool _rtmpConfigured = false;
    private static bool _streamStartSuccessful = false;

    public static CancellationTokenSource authCancellationTokenSource = new();
    public static CancellationTokenSource wifiCancellationTokenSource = new();
    public static CancellationTokenSource rtmpCancellationTokenSource = new();
    public static CancellationTokenSource streamStartCancellationTokenSource = new();

    // Battery percentage storage
    private static int _batteryPercentage = -1; // -1 indicates unknown
    public static int BatteryPercentage => _batteryPercentage;
    // Constants based on the DjiMessage structure
    private const int TYPE_OFFSET = 8; // Type starts at offset 8 (after 0x55, length, version, header CRC, target, id)
    private const int BATTERY_OFFSET = 31; // Type (3 bytes) + 20 bytes = 23 bytes from type start, but we need absolute offset

    public static void ParseNotificationResponse(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return;
        }
        // First, check if this is a battery status message (type 0x020d00)
        if (IsBatteryStatusMessage(data) && BatteryPercentage == -1)
        {
            ExtractBatteryPercentage(data);
            // Don't return here as we might want to process other message types too
        }
        // Convert to hex for pattern matching
        string dataHex = BitConverter.ToString(data);

        // Check for authentication success
        if (!_authSuccessful && IsMatchingPattern(data, AuthSuccessResponse))
        {
            _authSuccessful = true;
            _logger.Debug("✅ AUTHENTICATION SUCCESSFUL! Device is now paired.");
            authCancellationTokenSource.Cancel();
            return;
        }

        // Check for wifi success
        if (_authSuccessful && !_wifiConfigured && IsMatchingPattern(data, WifiSuccessResponse))
        {
            _wifiConfigured = true;
            _logger.Debug("✅ WiFi CONFIGURATION SUCCESSFUL!");
            wifiCancellationTokenSource.Cancel();
            return;
        }

        // Check for rtmp config success
        if (_authSuccessful && _wifiConfigured && !_rtmpConfigured && IsMatchingPattern(data, RtmpConfigSuccessResponse))
        {
            _rtmpConfigured = true;
            _logger.Debug("✅ RTMP CONFIGURATION SUCCESSFUL!");
            rtmpCancellationTokenSource.Cancel();
            return;
        }

        // Check for stream start success
        if (_authSuccessful && _wifiConfigured && _rtmpConfigured && !_streamStartSuccessful && IsMatchingPattern(data, StreamStartSuccessResponse))
        {
            _streamStartSuccessful = true;
            _logger.Debug("✅ STREAM START SUCCESSFUL!");
            streamStartCancellationTokenSource.Cancel();
            return;
        }

        // If no known patterns, use legacy parsing
        ParseLegacyResponse(data);
    }

    private static bool IsBatteryStatusMessage(byte[] data)
    {
        // Check if we have enough data for a complete message with type field
        if (data.Length < TYPE_OFFSET + 3)
            return false;

        // Check if the message type is 0x020d00 (little-endian)
        // In little-endian, 0x020d00 is stored as 0x00, 0x0D, 0x02
        return data[TYPE_OFFSET] == 0x00 && 
               data[TYPE_OFFSET + 1] == 0x0D && 
               data[TYPE_OFFSET + 2] == 0x02;
    }

    private static void ExtractBatteryPercentage(byte[] data)
    {
        try
        {
            // Battery percentage is at offset 31 (TYPE_OFFSET + 3 + 20)
            if (data.Length >= BATTERY_OFFSET + 1)
            {
                _batteryPercentage = data[BATTERY_OFFSET];
                _logger.Debug($"🔋 Battery Percentage: {_batteryPercentage}%");
                
                // You might want to raise an event here so other parts of your application
                // can be notified when the battery percentage changes
            }
            else
            {
                _logger.Warning("Battery message too short to extract percentage");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error extracting battery percentage: {ex.Message}");
        }
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
        byte[] spamStatuses = [0x2E, 0xFC, 0x92, 0xA8, 0x63, 0x33];
        if (!spamStatuses.Contains(status) && !command.Equals(0x04))
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