using System;
using Plugin.Abstractions.Entity;
using Plugin.Abstractions.Interface;
using Plugin_SECS.Sml;
using Secs4Net;
using Secs4Net.Sml;

namespace Plugin_SECS
{
    /// <summary>
    /// Secs4Net 日志
    /// </summary>
    public sealed class PluginGemLogger : ISecsGemLogger
    {
        private readonly IPluginContext _ctx;
        private readonly string _tag;
        private readonly Func<int> _deviceIdProvider;  
        private int _txId;                           

        public PluginGemLogger(IPluginContext ctx, string tag, Func<int> deviceIdProvider = null)
        {
            _ctx = ctx;
            _tag = tag;
            _deviceIdProvider = deviceIdProvider ?? (() => 0);
        }

      
        public void MessageIn(SecsMessage msg, int id)
        {
            Report(msg, "RX", "收到 " + MsgName(msg, id));
        }


        public void MessageOut(SecsMessage msg, int id)
        {
            Report(msg, "TX", "发出 " + MsgName(msg, id));
        }


        public void Debug(string msg)   { _ctx.Log(LogLevel.Debug, "[" + _tag + "] " + msg); }
        public void Info(string msg)    { _ctx.Log(LogLevel.Info,  "[" + _tag + "] " + msg); }
        public void Warning(string msg) { _ctx.Log(LogLevel.Warn,  "[" + _tag + "] " + msg); }
        public void Error(string msg)   { _ctx.Log(LogLevel.Error, "[" + _tag + "] " + msg); }

        public void Error(string msg, Exception ex)
        {
            _ctx.Log(LogLevel.Error, "[" + _tag + "] " + msg, ex);
        }

        public void Error(string msg, SecsMessage message, Exception ex)
        {
         
            _ctx.Log(LogLevel.Error, "[" + _tag + "] " + msg + " | 消息: " + $"Name:{ message.Name},S:{message.S},F:{message.F},ID:0", ex);
        }



        private static string MsgName(SecsMessage msg, int id)
        {
            if (msg == null) return "(空消息)";
            string name = string.IsNullOrEmpty(msg.Name) ? "" : " " + msg.Name;
            return string.Format("S{0}F{1}{2} (id={3})", msg.S, msg.F, name, id);
        }

        private void Report(SecsMessage msg, string direction, string remark)
        {
            try
            {
                if (msg == null) return;


                byte[] raw = null;
                try
                {
                    int dev = _deviceIdProvider();
                    raw = SmlService.MessageToFrame(msg, dev, ++_txId);
                }
                catch { raw = null; }

                var rec = new MessageRecord
                {
                    Timestamp = DateTime.Now,
                    Protocol = "HSMS",
                    Direction = direction,
                    Text = SmlWriter.ToSml(msg),          
                    Remark = remark,
                    RawBytes = raw,                      
                    Meta =
                    {
                        ["S"] = msg.S,
                        ["F"] = msg.F,
                        ["ReplyExpected"] = msg.ReplyExpected,
                        ["Name"] = msg.Name ?? ""
                    }
                };
                _ctx.ReportMessage(rec);
            }
            catch {  }
        }
    }
}
