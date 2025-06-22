using System.Text.Json;

namespace OpenCafe.Server.Logging;
/// <summary>
/// LogFile class. 
/// </summary>
public static class LogFile
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "OpenCafe");
    
    private static readonly string LogFilePath = Path.Combine(DirectoryPath, "log.json");
    
    public static async Task<string> LocateAsync()
    {
        try
        {
            if (File.Exists(LogFilePath)) return LogFilePath;
            await CreateAsync();
            return LogFilePath;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to locate log file: {ex.Message}");
            throw;
        }
    }
    
    private static async Task CreateAsync()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var log = new Log(
                type: "Info",
                message: "Logfile created",
                where: "OpenCafe.Server.Logging.LogFile.CreateAsync()");
                
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            await File.WriteAllTextAsync(LogFilePath, $"[{JsonSerializer.Serialize(log, options)}]");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to create log file: {ex.Message}");
            throw;
        }
    }
}