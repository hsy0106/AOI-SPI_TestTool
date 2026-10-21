using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace App
{
    /// <summary>
    /// 通用 INI 配置读取器（静态类，全进程一份缓存，文件改动后自动重新加载）。
    ///
    /// 默认读取：程序目录\Config\config.ini
    ///
    /// 用法（不需要 new，直接类名调用）：
    ///     string p1 = App.IniConfigServer.GetValue("Server", "ConfigPath");     // 段 + 键
    ///     string p2 = App.IniConfigServer.GetValue("Server.ConfigPath");        // 段.键 连写
    ///     string p3 = App.IniConfigServer.GetValue("ConfigPath");               // 默认段 [Server]
    ///     int    n1 = App.IniConfigServer.GetInt("Server", "Port", 502);
    ///     bool   b1 = App.IniConfigServer.GetBool("Server", "Enabled", false);
    ///
    /// 想换文件：
    ///     App.IniConfigServer.ConfigPath = @"D:\Xxx\config.ini";
    ///     App.IniConfigServer.Reload();        // 强制重读
    ///
    /// 说明：
    ///   1) 段名、键名均忽略大小写；
    ///   2) 取值失败（文件不存在 / 段键不存在）返回 defaultValue，绝不抛异常；
    ///   3) 支持 ; 和 # 整行注释；行内注释只认「前面有空白的 ;」；
    ///   4) 值两侧的成对引号会被去掉；
    ///   5) 编码：有 BOM 按 BOM，无 BOM 按系统默认（中文 Windows 即 GBK/ANSI）。
    /// </summary>
    public static class IniConfigServer
    {
        private static readonly object _sync = new object();

        // 段 -> (键 -> 值)，段名与键名都是 OrdinalIgnoreCase
        private static Dictionary<string, Dictionary<string, string>> _cache;

        private static string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "config.ini");
        private static DateTime _cacheWriteTime = DateTime.MinValue;
        private static long _cacheLength = -1;

        // 日志节流，避免每次取不到值都刷一条日志
        private static string _lastWarnMessage;
        private static DateTime _lastWarnTime = DateTime.MinValue;

        // 无段名时的归属段（INI 里出现在第一个 [Section] 之前的键值）
        private const string RootSection = "";

        /// <summary>当前使用的 ini 文件完整路径（默认 程序目录\Config\config.ini）。赋值后缓存自动失效。</summary>
        public static string ConfigPath
        {
            get { return _configPath; }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                lock (_sync)
                {
                    _configPath = value;
                    _cache = null;
                    _cacheLength = -1;
                    _lastWarnMessage = null;
                }
            }
        }

        /// <summary>
        /// 单参数取值时使用的默认段，默认 "Server"。
        /// 即 GetValue("ConfigPath") 等价于 GetValue("Server", "ConfigPath")。
        /// </summary>
        public static string DefaultSection { get; set; } = "Server";

        /// <summary>让缓存失效，下次取值时重新读文件。</summary>
        public static void Reload()
        {
            lock (_sync)
            {
                _cache = null;
                _cacheLength = -1;
            }
        }

        #region ---------- 取值（字符串） ----------

        /// <summary>
        /// 单参数取值：可写 "段名.键名"，也可只写 "键名"（先查默认段，再全表唯一匹配）。
        /// 取不到返回 null。
        /// </summary>
        public static string GetValue(string keyOrSectionDotKey)
        {
            return GetValueCore(keyOrSectionDotKey, null);
        }

        /// <summary>按「段 + 键」取值，取不到返回 defaultValue。</summary>
        public static string GetValue(string section, string key, string defaultValue = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return defaultValue;

            var data = GetData();
            if (data == null) return defaultValue;

            Dictionary<string, string> sec;
            string value;
            if (TryGetSection(data, section, out sec) && sec.TryGetValue(key.Trim(), out value))
                return value;

            return defaultValue;
        }

        /// <summary>取不到返回 false（value 为 null）。</summary>
        public static bool TryGetValue(string section, string key, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(key)) return false;

            var data = GetData();
            if (data == null) return false;

            Dictionary<string, string> sec;
            return TryGetSection(data, section, out sec) && sec.TryGetValue(key.Trim(), out value);
        }

        #endregion

        #region ---------- 取值（强类型） ----------

        /// <summary>int 取值，不存在或转换失败返回 defaultValue。</summary>
        public static int GetInt(string section, string key, int defaultValue = 0)
        {
            return ToInt(GetValue(section, key), defaultValue);
        }

        public static int GetInt(string keyOrSectionDotKey, int defaultValue)
        {
            return ToInt(GetValueCore(keyOrSectionDotKey, null), defaultValue);
        }

        /// <summary>double 取值，不存在或转换失败返回 defaultValue。</summary>
        public static double GetDouble(string section, string key, double defaultValue = 0)
        {
            return ToDouble(GetValue(section, key), defaultValue);
        }

        public static double GetDouble(string keyOrSectionDotKey, double defaultValue)
        {
            return ToDouble(GetValueCore(keyOrSectionDotKey, null), defaultValue);
        }

        /// <summary>bool 取值：1/0、true/false、yes/no、on/off 均可（忽略大小写）。</summary>
        public static bool GetBool(string section, string key, bool defaultValue = false)
        {
            return ToBool(GetValue(section, key), defaultValue);
        }

        public static bool GetBool(string keyOrSectionDotKey, bool defaultValue)
        {
            return ToBool(GetValueCore(keyOrSectionDotKey, null), defaultValue);
        }

        #endregion

        #region ---------- 整段 / 全表 ----------

        /// <summary>取整个段（键值对副本），段不存在返回空字典（不返回 null）。</summary>
        public static Dictionary<string, string> GetSection(string section)
        {
            var data = GetData();
            Dictionary<string, string> sec;
            if (data != null && TryGetSection(data, section, out sec))
                return new Dictionary<string, string>(sec, StringComparer.OrdinalIgnoreCase);

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>全部段名（不含无段名的根键区）。</summary>
        public static List<string> GetSectionNames()
        {
            var names = new List<string>();
            var data = GetData();
            if (data != null)
            {
                foreach (var kv in data)
                    if (!string.IsNullOrEmpty(kv.Key)) names.Add(kv.Key);
            }
            return names;
        }

        /// <summary>全部键值快照，键形如 "Server.ConfigPath"，便于调试遍历。</summary>
        public static Dictionary<string, string> GetAll()
        {
            var all = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var data = GetData();
            if (data == null) return all;

            foreach (var sec in data)
                foreach (var kv in sec.Value)
                    all[string.IsNullOrEmpty(sec.Key) ? kv.Key : sec.Key + "." + kv.Key] = kv.Value;

            return all;
        }

        #endregion

        #region ---------- 内部实现 ----------

        /// <summary>
        /// 核心取值："段.键" → 按段取；否则先查默认段，再全表唯一匹配（多段同名键视为歧义，返回默认值）。
        /// </summary>
        private static string GetValueCore(string keyOrSectionDotKey, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(keyOrSectionDotKey)) return defaultValue;

            var data = GetData();
            if (data == null) return defaultValue;

            string raw = keyOrSectionDotKey.Trim();

            // 1) 段.键 写法
            int dot = raw.IndexOf('.');
            if (dot > 0 && dot < raw.Length - 1)
            {
                string secName = raw.Substring(0, dot).Trim();
                string keyName = raw.Substring(dot + 1).Trim();

                Dictionary<string, string> sec;
                string v;
                if (TryGetSection(data, secName, out sec) && sec.TryGetValue(keyName, out v))
                    return v;
            }

            // 2) 默认段
            Dictionary<string, string> defSec;
            string value;
            string defName = DefaultSection;
            if (!string.IsNullOrEmpty(defName) && TryGetSection(data, defName, out defSec) && defSec.TryGetValue(raw, out value))
                return value;

            // 3) 全表唯一匹配（含根键区）
            string found = null;
            int hit = 0;
            foreach (var sec in data)
            {
                string v;
                if (sec.Value.TryGetValue(raw, out v))
                {
                    found = v;
                    hit++;
                }
            }

            if (hit == 1) return found;
            if (hit > 1) WarnOnce("INI 键 [" + raw + "] 在多个段中重复出现，取值有歧义：" + _configPath);

            return defaultValue;
        }

        private static bool TryGetSection(Dictionary<string, Dictionary<string, string>> data,
                                          string section, out Dictionary<string, string> value)
        {
            value = null;
            if (data == null) return false;

            // 段名传 null / 空 → 视为根键区
            string name = (section ?? string.Empty).Trim();
            if (name.Length == 0) name = RootSection;

            return data.TryGetValue(name, out value);
        }

        /// <summary>取数据（带文件长度与修改时间校验，文件改过就自动重读）。</summary>
        private static Dictionary<string, Dictionary<string, string>> GetData()
        {
            lock (_sync)
            {
                FileInfo fi = null;
                try
                {
                    fi = new FileInfo(_configPath);
                }
                catch
                {
                    // 路径非法，按空配置处理
                    return _cache ?? new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                }

                bool mustReload = _cache == null;

                if (!mustReload && fi.Exists)
                {
                    // 内容长度或修改时间变了 → 重读
                    if (_cacheLength < 0 || fi.Length != _cacheLength || fi.LastWriteTimeUtc != _cacheWriteTime)
                        mustReload = true;
                }

                if (mustReload)
                {
                    _cache = LoadCore(fi);
                    if (fi.Exists && _cache != null)
                    {
                        _cacheLength = fi.Length;
                        _cacheWriteTime = fi.LastWriteTimeUtc;
                    }
                }

                return _cache;
            }
        }

        /// <summary>真正的解析：逐行读，识别 [段] 与 键=值。</summary>
        private static Dictionary<string, Dictionary<string, string>> LoadCore(FileInfo fi)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            if (fi == null || !fi.Exists)
            {
                WarnOnce("INI 文件不存在：" + _configPath);
                return result;
            }

            string[] lines;
            try
            {
                // 有 BOM 时自动识别；无 BOM 按系统默认编码（中文 Windows = GBK）
                lines = File.ReadAllText(fi.FullName, Encoding.Default)
                            .Replace("\r\n", "\n").Replace('\r', '\n')
                            .Split('\n');
            }
            catch (Exception ex)
            {
                WarnOnce("读取 INI 文件失败(" + _configPath + ")：" + ex.Message);
                return result;
            }

            Dictionary<string, string> current = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = (lines[i] ?? string.Empty).Trim();

                // 空行 / 整行注释
                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

                // [段名]
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    string name = line.Substring(1, line.Length - 2).Trim();
                    if (!result.TryGetValue(name, out current))
                    {
                        current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        result[name] = current;
                    }
                    continue;
                }

                // 键 = 值
                int sep = line.IndexOf('=');
                if (sep <= 0)
                {
                    // 兼容 key : value（前提是冒号右边不像路径）
                    sep = IndexOfKeyColon(line);
                    if (sep <= 0) continue;   // 既没 = 也没 :，忽略该行
                }

                string key = line.Substring(0, sep).Trim();
                if (key.Length == 0) continue;

                string value = TrimQuotes(StripInlineComment(line.Substring(sep + 1).Trim()));

                if (current == null)
                {
                    // 第一个 [Section] 之前的键，归到根键区
                    if (!result.TryGetValue(RootSection, out current))
                    {
                        current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        result[RootSection] = current;
                    }
                }

                current[key] = value;   // 同名键以后出现的为准
            }

            return result;
        }

        /// <summary>找「像 key:」的冒号位置；不像则返回 -1（防止把 D:\xxx 当成键值对）。</summary>
        private static int IndexOfKeyColon(string line)
        {
            int idx = line.IndexOf(':');
            if (idx <= 0) return -1;

            // 冒号右边紧跟 \ 或 / 说明这是路径而非分隔符
            if (idx + 1 < line.Length && (line[idx + 1] == '\\' || line[idx + 1] == '/')) return -1;

            return idx;
        }

        /// <summary>去行内注释：只认「前面有空白的 ;」，避免误伤值里的分号。</summary>
        private static string StripInlineComment(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            for (int i = 1; i < value.Length; i++)
            {
                if (value[i] != ';') continue;
                if (char.IsWhiteSpace(value[i - 1]))
                    return value.Substring(0, i).Trim();
            }
            return value;
        }

        /// <summary>去掉包裹整个值的成对引号："abc" / 'abc'。</summary>
        private static string TrimQuotes(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2) return value;

            char first = value[0];
            char last = value[value.Length - 1];
            if ((first == '"' || first == '\'') && first == last)
                return value.Substring(1, value.Length - 2).Trim();

            return value;
        }

        private static int ToInt(string s, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(s)) return defaultValue;

            int v;
            return int.TryParse(s.Trim(), out v) ? v : defaultValue;
        }

        private static double ToDouble(string s, double defaultValue)
        {
            if (string.IsNullOrWhiteSpace(s)) return defaultValue;

            double v;
            return double.TryParse(s.Trim(), out v) ? v : defaultValue;
        }

        private static bool ToBool(string s, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(s)) return defaultValue;

            string v = s.Trim().ToLowerInvariant();

            if (v == "1" || v == "true" || v == "yes" || v == "on" || v == "y") return true;
            if (v == "0" || v == "false" || v == "no" || v == "off" || v == "n") return false;

            bool b;
            return bool.TryParse(v, out b) ? b : defaultValue;
        }

        /// <summary>同一句告警 60 秒内只写一次，避免刷日志。</summary>
        private static void WarnOnce(string message)
        {
            try
            {
                lock (_sync)
                {
                    if (message == _lastWarnMessage && (DateTime.Now - _lastWarnTime).TotalSeconds < 60)
                        return;

                    _lastWarnMessage = message;
                    _lastWarnTime = DateTime.Now;
                }

                Server.LogServer.Warn(message);
            }
            catch
            {
                // 日志失败不影响取值
            }
        }

        #endregion
    }
}
