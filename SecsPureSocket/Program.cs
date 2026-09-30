using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SecsPureSocket
{
    public sealed class Program
    {
        static async Task Main(string[] args)
        {
            #region HSMS Protocol 
            // Total Message Package Example:[MessagePackageTotalLength: MessagePackageHeader.Length+Data.Length][MessagePackageHeader][Data]
            // HSMS Message Header (10 bytes)
            // Structure:
            //- Session ID (2 bytes): Identifies the communication session
            //- Header Byte 2 (1 byte): Stream code (for data messages) 高1位字节表示是否必须回复，低7位表示Stream编号
            //- Header Byte 3 (1 byte): Function code (for data messages)
            //- PType (1 byte): Presentation type (always 0 for SECS-II)
            //- SType (1 byte): Session type (message type)
            //- System Bytes (4 bytes): Transaction identifier

            // HSMS Message Data:           
            //    """SECS-II Format Codes (6-bit values)"""
            //    LIST = 0b000000      # 0x00 - List
            //    BINARY = 0b001000    # 0x08 - Binary
            //    BOOLEAN = 0b001001   # 0x09 - Boolean
            //    ASCII = 0b010000     # 0x10 - ASCII
            //    JIS8 = 0b010001      # 0x11 - JIS-8 (not commonly used)
            //    I8 = 0b011000        # 0x18 - 8-byte signed integer
            //    I1 = 0b011001        # 0x19 - 1-byte signed integer
            //    I2 = 0b011010        # 0x1A - 2-byte signed integer
            //    I4 = 0b011100        # 0x1C - 4-byte signed integer
            //    F8 = 0b100000        # 0x20 - 8-byte floating point
            //    F4 = 0b100100        # 0x24 - 4-byte floating point
            //    U8 = 0b101000        # 0x28 - 8-byte unsigned integer
            //    U1 = 0b101001        # 0x29 - 1-byte unsigned integer
            //    U2 = 0b101010        # 0x2A - 2-byte unsigned integer
            //    U4 = 0b101100        # 0x2C - 4-byte unsigned integer


            //# Reverse mapping for decoding
            //FORMAT_CODE_NAMES = {
            //    FormatCode.LIST: "List",
            //    FormatCode.BINARY: "Binary",
            //    FormatCode.BOOLEAN: "Boolean",
            //    FormatCode.ASCII: "ASCII",
            //    FormatCode.I1: "I1",
            //    FormatCode.I2: "I2",
            //    FormatCode.I4: "I4",
            //    FormatCode.I8: "I8",
            //    FormatCode.F4: "F4",
            //    FormatCode.F8: "F8",
            //    FormatCode.U1: "U1",
            //    FormatCode.U2: "U2",
            //    FormatCode.U4: "U4",
            //    FormatCode.U8: "U8",
            //}


            // Constants
            //class HSMSMessageType(IntEnum) :
            //    """HSMS Message Types (Session Type field)"""
            //    DATA_MESSAGE = 0          # SECS-II data message
            //    SELECT_REQ = 1            # Select.req
            //    SELECT_RSP = 2            # Select.rsp
            //    DESELECT_REQ = 3          # Deselect.req
            //    DESELECT_RSP = 4          # Deselect.rsp
            //    LINKTEST_REQ = 5          # Linktest.req
            //    LINKTEST_RSP = 6          # Linktest.rsp
            //    REJECT_REQ = 7            # Reject.req
            //    SEPARATE_REQ = 9          # Separate.req


            //class HSMSSelectStatus(IntEnum) :
            //    """Select Response Status Codes"""
            //    SUCCESS = 0               # Communication established successfully
            //    ALREADY_ACTIVE = 1        # Communication already active
            //    NOT_READY = 2             # Equipment not ready to communicate
            //    EXHAUSTED = 3             # Connection resources exhausted


            //class HSMSDeselectStatus(IntEnum) :
            //    """Deselect Response Status Codes"""
            //    SUCCESS = 0               # Communication terminated successfully
            //    NOT_SELECTED = 1          # Communication not established
            //    BUSY = 2                  # Busy, cannot terminate now


            //class HSMSRejectReason(IntEnum) :
            //    """Reject Request Reason Codes"""
            //    NOT_SUPPORTED = 1         # Message type not supported
            //    NOT_SELECTED = 2          # Not in selected state
            //    MESSAGE_TOO_LONG = 3      # Message length exceeds maximum
            //    ENTITY_TOO_LONG = 4       # Entity ID field too long
            #endregion

            HsmsGemClient gemClient = new HsmsGemClient(deviceId: 1, host: "127.0.0.1", port: 5000, autoSelect: true);
            await gemClient.ConnectAsync();
            gemClient.OnMessageReceived += (message) =>
            {
                string receivedHexString = Convert.ToHexString(message.GetRawHsmsMessage());
                Console.WriteLine($"GemClient收到返回:{receivedHexString}");
            };

            await gemClient.SendAsync(streamNo: 1, functionNo: 1, data: Array.Empty<byte>());
            await gemClient.SendAsync(streamNo: 1, functionNo: 13, data: Array.Empty<byte>());
            await gemClient.SendAsync(streamNo: 2, functionNo: 17, data: Array.Empty<byte>());
            await gemClient.SendAsync(streamNo: 2, functionNo: 33, data: Array.Empty<byte>());
            await gemClient.SendAsync(streamNo: 5, functionNo: 1, data: Array.Empty<byte>());
            await gemClient.SendAsync(streamNo: 6, functionNo: 11, data: Array.Empty<byte>());
            await gemClient.SendAsync(streamNo: 7, functionNo: 3, data: Array.Empty<byte>());
            await gemClient.SendAsync(streamNo: 10, functionNo: 3, data: Array.Empty<byte>());

            await Console.Out.WriteLineAsync("Press Enter key to continue...");
            Console.ReadKey();
        }
    }
}
