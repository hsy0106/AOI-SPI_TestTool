using System;

namespace Plugin_SECS.Core
{
    /// <summary>
    /// SECS/HSMS 插件配置模型 
    /// </summary>
    public class SecsPluginConfig
    {

        public int DeviceId { get; set; } = 0;


        public string Mode { get; set; } = "Passive";

        /// <summary>
        /// 协议类型
        /// </summary>
        public string ProtocolType { get; set; } = "SECS-G";

        /// <summary>被动（监听）模式绑定的本机 IP</summary>
        public string ListenIp { get; set; } = "127.0.0.1";

        /// <summary>被动（监听）模式端口</summary>
        public int ListenPort { get; set; } = 5000;

        /// <summary>主动连接的对端主机 IP </summary>
        public string RemoteHost { get; set; } = "127.0.0.1";

        /// <summary>主动连接的对端端口 </summary>
        public int RemotePort { get; set; } = 5000;

        /// <summary>心跳</summary>
        public int LinktestIntervalMs { get; set; } = 3000;


        /// <summary>T1：SECS-I 字符间超时 </summary>
        public int T1Ms { get; set; } = 10000;

        /// <summary>T2：SECS-I 块间超时 </summary>
        public int T2Ms { get; set; } = 15000;

        /// <summary>T3：事务超时 </summary>
        public int T3Ms { get; set; } = 45000;

        /// <summary>T4：SECS-I 块到块超时 </summary>
        public int T4Ms { get; set; } = 45000;

        /// <summary>T5：连接建立失败后的重试间隔。</summary>
        public int T5Ms { get; set; } = 10000;

        /// <summary>T6：控制消息（Select/Linktest/Separate）应答超时。</summary>
        public int T6Ms { get; set; } = 5000;

        /// <summary>T7：TCP 连接建立超时。</summary>
        public int T7Ms { get; set; } = 10000;

        /// <summary>T8：网络读超时（对端无数据判定连接失效）。</summary>
        public int T8Ms { get; set; } = 5000;



        /// <summary>SML 版本（SEMI E5 消息语言版本，如 "1.0"）。</summary>
        public string SmlVersion { get; set; } = "1.0";

        /// <summary>SML 标准描述 </summary>
        public string SmlStandard { get; set; } = "SEMI E5/E30";

        /// <summary>设备型号名（S1F14 MDLN 应答内容）。</summary>
        public string MdlName { get; set; } = "DemoTool";

        /// <summary>软件版本（S1F14 SOFTREV 应答内容）。</summary>
        public string SoftRev { get; set; } = "1.0.0";

        // 配置读写已统一走Host IPluginContext（Plugins\Config\PluginConfig.json 的 SECS 节点），本类仅作纯数据模型。
        // 见 SecsCommunicationPlugin.Initialize / ReloadConfig 的 GetConfig/SaveConfig 调用。

        /// <summary>克隆一份配置（热重载时避免引用旧实例）。</summary>
        public SecsPluginConfig Clone()
        {
            return (SecsPluginConfig)MemberwiseClone();
        }

        /// <summary>把 T1-T8 计时器串成一行摘要，便于日志展示。</summary>
        public string TimersSummary()
        {
            return string.Format("T1={0} T2={1} T3={2} T4={3} T5={4} T6={5} T7={6} T8={7}",
                T1Ms, T2Ms, T3Ms, T4Ms, T5Ms, T6Ms, T7Ms, T8Ms);
        }
    }
}
