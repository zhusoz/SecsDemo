using Microsoft.Extensions.Options;
using Secs4Net;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using static Secs4Net.Item;


namespace SecsDemo
{
    internal class Program
    {
        private static readonly SecsMessage _ping = new SecsMessage(s: 1, f: 13)
        {
            SecsItem = A("Ping"),
        };
        private static readonly SecsMessage _pong = new SecsMessage(s: 1, f: 14, replyExpected: false)
        {
            SecsItem = A("Pong"),
        };

        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            //using CancellationTokenSource cts = new CancellationTokenSource();
            //await SecsGemSendPingAsync(cts);
            //await StartGemHostDevice(cts);


            try
            {
                // StartDemoDevice();                
                await StartDemoClient();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SecsException: {ex.Message}");
                throw;
            }

            Console.ReadKey();
        }

        private static async Task StartDemoClient()
        {
            var hostOption = Options.Create(new SecsGemOptions
            {
                SocketReceiveBufferSize = 65535,
                DeviceId = 0,                
                Port = 5000,
                IsActive = true // 客户端主动，程序主动发起连接
            });

            var hostConnection = new HsmsConnection(hostOption, new DeviceLogger());
            using var secsGem = new SecsGem(hostOption, hostConnection, new DeviceLogger());
            using var cts = new CancellationTokenSource();
            hostConnection.Start(cts.Token);
                        
            await secsGem.SendAsync(new SecsMessage(1, 1, replyExpected: true));
            var reply = await secsGem.SendAsync(_ping, cts.Token);
            await Console.Out.WriteLineAsync($"reply == pong:{reply}");
        }

        private static async Task StartDemoDevice()
        {
            var deviceOption = Options.Create(new SecsGemOptions
            {
                SocketReceiveBufferSize = 65535,
                DeviceId = 0,
                Port = 5000,
                IsActive = false // 被动模式   设备监听客户端发起连接
            });
            var deviceConnection = new HsmsConnection(deviceOption, new DeviceLogger());

            using var secsGem = new SecsGem(deviceOption, deviceConnection, new DeviceLogger());
            using var cts = new CancellationTokenSource();
            deviceConnection.Start(cts.Token);

            await Console.Out.WriteLineAsync("等待接收消息");
            var msg = await secsGem.GetPrimaryMessageAsync(cts.Token).FirstAsync(cts.Token);
            await Console.Out.WriteLineAsync($"message:{msg.PrimaryMessage}");
            await msg.TryReplyAsync(_pong);

            
        }

        private static async Task StartGemHostDevice(CancellationTokenSource cts)
        {
            var hostOption = Options.Create(new SecsGemOptions
            {
                SocketReceiveBufferSize = 65535,
                DeviceId = 0,
                Port = 5001,
                IsActive = true
            });

            var hostConnection = new HsmsConnection(hostOption, new DeviceLogger());
            using var secsGem = new SecsGem(hostOption, hostConnection, new DeviceLogger());
            hostConnection.Start(cts.Token);

            _ = Task.Run(async () =>
            {
                var msg = await secsGem.GetPrimaryMessageAsync(cts.Token).FirstAsync(cts.Token);
                var isEqual = msg.PrimaryMessage == _ping;
                await Console.Out.WriteLineAsync($"isEqual:{isEqual}");
                await msg.TryReplyAsync(_pong);
            });
        }

        private static async Task SecsGemSendPingAsync(CancellationTokenSource cts)
        {
            var deviceOption = Options.Create(new SecsGemOptions
            {
                SocketReceiveBufferSize = 65535,
                DeviceId = 0,
                IsActive = false // 设备端监听, 通常由 Host 发起连接
            });
            var deviceConnection = new HsmsConnection(deviceOption, new DeviceLogger());

            using var secsGem = new SecsGem(deviceOption, deviceConnection, new DeviceLogger());

            deviceConnection.Start(cts.Token);

            await Task.Delay(2000);
            var reply = await secsGem.SendAsync(_ping, cts.Token);
            await Console.Out.WriteLineAsync($"reply == pong:{reply == _pong}");
        }
    }
}
