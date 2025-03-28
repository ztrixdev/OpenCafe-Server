using MongoDB.Driver;
using System.Threading.Tasks;

namespace server.DBmgmt;

public static class ConnectionString
{
    public static Task<string> CreateAsync(DBConfig dbConfig)
    {
        return Task.Run(() => 
            $"mongodb://{dbConfig.User}:{dbConfig.Password}@{dbConfig.Host}:{dbConfig.Port}/{dbConfig.Name}?authSource={dbConfig.AuthSource}");
    }

    public static Task<DBConfig> ReadAsync(string cStr)
    {
        return Task.Run(() =>
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
        });
    }
    
    public static string Create(DBConfig dbConfig)
    {
        return CreateAsync(dbConfig).GetAwaiter().GetResult();
    }
    
}