using System.Text.Json;
using server.Helpers;

namespace server.DBmgmt;

class ConfigFile
{
    private static readonly string DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"/OpenCafe/";
    private static readonly string ConfigFilePath = DirectoryPath + "db.cfg";
    
    public static async Task<DBConfig> Read()
    {
        var key = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
        var iv = Environment.GetEnvironmentVariable("ENCRYPTION_IV");

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
        {
            Console.Error.WriteLine("Ensure you have AES-based ENCRYPTION_KEY and ENCRYPTION_IV in your environment variables!");
            Environment.Exit(1);
            return null;
        }

        if (File.Exists(ConfigFilePath))
        {
            var json = File.ReadAllText(ConfigFilePath);
            json = CryptoHelper.Decrypt(json, key, iv);
            return JsonSerializer.Deserialize<DBConfig>(json);
        }

        if (!Directory.Exists(DirectoryPath))
        {
            Directory.CreateDirectory(DirectoryPath);
        }
        var init = await DBConfig.Init();
        return JsonSerializer.Deserialize<DBConfig>(CryptoHelper.Decrypt(New(init, key, iv), key, iv));
    }

    private static string New(DBConfig dbConfig, string key, string iv)
    {
        var json = JsonSerializer.Serialize(dbConfig);
        json = CryptoHelper.Encrypt(json, key, iv);
        File.WriteAllText(ConfigFilePath, json);
        return File.ReadAllText(ConfigFilePath);
    }
}
