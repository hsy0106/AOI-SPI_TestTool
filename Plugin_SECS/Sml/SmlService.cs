using System;
using System.Buffers;
using System.Text;
using Secs4Net;
using Secs4Net.Sml;

namespace Plugin_SECS.Sml
{
    /// <summary>
    /// SML转换 HSMS 报文（hex）

    public static class SmlService
    {
        /// <summary>HSMS 消息头长度（固定 10 字节）。</summary>
        public const int HeaderLength = 10;

        /// <summary>解析 SML 文本为 SecsMessage </summary>
        public static SecsMessage Parse(string sml)
        {
            if (string.IsNullOrWhiteSpace(sml))
                throw new ArgumentException("SML 文本为空");
            SecsMessage msg = SmlReader.ToSecsMessage(sml);
            if (msg == null)
                throw new FormatException("SML 解析结果为空");
            return msg;
        }

        /// <summary>把 SecsMessage 序列化为 SML 文本。</summary>
        public static string ToSml(SecsMessage msg)
        {
            return msg == null ? "(null)" : SmlWriter.ToSml(msg);
        }

        /// <summary>把 SML 文本翻译成完整 HSMS 报文帧字节（含 10 字节头）——「SML → 原始报文」翻译核心。</summary>
        /// <param name="sml">SML 文本，如 "'S1F13'W\r\n<L [2] \r\n<A [8] 'DemoTool'>\r\n<A [5] '1.0.0'>\r\n>\r\n."</param>
        /// <param name="deviceId">设备 ID（0~32767）。</param>
        /// <param name="transactionId">事务 ID（SystemBytes）。</param>
        public static byte[] SmlToHSMS(string sml, int deviceId, int transactionId)
        {
            using (var msg = SmlReader.ToSecsMessage(sml))
            {
                return MessageToFrame(msg, deviceId, transactionId);
            }
        }

        /// <summary>把 SecsMessage 编码为完整 HSMS 帧字节（10 字节头 + SECS-II 数据区）。</summary>
        public static byte[] MessageToFrame(SecsMessage msg, int deviceId, int transactionId)
        {
            if (msg == null) throw new ArgumentNullException("msgNull");

  
            byte[] data = new byte[0];
            if (msg.SecsItem != null)
            {
                var writer = new SimpleBufferWriter();
                msg.SecsItem.EncodeTo(writer);
                data = writer.ToArray();
            }

            // ---------- 2) 拼 10 字节消息头 ----------
            var frame = new byte[HeaderLength + data.Length];

            // 字节 0-1：设备 ID（大端；bit15 置 0 表示全 16bit 设备ID）
            int dev =deviceId & 0x7FFF;
            frame[0] = (byte)((dev >> 8) & 0xFF);
            frame[1] = (byte)(dev & 0xFF);

            // 字节 2：Stream（bit7 = W 位）
            frame[2] = (byte)((msg.S & 0x7F) | (msg.ReplyExpected ? 0x80 : 0x00));

            // 字节 3：Function
            frame[3] = (byte)(msg.F & 0xFF);

            // 字节 4：PType（保留，HSMS 恒为 0）
            frame[4] = 0x00;

            // 字节 5：MessageType（0x00 = 数据消息 Data Message）
            frame[5] = (byte)MessageType.DataMessage;

            // 字节 6-9：事务 ID（大端）
            frame[6] = (byte)((transactionId >> 24) & 0xFF);
            frame[7] = (byte)((transactionId >> 16) & 0xFF);
            frame[8] = (byte)((transactionId >> 8) & 0xFF);
            frame[9] = (byte)(transactionId & 0xFF);

            // ---------- 3) 数据区 ----------
            Array.Copy(data, 0, frame, HeaderLength, data.Length);

            return frame;
        }

