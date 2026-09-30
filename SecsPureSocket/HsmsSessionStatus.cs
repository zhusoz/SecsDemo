using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsPureSocket;

/// <summary>
/// 会话状态
/// </summary>
public enum HsmsSessionStatus : byte
{
    /// <summary>
    /// TCP连接未建立
    /// </summary>
    NotConnected,

    /// <summary>
    /// 已建立TCP连接，正在发送Select.req等待Select.rsp
    /// </summary>
    Selecting, 

    /// <summary>
    /// HSMS会话已建立，可以收发数据消息。 在SELECTED状态下，还需要通过Linktest心跳维持会话活性。如果连续多次Linktest超时，会话回退到NotConnected。
    /// </summary>
    Selected, 

    /// <summary>
    /// 正在关闭会话
    /// </summary>
    DELSECTING
}
