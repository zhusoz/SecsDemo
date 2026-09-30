using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsPureSocket
{
    public class HsmsSystemByteCounter
    {
        private static readonly object _lock = new object();
        private volatile static HsmsSystemByteCounter _instance;
        public static HsmsSystemByteCounter Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new HsmsSystemByteCounter();
                        }
                    }
                }
                return _instance;
            }
        }

        private HsmsSystemByteCounter() { }

        private uint _currentValue;
        public uint GetNextValue()
        {
            _currentValue = (uint)((_currentValue + 1) % 0x100000000);
            return _currentValue;
        }


    }
}
