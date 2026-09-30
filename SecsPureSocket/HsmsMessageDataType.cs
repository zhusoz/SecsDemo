using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsPureSocket;

public enum HsmsMessageDataType
{
    // """SECS-II Format Codes (6-bit values)"""
    List = 0b000000,      // # 0x00 - List
    Binary = 0b001000,    // # 0x08 - Binary
    Boolean = 0b001001,   // # 0x09 - Boolean
    ASCII = 0b010000,     // # 0x10 - ASCII
    JIS8 = 0b010001,      // # 0x11 - JIS-8 (not commonly used)
    I8 = 0b011000,        // # 0x18 - 8-byte signed integer
    I1 = 0b011001,        // # 0x19 - 1-byte signed integer
    I2 = 0b011010,        // # 0x1A - 2-byte signed integer
    I4 = 0b011100,        // # 0x1C - 4-byte signed integer
    F8 = 0b100000,        // # 0x20 - 8-byte floating point
    F4 = 0b100100,        // # 0x24 - 4-byte floating point
    U8 = 0b101000,        // # 0x28 - 8-byte unsigned integer
    U1 = 0b101001,        // # 0x29 - 1-byte unsigned integer
    U2 = 0b101010,        // # 0x2A - 2-byte unsigned integer
    U4 = 0b101100,        // # 0x2C - 4-byte unsigned integer
}
