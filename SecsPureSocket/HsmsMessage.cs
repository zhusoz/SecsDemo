using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsPureSocket;

public class HsmsMessage
{
    public HsmsMessageLength Length { get; }
    public HsmsMessageHeader Header { get; }
    public HsmsMessageData Data { get; }

    private HsmsMessage(HsmsMessageHeader header, HsmsMessageData data)
    {
        Length = HsmsMessageLength.Create(header, data);
        Header = header;
        Data = data;
    }

    public HsmsMessage(ushort deviceId, byte streamNo, byte functionNo, MessageType messageType, uint systemId,
        byte[] data, bool needReply = true) :
        this(new HsmsMessageHeader(deviceId, streamNo, functionNo, messageType, systemId, needReply), new HsmsMessageData(data))
    {

    }

    public HsmsMessage(byte[] rawData)
    {
        if (rawData == null || rawData.Length < 0)
        {
            throw new ArgumentException("Invalid raw data length for HSMS message.");
        }

        Length = new HsmsMessageLength(rawData.Take(4).ToArray());
        Header = new HsmsMessageHeader(rawData.Skip(4).Take(10).ToArray());
        Data = new HsmsMessageData(rawData.Skip(14).ToArray());
    }


    public byte[] GetRawHsmsMessage() => Length.RawData.Concat(Header.RawData).Concat(Data.RawData).ToArray();
}
