using HslCommunication;
using HslCommunication.Secs;

namespace SecsHslDemo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            bool isActived = HslCommunication.Authorization.SetAuthorizationCode("a42e6a88-b14a-4bca-87bc-d3a311040bca");
            SecsHsmsServer server = new SecsHsmsServer();
            server.ServerStart(5000, modeTcp: true);
            server.OnSecsMessageReceived += Server_OnSecsMessageReceived;
            
            await Task.Delay(1000);
            StartClient();

            Console.ReadKey();                    
        }

        private static void Server_OnSecsMessageReceived(object sender, HslCommunication.Core.Net.PipeSession session, HslCommunication.Secs.Types.SecsMessage message)
        {
            Console.WriteLine($"收到客户端消息 SteamNo:{message.StreamNo} FunctionNo:{message.FunctionNo}");
        }

        private static async Task StartClient()
        {
            SecsHsms client = new SecsHsms("127.0.0.1", 5000);
            client.AutoBackS1F1 = false;
            OperateResult connectedResult = await client.ConnectServerAsync();
            Console.WriteLine($"connectedResult:{connectedResult.IsSuccess}");
            
            OperateResult result = await client.SendByCommandAsync(stream: 1, function: 13, data: [], back: false);
            Console.WriteLine($"result:{result.IsSuccess}");

        }
    }
}
