namespace OpenCafe.Server.Logging;

/// <summary>
/// Log class. Used to log errors, notifications, requests etc. 
/// </summary>
public class Log
{
    public string? Type { get; set; }
    public string? Message { get; set; }
    public string? Where { get; set; }
    
    public DateTime? Date = DateTime.Now;
    
    public Log (string? type, string? message, string? where)
    {
        Type = type;
        Message = message;
        Where = where;
    }
}
