using MongoDB.Driver;
using server.Helpers;
using server.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace server.DBmgmt;

public class DBConfig
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Name { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? AuthSource { get; set; }

    public Dictionary<string, Dictionary<string, string>> CollectionEncryption { get; set; }
    
    public static async Task<DBConfig> Init()
    {
        var configMethod = 0;
        while (configMethod != 1 && configMethod != 2)
        {
            await Console.Out.WriteLineAsync(@"We need to make a database configuration file!
        1. Enter credentials manually (Username, password, hostname, etc.).
        2. Enter a connection string.");
            try
            {
                var input = await Console.In.ReadLineAsync();
                configMethod = Convert.ToInt32(input);
            }
            catch (FormatException)
            {
                await Console.Error.WriteLineAsync("Please enter a valid integer!");
            }
        }

        var dbConfig = new DBConfig();
        var isConnectionSuccessful = false;
        var attempts = 0;
        
        while (!isConnectionSuccessful && attempts < 5)
        {
            if (configMethod == 1)
            {
                await Console.Out.WriteLineAsync("Enter DB hostname (format: mydomainname.org): ");
                dbConfig.Host = await Console.In.ReadLineAsync();
                
                await Console.Out.WriteLineAsync("Enter DB port: ");
                var portInput = await Console.In.ReadLineAsync();
                dbConfig.Port = Convert.ToInt32(portInput);
                
                await Console.Out.WriteLineAsync("Enter default database to use: ");
                dbConfig.Name = await Console.In.ReadLineAsync();
                
                await Console.Out.WriteLineAsync("Enter DB username: ");
                dbConfig.User = await Console.In.ReadLineAsync();
                
                await Console.Out.WriteLineAsync("Enter DB password: ");
                dbConfig.Password = await Console.In.ReadLineAsync();
                
                await Console.Out.WriteLineAsync("Enter DB AuthSource: ");
                dbConfig.AuthSource = await Console.In.ReadLineAsync();
                
                var database = new Database(dbConfig);
                isConnectionSuccessful = await database.CheckConnection();
                attempts++;
            }
            else
            {
                await Console.Out.WriteLineAsync("Enter your connection string: ");
                var connectionString = await Console.In.ReadLineAsync();
                
                if (connectionString != null)
                {
                    try
                    {
                        dbConfig = await ConnectionString.ReadAsync(connectionString);
                    }
                    catch (MongoConfigurationException exception)
                    {
                        await Console.Error.WriteLineAsync("Provided string is not a valid connection string!");
                        var logger = new Logger();
                        await logger.New(new Log(
                            type: "Error", 
                            message: exception.Message, 
                            where: exception.Source, 
                            date: DateTime.Now));
                        isConnectionSuccessful = false;
                        attempts++;
                        continue;
                    }
                }
                
                var database = new Database(dbConfig);
                isConnectionSuccessful = await database.CheckConnection();
                attempts++;
            }
        }

        if (attempts >= 5)
        {
            await Console.Error.WriteLineAsync("Configuration failed. Try again!");
            Environment.Exit(0);
        }
        
        await Console.Out.WriteLineAsync("Creating collection encryption keys and ivs...");
        dbConfig.CollectionEncryption = new Dictionary<string, Dictionary<string, string>>()
        {
            { "admins", new Dictionary<string, string>{
                {"key", await CryptoHelper.RandomBase64Async()}, 
                {"iv", await CryptoHelper.RandomBase64Async()}} 
            },
            { "customers", new Dictionary<string, string>{
                {"key", await CryptoHelper.RandomBase64Async()}, 
                {"iv", await CryptoHelper.RandomBase64Async()}} 
            }
        };
        
        if (isConnectionSuccessful)
        {
            await Console.Out.WriteLineAsync("Database connection successful! Creating a config file...");
        }
        
        return dbConfig;
    }
}