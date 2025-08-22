namespace djiconnect.Utils;
public static class DjiNotificationParserUtils
{
    public static DjiNotification ParseNotify(byte[] data)
    {
        if (data.Length < 6)
            return new DjiNotification { RawData = data, IsValid = false };

        try
        {
            // Check if this is an authentication response
            if (data[4] == 0x07 && data[5] == 0x02)
            {
                if (data.Length >= 13 && data[9] == 0x07 && data[10] == 0x45)
                {
                    if (data[11] == 0x00 && data[12] == 0x01)
                        return new DjiNotification { RawData = data, IsValid = true, AuthSuccess = true };

                    if (data[11] == 0x00 && data[12] == 0x02)
                        return new DjiNotification { RawData = data, IsValid = true, AuthSuccess = false };
                }
            }

            // Check for streaming status responses
            if (data.Length >= 10 && data[4] == 0x07 && data[5] == 0x02)
            {
                if (data[9] == 0x07 && data[10] == 0x43)
                {
                    // Streaming status response
                    return new DjiNotification
                    {
                        RawData = data,
                        IsValid = true,
                        StreamingStatus = data.Length > 11 ? data[11] : (byte)0
                    };
                }
            }

            // Generic valid response
            return new DjiNotification { RawData = data, IsValid = true };
        }
        catch
        {
            return new DjiNotification { RawData = data, IsValid = false };
        }
    }
}

public class DjiNotification
{
    public byte[] RawData { get; set; }
    public bool IsValid { get; set; }
    public bool? AuthSuccess { get; set; }
    public byte? StreamingStatus { get; set; }
    
    public override string ToString()
    {
        if (!IsValid) return "Invalid notification";
        
        if (AuthSuccess.HasValue)
            return AuthSuccess.Value ? "Authentication successful" : "Authentication failed";
        
        if (StreamingStatus.HasValue)
            return $"Streaming status: {StreamingStatus.Value}";
        
        return $"Notification: {BitConverter.ToString(RawData)}";
    }
}