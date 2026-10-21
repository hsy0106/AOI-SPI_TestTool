using System;
using System.Threading;
using System.Threading.Tasks;
using Plugin.Abstractions.Entity;

namespace Plugin.Abstractions.Interface
{
    /// <summary>
    /// 统一通讯接口
    /// </summary>
    public interface ICommunicationPlugin
    {

        PluginState State { get; }

        /// <summary>初始化上下文</summary>
        void Initialize(IPluginContext ctx);


        void Start();


        void Stop();

        /// <summary>应用配置</summary>
        void ApplySetting(string json);

      
        Task<SendResult> SendAsync(string sml, CancellationToken ct);
    }

    /// <summary>
    /// 插件上下文 接口
    /// </summary>
    public interface IPluginContext
    {

        void Log(LogLevel level, string message, Exception ex = null);

        /// <summary>上报一条收发报文</summary>
        void ReportMessage(MessageRecord record);

  
        void ReportState(PluginState state, string detail);


        T GetConfig<T>(string key) where T : class, new();


        void SaveConfig(object config, string key);
    }
}
