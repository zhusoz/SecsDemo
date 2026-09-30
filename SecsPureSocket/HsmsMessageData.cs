using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsPureSocket
{
    public class HsmsMessageData
    {
        private readonly byte[] _rawData;

        public HsmsMessageData() : this(Array.Empty<byte>())
        {

        }
        public HsmsMessageData(byte[] rawData)
        {
            _rawData = rawData;
        }

        public byte[] RawData => _rawData;

        public HsmsMessageDataItem[] GetSecsValueItems()
        {
            throw new NotImplementedException();
        }
    }
}
