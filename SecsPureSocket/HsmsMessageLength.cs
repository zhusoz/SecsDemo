using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsPureSocket;

public class HsmsMessageLength
{
    public HsmsMessageLength(byte[] rawData)
    {
        RawData = rawData;
    }

    public byte[] RawData { get; }

    public static HsmsMessageLength Create(HsmsMessageHeader header, HsmsMessageData data)
    {
        return new HsmsMessageLength(BitConverter.GetBytes(header.RawData.Length + data.RawData.Length).Reverse().ToArray());
    }
}
