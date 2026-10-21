using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Plugin.Abstractions.Attribute;
using Plugin.Abstractions.Entity;
using Plugin.Abstractions.Interface;
using Plugin_SECS.Core;
using Plugin_SECS.Service;

namespace Plugin_SECS.Plugin
{
    /// <summary>
    /// SECS/HSMS 通信插件
    [CommunicationPlugin(
        Id = "secs.localhost",
        Name = "SECS/HSMS",
        Version = "2.0.1",
        Description = "自测版本")]
    public class SecsCommunicationPlugin : ICommunicationPlugin
    {
        private IPluginContext _ipc;
        private SecsPluginConfig _spf;
        private Secs4NetService _service;

        public PluginState State { get; private set; } = PluginState.None;

        /// <summary>初始化：读配置文件、创建传输服务、订阅状态事件</summary>
        public void Initialize(IPluginContext ipc)
        {
            _ipc = ipc;

            _spf = ipc.GetConfig<SecsPluginConfig>("SECS") ?? new SecsPluginConfig();

            _service = new Secs4NetService(ipc);
            //订阅状态变更事件
            _service.StateChanged += OnSessionStateChanged;

            State = PluginState.Ready;
            _ipc.Log(LogLevel.Info, string.Format(
                "SECS 插件初始化完成：设备ID={0}，模式={1}，协议={2}，监听 {3}:{4}，SML {5}",
                _spf.DeviceId, _spf.Mode, _spf.ProtocolType, _spf.ListenIp, _spf.ListenPort, _spf.SmlVersion));
            _ipc.Log(LogLevel.Info, "配置文件: Plugins\\Config\\PluginConfig.json（节点 SECS）");
            _ipc.Log(LogLevel.Info, "计时器: " + _spf.TimersSummary());
        }

        /// <summary>插件启动</summary>
        public void Start()
        {
            if (_service == null) throw new InvalidOperationException("插件未初始化");
            State = PluginState.Starting;
            _service.Start(_spf);
            State = PluginState.Running;
            _ipc.Log(LogLevel.Info, "SECS 插件已启动（" + _spf.Mode + " / " + _spf.ProtocolType + "）");
        }


        public void Stop()
        {
            State = PluginState.Stopping;
            _service?.Stop();
            State = PluginState.Ready;
            _ipc.Log(LogLevel.Info, "SECS 插件已停止");
        }

        /// <summary>Host 发送sml </summary>
        public async Task<SendResult> SendAsync(string sml, CancellationToken ct)
        {
            return await _service.SendAsync(sml, ct);
        }




     /// <summary>
     /// 应用配置
     /// </summary>
     /// <param name="json"></param>
        public void ApplySetting(string json)
        {
            try
            {
                var newspf = JsonConvert.DeserializeObject<SecsPluginConfig>(json);
                if (newspf == null)
                {
                    _ipc.Log(LogLevel.Warn, "配置为空");
                    return;
                }
                if (newspf.DeviceId < 0 || newspf.DeviceId > 32767) newspf.DeviceId = 1;
                ReloadConfig(newspf, hotReload: true);
                _ipc.Log(LogLevel.Info, "SECS 配置已更新并重载");
            }
            catch (Exception ex)
            {
                _ipc.Log(LogLevel.Error, "应用配置失败: " + ex.Message);
            }
        }

        /// 热重载(重新加载配置如果正在则重新启动Secs4NetServer)
        private void ReloadConfig(SecsPluginConfig newspf, bool hotReload)
        {
            _spf = newspf;
            _ipc.SaveConfig(_spf, "SECS");               // 写入 Json文件 的 SECS 节点

            if (hotReload && State == PluginState.Running)
            {
                _ipc.Log(LogLevel.Info, "检测到配置变更，正在热重载...");
                try
                {
                    _ipc.Log(LogLevel.Info, "热重载停止服务");
                    _service.Stop();
                    _ipc.Log(LogLevel.Info, "热重载启动服务");
                    _service.Start(_spf);
                    _ipc.Log(LogLevel.Info, "参数配置热重载完成");
                }
                catch (Exception ex)
                {
                    State = PluginState.Faulted;
                    _ipc.Log(LogLevel.Error, "参数热重载失败 " + ex.Message);
                }
            }
            else
            {
                _ipc.Log(LogLevel.Info, hotReload
                    ? "配置已保存（当前未运行，启动时生效）"
                    : "配置已保存（未热重载，重启服务后生效）");
            }
        }



        private void OnSessionStateChanged(string s)
        {
            _ipc.ReportState(State, "HSMS: " + s);
        }
    }
}
