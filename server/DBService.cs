using server.DBmgmt;

namespace server;

public class DBService
{
    public async Task Start()
    {
        Database ndb = new Database(await ConfigFile.Read());
        Console.WriteLine("Is connection successful: " + await ndb.CheckConnection());
        Console.WriteLine("Is OpenCafe: " + await ndb.CheckForOpenCafe());
        if (!await ndb.CheckForOpenCafe())
        {
            Console.Write("Should we initialize collections? [Y/n]: ");
            var answer = Console.ReadLine();
            if (answer.ToLower() == "y")
            {
                await ndb.InitCollections();
            }
        }
    }
    
}