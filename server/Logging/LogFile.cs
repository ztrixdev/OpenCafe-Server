using System.Text.Json;

namespace server.Logging;

public class LogFile
{
    private static readonly string DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"/OpenCafe/";
    private static readonly string LogFilePath = DirectoryPath + "/log.json";
    
    public static async Task<string> Locate()
    {
        if (File.Exists(LogFilePath)) return LogFilePath;
        await Create();
        return LogFilePath;
    }
    
    private static async Task Create()
    {
        Log log = new Log("Notification", "Logfile created", "server.Logging.LogFile.Create()", DateTime.Now);
        await File.WriteAllTextAsync(LogFilePath, $"[{JsonSerializer.Serialize(log)}]");
    }
}