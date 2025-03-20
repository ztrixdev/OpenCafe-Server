using System.Text;
using System.Text.Json;

namespace server.Logging;

public class Logger
{
    public async Task New(Log log)
    {
        var path = LogFile.Locate();
        var json = await File.ReadAllTextAsync(path);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        
        var logs = await JsonSerializer.DeserializeAsync<Log[]>(stream);
        logs = logs.Append(log).ToArray();
        
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(logs));
    }
}
