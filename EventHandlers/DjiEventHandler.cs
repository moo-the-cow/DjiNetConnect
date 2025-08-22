using djiconnect.Utils;
using Serilog;
using Tmds.DBus;
namespace djiconnect.EventHandlers;
public static class DjiEventHandler
{
    private static readonly Serilog.ILogger _logger = Log.Logger;
    public static void OnPropertiesChanged(PropertyChanges changes)
    {
        try
        {
            foreach (var change in changes.Changed)
            {
                if (change.Key == "Value" && change.Value is byte[] data)
                {
                    // Always show the raw data regardless of content
                    _logger.Information($"RAW NOTIFY: {BitConverter.ToString(data)}");

                    // Try to parse but show everything even if parsing fails
                    try
                    {
                        var notification = DjiNotificationParserUtils.ParseNotify(data);
                        if (notification.IsValid)
                        {
                            if (notification.AuthSuccess.HasValue)
                            {
                                _logger.Information($"AUTH: {(notification.AuthSuccess.Value ? "SUCCESS" : "FAILED")}");
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
                                _logger.Information($"STREAMING: {status}");
                            }
                            else
                            {
                                _logger.Information("VALID BUT UNKNOWN RESPONSE");
                            }
                        }
                        else
                        {
                            _logger.Information("INVALID OR UNRECOGNIZED FORMAT");
                        }
                    }
                    catch (Exception parseEx)
                    {
                        _logger.Information($"PARSE ERROR: {parseEx.Message}");
                    }
                }
                else if (change.Key == "Value")
                {
                    _logger.Information($"VALUE CHANGED (non-byte[]): {change.Value?.GetType().Name} = {change.Value}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Information($"PROPERTIES CHANGED ERROR: {ex.Message}");
        }
    }
}