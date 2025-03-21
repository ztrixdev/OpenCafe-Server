using System.Text.Json;

namespace server.Logging;

public class Logger
{
    public async Task New(Log log)
    {
        var path = await LogFile.Locate();
        var json = await File.ReadAllTextAsync(path);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        try
        {
            var logs = await JsonSerializer.DeserializeAsync<Log[]>(stream);
            logs = logs.Append(log).ToArray();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(logs));
        }
        catch (JsonException exception)
        {
            await Console.Error.WriteLineAsync("Can't deserialize the log file (not a valid JSON)! Check if it's valid or remove it." + Environment.NewLine);
            await Console.Error.WriteLineAsync("Log file path: "  + path + "Exception message: " + exception.Message);
        }
    }
}
