using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SecsPureSocket
{
    public class HsmsMessageHeader
    {
        public ushort DeviceId { get; }
        public byte StreamNo { get; }
        public byte FunctionNo { get; }
        public MessageType MessageType { get; }
        public uint SystemId { get; }
        public byte[] RawData => GetRawData();
        public bool NeedReplay => (StreamNo >> 7) == 1;

        public HsmsMessageHeader(byte[] rawData)
        {
            ArgumentNullException.ThrowIfNull(rawData, nameof(rawData));
            if (rawData.Length != 10)
            {
                throw new ArgumentException("Invalid raw data length for HSMS message header. Expected 10 bytes.", nameof(rawData));
            }

            DeviceId = BitConverter.ToUInt16(rawData.Take(2).Reverse().ToArray());
            StreamNo = rawData[2];
            FunctionNo = rawData[3];
            MessageType = (MessageType)rawData[5];
            SystemId = BitConverter.ToUInt32(rawData.Skip(6).Take(4).Reverse().ToArray());
        }
        public HsmsMessageHeader(
            ushort deviceId,
            byte streamNo, byte functionNo,
            MessageType messageType,
            uint systemId,
            bool needReplay = true)
        {
            DeviceId = deviceId;
            StreamNo = needReplay ? (byte)(streamNo + 0x80) : streamNo;
            FunctionNo = functionNo;
            MessageType = messageType;
            SystemId = systemId;
        }

        private byte[] GetRawData()
        {
            IEnumerable<byte> deviceBytes = BitConverter.GetBytes(DeviceId).Reverse(); // Fixed length : 2
            byte[] data = [
                    deviceBytes.ElementAt(0), deviceBytes.ElementAt(1), // deviceId
                    StreamNo, // streamNo
                    FunctionNo, // functionNo
                    0, // PType (1 byte): Presentation type(always 0 for SECS - II)
                    (byte)MessageType // SType (1 byte): Message type (SelectReq = 1)                    
                ];
            data = data.Concat(BitConverter.GetBytes(SystemId).Reverse()).ToArray();
            if (data.Length != 10)
            {
                throw new InvalidOperationException("Header length is not 10 bytes.");
            }

            return data;
        }

    }
}
