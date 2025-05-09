using System.Text.Json;
using server.DBmgmt;

namespace server;

public class Utils
{
    public void EchoConfig()
    {
        var cfg = JsonSerializer.Serialize(ConfigFile.Read());
        Console.WriteLine(cfg);
    }
}