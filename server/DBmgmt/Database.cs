using MongoDB.Bson;
using MongoDB.Driver;
using server.Logging;
using System.Linq;
using server.Collections;
using server.Helpers;

namespace server.DBmgmt;

public class Database
{
    private string connectionString;
    private MongoClient client;
    private IMongoDatabase _database;

    public Database(DBConfig config)
    {
        connectionString = ConnectionString.Create(config);
        client = new MongoClient(connectionString);
        _database = client.GetDatabase(config.Name);
    }
    
    public async Task<BsonDocument> RunCommand(BsonDocument command)
    {
        var result = await _database.RunCommandAsync<BsonDocument>(command);
        return result;
    }
    
    public async Task<bool> CheckConnection()
    {
        try
        {
            await RunCommand(new BsonDocument("ping", 1));
            return true;
        }
        catch (MongoAuthenticationException exception)
        {
            var logger = new Logger();
            await logger.New(new Log(type: "Error", message: exception.Message, where: exception.Source, DateTime.Now));
            Console.WriteLine("Unable to authenticate, re-enter your credentials.");
            return false;
        }
        catch (MongoConfigurationException exception)
        {
            var logger = new Logger();
            await logger.New(new Log(type: "Error", message: exception.Message, where: exception.Source, DateTime.Now));
            Console.WriteLine("THe connection string is invalid.");
            return false;
        }
    }

    public async Task<bool> CheckForOpenCafe()
    {
        var areCollectionsPresent = new Dictionary<string, bool>()
        {
            { "customers", false }, { "admins", false },
            { "dishes", false }, { "images", false }
        };
        
        var collectionNames = await _database.ListCollectionNamesAsync().Result.ToListAsync<string>();
        foreach (string name in collectionNames)
        {
            if (areCollectionsPresent.ContainsKey(name))
            {
                areCollectionsPresent[name] = true;
            }
        }

        foreach (KeyValuePair<string, bool> kvp in areCollectionsPresent)
        {
            if (kvp.Value == false)
            {
                return false;
            }
        }

        return true;
    }

    public async Task InitCollections()
    {
        var key = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
        var iv = Environment.GetEnvironmentVariable("ENCRYPTION_IV");
        
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
        {
            Console.Error.WriteLine("Ensure you have AES-based ENCRYPTION_KEY and ENCRYPTION_IV in your environment variables!");
            Environment.Exit(1);
            return;
        }

        await _database.CreateCollectionAsync("customers");
        await _database.CreateCollectionAsync("admins");
        await _database.CreateCollectionAsync("dishes");
        await _database.CreateCollectionAsync("images");
        
        var firstHeadToken = await new Admins().GenToken();
        Console.WriteLine("This is an auto-generated token for a head admin, it's CRUCIAL to write it down somewhere secure. It is also stored in a file in the app folder. You NEED to remove it afterwards." + Environment.NewLine + firstHeadToken);
        await File.WriteAllTextAsync(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"/OpenCafe/firsthead_token.txt", firstHeadToken);
        
        firstHeadToken = await CryptoHelper.EncryptAsync(firstHeadToken, key, iv);
        var adminCollection = _database.GetCollection<BsonDocument>("admins");
        await adminCollection.InsertOneAsync(new Admin(name: "FIRSTADMIN", role: "head",  token: firstHeadToken).ToBsonDocument());
    }
}
