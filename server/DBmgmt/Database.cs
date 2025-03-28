using MongoDB.Bson;
using MongoDB.Driver;
using server.Logging;
using server.Collections;
using server.Helpers;

namespace server.DBmgmt;

public class Database
{
    private readonly string connectionString;
    private readonly MongoClient client;
    public IMongoDatabase _database;
    private readonly Dictionary<string, Dictionary<string, string>> collectionEncryption;

    public Database(DBConfig config)
    {
        connectionString = ConnectionString.Create(config);
        client = new MongoClient(connectionString);
        _database = client.GetDatabase(config.Name);
        collectionEncryption = config.CollectionEncryption;
    }
    
    public async Task<BsonDocument> RunCommand(BsonDocument command)
    {
        return await _database.RunCommandAsync<BsonDocument>(command);
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
            await Console.Out.WriteLineAsync("Unable to authenticate, re-enter your credentials.");
            return false;
        }
        catch (MongoConfigurationException exception)
        {
            var logger = new Logger();
            await logger.New(new Log(type: "Error", message: exception.Message, where: exception.Source, DateTime.Now));
            await Console.Out.WriteLineAsync("The connection string is invalid.");
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
        
        var collectionNames = await _database.ListCollectionNames().ToListAsync();
        foreach (var name in collectionNames)
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
        var logger = new Logger();
        await logger.New(new Log(type: "Info", message: "Initializing database collections.", where: "Database::InitCollections()", date: DateTime.Now));
        
        var key = collectionEncryption["admins"]["key"];
        var iv = collectionEncryption["admins"]["iv"];
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
        {
            Console.ForegroundColor = ConsoleColor.Red;

            var log = new Log(type: "Error",
                message: "Check or regenerate your db.cfg as it doesn't contain correct collection encryption credentials!",
                where: "Database::InitCollections()", 
                date: DateTime.Now);
            await logger.New(log);
            await Console.Error.WriteLineAsync(log.Message);
            Environment.Exit(1);
            return;
        }

        await _database.CreateCollectionAsync("customers"); 
        await _database.CreateCollectionAsync("admins");
        await _database.CreateCollectionAsync("dishes");
        await _database.CreateCollectionAsync("images");
        
        var firstHeadToken = await new Admins().GenTokenAsync();
        await Console.Out.WriteLineAsync("This is an auto-generated token for a head admin, it's CRUCIAL to write it down somewhere secure. It is also stored in a file in the app folder. You NEED to remove it afterwards." + Environment.NewLine + firstHeadToken);
        
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directoryPath = Path.Combine(appDataPath, "OpenCafe");
        Directory.CreateDirectory(directoryPath); // Ensure directory exists
        await File.WriteAllTextAsync(Path.Combine(directoryPath, "firsthead_token.txt"), firstHeadToken);
        
        firstHeadToken = await CryptoHelper.EncryptAsync(firstHeadToken, key, iv);
        var adminCollection = _database.GetCollection<BsonDocument>("admins");
        await adminCollection.InsertOneAsync(new Admin(name: "FIRSTADMIN", role: "head", token: firstHeadToken).ToBsonDocument());
    }
}