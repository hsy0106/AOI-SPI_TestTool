using System;
using System.Collections.Generic;

namespace Plugin.Abstractions.Entity
{
    /// <summary>
    /// 一条收发报文记录：插件通过 IPluginContext.ReportMessage 上报，
    /// </summary>
    public class MessageRecord
    {
        /// <summary>
        /// 收发时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 协议标识
        /// </summary>
        public string Protocol { get; set; }

        /// <summary>
        /// 原始字节
        /// </summary>
        public byte[] RawBytes { get; set; }

        /// <summary>
        /// 报文本体
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 方向："TX"（发送）/ "RX"（接收）
        /// </summary>
        public string Direction { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

        /// <summary>
        /// 扩展元数据
        /// </summary>
        public Dictionary<string, object> Meta { get; set; } = new Dictionary<string, object>();
    }
}
