using System;
using Plugin.Abstractions.Interface;
using Secs4Net;

namespace Plugin_SECS.Core
{
    /// <summary>
    /// GEM 设备模拟应答器 —— 收到 Primary 消息后自动构造标准应答（学习 HSMS 应答行为用）。
    ///
    /// 2026-09-02 升级为「动态应答」：应答内容不再写死，而是从设备内部状态字段实时取值，
    /// 模拟真实设备的传感器行为 —— 温度/压力每次采样小幅漂移，Host 每次查询看到的数值都不同，
    /// 这正是「固定应答 vs 动态应答」的教学对比点。
    ///
    /// 应答框架（协议层，固定）：
    ///   S1F13 (Establish Communications Request) → S1F14（MDLN + SOFTREV）
    ///   S1F1  (Are You Online)                  → S1F2（Online，取当前 _online 状态）
    ///   S1F3  (Status Variable Request)         → S1F4（当前全部 SV 实时值）
    ///   S1F15 (Request OFF-LINE)                → S1F16（接受离线，_online=false）
    ///   S1F17 (Request ON-LINE)                 → S1F18（接受在线，_online=true）
    ///   S2F17 (Date and Time Request)           → S2F18（返回设备当前时间）
    ///   S2F41 (Remote Command Send)             → S2F42（解析命令并驱动设备状态：START/STOP）
    ///   S6F11 (Event Report)                    → S6F12（确认）
    ///   S7F20 (Process Program Download)        → S7F21（EAC 确认）
    ///   其它                                    → SxF0（不接受）
    ///
    /// 基于 Secs4Net.Item 工厂构造 SECS-II 数据项。
    /// </summary>
    public sealed class EquipmentSimulator
    {
        private readonly IPluginContext _ctx;
        private readonly SecsPluginConfig _cfg;   // 提供 S1F14 应答内容（MDLN/SOFTREV）
        private readonly Random _rnd = new Random();


        private int    _runState    = 0;      // SV3 运行状态：0=空闲 1=运行中 2=报警
        private bool   _online      = true;   // 是否在线（S1F2 应答依据，真实设备离线时不应答「在线」）

        public EquipmentSimulator(IPluginContext ctx, SecsPluginConfig cfg)
        {
            _ctx = ctx;
            _cfg = cfg;
        }


        public SecsMessage Handle(SecsMessage pm)
        {
            if (pm == null) return null;



            // 模拟应答
            switch (pm.S)
            {
                case 1:
                    switch (pm.F)
                    {
                        case 13: return S1F14();
                        case 1:  return S1F2();
                        case 15: return S1F16();   
                        case 17: return S1F18();   
                    }
                    break;
                case 2:
                    if (pm.F == 41) return S2F42(pm);   
                    if (pm.F == 17) return S2F18();     
                    break;
                case 6:
                    if (pm.F == 11) return S6F12();
                    break;
                case 7:
                    if (pm.F == 20) return S7F21();
                    break;
            }


            _ctx.Log(LogLevel.Warn, string.Format("未支持模拟消息回复，回 S{0}F0", pm.S, pm.F));
            return new SecsMessage(pm.S, 0, replyExpected: false)
            {
                Name = "NotAcceptable",
                SecsItem = Item.U4(0)   // 错误码 0：无法识别的消息
            };
        }



        #region 构造应答

        private SecsMessage S1F14()
        {
            return new SecsMessage(1, 14, replyExpected: false)
            {
                Name = "EstablishCommunicationsRequest-Ack",
                SecsItem = Item.L(
                    Item.A(_cfg.MdlName ?? "DemoTool"),
                    Item.A(_cfg.SoftRev ?? "1.0.0"))
            };
        }

        private SecsMessage S1F2()
        {
            return new SecsMessage(1, 2, replyExpected: false)
            {
                Name = "AreYouOnline-Ack",
                SecsItem = Item.Boolean(_online)
            };
        }



        private SecsMessage S1F16()
        {
            _online = false;   // 设备进入离线状态
            _ctx.Log(LogLevel.Info, "S1F15 请求离线 → 设备已离线");
            return new SecsMessage(1, 16, replyExpected: false)
            {
                Name = "Offline-Ack",
                SecsItem = Item.B(0)   
            };
        }


        private SecsMessage S1F18()
        {
            _online = true;    
            _ctx.Log(LogLevel.Info, "S1F17 请求在线 → 设备已在线");
            return new SecsMessage(1, 18, replyExpected: false)
            {
                Name = "Online-Ack",
                SecsItem = Item.B(0)   
            };
        }

        private SecsMessage S2F18()
        {
            string now = DateTime.Now.ToString("yyyyMMddHHmmss"); 
            return new SecsMessage(2, 18, replyExpected: false)
            {
                Name = "DateTimeData",
                SecsItem = Item.A(now)   
            };
        }


        private SecsMessage S2F42(SecsMessage pm)
        {
            byte hcack = 2;          // 默认：无法解析
            string cmd = "";
            try
            {
                // S2F41 数据区格式：<L [n] <A RCMD> <L [CP 参数列表]（可选）>>
                var item = pm.SecsItem;
                if (item != null && item.Count >= 1)
                {
                    cmd = item[0].GetString() ?? "";   // 第 0 个元素 = 命令名（ASCII）
                    switch (cmd)
                    {
                        case "START": _runState = 1; hcack = 0; break;   // 接受，设备进入运行中
                        case "STOP":  _runState = 0; hcack = 0; break;   // 接受，设备回到空闲
                        default:      hcack = 1; break;                  // 命令不支持
                    }
                }
            }
            catch (Exception ex)
            {
                _ctx.Log(LogLevel.Warn, "S2F41 解析异常: " + ex.Message);
                hcack = 2;
            }

            _ctx.Log(LogLevel.Info, string.Format(
                "S2F41 远程命令 [{0}] → 设备状态={1}, HCACK={2}", cmd, _runState, hcack));
            return new SecsMessage(2, 42, replyExpected: false)
            {
                Name = "RemoteCommand-Ack",
                SecsItem = Item.B(hcack)   
            };
        }

       
        private SecsMessage S6F12()
        {
            return new SecsMessage(6, 12, replyExpected: false)
            {
                Name = "EventReport-Ack",
                SecsItem = Item.Boolean(true)   
            };
        }


        private SecsMessage S7F21()
        {
            return new SecsMessage(7, 21, replyExpected: false)
            {
                Name = "ProcessProgram-Ack",
                SecsItem = Item.L(Item.B(0))   
            };
        }

        #endregion
    }
}
