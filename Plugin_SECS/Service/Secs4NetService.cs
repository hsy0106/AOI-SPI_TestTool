using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Plugin.Abstractions.Entity;
using Plugin.Abstractions.Interface;
using Plugin_SECS.Core;
using Plugin_SECS.Sml;
using Secs4Net;

namespace Plugin_SECS.Service
{
    /// <summary>
    /// SecsServer核心服务
    /// 三种连接模式：
    ///   Passive  = 监听 ListenIp:ListenPort被动监听；
    ///   Active   = 主动连接
    ///   SelfTest = 自检模拟验证。
    /// </summary>
    public sealed class Secs4NetService : IDisposable
    {
        private readonly IPluginContext _ipc;
        private readonly PluginGemLogger _logger;

        // 被动会话（Passive / SelfTest 监听端）
        private HsmsConnection _passiveConn;
        private SecsGem _passiveGem;
        // 主动会话（Active / SelfTest 自连端）
        private HsmsConnection _activeConn;
        private SecsGem _activeGem;

        //模拟应答设备selftest 
        private EquipmentSimulator _equipment;

        private CancellationTokenSource _cts;
        private Task _rxTaskPassive;
        private Task _rxTaskActive;


        public bool IsSelected { get; private set; }

        public SecsPluginConfig Config { get; private set; }

        //状态变更事件
        public event Action<string> StateChanged;

        public Secs4NetService(IPluginContext ipc)
        {
            _ipc = ipc;
       
            _logger = new PluginGemLogger(ipc, "Secs4Net", () => Config != null ? Config.DeviceId : 0);
        }



        /// <summary>
        /// 按配置启动传输层
        /// </summary>
        /// <param name="cfg"></param>
        public void Start(SecsPluginConfig cfg)
        {
            Stop();
            Config = cfg.Clone();
            _cts = new CancellationTokenSource();
            
        

            string mode = (cfg.Mode ?? "Passive").Trim();
            switch (mode)
            {
                case "SelfTest":
                    // 1) 被动监听端
                    _passiveGem = BuildGem(isActive: false, ip: cfg.ListenIp, port: cfg.ListenPort, label: "监听");
                    // 2) 内部主动自连端（连自己）
                    _equipment = new EquipmentSimulator(_ipc, cfg);
                    _activeGem = BuildGem(isActive: true, ip: "127.0.0.1", port: cfg.ListenPort, label: "自测-客户端");
                    _rxTaskPassive = RunReceiverAsync(_passiveGem, "监听", _cts.Token);
                    _rxTaskActive   = RunReceiverAsync(_activeGem,   "自测-客户端", _cts.Token);
                    _ipc.Log(LogLevel.Info, string.Format("单机自测已启动：监听 {0}:{1} ", cfg.ListenIp, cfg.ListenPort));
                    break;
                //Host
                case "Active":
                    _activeGem = BuildGem(isActive: true, ip: cfg.RemoteHost, port: cfg.RemotePort, label: "主动");
                    _rxTaskActive = RunReceiverAsync(_activeGem, "主动", _cts.Token);
                    _ipc.Log(LogLevel.Info, string.Format("主动连接已启动：{0}:{1}", cfg.RemoteHost, cfg.RemotePort));
                    break;
                //Client
                default: // Passive
                    _passiveGem = BuildGem(isActive: false, ip: cfg.ListenIp, port: cfg.ListenPort, label: "监听");
                    _rxTaskPassive = RunReceiverAsync(_passiveGem, "监听", _cts.Token);
                    _ipc.Log(LogLevel.Info, string.Format("被动监听已启动：{0}:{1}", cfg.ListenIp, cfg.ListenPort));
                    break;
            }
        }


        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }

            SafeDisposeConn(ref _passiveConn); _passiveGem = null;
            SafeDisposeConn(ref _activeConn);  _activeGem = null;

