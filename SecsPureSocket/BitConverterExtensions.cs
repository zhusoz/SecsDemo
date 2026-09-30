using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsPureSocket
{
    public class BitConverterExtensions
    {
        public static byte[] GetFixedLengthBytes(uint value)
        {
            return BitConverter.GetBytes(value).ToArray();
        }

        public static byte[] GetFixedLengthBytesReversed(uint value)
        {
            return GetFixedLengthBytes(value).Reverse().ToArray();
        }

    }
}
