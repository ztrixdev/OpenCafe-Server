using MongoDB.Bson;
using MongoDB.Driver;
using server.Logging;

namespace server.DBmgmt;

public class Database
{
    private string? connectionString;
    private MongoClient? client;
    private IMongoDatabase? _database;

    public Database(DBConfig config)
    {
        connectionString = ConnectionString.Create(config);
        client = new MongoClient(connectionString);
        _database = client.GetDatabase(config.Name);
    }
    
    public async Task<bool> CheckConnection()
    {
        try
        {
            await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return true;
        }
        catch (MongoAuthenticationException exception)
        {
            Logger logger = new Logger();
            logger.New(new Log(type: "Error", message: exception.Message, where: exception.Source, DateTime.Now));
            Console.WriteLine("Unable to authenticate, re-enter your credentials.");
            return false;
        }
        catch (MongoConfigurationException exception)
        {
            Logger logger = new Logger();
            logger.New(new Log(type: "Error", message: exception.Message, where: exception.Source, DateTime.Now));
            Console.WriteLine("THe connection string is invalid.");
            return false;
        }
    }

    public async Task<BsonDocument> RunCommand(BsonDocument command)
    {
        var result = await _database.RunCommandAsync<BsonDocument>(command);
        return result;
    }
}
