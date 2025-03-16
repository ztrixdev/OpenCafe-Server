using System.Text.Json;

namespace server.Logging;

public class LogFile
{
    private static readonly string DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"/OpenCafe/";
    private static readonly string LogFilePath = DirectoryPath + "/log.json";

    public static string Locate()
    {
        if (File.Exists(LogFilePath)) return LogFilePath;
        Create();
        return LogFilePath;
    }
    
    private static void Create()
    {
        Log log = new Log("Notification", "Logfile created", "server.Logging.LogFile.Create()", DateTime.Now);
        // TODO: Make logging system work. (It doesn't now)
    }
}