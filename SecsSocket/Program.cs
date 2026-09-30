using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SecsSocket;

public class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        CancellationToken ct = new CancellationToken();
        //EquipmentServer.RunAsync(IPAddress.Loopback, 5000, ct: ct);

        
        HostClient.RunAsync(IPAddress.Loopback.ToString(), 5000);


        Console.ReadKey();
    }
}
