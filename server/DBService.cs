using server.DBmgmt;
using server.Logging;

namespace server;

public class DBService
{
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
        var configFile = await ConfigFile.Read();
        var ndb = new Database(configFile);

        // Check database connection asynchronously
        Console.WriteLine("Is connection successful: " + await ndb.CheckConnection());
        Console.WriteLine("Is OpenCafe: " + await ndb.CheckForOpenCafe());

        if (!await ndb.CheckForOpenCafe())
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
                where: "DBService::Start()",
                date: DateTime.Now
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
                        where: "DBService::Start()",
                        date: DateTime.Now
                    );
                    await logger.New(log); 
                    await Console.Error.WriteLineAsync(log.Message); 
                }
            }
        }

        Console.WriteLine("Keys are OK!");
        Console.WriteLine("Service started!");
        
        return ndb; // Return the initialized database object
    }
}