using djiconnect.Utils;
using Tmds.DBus;
namespace djiconnect.EventHandlers;
public static class DjiEventHandler
{
    public static void OnPropertiesChanged(PropertyChanges changes)
    {
        try
        {
            foreach (var change in changes.Changed)
            {
                if (change.Key == "Value" && change.Value is byte[] data)
                {
                    // Always show the raw data regardless of content
                    Console.WriteLine($"RAW NOTIFY: {BitConverter.ToString(data)}");
                    
                    // Try to parse but show everything even if parsing fails
                    try
                    {
                        var notification = DjiNotificationParserUtils.ParseNotify(data);
                        if (notification.IsValid)
                        {
                            if (notification.AuthSuccess.HasValue)
                            {
                                Console.WriteLine($"AUTH: {(notification.AuthSuccess.Value ? "SUCCESS" : "FAILED")}");
                            }
                            else if (notification.StreamingStatus.HasValue)
                            {
                                string status = notification.StreamingStatus.Value switch
                                {
                                    0x00 => "STOPPED",
                                    0x01 => "STARTED",
                                    0x02 => "IN PROGRESS", 
                                    0x03 => "PREPARING",
                                    0x04 => "FAILED",
                                    _ => $"UNKNOWN (0x{notification.StreamingStatus.Value:X2})"
                                };
                                Console.WriteLine($"STREAMING: {status}");
                            }
                            else
                            {
                                Console.WriteLine("VALID BUT UNKNOWN RESPONSE");
                            }
                        }
                        else
                        {
                            Console.WriteLine("INVALID OR UNRECOGNIZED FORMAT");
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Console.WriteLine($"PARSE ERROR: {parseEx.Message}");
                    }
                }
                else if (change.Key == "Value")
                {
                    Console.WriteLine($"VALUE CHANGED (non-byte[]): {change.Value?.GetType().Name} = {change.Value}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PROPERTIES CHANGED ERROR: {ex.Message}");
        }
    }
}