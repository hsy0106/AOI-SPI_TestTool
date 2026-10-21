using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Plugin.Abstractions.Attribute;
using Plugin.Abstractions.Entity;
using Plugin.Abstractions.Interface;

namespace Plugin_WebAPI
{
    /// <summary>
    /// Web API 客户端插件：用 HttpClient 调用远程 HTTP 接口。
    /// </summary>
    [CommunicationPlugin(
        Id = "webapi.client",
        Name = "Web API",
        Version = "1.0.0",
        Description = "Web API")]
    public class WebApiCommunicationPlugin : ICommunicationPlugin
    {
        /// <summary>插件配置（Host按 JSON 存取）。</summary>
        public class WebApiPluginConfig
        {

            public string BaseUrl { get; set; } = "http://127.0.0.1:8080/api/message";

            /// <summary>请求方法：GET / POST。</summary>
            public string Method { get; set; } = "POST";

            /// <summary>超时（毫秒）。</summary>
            public int TimeoutMs { get; set; } = 10000;
        }

        private IPluginContext _ctx;
        private WebApiPluginConfig _cfg;
        private HttpClient _client;

        public PluginState State { get; private set; } = PluginState.None;

        public void Initialize(IPluginContext ctx)
        {
            _ctx = ctx;
            _cfg = ctx.GetConfig<WebApiPluginConfig>("WebApi") ?? new WebApiPluginConfig();
            State = PluginState.Ready;
            _ctx.Log(LogLevel.Info, "WebAPI 插件初始化完成: " + _cfg.BaseUrl);
        }

        public void Start()
        {
            _client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(_cfg.TimeoutMs) };
            State = PluginState.Running;
            _ctx.Log(LogLevel.Info, "WebAPI 插件已启动");
        }

        public void Stop()
        {
            State = PluginState.Stopping;
            try { _client?.Dispose(); } catch { }
            _client = null;
            State = PluginState.Ready;
            _ctx.Log(LogLevel.Info, "WebAPI 插件已停止");
        }

        /// <summary>Host下发新配置（JSON）。</summary>
        public void ApplySetting(string json)
        {
            try
            {
                // 用 Newtonsoft.Json 反序列化，与 HostContext 保持一致
                var newCfg = JsonConvert.DeserializeObject<WebApiPluginConfig>(json);
                if (newCfg == null) return;
                _cfg = newCfg;
                _ctx.SaveConfig(_cfg, "WebApi");
                _ctx.Log(LogLevel.Info, "WebAPI 配置已更新，重启后生效");
            }
            catch (Exception ex)
            {
                _ctx.Log(LogLevel.Error, "应用配置失败: " + ex.Message);
            }
        }

        /// <summary>发送：sml 作为请求体（POST）或查询串（GET），返回响应文本。</summary>
        public async Task<SendResult> SendAsync(string sml, CancellationToken ct)
        {
            if (_client == null)
                return new SendResult { Success = false, Message = "插件未启动" };

            try
            {
                Report("TX", "请求 " + _cfg.Method + " " + _cfg.BaseUrl, sml);

                HttpResponseMessage resp;
                if (_cfg.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    resp = await _client.GetAsync(_cfg.BaseUrl, ct);
                }
                else
                {
                    var content = new StringContent(sml ?? "", Encoding.UTF8, "application/json");
                    resp = await _client.PostAsync(_cfg.BaseUrl, content, ct);
                }

                string body = await resp.Content.ReadAsStringAsync();
                string summary = string.Format("HTTP {0} {1}", (int)resp.StatusCode, resp.ReasonPhrase);
                Report("RX", summary, body);

                return new SendResult
                {
                    Success = resp.IsSuccessStatusCode,
                    Message = body ?? summary
                };
            }
            catch (Exception ex)
            {
                _ctx.Log(LogLevel.Error, "WebAPI 请求失败: " + ex.Message);
                return new SendResult { Success = false, Message = ex.Message };
            }
        }

        /// <summary>报文上报（UI 列表可见）。</summary>
        private void Report(string direction, string remark, string text)
        {
            try
            {
                _ctx.ReportMessage(new MessageRecord
                {
                    Timestamp = DateTime.Now,
                    Protocol = "HTTP",
                    Direction = direction,
                    Text = text,
                    Remark = remark,
                    RawBytes = Encoding.UTF8.GetBytes(text ?? "")
                });
            }
            catch { }
        }
    }
}