namespace server.Logging;

public class Log
{
    public string? Type { get; set; }
    public string? Message { get; set; }
    public string? Where { get; set; }
    public DateTime? Date { get; set; }

    public Log (string? type, string? message, string? where , DateTime? date)
    {
        this.Type = type;
        this.Message = message;
        this.Where = where;
        this.Date = date;
    }
}
