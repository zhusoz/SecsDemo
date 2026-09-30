using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsPureSocket;

/// <summary>
/// HSMS Message Types (Session Type field)
/// </summary>
public enum MessageType : byte
{    
    DataMessage = 0,          // SECS-II data message
    SelectReq = 1,            // Select.req
    SelectRsp = 2,            // Select.rsp
    DeselectReq = 3,          // Deselect.req
    DeselectRsp = 4,          // Deselect.rsp
    LinkTestReq = 5,          // Linktest.req
    LinkTestRsp = 6,          // Linktest.rsp
    RejectReq = 7,            // Reject.req
    SeparateReq = 9,          // Separate.req
}
