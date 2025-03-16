using MongoDB.Bson;
using MongoDB.Driver;

namespace server.DBmgmt;

public class Database
{
    public static async Task<bool> CheckConnection(DBConfig dbConfig)
    {
        try
        {
            var connectionString = ConnectionString.Create(dbConfig);
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(dbConfig.Name);
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return false;
        }
    }
}
