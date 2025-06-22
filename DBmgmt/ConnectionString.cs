using MongoDB.Driver;

namespace OpenCafe.Server.DBmgmt;

public static class ConnectionString
{
    /// <summary>
    /// Creates a new MongoDB connection string from DBConfig.
    /// </summary>
    /// <param name="dbConfig"></param>
    /// <returns>string - the generated connection string</returns>
    public static string Create(DBConfig dbConfig)
    {
        return $"mongodb://{dbConfig.User}:{dbConfig.Password}@{dbConfig.Host}:{dbConfig.Port}/{dbConfig.Name}?authSource={dbConfig.AuthSource}";
    }

    /// <summary>
    /// Reads the contents from a MongoDB connection string. 
    /// </summary>
    /// <param name="cStr">MongoDB connection string</param>
    /// <returns>A mew DBConfig with the data extracted from the cStr</returns>
    public static DBConfig Read(string cStr)
    {
        var mongoUrl = new MongoUrl(cStr);
        return new DBConfig
        {
            Host = mongoUrl.Server.Host,
            Port = mongoUrl.Server.Port,
            Name = mongoUrl.DatabaseName,
            User = mongoUrl.Username,
            Password = mongoUrl.Password,
            AuthSource = mongoUrl.AuthenticationSource
        };
    }
}
