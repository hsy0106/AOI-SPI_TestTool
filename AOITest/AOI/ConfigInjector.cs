using App.Server;
using DomainAOI.DataExport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace AOITest.AOI
{
    /// <summary>注入结果。</summary>
    public class InjectResult
    {
        /// <summary>校验全部通过才为 true</summary>
        public bool Success;

       
        public string ConfigCode;

        public int TemplateParamCount;

        public readonly List<string> Applied = new List<string>();
        public readonly List<string> Added = new List<string>();
        public readonly List<string> Unknown = new List<string>();
        public readonly List<string> VerifyFailed = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        /// <summary>一行摘要</summary>
        public string Message
        {
            get
            {
                return string.Format("基准={0} 参数={1} 改值={2} 新增={3} 未识别={4} 校验失败={5}",
                    ConfigCode, TemplateParamCount, Applied.Count, Added.Count,
                    Unknown.Count, VerifyFailed.Count);
            }
        }

        /// <summary>有未识别项（XML 里有、插件参数表里没有）。不影响执行，只影响日志。</summary>
        public bool HasUnknown
        {
            get { return Unknown.Count > 0; }
        }

        /// <summary>多行明细，适合 richTextBox1</summary>
        public string Detail()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[参数注入] " + Message);
            if (Applied.Count > 0)
                sb.AppendLine("[参数注入] 已改值：" + JoinLimited(Applied, 20));
            if (Added.Count > 0)
                sb.AppendLine("[参数注入] 已新增：" + JoinLimited(Added, 20));
            if (Unknown.Count > 0)
                sb.AppendLine("[参数注入] 未识别（已跳过，继续执行）：" + JoinLimited(Unknown, 10));
            if (VerifyFailed.Count > 0)
                sb.AppendLine("[参数注入] 校验失败：" + JoinLimited(VerifyFailed, 10));
            if (Warnings.Count > 0)
                sb.AppendLine("[参数注入] 提示：" + JoinLimited(Warnings, 5));
            return sb.ToString();
        }


        private static string JoinLimited(List<string> items, int max)
        {
            if (items.Count <= max)
                return string.Join(", ", items.ToArray());

            string[] head = items.GetRange(0, max).ToArray();
            return string.Join(", ", head) + " … 等 " + items.Count + " 项";
        }
    }

    /// <summary>
    /// XML注入插件，注入插件里的 InParams.XXX 
    ///
    /// 原理：InParams 是只读属性，每次读都按 Code 去 BaseParams._InConfig 里查表。
    ///       所以注入点只有一个 —— 换掉 _InConfig 指向的 TemplateConfig。
    /// </summary>
    public static class ConfigInjector
    {
        private static readonly BindingFlags StaticAll =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        // 按插件 DLL 路径缓存 InParams 的属性表，避免每次注入都反射一遍
        private static readonly Dictionary<string, Dictionary<string, PropertyInfo>> _propCache =
            new Dictionary<string, Dictionary<string, PropertyInfo>>(StringComparer.OrdinalIgnoreCase);

        public static InjectResult Inject(XmlConfigServer config, string pluginDllPath, string configCode = null)
        {
            InjectResult r = new InjectResult();

            if (string.IsNullOrWhiteSpace(pluginDllPath))
                throw new ArgumentException("pluginDllPath 不能为空。");

            // ① 定 ConfigCode：必须是 Config\Setting\ 下的文件名（不含 .config）
            r.ConfigCode = string.IsNullOrWhiteSpace(configCode)
                ? ResolveConfigCode(pluginDllPath)
                : configCode;

            if (string.IsNullOrWhiteSpace(r.ConfigCode))
                throw new InvalidOperationException(
                    "无法为该插件找到可用的基准配置。已尝试：完整文件名、文件名第一段、所在文件夹名、DefaultMES。\r\n" +
                    "请确认 D:\\EYAOI\\MesCustom\\Bin\\Config\\Setting\\ 下存在对应 .config 文件。\r\n" +
                    "插件：" + pluginDllPath);

            // ② 取基准配置：必须走插件自己的 LoadTemlateConfig，
            //    这样 JudgeResultSetting / DefectTypeSetting / Templates 才是完整的
            TemplateConfig cfg;
            try
            {
                cfg = DevelopService.LoadTemlateConfig(r.ConfigCode);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "加载配置 " + r.ConfigCode + " 时插件内部抛异常：" + ex.Message, ex);
            }

            if (!IsUsableConfig(cfg))
            {
                int count = (cfg == null || cfg.TemplateParamSettings == null)
                    ? -1
                    : cfg.TemplateParamSettings.Count;

                throw new InvalidOperationException(
                    "基准配置 " + r.ConfigCode + " 无效（TemplateParamSettings=" + count + " 项，预期远大于 1）。\r\n" +
                    "插件对不存在的 ConfigCode 不会返回 null，而是返回只含 EnCode 一项的空壳。\r\n" +
                    "请确认文件存在：D:\\EYAOI\\MesCustom\\Bin\\Config\\Setting\\" + r.ConfigCode + ".config");
            }

            List<TemplateParamSetting> list = cfg.TemplateParamSettings;
            r.TemplateParamCount = list.Count;

            // ③ 用外部 XML 的值覆盖
            Dictionary<string, PropertyInfo> props = GetPropTable(pluginDllPath);

            if (config != null)
            {
                foreach (XmlConfigEntry e in config.Entries)
                {
                    if (e == null || string.IsNullOrWhiteSpace(e.Code))
                        continue;

                    TemplateParamSetting hit = list.Find(s =>
                        string.Equals(s.Code, e.Code, StringComparison.OrdinalIgnoreCase));

                    if (hit != null)
                    {
                        if (!string.Equals(hit.Value, e.Value, StringComparison.Ordinal) &&
                            hit.Value != null && hit.Value.Trim() == e.Value)
                        {
                            r.Warnings.Add(e.Code + "：XML 值含首尾空格，XmlConfigServer 已 Trim（插件原值 [" +
                                           hit.Value + "]）");
                        }

                        if (!string.Equals(hit.Value, e.Value, StringComparison.Ordinal))
                        {
                            // 用插件自己的 SetValue 改，保持与插件写配置行为一致
                            DevelopService.SetValue(list, hit.Code, null, e.Value);
                            r.Applied.Add(e.Code + "=" + e.Value);
                        }
                    }
                    else if (props.ContainsKey(e.Code))
                    {
                        // 基准配置缺这一项，但插件确实有这个参数 → 补一条
                        list.Add(new TemplateParamSetting
                        {
                            Code = e.Code,
                            Name = string.IsNullOrEmpty(e.Name) ? e.Code : e.Name,
                            Value = e.Value,
                            IsUserEdit = true
                        });
                        r.Added.Add(e.Code + "=" + e.Value);
                    }
                    else
                    {
                        // 插件参数表里也没有 → 拼错，或模板过时（如 ExportDataPathRule-1）
                        r.Unknown.Add(e.Code);
                    }
                }
            }

            // ③.5 全都没匹配上：典型的 ConfigPath 指错机型。
            //     这种情况 Success 仍为 true 但实际一个值都没注入，必须显式提示，否则会误判成功。
            if (r.Applied.Count == 0 && r.Added.Count == 0 && r.Unknown.Count > 0)
            {
                r.Warnings.Add("XML 里没有任何参数匹配上基准配置 " + r.ConfigCode +
                    "，可能是 config.ini 的 ConfigPath 指向了别的机型的配置。这些项按未识别处理，不影响执行。");
            }

            // ④ 写回唯一注入点
            FieldInfo fInConfig = FindInConfigField();
            if (fInConfig == null)
                throw new MissingFieldException("BaseParams 中找不到存储 TemplateConfig 的静态字段（预期名 _InConfig）。");

            fInConfig.SetValue(null, cfg);

            // ⑤ 回读校验：InParams 读回来的是不是我们写进去的值
            Verify(props, r);

            r.Success = r.VerifyFailed.Count == 0;
            return r;
        }

        // ---------------- 内部 ----------------

        /// <summary>
        /// 定位 BaseParams 里存 TemplateConfig 的静态字段。
        /// 实测字段名就是 _InConfig（private static，非 readonly），
        /// 但保留"按类型兜底"以防插件换版本改名。
        /// </summary>
        private static FieldInfo FindInConfigField()
        {
            FieldInfo f = typeof(BaseParams).GetField("_InConfig", StaticAll);
            if (f != null) return f;

            foreach (FieldInfo x in typeof(BaseParams).GetFields(StaticAll))
            {
                if (x.FieldType == typeof(TemplateConfig))
                    return x;
            }
            return null;
        }

        /// <summary>
        /// 推导 ConfigCode。插件规则：Path.Combine(Config\Setting 目录, ConfigCode + ".config")
        /// 候选顺序：完整文件名 → 第一段 → 所在文件夹名 → DefaultMES。
        ///
        /// 注意：LoadTemlateConfig 对不存在的 Code **不返回 null**，而是返回一个
        /// 只有 EnCode 一项的空壳配置（实测 DefaultMES.SCR / NOT_EXIST / "" 都是 count=1）。
        /// 所以判据必须是"参数数量"，不能只判 null。
        /// </summary>
        private static string ResolveConfigCode(string pluginDllPath)
        {
            if (string.IsNullOrWhiteSpace(pluginDllPath))
                return null;

            string noExt = Path.GetFileNameWithoutExtension(pluginDllPath);
            if (string.IsNullOrEmpty(noExt))
                return null;

            string[] parts = noExt.Split('.');
            string folder = Path.GetFileName(Path.GetDirectoryName(pluginDllPath));

            List<string> candidates = new List<string>();
            candidates.Add(noExt);
            if (parts.Length > 0) candidates.Add(parts[0]);
            if (!string.IsNullOrEmpty(folder)) candidates.Add(folder);
            candidates.Add("DefaultMES");

            foreach (string c in candidates)
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                try
                {
                    if (IsUsableConfig(DevelopService.LoadTemlateConfig(c)))
                        return c;
                }
                catch
                {
                    // 插件内部抛异常等同于"这个 Code 不可用"，继续试下一个
                }
            }
            return null;
        }

        /// <summary>
        /// 判断配置是否真的可用。
        /// 实测空壳特征：TemplateParamSettings.Count == 1 且唯一项是 EnCode
        /// （DefaultMES.SCR / NOT_EXIST_XYZ / 空串都会命中）。
        /// </summary>
        private static bool IsUsableConfig(TemplateConfig cfg)
        {
            if (cfg == null || cfg.TemplateParamSettings == null)
                return false;
            if (cfg.TemplateParamSettings.Count <= 1)
                return false;

            return true;
        }

        /// <summary>取插件 InParams 的属性表（Code → PropertyInfo），按路径缓存。</summary>
        private static Dictionary<string, PropertyInfo> GetPropTable(string pluginDllPath)
        {
            string key = Path.GetFullPath(pluginDllPath);

            lock (_propCache)
            {
                if (_propCache.ContainsKey(key))
                    return _propCache[key];

                Dictionary<string, PropertyInfo> table =
                    new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);

                Type inParams = LoadInParams(key);
                if (inParams != null)
                {
                    foreach (PropertyInfo p in inParams.GetProperties(StaticAll))
                    {
                        if (p.GetMethod == null || !p.GetMethod.IsStatic) continue;
                        if (p.GetIndexParameters().Length > 0) continue;
                        table[p.Name] = p;
                    }
                }

                _propCache[key] = table;
                return table;
            }
        }

        private static Type LoadInParams(string pluginDllPath)
        {
            if (string.IsNullOrWhiteSpace(pluginDllPath) || !File.Exists(pluginDllPath))
                return null;

            Assembly asm = Assembly.LoadFrom(pluginDllPath);

            Type t = asm.GetType("SpecialCaseRun.InParams", false, false);
            if (t != null) return t;

            // 兜底：命名空间不同的情况，按"名字叫 InParams 且继承 BaseParams"找
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                List<Type> ok = new List<Type>();
                foreach (Type x in ex.Types)
                    if (x != null) ok.Add(x);
                types = ok.ToArray();
            }

            foreach (Type x in types)
                if (x.Name == "InParams" && typeof(BaseParams).IsAssignableFrom(x))
                    return x;

            return null;
        }

        private static void Verify(Dictionary<string, PropertyInfo> props, InjectResult r)
        {
            if (props.Count == 0)
            {
                r.Warnings.Add("未找到 SpecialCaseRun.InParams（或插件未加载），已跳过回读校验。");
                return;
            }

            List<string> items = new List<string>(r.Applied);
            items.AddRange(r.Added);

            foreach (string item in items)
            {
                int eq = item.IndexOf('=');
                if (eq <= 0) continue;

                string code = item.Substring(0, eq);
                string expected = item.Substring(eq + 1);

                PropertyInfo p;
                if (!props.TryGetValue(code, out p))
                {
                    r.VerifyFailed.Add(code + "：InParams 中无此属性");
                    continue;
                }

                object actual = p.GetValue(null, null);
                if (!string.Equals(Convert.ToString(actual), expected, StringComparison.Ordinal))
                    r.VerifyFailed.Add(code + "：期望 [" + expected + "] 实际 [" + actual + "]");
            }
        }
    }
}