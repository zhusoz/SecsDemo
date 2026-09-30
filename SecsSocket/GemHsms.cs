// ============================================================================
// GEM / HSMS-SS 通信：设备端被动（Passive）+ 上位机主动（Active）
// 基于 .NET 原生 Socket，无第三方依赖。
//   - 设备(Equipment) = Passive：TcpListener 监听 5000，等 Host 连入，
//     收到 Host 的 Select.req 后回 Select.rsp。
//   - 上位机(Host)    = Active ：主动连接并先发 Select.req，建会话后交互。
// 标准：SEMI E37(HSMS) / E5(SECS-II) / E30(GEM)
// 运行：.NET 8
//   dotnet run -- passive          # 设备端，监听 0.0.0.0:5000
//   dotnet run -- active <host> <port>   # Host 端，默认 127.0.0.1:5000
// ============================================================================

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SecsSocket;

// ------------------------------------------------------------------
// HSMS 报文（E37 帧结构：4B长度头 + 10B HSMS头 + data）
// ------------------------------------------------------------------
public sealed class HsmsMessage
{
    // 会话类型（SType）
    public const byte ST_DATA = 0x00;
    public const byte ST_SELECT_REQ = 0x01;
    public const byte ST_SELECT_RSP = 0x02;
    public const byte ST_DESELECT_REQ = 0x03;
    public const byte ST_DESELECT_RSP = 0x04;
    public const byte ST_LINKTEST_REQ = 0x05;
    public const byte ST_LINKTEST_RSP = 0x06;
    public const byte ST_REJECT_REQ = 0x07;
    public const byte ST_SEPARATE_REQ = 0x09;

    // E37：所有控制消息的会话号固定为 0xFFFF
    public const ushort CONTROL_SESSION_ID = 0xFFFF;

    public ushort SessionId;    // 数据消息：bit15=1 设备发起，0=Host 发起
    public byte PType;        // 0x00 = SECS-II
    public byte SType;        // 会话类型
    public uint SystemBytes;  // 事务号
    public byte[] Data;         // 数据消息的 SECS-II 报文体

    public static HsmsMessage Decode(ReadOnlySpan<byte> frame)
    {
        var m = new HsmsMessage
        {
            SessionId = BinaryPrimitives.ReadUInt16BigEndian(frame[4..6]),
            PType = frame[7],
            SType = frame[8],
            SystemBytes = BinaryPrimitives.ReadUInt32BigEndian(frame[9..13]),
        };
        int dataLen = BinaryPrimitives.ReadInt32BigEndian(frame[0..4]) - 10;
        m.Data = dataLen > 0 ? frame.Slice(13, dataLen).ToArray() : Array.Empty<byte>();
        return m;
    }

    public byte[] Encode()
    {
        int dataLen = Data?.Length ?? 0;
        var frame = new byte[14 + dataLen];
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, 4), 10 + dataLen);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4, 2), SessionId);
        frame[6] = 0x0A;                  // Header Byte Count
        frame[7] = PType;
        frame[8] = SType;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(9, 4), SystemBytes);
        if (dataLen > 0) Data.CopyTo(frame, 13);
        return frame;
    }
}