            _equipment = null;
            _cts?.Dispose();
            _cts = null;
            IsSelected = false;
        }
        //释放资源
        private static void SafeDisposeConn(ref HsmsConnection conn)
        {
            if (conn == null) return;
            try { conn.DisposeAsync().AsTask().Wait(2000); } catch { }
            conn = null;
        }


        //构造SecsGem（设备行为）
        private SecsGem BuildGem(bool isActive, string ip, int port, string label)
        {
            var cfg = Config;
            var opts = new SecsGemOptions
            {
                DeviceId = (ushort)cfg.DeviceId,
                IsActive = isActive,
                IpAddress = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim(),
                Port = port,
                // HSMS 计时器（毫秒）直接映射到开源库
                T3 = cfg.T3Ms,   // 事务应答超时
                T5 = cfg.T5Ms,   // 连接重试间隔
                T6 = cfg.T6Ms,   // 控制消息应答超时
                T7 = cfg.T7Ms,   // TCP 建立超时
                T8 = cfg.T8Ms,   // 网络读超时
                LinkTestInterval = cfg.LinktestIntervalMs, // 心跳间隔，0=关闭
                SocketReceiveBufferSize = 81920,
                EncodeBufferInitialSize = 1024
            };

            var iopts = Options.Create(opts);
            var conn = new HsmsConnection(iopts, _logger);
            var gem  = new SecsGem(iopts, conn, _logger);
            //状态映射
            conn.ConnectionChanged += (s, e) => OnConnectionChanged(e, label);

            if (isActive)
            {
                _activeConn = conn;
                _ipc.Log(LogLevel.Info, string.Format("[{0}] 正在连接 {1}:{2}（设备ID={3}）", label, opts.IpAddress, opts.Port, opts.DeviceId));
            }
            else
            {
                _passiveConn = conn;
                _ipc.Log(LogLevel.Info, string.Format("[{0}] 监听 {1}:{2}（设备ID={3}）", label, opts.IpAddress, opts.Port, opts.DeviceId));
            }

            conn.Start(CancellationToken.None);
            return gem;
        }

        /// <summary>
        /// 连接状态变化
        /// </summary>
        /// <param name="state"></param>
        /// <param name="label"></param>
        private void OnConnectionChanged(ConnectionState state, string label)
        {
            string text;
            switch (state)
            {
                case ConnectionState.Selected:  text = "Selected";   break;
                case ConnectionState.Connected: text = "Connected";  break;
                case ConnectionState.Retry:     text = "Retrying";   break;
                default:                        text = "Connecting"; break;
            }

            if (text == "Selected")
            {
                IsSelected = true;
                _ipc.Log(LogLevel.Info, string.Format("[{0}] HSMS 会话已建立（Selected）", label));
            }
            else if (text == "Retrying")
            {
                // 仅主动端 TCP 连接失败重试才算异常 → Warn；监听端不会进入 Retry
                IsSelected = false;
                _ipc.Log(LogLevel.Warn, string.Format("[{0}] HSMS 连接失败，重试中：{1}", label, text));
            }
            else if (text == "Connecting")
            {
                
                IsSelected = false;
                _ipc.Log(LogLevel.Info, string.Format("[{0}] HSMS 连接建立中：{1}", label, text));
            }
            else
            {
                IsSelected = false;
                _ipc.Log(LogLevel.Info, string.Format("[{0}] HSMS 连接状态：{1}", label, text));
            }
            StateChanged?.Invoke(text);
        }

        //设备应答
        private async Task RunReceiverAsync(SecsGem gem, string label, CancellationToken ct)
        {
            try
            {
              
                var enumerator = gem.GetPrimaryMessageAsync(ct).GetAsyncEnumerator();
                try
                {
                    while (await enumerator.MoveNextAsync())
                    {
                        var wrapper = enumerator.Current;
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            using (var pm = wrapper.PrimaryMessage)
                            {
                                var reply = _equipment?.Handle(pm);
                                if (reply != null)
                                {
                                    _ipc.Log(LogLevel.Info, string.Format("[{0}] 自动应答 S{1}F{2}", label, reply.S, reply.F));
                                    await wrapper.TryReplyAsync(reply, ct);
                                }
                            }
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex)
                        {
                            _ipc.Log(LogLevel.Error, string.Format("[{0}] 处理主消息异常: {1}", label, ex.Message));
                        }
                    }
                }
                finally
                {
                    await enumerator.DisposeAsync();
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                _ipc.Log(LogLevel.Error, string.Format("[{0}] 接收循环退出: {1}", label, ex.Message));
            }
        }

        //发送一条 SML 消息（插件接收界面发送的sml string）
        public async Task<SendResult> SendAsync(string sml, CancellationToken ct)
        {
            //判断是主动还是被动
            var gem = _activeGem ?? _passiveGem;
            if (gem == null)
                return new SendResult { Success = false, Message = "插件未启动，无法发送" };

            SecsMessage msg =  null;
            try
            {
                msg = SmlService.Parse(sml);          // SML 文本 → SecsMessage
                bool wait = msg.ReplyExpected;

                // Secs4Net：W 位消息等待 reply（T3 超时抛 SecsException）；
                // 非 W 位消息返回原消息本身。
                SecsMessage reply = await gem.SendAsync(msg, ct);
                if (msg.ReplyExpected)
                {
                    using (reply)
                    {
                        _ipc.Log(LogLevel.Info, "SendAsync 收到应答: " + SmlService.ToSml(reply));
                        return new SendResult { Success = true, Message = SmlService.ToSml(reply) };
                    }
                }
                return new SendResult { Success = true, Message = "已发送（无应答等待）" };
            }
            catch (SecsException ex)
            {
                // T3 超时 / 对端回 SxF0 / 对端回 S9Fx 都会抛 SecsException
                _ipc.Log(LogLevel.Warn, "SendAsync 失败（协议级）: " + ex.Message);
                return new SendResult { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _ipc.Log(LogLevel.Error, "SendAsync 异常: " + ex.Message);
                return new SendResult { Success = false, Message = ex.Message };
            }
            finally
            {
                msg?.Dispose();
            }
        }

        
        public void Dispose()
        {
            Stop();
        }
    }
}