        /// <summary>
        /// 解析一段 HSMS 帧字节，生成人类可读的报文头说明
        /// 输出：设备ID / S·F / W位 / MessageType / 事务ID / 数据区长度。
        /// </summary>
        public static string DescribeFrame(byte[] frame)
        {
            if (frame == null || frame.Length < HeaderLength)
                return "帧长度不足 10 字节（不是合法 HSMS 报文）";

            int deviceId = ((frame[0] & 0x7F) << 8) | frame[1];     // bit15 为扩展标志，忽略
            bool replyExpected = (frame[2] & 0x80) != 0;
            byte s = (byte)(frame[2] & 0x7F);
            byte f = frame[3];
            byte pType = frame[4];
            byte msgType = frame[5];
            int transId = (frame[6] << 24) | (frame[7] << 16) | (frame[8] << 8) | frame[9];
            int dataLen = frame.Length - HeaderLength;

            var sb = new StringBuilder();
            sb.AppendFormat("设备ID     = {0} (0x{0:X4})\r\n", deviceId);
            sb.AppendFormat("消息编号   = S{0}F{1}{2}\r\n", s, f, replyExpected ? " W(期望应答)" : "");
            sb.AppendFormat("PType      = 0x{0:X2} (保留，HSMS 恒为 0)\r\n", pType);
            sb.AppendFormat("MessageType= 0x{0:X2} → {1}\r\n", msgType, MessageTypeName(msgType));
            sb.AppendFormat("事务ID     = {0} (0x{0:X8})\r\n", transId);
            sb.AppendFormat("数据区长度 = {0} 字节（SECS-II 编码）\r\n", dataLen);
            return sb.ToString();
        }

        /// <summary>MessageType 数值 → 名称（HSMS-SS 标准）。</summary>
        public static string MessageTypeName(byte type)
        {
            switch (type)
            {
                case (byte)MessageType.DataMessage:  return "数据消息 Data Message";
                case (byte)MessageType.SelectReq:    return "Select.req（请求建立会话）";
                case (byte)MessageType.SelectRsp:    return "Select.rsp（会话建立应答）";
                case (byte)MessageType.DeselectReq:  return "Deselect.req（请求释放会话）";
                case (byte)MessageType.DeselectRsp:  return "Deselect.rsp（释放应答）";
                case (byte)MessageType.LinktestReq:  return "Linktest.req（心跳请求）";
                case (byte)MessageType.LinktestRsp:  return "Linktest.rsp（心跳应答）";
                case (byte)MessageType.RejectReq:    return "Reject.req（拒绝）";
                case (byte)MessageType.SeparateReq:  return "Separate.req（断开通知）";
                default:                             return "未知控制消息";
            }
        }

        /// <summary>把字节数组格式化为十六进制文本（空格分隔）。</summary>
        public static string BytesToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            var sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("X2")).Append(' ');
            return sb.ToString().Trim();
        }
    }

    /// <summary>HSMS MessageType（字节 5 取值）。</summary>
    public enum MessageType : byte
    {
        DataMessage = 0x00,
        SelectReq = 0x01,
        SelectRsp = 0x02,
        DeselectReq = 0x03,
        DeselectRsp = 0x04,
        LinktestReq = 0x05,
        LinktestRsp = 0x06,
        RejectReq = 0x07,
        SeparateReq = 0x09
    }

    /// <summary>
    /// 极简 IBufferWriter 实现（Secs4Net 的 Item.EncodeTo 需要）：
    /// 把编码结果收集到内部数组，最后 ToArray 取出。
    /// </summary>
    public sealed class SimpleBufferWriter : IBufferWriter<byte>
    {
        private byte[] _buf = new byte[1024];
        private int _len;


        public void Advance(int count)
        {
            if (count < 0 || _len + count > _buf.Length)
                throw new ArgumentOutOfRangeException("count");
            _len += count;
        }

   
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return new Memory<byte>(_buf, _len, _buf.Length - _len);
        }


        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Ensure(sizeHint);
            return new Span<byte>(_buf, _len, _buf.Length - _len);
        }

        private void Ensure(int sizeHint)
        {
            if (_buf.Length - _len >= sizeHint) return;
            int newSize = Math.Max(_buf.Length * 2, _len + sizeHint);
            Array.Resize(ref _buf, newSize);
        }

        /// <summary>取出已写入的全部字节。</summary>
        public byte[] ToArray()
        {
            var result = new byte[_len];
            Array.Copy(_buf, result, _len);
            return result;
        }
    }
}