// ------------------------------------------------------------------
// SECS-II（E5）报文构造：2B长度 + 10B头 + 格式项
// ------------------------------------------------------------------
public static class Secs2
{
    public static byte[] Frame(byte stream, byte function, bool wantReply, uint sysBytes, byte[] body)
    {
        var head = new byte[10 + body.Length];
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(0, 2), (ushort)(10 + body.Length));
        head[2] = stream;
        head[3] = (byte)(function | (wantReply ? 0x80 : 0x00)); // W-bit
        head[4] = 0x00;                                        // E-bit
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(5, 4), sysBytes);
        Array.Copy(body, 0, head, 10, body.Length);
        return head;
    }

    public static byte StreamOf(byte[] secsMsg) => secsMsg.Length >= 3 ? secsMsg[2] : (byte)0;
    public static byte FuncOf(byte[] secsMsg) => secsMsg.Length >= 3 ? (byte)(secsMsg[3] & 0x7F) : (byte)0;

    public static byte[] List(int n) => new byte[] { 0x01, (byte)n };                    // <L n>
    public static byte[] Ascii(string s)                                                   // <A len>
    {
        var b = System.Text.Encoding.ASCII.GetBytes(s);
        var r = new byte[2 + b.Length];
        r[0] = 0x41; r[1] = (byte)b.Length;
        Array.Copy(b, 0, r, 2, b.Length);
        return r;
    }
    public static byte[] Binary(byte[] b)                                                  // <B len>
    {
        var r = new byte[2 + b.Length];
        r[0] = 0x25; r[1] = (byte)b.Length;
        Array.Copy(b, 0, r, 2, b.Length);
        return r;
    }
    public static byte[] U4(uint v)                                                        // <U4>
    {
        var r = new byte[5];
        r[0] = 0xA1;
        BinaryPrimitives.WriteUInt32BigEndian(r.AsSpan(1, 4), v);
        return r;
    }
    public static byte[] Concat(params byte[][] parts)
    {
        int len = 0;
        foreach (var p in parts) len += p.Length;
        var r = new byte[len];
        int o = 0;
        foreach (var p in parts) { Array.Copy(p, 0, r, o, p.Length); o += p.Length; }
        return r;
    }
}

// ------------------------------------------------------------------
// HSMS 会话：读写循环 + Select + 事务匹配 + Linktest（两端共用）
// ------------------------------------------------------------------
public sealed class HsmsSession : IDisposable
{
    private readonly Socket _sock;
    private readonly bool _isEquipment;   // 是否设备侧（决定数据消息 SessionId bit15）
    private readonly CancellationTokenSource _cts = new();
    private Task _readTask;
    private uint _sysByte = 1;
    private readonly Dictionary<uint, TaskCompletionSource<HsmsMessage>> _pending = new();
    private readonly object _sync = new();

    public bool IsSelected { get; private set; }

    /// <summary>对端主动发来的数据消息（Primary），由调用方按 Function 应答。</summary>
    public event Action<HsmsMessage> OnDataMessage;
    /// <summary>Select 完成（会话建立）。</summary>
    public event Action OnSelected;

    public HsmsSession(Socket sock, bool isEquipment)
    {
        _sock = sock;
        _isEquipment = isEquipment;
        _readTask = Task.Run(() => ReadLoop(_cts.Token));
    }

    /// <summary>Active 方（Host）调用：发起 Select，等待设备回 Select.rsp。</summary>
    public async Task SelectAsync(int timeoutMs = 5000)
    {
        var rsp = await RequestAsync(Ctrl(HsmsMessage.ST_SELECT_REQ), timeoutMs);
        if (rsp.SType != HsmsMessage.ST_SELECT_RSP)
            throw new InvalidOperationException("未收到 Select.rsp");

        byte status = rsp.Data.Length > 0 ? rsp.Data[0] : (byte)0xFF;
        if (status != 0)
            throw new InvalidOperationException($"Select 失败，status={status}（0成功 1忙 2未就绪）");
        IsSelected = true;
    }

