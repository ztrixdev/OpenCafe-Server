using System.Text.Json;

namespace OpenCafe.Server.Logging;

public class Logger
{
    /// <summary>
    ///  Asynchronously writes a log into the logfile.
    /// </summary>
    /// <param name="log">a Log object</param>
    public async Task New(Log log)
    {
        var path = await LogFile.LocateAsync();
        var json = await File.ReadAllTextAsync(path);
        Log[] logs;
        using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
        {
            try
            {
                logs = await JsonSerializer.DeserializeAsync<Log[]>(stream);
                logs = logs.Append(log).ToArray();
                await using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
                await using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });
                JsonSerializer.Serialize(writer, logs);
            }
            catch (JsonException exception)
            {
                throw new JsonException($"Failed to parse log file: {path}", exception);
            }
        }
    }

    /// <summary>
    /// Logs an Exception into the logfile.
    /// </summary>
    /// <param name="exception">A C# exception.</param>
    public async Task LogException(Exception exception)
    {
        await New(new Log(type: "Exception", message: exception.Message, where: exception.Source));
    }
}
