# SecsDemo

基于 **.NET 8** 原生 `System.Net.Sockets.Socket` 从零实现的 **HSMS（SEMI E37）/ SECS-II（SEMI E5）** 通信示例工程。

不依赖任何第三方 SECS/GEM 库，报文的长度域、消息头、数据体全部按协议规范手工组包与解析，可直接用于：

- 理解 HSMS 报文结构（4 字节长度域 + 10 字节消息头 + 数据体）
- 快速搭建上位机（Host）/ 设备端（Equipment）联调用的最小通信脚手架
- 二次开发 MES / EAP / GEM 设备通信模块的起点

---

## 目录

- [运行环境](#运行环境)
- [快速开始](#快速开始)
- [项目结构](#项目结构)
- [协议模型](#协议模型)
- [核心类型说明](#核心类型说明)
- [使用示例](#使用示例)
- [实现状态与已知问题](#实现状态与已知问题)
- [路线图](#路线图)
- [许可证](#许可证)

---

## 运行环境

| 项目 | 要求 |
| --- | --- |
| 框架 | .NET 8.0（`net8.0`） |
| 语言特性 | `ImplicitUsings` / `Nullable` 均已启用 |
| 第三方依赖 | 无（仅使用 BCL：`System.Net.Sockets`、`System.Buffers.Binary` 等） |
| 开发工具 | Visual Studio 2022 17.14+ 或 .NET SDK 8.0+ |
| 解决方案 | `SecsDemo.sln`（单一项目 `SecsPureSocket`） |

---

## 快速开始

```bash
# 克隆
git clone https://github.com/zhusoz/SecsDemo.git
cd SecsDemo

# 编译
dotnet build

# 运行（示例客户端默认连接 127.0.0.1:5000）
dotnet run --project SecsPureSocket
```

也可以在 Visual Studio 中直接打开 `SecsDemo.sln` 后按 `F5` 运行。

> ⚠️ 运行前请先在 `127.0.0.1:5000` 启动一个 HSMS 被动端（Passive / Equipment）监听，否则 `ConnectAsync()` 会抛出连接异常。缺少被动端时，可自行用 `Socket`/`TcpListener` 起一个满足 Select.rsp 应答的最小桩服务。

---

## 项目结构

```
SecsDemo/
├── SecsDemo.sln                     # 解决方案
├── LICENSE.txt                      # MIT License
├── README.md
└── SecsPureSocket/                  # 唯一工程：控制台示例
    ├── SecsPureSocket.csproj        # net8.0 / Exe
    ├── Program.cs                   # 入口：连接 + 收发演示（含协议常量注释）
    ├── HsmsGemClient.cs             # HSMS 客户端：连接、Select、收发、事件
    ├── HsmsMessage.cs               # 整条报文：长度域 + 消息头 + 数据体
    ├── HsmsMessageLength.cs         # 4 字节长度域（大端，= 10 + 数据体长度）
    ├── HsmsMessageHeader.cs         # 10 字节消息头
    ├── HsmsMessageData.cs           # 数据体（SECS-II 内容，待实现解析）
    ├── HsmsMessageDataItem.cs       # SECS-II 数据项（骨架）
    ├── HsmsMessageDataType.cs       # SECS-II 格式码枚举（6 bit）
    ├── HsmsMessageFactory.cs        # 报文工厂（骨架）
    ├── HsmsSessionStatus.cs         # 会话状态机枚举
    ├── HsmsSystemByteCounter.cs     # System Bytes 单例自增计数器
    ├── MessageType.cs               # HSMS 会话类型（SType）枚举
    └── BitConverterExtensions.cs    # 字节序辅助方法
```

---

## 协议模型

### 报文整体结构

```
┌──────────────────────┬────────────────────────┬──────────────────────┐
│  Length (4 bytes)    │  Header (10 bytes)     │  Data (0 ~ N bytes)  │
│  大端，值 = 10 + N   │  见下方消息头结构       │  SECS-II 数据体      │
└──────────────────────┴────────────────────────┴──────────────────────┘
```

### 10 字节消息头

| 偏移 | 字段 | 长度 | 说明 |
| --- | --- | --- | --- |
| 0 ~ 1 | Session ID / Device ID | 2 | 会话标识，**大端** |
| 2 | Stream No | 1 | Stream 编号；最高位为 **W-bit**（1 表示需要回复），低 7 位为 Stream |
| 3 | Function No | 1 | Function 编号 |
| 4 | PType | 1 | 表示类型，SECS-II 固定为 `0` |
| 5 | SType | 1 | 会话类型，见 [MessageType](#messagetype-枚举) |
| 6 ~ 9 | System Bytes | 4 | 事务标识，**大端**，同一 Primary/Reply 必须一致 |

### MessageType 枚举

| 值 | 名称 | 含义 |
| --- | --- | --- |
| 0 | `DataMessage` | SECS-II 数据消息 |
| 1 / 2 | `SelectReq` / `SelectRsp` | 建立会话选择 |
| 3 / 4 | `DeselectReq` / `DeselectRsp` | 结束会话选择 |
| 5 / 6 | `LinkTestReq` / `LinkTestRsp` | 链路心跳 |
| 7 | `RejectReq` | 拒绝请求 |
| 9 | `SeparateReq` | 断开连接 |

### SECS-II 格式码（`HsmsMessageDataType`）

| 值 | 名称 | 类型 | 值 | 名称 | 类型 |
| --- | --- | --- | --- | --- | --- |
| `0x00` | `List` | 列表 | `0x20` | `F8` | 8 字节浮点 |
| `0x08` | `Binary` | 二进制 | `0x24` | `F4` | 4 字节浮点 |
| `0x09` | `Boolean` | 布尔 | `0x28` | `U8` | 8 字节无符号整型 |
| `0x10` | `ASCII` | 字符串 | `0x29` | `U1` | 1 字节无符号整型 |
| `0x11` | `JIS8` | JIS-8（不常用） | `0x2A` | `U2` | 2 字节无符号整型 |
| `0x18` | `I8` | 8 字节有符号整型 | `0x2C` | `U4` | 4 字节无符号整型 |
| `0x19` | `I1` | 1 字节有符号整型 | | | |
| `0x1A` | `I2` | 2 字节有符号整型 | | | |
| `0x1C` | `I4` | 4 字节有符号整型 | | | |

### 会话状态机（`HsmsSessionStatus`）

```
NotConnected ──TCP连接成功──► Selecting ──收到 Select.rsp──► Selected
      ▲                            │                            │
      └────── Linktest 连续超时 / Separate ───────────────────────┘
```

---

## 核心类型说明

| 类型 | 职责 |
| --- | --- |
| `HsmsGemClient` | 客户端门面。构造参数 `(deviceId, host, port, autoSelect)`；提供 `ConnectAsync()`、`SendAsync(streamNo, functionNo, data, ...)`，通过 `OnMessageReceived` 事件回传收到的报文 |
| `HsmsMessage` | 一条完整报文。支持由字段构造发送，或由 `byte[]` 反序列化（自动按 4 + 10 + N 切分）；`GetRawHsmsMessage()` 输出可直接写入 Socket 的字节序列 |
| `HsmsMessageLength` | 4 字节长度域，值为 `Header.RawData.Length + Data.RawData.Length`，大端写出 |
| `HsmsMessageHeader` | 10 字节消息头。构造时自动根据 `needReply` 置 W-bit（`streamNo + 0x80`） |
| `HsmsMessageData` | 数据体。`RawData` 直通；`GetSecsValueItems()` 为后续 SECS-II 解析预留 |
| `HsmsSystemByteCounter` | 进程内单例，`GetNextValue()` 自增返回 System Bytes（模 2³²） |
| `MessageType` / `HsmsMessageDataType` / `HsmsSessionStatus` | 协议枚举常量 |
| `BitConverterExtensions` | `GetFixedLengthBytes` / `GetFixedLengthBytesReversed` 字节序辅助 |

---

## 使用示例

### 1. 建立连接并订阅收包

```csharp
HsmsGemClient gemClient = new HsmsGemClient(
    deviceId: 1,
    host: "127.0.0.1",
    port: 5000,
    autoSelect: true);          // true 时自动发送 Select.req 并等待 Select.rsp

gemClient.OnMessageReceived += message =>
{
    string hex = Convert.ToHexString(message.GetRawHsmsMessage());
    Console.WriteLine($"收到报文: {hex}");
};

await gemClient.ConnectAsync();
```

### 2. 发送 SECS-II 数据消息

```csharp
// 第三参数为数据体原始字节；W-bit 默认置位（needReply = true）
await gemClient.SendAsync(streamNo: 1, functionNo: 1,  data: Array.Empty<byte>()); // S1F1  Are You There
await gemClient.SendAsync(streamNo: 2, functionNo: 17, data: Array.Empty<byte>()); // S2F17 Date and Time Request
await gemClient.SendAsync(streamNo: 6, functionNo: 11, data: Array.Empty<byte>()); // S6F11 Event Report Send
```

`Program.cs` 中已内置一段演示，依次发送以下常用消息后等待回车退出：

| 报文 | 含义 |
| --- | --- |
| `S1F1` | Are You There |
| `S1F13` | Establish Communications Request |
| `S2F17` | Date and Time Request |
| `S2F33` | Define Report |
| `S5F1` | Alarm Report Send |
| `S6F11` | Event Report Send |
| `S7F3` | Process Program Send |
| `S10F3` | Terminal Display, Single |

### 3. 手工构造报文

```csharp
var message = new HsmsMessage(
    deviceId:    1,
    streamNo:    1,
    functionNo:  1,
    messageType: MessageType.DataMessage,
    systemId:    HsmsSystemByteCounter.Instance.GetNextValue(),
    data:        Array.Empty<byte>(),
    needReply:   true);           // 置 W-bit，即 Stream No 最高位写 1

byte[] raw = message.GetRawHsmsMessage();   // 4 + 10 + N 字节，可直接写入 Socket
```

> `HsmsMessage(HsmsMessageHeader, HsmsMessageData)` 为 `private`，仅内部复用；对外请使用上面的公开重载。

---

## 实现状态与已知问题

### 已实现

- HSMS 报文组包 / 拆包：长度域、消息头（含 W-bit）、数据体直通
- TCP 连接 + 自动 Select 握手
- System Bytes 单例自增分配
- 收包事件回调、十六进制打印

### 已知问题（建议修复）

| # | 位置 | 问题 | 建议 |
| --- | --- | --- | --- |
| 1 | `HsmsGemClient.ListenReplyAsync` | 调用处未 `await`（fire-and-forget）；且使用固定 1024 字节缓冲区，**未处理 TCP 分包/粘包** | 按长度域先读 4 字节、再读剩余字节，循环组包 |
| 2 | `HsmsSystemByteCounter.GetNextValue` | 未做原子操作，并发发送时 System Bytes 可能重复 | 使用 `Interlocked.Increment` |
---

## 路线图

- [ ] **SECS-II 数据体编解码**：`List` / `ASCII` / `U1~U8` / `I1~I8` / `F4` / `F8` / `Boolean` / `Binary`
- [ ] **TCP 分包与粘包处理**：基于长度域实现可靠报文分帧
- [ ] **控制消息补全**：`Linktest` 心跳、`Deselect`、`Separate`、`Reject` 及超时重连
- [ ] **请求-应答事务管理**：以 System Bytes 关联 Primary / Reply，支持异步等待与超时
- [ ] **事件与告警模型**：`S5Fx`、`S6Fx` 上层封装
- [ ] **被动端（Passive）示例**：补齐 Host / Equipment 双向联调样例
- [ ] **单元测试**：针对组包 / 拆包与大数据体分片的边界用例

---

## 许可证

本项目基于 [MIT License](LICENSE.txt) 开源。