    /// <summary>发送数据消息（Primary），可按需等待应答（Secondary）。</summary>
    public async Task<HsmsMessage> SendAsync(
        byte stream, byte function, byte[] body, bool wantReply = true, int timeoutMs = 10000)
    {
        uint sys = NextSys();
        var msg = new HsmsMessage
        {
            SessionId = DataSessionId(),
            PType = 0,
            SType = HsmsMessage.ST_DATA,
            SystemBytes = sys,
            Data = Secs2.Frame(stream, function, wantReply, sys, body),
        };
        SendRaw(msg.Encode());
        if (!wantReply) return msg;

        var tcs = new TaskCompletionSource<HsmsMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_sync) _pending[sys] = tcs;
        try { return await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs)); }
        finally { lock (_sync) _pending.Remove(sys); }
    }

    /// <summary>应答对端 Primary（Secondary）：回显请求的 SystemBytes。</summary>
    public void SendSecondary(HsmsMessage request, byte stream, byte function, byte[] body)
    {
        var msg = new HsmsMessage
        {
            SessionId = DataSessionId(),
            PType = 0,
            SType = HsmsMessage.ST_DATA,
            SystemBytes = request.SystemBytes,          // 必须回显
            Data = Secs2.Frame(stream, function, false, request.SystemBytes, body),
        };
        SendRaw(msg.Encode());
    }

    public async Task<bool> LinkTestAsync(int timeoutMs = 3000)
    {
        var rsp = await RequestAsync(Ctrl(HsmsMessage.ST_LINKTEST_REQ), timeoutMs);
        return rsp.SType == HsmsMessage.ST_LINKTEST_RSP;
    }

    // --------------------------------------------------------------
    private ushort DataSessionId() => (ushort)(_isEquipment ? 0x8000 : 0x0000);

    private HsmsMessage Ctrl(byte stype) =>
        new()
        {
            SessionId = HsmsMessage.CONTROL_SESSION_ID,
            PType = 0,
            SType = stype,
            SystemBytes = NextSys(),
            Data = Array.Empty<byte>()
        };

    private async Task<HsmsMessage> RequestAsync(HsmsMessage msg, int timeoutMs)
    {
        SendRaw(msg.Encode());
        var tcs = new TaskCompletionSource<HsmsMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_sync) _pending[msg.SystemBytes] = tcs;
        try { return await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs)); }
        finally { lock (_sync) _pending.Remove(msg.SystemBytes); }
    }

    private void SendRaw(byte[] frame) { lock (_sync) _sock.Send(frame, SocketFlags.None); }

    private uint NextSys()
    {
        lock (_sync)
        {
            _sysByte = (_sysByte + 1) & 0x7FFFFFFF;
            if (_sysByte == 0) _sysByte = 1;
            return _sysByte;
        }
    }

    private void ReadLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var lenBuf = new byte[4];
                ReadExactly(lenBuf, 0, 4, ct);
                int total = BinaryPrimitives.ReadInt32BigEndian(lenBuf) + 4;

                var frame = new byte[total];
                Array.Copy(lenBuf, frame, 4);
                ReadExactly(frame, 4, total - 4, ct);

                Handle(HsmsMessage.Decode(frame));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HSMS] 读循环退出: {ex.Message}");
        }
    }

    private void ReadExactly(byte[] buf, int offset, int count, CancellationToken ct)
    {
        int got = 0;
        while (got < count)
        {
            int n = _sock.Receive(buf, offset + got, count - got, SocketFlags.None);
            if (n == 0) throw new IOException("连接被对端关闭");
            got += n;
        }
    }

    private void Handle(HsmsMessage msg)
    {
        // 1) Select.req：被动设备回 Select.rsp（status=0 成功）
        if (msg.SType == HsmsMessage.ST_SELECT_REQ)
        {
            SendRaw(new HsmsMessage
            {
                SessionId = HsmsMessage.CONTROL_SESSION_ID,
                PType = 0,
                SType = HsmsMessage.ST_SELECT_RSP,
                SystemBytes = msg.SystemBytes,
                Data = new byte[] { 0x00 },   // 回显事务号 + 状态 0
            }.Encode());
            IsSelected = true;
            OnSelected?.Invoke();
            return;
        }

        // 2) Linktest.req：应答（回显 SystemBytes）
        if (msg.SType == HsmsMessage.ST_LINKTEST_REQ)
        {
            SendRaw(new HsmsMessage
            {
                SessionId = HsmsMessage.CONTROL_SESSION_ID,
                PType = 0,
                SType = HsmsMessage.ST_LINKTEST_RSP,
                SystemBytes = msg.SystemBytes,
                Data = Array.Empty<byte>(),
            }.Encode());
            return;
        }

        // 3) 事务应答 / Secondary：优先匹配 pending
        TaskCompletionSource<HsmsMessage> tcs = null;
        lock (_sync) _pending.TryGetValue(msg.SystemBytes, out tcs);
        if (tcs != null) { tcs.TrySetResult(msg); return; }

        // 4) 其余数据消息（对端主动发的 Primary）交给事件
        if (msg.SType == HsmsMessage.ST_DATA)
        {
            OnDataMessage?.Invoke(msg);
            return;
        }

        Console.WriteLine($"[HSMS] 收到控制消息 SType=0x{msg.SType:X2}");
    }

    public void Dispose()
    {
        try { if (_sock.Connected) _sock.Shutdown(SocketShutdown.Both); } catch { }
        _sock.Close();
        _cts.Cancel();
        try { _readTask?.Wait(1000); } catch { }
        _cts.Dispose();
        _sock.Dispose();
    }
}

