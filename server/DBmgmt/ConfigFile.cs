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
            await Console.Error.WriteLineAsync("Ensure you have AES-based ENCRYPTION_KEY and ENCRYPTION_IV in your environment variables!");
            Environment.Exit(1);
            return null;
        }

        if (File.Exists(ConfigFilePath))
        {
            var json = await File.ReadAllTextAsync(ConfigFilePath);
            json = await CryptoHelper.DecryptAsync(json, key, iv);
            var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            var cfg = await JsonSerializer.DeserializeAsync<DBConfig>(ms); 
            await ms.DisposeAsync();
            return cfg;
        }

        if (!Directory.Exists(DirectoryPath))
        {
            Directory.CreateDirectory(DirectoryPath);
        }

        var init = await DBConfig.Init();
        var encryptedInit = await CryptoHelper.DecryptAsync(await New(init, key, iv), key, iv); 
        
        return await Task.FromResult(JsonSerializer.Deserialize<DBConfig>(encryptedInit)); 
    }

    private static async Task<string> New(DBConfig dbConfig, string key, string iv)
    {
        using (var ms = new MemoryStream())
        {
            await JsonSerializer.SerializeAsync(ms, dbConfig);
            ms.Seek(0, SeekOrigin.Begin);
            
            using (var reader = new StreamReader(ms, System.Text.Encoding.UTF8))
            {
                var serializedContent = await reader.ReadToEndAsync(); 
                serializedContent = await CryptoHelper.EncryptAsync(serializedContent, key, iv);
                await File.WriteAllTextAsync(ConfigFilePath, serializedContent);
                return await File.ReadAllTextAsync(ConfigFilePath);
            }
        }
    }
}
