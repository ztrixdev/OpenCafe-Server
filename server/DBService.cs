using server.DBmgmt;
using server.Logging;

namespace server;

/// <summary>
/// DBService class. 
/// </summary>
public class DBService
{
    /// <summary>
    /// Starts the connection and validates everything.
    /// </summary>
    /// <returns>A new Database object</returns>
    public async Task<Database> Start()
    {
        Console.ForegroundColor = ConsoleColor.DarkBlue;
        Console.WriteLine("Start the DBService? [Y/n]: ");
        
        var startOrNot = await Console.In.ReadLineAsync();
        if (startOrNot?.ToLower() != "y")
        {
            Environment.Exit(1);
        }

        Console.ForegroundColor = ConsoleColor.Green;
        var configFile = ConfigFile.Read();
        var ndb = new Database(configFile);

        var isCon200 = await ndb.CheckConnection();
        Console.WriteLine("Is connection successful: " + isCon200);
        if (!isCon200)
        {
            Environment.Exit(1);
        }
        
        Console.WriteLine("Is OpenCafe: " + ndb.CheckForOpenCafe());

        if (!ndb.CheckForOpenCafe())
        {
            Console.Write("Should I initialize collections? [Y/n]: ");
            var initColOrNot = await Console.In.ReadLineAsync(); 
            if (initColOrNot?.ToLower() == "y")
            {
                await ndb.InitCollections(); 
            }

            Environment.Exit(0);
        }

        Console.WriteLine("Reading keys...");
        if (configFile.CollectionEncryption == null)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            var logger = new Logger();
            var log = new Log(
                type: "Error",
                message: "Collection encryption not specified. Unable to proceed, delete the old configuration file and restart the process.",
                where: "DBService::Start()"
            );
            await logger.New(log); 
            await Console.Error.WriteLineAsync(log.Message); 
            Environment.Exit(0);
        }

        foreach (var val in configFile.CollectionEncryption.Values)
        {
            foreach (var val2 in val.Values)
            {
                try
                {
                    Convert.FromBase64String(val2); 
                }
                catch (FormatException)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    var logger = new Logger();
                    var log = new Log(
                        type: "Error",
                        message: "Some of the keys/ivs are not valid! Unable to proceed, delete the old configuration file and restart the process.",
                        where: "DBService::Start()"
                    );
                    await logger.New(log); 
                    await Console.Error.WriteLineAsync(log.Message); 
                }
            }
        }

        Console.WriteLine("Keys are OK!");
        Console.WriteLine("Service started!");
        
        return ndb;
    }
}