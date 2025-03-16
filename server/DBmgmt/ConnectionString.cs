using MongoDB.Driver;

namespace server.DBmgmt;

class ConnectionString
{
    public static string Create(DBConfig dbConfig)
    {
        return $"mongodb://{dbConfig.User}:{dbConfig.Password}@{dbConfig.Host}:{dbConfig.Port}/{dbConfig.Name}?authSource={dbConfig.AuthSource}";
    }

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

