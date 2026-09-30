using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SecsPureSocket
{
    public class HsmsGemClient
    {
        private readonly ushort _deviceId;
        private readonly bool _autoSelect;
        private readonly IPEndPoint _endPoint;
        private readonly Socket _clientSocket;
        private HsmsSessionStatus _sessionStatus;

        public event Action<HsmsMessage> OnMessageReceived;

        public HsmsGemClient(ushort deviceId = 0, string host = "127.0.0.1", int port = 5000, bool autoSelect = true)
        {
            _deviceId = deviceId;
            _autoSelect = autoSelect;
            _endPoint = new IPEndPoint(IPAddress.Parse(host), port);
            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public async Task ListenReplyAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] replyRawMessage = new byte[1024];
                int replyByteCount = await _clientSocket.ReceiveAsync(replyRawMessage);
                HsmsMessage reply = new HsmsMessage(replyRawMessage.Take(replyByteCount).ToArray());
                OnMessageReceived?.Invoke(reply);
            }
        }

        public async Task ConnectAsync()
        {
            // Implement the logic to establish a connection to the HSMS/GEM server.
            // This is a placeholder for the actual implementation.
            try
            {
                await _clientSocket.ConnectAsync(_endPoint);

                if (_autoSelect)
                {
                    _sessionStatus = HsmsSessionStatus.Selecting;
                    await SendAsync(new HsmsMessage(_deviceId, 0, 0, MessageType.SelectReq, HsmsSystemByteCounter.Instance.GetNextValue(), Array.Empty<byte>()));
                    byte[] replyRawMessage = new byte[1024];
                    int replyByteCount = await _clientSocket.ReceiveAsync(replyRawMessage);
                    HsmsMessage reply = new HsmsMessage(replyRawMessage.Take(replyByteCount).ToArray());
                    HsmsMessageHeader header = reply.Header;
                    if (header.MessageType == MessageType.SelectRsp)
                    {
                        _sessionStatus = HsmsSessionStatus.Selected;
                    }
                }

                ListenReplyAsync(CancellationToken.None); // Start listening for replies in the background
            }
            catch (Exception)
            {

                throw;
            }
        }

        private async Task SendAsync(HsmsMessage message)
        {
            try
            {


                await _clientSocket.SendAsync(message.GetRawHsmsMessage());
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task SendAsync(int streamNo, int functionNo, byte[] data,
            MessageType messageType = MessageType.DataMessage, bool needReply = true)
        {
            // Implement the logic to send the message and wait for a reply asynchronously.
            // This is a placeholder for the actual implementation.            
            await SendAsync(new HsmsMessage(_deviceId, (byte)streamNo, (byte)functionNo, messageType, HsmsSystemByteCounter.Instance.GetNextValue(), data));
        }
    }
}
