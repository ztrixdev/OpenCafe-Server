using System.Text.Json;

namespace server.Logging;

public class Logger
{
    public void New(Log log)
    {
        var path = LogFile.Locate();
        Log[] logs = JsonSerializer.Deserialize<Log[]>(File.ReadAllText(path));
        logs = logs.Append(log).ToArray();
        File.WriteAllText(path, JsonSerializer.Serialize(logs));
    }
}
