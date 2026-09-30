using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsPureSocket;

public abstract class HsmsMessageBase
{
    protected HsmsMessageBase(byte[] rawData)
    {
        RawData = rawData;
    }

    public byte[] RawData { get; }
}
