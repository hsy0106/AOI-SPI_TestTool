using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace App.Server
{
    /// <summary>
    /// 读取 MesCustom 的 XML 配置文件（如 DefaultMES.config）。
    /// 配置路径取自程序目录下 Config\config.ini 的 [Server] 段 ConfigPath。
    ///
    /// 取值示例（三种等价写法，MachineName 对应 XML 中 Code="MachineName" 的 Setting 文本）：
    ///     XmlConfigServer config = XmlConfigServer.LoadFromIni();   // 按 config.ini 自动定位
    ///     // 或 XmlConfigServer config = XmlConfigServer.Load(@"D:\EYAOI\...\DefaultMES.config");
    ///
    ///     string name1 = ((dynamic)config).MachineName;   // => "MI6071"（属性式，需 dynamic）
    ///     string name2 = config["MachineName"];           // => "MI6071"（索引器）
    ///     string name3 = config.GetValue("MachineName");  // => "MI6071"（强类型安全写法）
    /// </summary>
    public class XmlConfigServer : DynamicObject
    {
        // 按 Code 建字典，忽略大小写（machinename / MachineName 都能取）
        private readonly Dictionary<string, string> _items =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 完整条目列表（含 Code/Name/IsUserEdit/Value），用于 DataGridView 等多列展示场景
        private readonly List<XmlConfigEntry> _entries = new List<XmlConfigEntry>();

        /// <summary>本次实际加载的 XML 文件完整路径</summary>
        public string ConfigFilePath { get; private set; }

        /// <summary>
        /// 从程序目录下的 Config\config.ini 读取 [Server] 段的 ConfigPath，再加载对应的 XML。
        /// 不传 iniPath 时使用：AppDomain.CurrentDomain.BaseDirectory\Config\config.ini
        /// </summary>
        public static XmlConfigServer LoadFromIni(string iniPath = null)
        {
            if (string.IsNullOrWhiteSpace(iniPath))
                iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "config.ini");

            string xmlPath = ReadConfigPath(iniPath);

            // 相对路径按程序目录解析（不依赖进程当前工作目录），
            // 这样 config.ini 里可以写 Config\TestXml\aoi_test.config 这种可移植路径。
            if (!Path.IsPathRooted(xmlPath))
                xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, xmlPath);

            // ConfigPath 可能是“文件”，也可能是“目录”（用户描述里两种说法都有），都兼容：
            // 若是目录，则取里面第一个 .config（没有就取第一个 .xml）
            if (Directory.Exists(xmlPath))
            {
                string first = Directory.GetFiles(xmlPath, "*.config").FirstOrDefault()
                            ?? Directory.GetFiles(xmlPath, "*.xml").FirstOrDefault();
                if (first == null)
                    throw new FileNotFoundException("ConfigPath 目录中未找到任何 .config/.xml 文件：" + xmlPath);
                xmlPath = first;
            }

            return Load(xmlPath);
        }

        /// <summary>
        /// 直接加载指定的 XML 配置文件（绝对路径或相对路径均可）。
        /// </summary>
        public static XmlConfigServer Load(string xmlPath)
        {
            if (string.IsNullOrWhiteSpace(xmlPath))
                throw new ArgumentException("XML 配置路径为空。");

            // 直接调用 Load 时同样支持相对路径（按程序目录解析）
            if (!Path.IsPathRooted(xmlPath))
                xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, xmlPath);

            if (!File.Exists(xmlPath))
                throw new FileNotFoundException("找不到 XML 配置文件：" + xmlPath, xmlPath);

            var server = new XmlConfigServer { ConfigFilePath = xmlPath };

            try
            {
                var doc = XDocument.Load(xmlPath);
                var root = doc.Root;
                if (root == null)
                    throw new InvalidDataException("XML 根节点 <root> 不存在。");

                // 1) 根节点下除 TemplateParam 外的“纯文本子元素”（如 <Name>模板名</Name>）
                //    也登记为可用键，便于 config.Name 取到模板名称。
                foreach (var child in root.Elements())
                {
                    if (child.Name.LocalName == "TemplateParam") continue; // 下面单独处理
                    if (!child.HasElements && !string.IsNullOrEmpty(child.Value))
                        server._items[child.Name.LocalName] = child.Value.Trim();
                }

                // 2) <TemplateParam> 下所有 <Setting Code="xxx">值</Setting>
                var tpl = root.Element("TemplateParam");
                if (tpl != null)
                {
                    foreach (var setting in tpl.Elements("Setting"))
                    {
                        string code = setting.Attribute("Code")?.Value;
                        if (string.IsNullOrWhiteSpace(code)) continue;
                        string value = (setting.Value ?? string.Empty).Trim();
                        server._items[code] = value;
                        server._entries.Add(new XmlConfigEntry
                        {
                            Code = code,
                            Name = setting.Attribute("Name")?.Value ?? string.Empty,
                            IsUserEdit = setting.Attribute("IsUserEdit")?.Value ?? string.Empty,
                            Value = value
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogServer.Warn("加载 XML 配置失败(" + xmlPath + ")：" + ex.Message);
                throw;
            }

            return server;
        }

        /// <summary>按 Code 取值；取不到返回 defaultValue（默认 null）。</summary>
        public string GetValue(string code, string defaultValue = null)
        {
            return _items.TryGetValue(code, out var v) ? v : defaultValue;
        }

        /// <summary>索引器写法：config["MachineName"]</summary>
        public string this[string code] => GetValue(code);

        /// <summary>全部键值快照（只读），便于调试/遍历。</summary>
        public IReadOnlyDictionary<string, string> Items => _items;

        /// <summary>全部配置条目（含 Code/Name/IsUserEdit/Value），便于表格展示。</summary>
        public IReadOnlyList<XmlConfigEntry> Entries => _entries;

        /// <summary>
        /// 支持 ((dynamic)config).MachineName 这种属性式取值。
        /// 注意：调用方必须把变量声明为 dynamic，否则编译器会去找真实 CLR 属性而报“不存在”。
        /// 未定义的键返回 null（不抛异常），方便缺失配置时安全降级。
        /// </summary>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (_items.TryGetValue(binder.Name, out var v))
            {
                result = v;
                return true;
            }
            result = null;
            return true;
        }

        // ---------- 私有：从 ini 读取 [Server] 段的 ConfigPath ----------

        private static string ReadConfigPath(string iniPath)
        {
            if (!File.Exists(iniPath))
                throw new FileNotFoundException("找不到 ini 配置文件：" + iniPath, iniPath);

            string currentSection = null;
            foreach (var raw in File.ReadAllLines(iniPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                }
                else if (currentSection != null &&
                         currentSection.Equals("Server", StringComparison.OrdinalIgnoreCase))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0)
                    {
                        string key = line.Substring(0, eq).Trim();
                        string val = line.Substring(eq + 1).Trim();
                        if (key.Equals("ConfigPath", StringComparison.OrdinalIgnoreCase))
                            return val;
                    }
                }
            }

            throw new KeyNotFoundException("ini 文件 [Server] 段中未找到 ConfigPath。");
        }
    }

    /// <summary>XML 配置中的单条 Setting 记录（对应 &lt;Setting Code="..." Name="..." IsUserEdit="..."&gt;值&lt;/Setting&gt;）。</summary>
    public class XmlConfigEntry
    {
        /// <summary>参数代码（Setting 的 Code 属性）</summary>
        public string Code { get; set; }
        /// <summary>参数名称（Setting 的 Name 属性，中文描述）</summary>
        public string Name { get; set; }
        /// <summary>是否允许用户编辑（Setting 的 IsUserEdit 属性，1/0）</summary>
        public string IsUserEdit { get; set; }
        /// <summary>参数取值（Setting 的文本节点）</summary>
        public string Value { get; set; }
    }
}
