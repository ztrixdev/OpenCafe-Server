using System.Text.Json;

namespace server.Logging;

public class Logger
{
    public void New(Log log)
    {
        var path = LogFile.Locate();
        var writer = File.AppendText(path);
        writer.WriteLine(JsonSerializer.Serialize(log));
    }
}
