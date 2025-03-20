using MongoDB.Driver;
using server.Logging;

namespace server.DBmgmt;

public class DBConfig
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Name { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? AuthSource { get; set; }
    
    public static async Task<DBConfig> Init()
    {
        var configMethod = 0;
        while (configMethod != 1 && configMethod != 2)
        {
            Console.WriteLine(@"We need to make a database configuration file!
        1. Enter credentials manually (Username, password, hostname, etc.).
        2. Enter a connection string.");
            try
            {
                configMethod = Convert.ToInt32(Console.ReadLine());
            }
            catch (FormatException)
            {
                Console.WriteLine("Please enter a valid integer!");
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
                dbConfig.Port = Convert.ToInt32(Console.ReadLine());
                Console.WriteLine("Enter default database to use:: ");
                dbConfig.Name = Console.ReadLine();
                Console.WriteLine("Enter DB username: ");
                dbConfig.User = Console.ReadLine();
                Console.WriteLine("Enter DB password: ");
                dbConfig.Password = Console.ReadLine();
                Console.WriteLine("Enter DB AuthSource: ");
                dbConfig.AuthSource = Console.ReadLine();
                
                var database = new Database(dbConfig);
                isConnectionSuccessful = await database.CheckConnection();
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
                        Console.WriteLine("Provided string is not a valid connection string!");
                        var logger = new Logger();
                        logger.New(new Log(type: "Error", message: exception.Message, where: exception.Source, date: DateTime.Now ));
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
            Console.WriteLine("Configuration failed. Try again!");
            Environment.Exit(0);
        }

        if (isConnectionSuccessful)
        {
            Console.WriteLine("Database connection successful! Creating a config file...");
        }
        
        return dbConfig;
    }
}
