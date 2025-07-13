using System.Security.Authentication;
using System.Text.Json;
using OpenCafe.Server.Helpers;
using OpenCafe.Server.Logging;

namespace OpenCafe.Server.DBmgmt;

/// <summary>
/// Config file class. Implements methods for reading the file and creating a new one. 
/// </summary>
class ConfigFile
{
    private static readonly string DirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"/OpenCafe/";
    private static readonly string ConfigFilePath = DirectoryPath + "db.cfg";
    
    /// <summary>
    /// Decrypts and reads the JSON data from the ConfigFile.
    /// </summary>
    /// <returns>A new DBConfig</returns>
    /// <exception cref="ArgumentNullException">Threw if db.cfg is null</exception>
    public static DBConfig Read()
    {
        var key = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
        var iv = Environment.GetEnvironmentVariable("ENCRYPTION_IV");

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
        {
            throw new AuthenticationException("Missing environment variable: ENCRYPTION_KEY or ENCRYPTION_IV");
        }

        if (File.Exists(ConfigFilePath))
        {
            var json = File.ReadAllText(ConfigFilePath);
            try
            {
                json = CryptoHelper.DecryptDBCfgAsync(json, key, iv).Result;
            }
            catch (AggregateException exception)
            { 
                Task.Run(() => new Logger().LogException(exception));
                throw new FormatException("The config file is corrupted. Please replace it with one of the backups or generate a new one.");
            }
            var cfg = JsonSerializer.Deserialize<DBConfig>(json) ?? throw new ArgumentNullException("cfg");
            return cfg;
        }

        if (!Directory.Exists(DirectoryPath))
        {
            Directory.CreateDirectory(DirectoryPath);
        }

        var init = DBConfig.Init();
        var encryptedInit = CryptoHelper.DecryptDBCfgAsync(New(init, key, iv), key, iv).Result; 
        
        return JsonSerializer.Deserialize<DBConfig>(encryptedInit); 
    }

    /// <summary>
    /// Creates a new config file inside the $SpecialFolder.ApplicationData/OpenCafe/ folder named db.cfg.
    /// The file stores encrypted configuration JSON.
    /// Key and IV are usually stored in the environment variables but can be replaced with any 16-bit Base64 strings.
    /// </summary>
    /// <param name="dbConfig"></param>
    /// <param name="key">env variable ENCRYPTION_KEY</param>
    /// <param name="iv">env variable ENCRYPTION_IV</param>
    /// <returns>string - contents of the new db.cfg file</returns>
    private static string New(DBConfig dbConfig, string key, string iv)
    {
        var json = JsonSerializer.Serialize(dbConfig);
        var encryptedCfg = CryptoHelper.EncryptDBCfgAsync(json, key, iv).Result;
        // Creates the main db.cfg
        File.WriteAllText(ConfigFilePath, encryptedCfg);
        // Creates duplicates (db.cfg.1.bckp, db.cfg.2.bckp)
        File.WriteAllText(ConfigFilePath + ".1.bckp", encryptedCfg);
        File.WriteAllText(ConfigFilePath + ".2.bckp", encryptedCfg);
        return File.ReadAllText(ConfigFilePath);
    }
}
