using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Plugin.Abstractions.Attribute;
using Plugin.Abstractions.Entity;
using Plugin.Abstractions.Interface;

namespace Plugin_WebServer
{
    /// <summary>
    /// Web Server 插件：用 HttpListener 监听 HTTP 请求并应答。
    /// </summary>
    [CommunicationPlugin(
        Id = "webserver.host",
        Name = "Web Server",
        Version = "1.0.0",
        Description = "通过 HttpListener 提供 HTTP 服务，接收并应答请求")]
    public class WebServerCommunicationPlugin : ICommunicationPlugin
    {
        /// <summary>插件配置（宿主按 JSON 存取）。</summary>
        public class WebServerPluginConfig
        {
            /// <summary>监听前缀，如 http://*:8080/（Windows 需以 / 结尾）。</summary>
            public string Prefix { get; set; } = "http://*:8080/";

            /// <summary>应答内容类型。</summary>
            public string ContentType { get; set; } = "application/json";

            /// <summary>固定应答体（模拟 MES 应答）。</summary>
            public string ResponseBody { get; set; } = "{\"status\":\"ok\",\"code\":0}";
        }

        private IPluginContext _ctx;
        private WebServerPluginConfig _cfg;
        private HttpListener _listener;
        private volatile bool _running;
        private string _lastRequest = "";

        public PluginState State { get; private set; } = PluginState.None;

        public void Initialize(IPluginContext ctx)
        {
            _ctx = ctx;
            _cfg = ctx.GetConfig<WebServerPluginConfig>("WebServer") ?? new WebServerPluginConfig();
            State = PluginState.Ready;
            _ctx.Log(LogLevel.Info, "WebServer 插件初始化完成: " + _cfg.Prefix);
        }

        public void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(_cfg.Prefix);
                _listener.Start();

                _running = true;
                var t = new Thread(AcceptLoop) { IsBackground = true };
                t.Start();

                State = PluginState.Running;
                _ctx.Log(LogLevel.Info, "WebServer 插件已启动，监听 " + _cfg.Prefix);
            }
            catch (Exception ex)
            {
                State = PluginState.Faulted;
                _ctx.Log(LogLevel.Error, "WebServer 启动失败: " + ex.Message);
            }
        }

        public void Stop()
        {
            State = PluginState.Stopping;
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
            State = PluginState.Ready;
            _ctx.Log(LogLevel.Info, "WebServer 插件已停止");
        }

        /// <summary>宿主下发新配置（JSON）。</summary>
        public void ApplySetting(string json)
        {
            try
            {
                // 用 Newtonsoft.Json 反序列化，与 HostContext 保持一致
                var newCfg = JsonConvert.DeserializeObject<WebServerPluginConfig>(json);
                if (newCfg == null) return;
                _cfg = newCfg;
                _ctx.SaveConfig(_cfg, "WebServer");
                _ctx.Log(LogLevel.Info, "WebServer 配置已更新，重启后生效");
            }
            catch (Exception ex)
            {
                _ctx.Log(LogLevel.Error, "应用配置失败: " + ex.Message);
            }
        }

        /// <summary>发送：返回最近一次收到的 HTTP 请求内容（模拟 MES 下发应答）。</summary>
        public Task<SendResult> SendAsync(string sml, CancellationToken ct)
        {
            string last = _lastRequest;
            return Task.FromResult(new SendResult
            {
                Success = !string.IsNullOrEmpty(last),
                Message = string.IsNullOrEmpty(last) ? "尚未收到任何请求" : "最近请求: " + last
            });
        }

        /// <summary>后台接受循环：每个请求 → 上报报文 → 返回固定应答。</summary>
        private void AcceptLoop()
        {
            while (_running && _listener != null)
            {
                try
                {
                    HttpListenerContext ctx = _listener.GetContext();
                    HandleRequest(ctx);
                }
                catch { break; }   // Stop 时跳出
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                string reqBody = "";
                using (var reader = new System.IO.StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding ?? Encoding.UTF8))
                    reqBody = reader.ReadToEnd();

                string line = string.Format("{0} {1}", ctx.Request.HttpMethod, ctx.Request.Url);
                _lastRequest = line + (string.IsNullOrEmpty(reqBody) ? "" : "\n" + reqBody);
                Report("RX", line, reqBody);

                byte[] data = Encoding.UTF8.GetBytes(_cfg.ResponseBody ?? "");
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = _cfg.ContentType;
                ctx.Response.ContentLength64 = data.Length;
                ctx.Response.OutputStream.Write(data, 0, data.Length);
                ctx.Response.Close();

                Report("TX", "HTTP 200 应答", _cfg.ResponseBody);
            }
            catch (Exception ex)
            {
                _ctx.Log(LogLevel.Error, "处理请求异常: " + ex.Message);
                try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
            }
        }

        /// <summary>报文上报 </summary>
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
