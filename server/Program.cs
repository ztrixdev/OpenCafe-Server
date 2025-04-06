using server;

Console.WriteLine("Echo DBConfig? [Y/n]");
var ans = Console.ReadLine().ToUpper();
if (ans == "Y")
{
    new Utils().EchoConfig();
}

var service = new DBService();
await service.Start();


