using MongoDB.Driver;
using server.Helpers;
using server.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace server.DBmgmt;

/// <summary>
/// DBConfig class. Contains database and data encryption credentials.
/// </summary>
public class DBConfig
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Name { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? AuthSource { get; set; }

    public Dictionary<string, Dictionary<string, string>>? CollectionEncryption { get; set; }

    /// <summary>
    /// Initializes a new DBConfig with user-typed settings.
    /// </summary>
    /// <returns>A new DBConfig</returns>
    public static DBConfig Init()
    {
        var configMethod = 0;
        while (configMethod != 1 && configMethod != 2)
        {
            Console.WriteLine(@"We need to make a database configuration file!
        1. Enter credentials manually (Username, password, hostname, etc.).
        2. Enter a connection string.");
            try
            {
                var input = Console.ReadLine();
                configMethod = Convert.ToInt32(input);
            }
            catch (FormatException)
            {
                Console.Error.WriteLine("Please enter a valid integer!");
            }
        }

        var dbConfig = new DBConfig();
        var isConnectionSuccessful = false;
        var attempts = 0;

        while (!isConnectionSuccessful && attempts < 5)
        {
            if (configMethod == 1)
            {
                Console.WriteLine("Enter DB hostname (format: mydomainname.org): ");
                dbConfig.Host = Console.ReadLine();

                Console.WriteLine("Enter DB port: ");
                var portInput = Console.ReadLine();
                dbConfig.Port = Convert.ToInt32(portInput);

                Console.WriteLine("Enter default database to use: ");
                dbConfig.Name = Console.ReadLine();

                Console.WriteLine("Enter DB username: ");
                dbConfig.User = Console.ReadLine();

                Console.WriteLine("Enter DB password: ");
                dbConfig.Password = Console.ReadLine();

                Console.WriteLine("Enter DB AuthSource: ");
                dbConfig.AuthSource = Console.ReadLine();

                var database = new Database(dbConfig);
                isConnectionSuccessful = database.CheckConnection().Result;
                attempts++;
            }
            else
            {
                Console.WriteLine("Enter your connection string: ");
                var connectionString = Console.ReadLine();

                if (connectionString != null)
                {
                    try
                    {
                        dbConfig = ConnectionString.Read(connectionString);
                    }
                    catch (MongoConfigurationException exception)
                    {
                        Console.Error.WriteLine("Provided string is not a valid connection string!");

                        var logger = new Logger();
                        Task.Run(() => logger.LogException(exception));

                        isConnectionSuccessful = false;
                        attempts++;
                        continue;
                    }
                }

                var database = new Database(dbConfig);
                isConnectionSuccessful = database.CheckConnection().Result;
                attempts++;
            }
        }

        if (attempts >= 5)
        {
            Console.Error.WriteLine("Configuration failed. Try again!");
            Environment.Exit(0);
        }

        Console.WriteLine("Creating collection encryption keys and ivs...");
        dbConfig.CollectionEncryption = new Dictionary<string, Dictionary<string, string>>()
        {
            { "admins", new Dictionary<string, string>
                {
                    {"key", CryptoHelper.RandomBase64Async().Result},
                    {"iv", CryptoHelper.RandomBase64Async().Result}
                }
            },
            { "customers", new Dictionary<string, string>
                {
                    {"key", CryptoHelper.RandomBase64Async().Result},
                    {"iv", CryptoHelper.RandomBase64Async().Result}
                }
            },
            {"cards", new Dictionary<string, string>
                {
                    {"key", CryptoHelper.RandomBase64Async().Result},
                    {"iv", CryptoHelper.RandomBase64Async().Result}
                }
            }
        };

        if (isConnectionSuccessful)
        {
            Console.WriteLine("Database connection successful! Creating a config file...");
        }

        return dbConfig;
    }
}