// ------------------------------------------------------------------
// 设备端（被动）：监听等待 Host 接入
// ------------------------------------------------------------------
public static class EquipmentServer
{
    public static async Task RunAsync(IPAddress ip, int port, CancellationToken ct)
    {
        var listener = new TcpListener(ip, port);
        listener.Start();
        Console.WriteLine($"[设备/被动] 监听 {ip}:{port}，等待 Host 接入...");

        while (!ct.IsCancellationRequested)
        {
            var sock = await listener.AcceptTcpClientAsync(ct);
            sock.NoDelay = true;
            Console.WriteLine($"[设备] Host 已接入 {sock.Client.RemoteEndPoint}");

            var session = new HsmsSession(sock.Client, isEquipment: true);
            session.OnSelected += () => Console.WriteLine("[设备] Host 已 Select，会话建立");
            session.OnDataMessage += m =>
            {
                byte s = Secs2.StreamOf(m.Data), f = Secs2.FuncOf(m.Data);
                Console.WriteLine($"[设备] 收到 Host S{s}F{f}");

                // 示例应答：S1F1(建立通信请求) -> S1F2（MDLN/SOFTREV 等）
                if (s == 1 && f == 1)
                    session.SendSecondary(m, 1, 2, Secs2.Concat(
                        Secs2.List(4),
                        Secs2.Ascii("MDLN-1"),
                        Secs2.Ascii("SOFT-1"),
                        Secs2.Ascii("1.0.0"),
                        Secs2.Ascii("2026-09-21")));
                // 其余 S/F 请按 GEM 规范补充应答
            };

            // 会话保活监控
            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(10_000);
                    if (!session.IsSelected) continue;
                    if (!await session.LinkTestAsync())
                    {
                        Console.WriteLine("[设备] Linktest 失败，断开该会话");
                        session.Dispose();
                        break;
                    }
                }
            });
        }
    }
}

// ------------------------------------------------------------------
// 上位机端（主动）：主动连接并 Select
// ------------------------------------------------------------------
public static class HostClient
{
    public static async Task RunAsync(string host, int port)
    {
        var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
            ReceiveBufferSize = 65536,
            SendBufferSize = 65536,
        };
        sock.Connect(host, port);
        Console.WriteLine($"[Host/主动] 已连接 {host}:{port}");

        var session = new HsmsSession(sock, isEquipment: false);
        await session.SelectAsync();
        Console.WriteLine("[Host] Select 完成，会话建立");

        session.OnDataMessage += m =>
            Console.WriteLine($"[Host] 收到设备 S{Secs2.StreamOf(m.Data)}F{Secs2.FuncOf(m.Data)}");

        // 发 S1F1（Want Reply），等设备回 S1F2
        var rsp = await session.SendAsync(1, 1, Secs2.Concat(Secs2.List(1), Secs2.Ascii("S")), wantReply: true);
        Console.WriteLine($"[Host] S1F2 应答字节数={rsp.Data.Length}");

        // 周期 Linktest 保活
        while (true)
        {
            await Task.Delay(10_000);
            if (!await session.LinkTestAsync())
            {
                Console.WriteLine("[Host] Linktest 失败，链路异常");
                break;
            }
        }
    }
}

//public static class Program
//{
//    public static async Task Main(string[] args)
//    {
//        string mode = args.Length > 0 ? args[0] : "active";

//        if (mode.Equals("passive", StringComparison.OrdinalIgnoreCase))
//            await EquipmentServer.RunAsync(IPAddress.Any, 5000, CancellationToken.None);
//        else
//        {
//            string host = args.Length > 1 ? args[1] : "127.0.0.1";
//            int port = args.Length > 2 ? int.Parse(args[2]) : 5000;
//            await HostClient.RunAsync(host, port);
//        }
//    }
//}