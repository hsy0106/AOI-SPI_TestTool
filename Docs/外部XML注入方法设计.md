# 外部 XML 注入方法设计（`ConfigInjector.Inject`）

> 目标：写一个和 `DllMethodInvoker.Invoke` 对称的静态方法，把**外部 XML 的值**注入进插件，
> 注入之后 `InParams.Xxx` 和 `GetBadMark(...)` 用的就是你 XML 里的值。

> **状态：已实现。** 落地代码在 `AOITest\AOI\ConfigInjector.cs`，
> 改动清单与验证步骤见 `Docs\代码修改步骤.md`。

> **⚠️ 本文档有一处结论需要更正**：`DevelopService.LoadTemlateConfig(code)` 对**不存在的
> ConfigCode 不会返回 null**，而是返回一个只含 `EnCode` 一项的空壳 `TemplateConfig`
> （且 `JudgeResultSetting` / `DefectTypeSetting` / `Templates` 都是非 null 的默认对象）。
> 实测 `DefaultMES.SCR`、`NOT_EXIST_XYZ_123`、`""` 的 `TemplateParamSettings.Count` 都是 1。
> 所以判断"配置是否可用"必须用 `Count > 1`，不能只判 null。落地代码用的是 `IsUsableConfig()`。

---

## 一、结论

**能，而且注入点只有一个。** 但你注入的不是 `InParams`，是 `BaseParams._InConfig` 指向的
`TemplateConfig` 对象。`InParams` 是**只读查询视图**，它每次读都去 `_InConfig` 里按名查表，
所以只要换掉 `_InConfig` 或者改掉里面某个 `Value`，`InParams` 立刻跟着变。

实测证据（PowerShell 直接跑，同一进程内）：

```
--- 1. load base config ---
  Code=DefaultMES  Params=65  JudgeRes=True  DefectType=True
--- 2. InParams from plugin dll ---
  type = SpecialCaseRun.InParams  base = DomainAOI.DataExport.BaseParams
  before inject = [MI6071]
--- 4. change Value in memory, read again ---
  after SetValue = [FROM-PS-TEST]        ← 关键：改 Value 立即生效
--- 5. clear _InConfig, expect reload from file ---
  after clear = [MI6071]                 ← 清空后惰性重读文件，证明链路
--- 6. ParamSetting attribute ---
  attr type = DomainAOI.DataExport.ParamSettingAttribute
    Code = MachineName   Name = 设备名称   DefaultValue = MI6071   IsUserEdit = True
--- 7. attribute coverage on InParams ---
  static props = 64  with attr = 64      ← 64 个属性全部带特性
```

三条关键事实：

1. `SetValue(list, "MachineName", null, "FROM-PS-TEST")` 之后 `InParams.MachineName` 立刻变成新值——
   **不需要重新 `LoadTemlateConfig`，也不需要 `RefreshConfig`**。
2. 把 `_InConfig` 置 null 再读，又回到文件里的 `MI6071`——证明 `get_InConfig` 是惰性重载，
   只要 `_InConfig` 非空就不再碰文件。
3. `InParams` 的 **64 个静态属性全部带 `ParamSetting` 特性**，且 `Code` == 属性名。
   这意味着**外部 XML 的合法 Code 集合是可枚举、可校验的**，不依赖任何配置文件。

## 二、方法签名（与 `DllMethodInvoker.Invoke` 对称）

`DllMethodInvoker.Invoke` 的形状是：**给 4 个入参 → 返回方法返回值**。
注入器对应 **给 2 个入参 → 返回注入结果**：

```csharp
public static class ConfigInjector
{
    /// <summary>
    /// 把外部 XML 的值注入插件，之后 InParams.* 与插件方法都使用这些值。
    /// </summary>
    /// <param name="externalXmlPath">外部 XML 路径；为 null 时只注入基准配置</param>
    /// <param name="options">基准配置 Code、未知参数如何处置等</param>
    public static InjectResult Inject(
        string externalXmlPath,
        InjectOptions options = null);
}
```

```csharp
public sealed class InjectOptions
{
    public string PluginDllPath;      // DefaultMES.SCR.dll 完整路径（必填）
    public string SdkDir;             // DomainAOI.DataExport.dll 所在目录（可空，自动探测）
    public string BaseConfigCode;     // 基准配置名，默认 "DefaultMES"
    public bool AllowNewParam;        // XML 里有基准没有的 Code 时，是否 Add，默认 true
    public bool VerifyAfterInject;    // 注入后回读校验，默认 true
}
```

```csharp
public sealed class InjectResult
{
    public bool   Success;            // 只有 VerifyFailed==0 才为 true
    public string BaseSource;         // "File" | "AttributeFallback"
    public int    TemplateParamCount;
    public List<string> Applied;      // 值被改写的 Code
    public List<string> Added;        // 外部新增的 Code
    public List<string> UnknownInXml; // XML 里有、特性表里没有 → 拼错或过时
    public List<string> VerifyFailed; // 注入后 InParams 回读不一致（最重要）
    public List<string> Warnings;
    public string Message;            // 一行摘要，直接显示在界面上
}
```

**为什么返回结构体而不是 bool**：注入最常见的失败不是抛异常，而是"没报错但值没变"
（配置名不对、注错程序集、被 `RefreshConfig` 冲掉）。把 `VerifyFailed` 做成返回值的一部分，
才能让界面直接说"注入了 12 项，但有 3 项回读不一致"，而不是让你去猜。

## 三、六步流程

```
① 解析程序集      PluginDllPath → 插件 asm → SpecialCaseRun.InParams
                  SdkDir       → 基类 asm → DomainAOI.DataExport.BaseParams
② 取基准配置      DevelopService.LoadTemlateConfig(BaseConfigCode)
                  失败 → ③兜底
③ 兜底构建        反射扫 InParams 全部 ParamSetting 特性
                  → 新建 TemplateConfig + 64 项 (Code, DefaultValue)
④ 套用外部 XML    Code 命中 → SetValue(改 Value)
                  未命中 + AllowNewParam → new TemplateParamSetting 并 Add
⑤ 写回静态字段    BaseParams._InConfig = cfg      ← 唯一注入点
⑥ 回读校验        InParams.Xxx 逐个比对 → VerifyFailed
```

### 完整可编译代码（反射版，零项目引用）

放反射版有两个硬理由，不是图省事：

1. `Plugin_Tool` / `AOITest` 里没有任何对 `DomainAOI.DataExport` 的**编译期**引用时也能用；
2. AOI 和 SPI 的 `SinicTek.SDK` 同名不同构建，编译期绑定死一个，另一个必然版本错配。
   全字符串类型名 + 运行时解析，一套代码两边通用。

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

public static class ConfigInjector
{
    private const BindingFlags AllStatic =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    public static InjectResult Inject(string externalXmlPath, InjectOptions options = null)
    {
        if (options == null) options = new InjectOptions();
        if (string.IsNullOrEmpty(options.PluginDllPath))
            throw new ArgumentException("PluginDllPath 不能为空。");
        if (string.IsNullOrEmpty(options.BaseConfigCode))
            options.BaseConfigCode = "DefaultMES";

        var result = new InjectResult
        {
            Applied = new List<string>(),
            Added = new List<string>(),
            UnknownInXml = new List<string>(),
            VerifyFailed = new List<string>(),
            Warnings = new List<string>()
        };

        // ① 解析程序集
        Assembly pluginAsm = Assembly.LoadFrom(Path.GetFullPath(options.PluginDllPath));
        Assembly sdkAsm = ResolveSdkAssembly(pluginAsm, options);

        Type inParamsType = FindType(pluginAsm, "InParams");
        Type baseParamsType = sdkAsm.GetType("DomainAOI.DataExport.BaseParams", false);
        Type configType = sdkAsm.GetType("DomainAOI.DataExport.TemplateConfig", false);
        Type settingType = sdkAsm.GetType("DomainAOI.DataExport.TemplateParamSetting", false);
        Type devType = sdkAsm.GetType("DomainAOI.DataExport.DevelopService", false);

        if (inParamsType == null) throw new TypeLoadException("插件中找不到 InParams。");
        if (baseParamsType == null || configType == null || settingType == null || devType == null)
            throw new TypeLoadException("SDK 中缺少 BaseParams/TemplateConfig/TemplateParamSetting/DevelopService。");

        // 属性表：Code -> PropertyInfo（64 个，全部带 ParamSetting 特性）
        var props = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (PropertyInfo p in inParamsType.GetProperties(AllStatic))
        {
            if (p.GetMethod == null || !p.GetMethod.IsStatic) continue;
            if (p.GetIndexParameters().Length > 0) continue;
            props[p.Name] = p;
        }

        // ② 取基准配置
        MethodInfo loadConfig = devType.GetMethod("LoadTemlateConfig", AllStatic);
        object cfg;
        try
        {
            cfg = loadConfig.Invoke(null, new object[] { options.BaseConfigCode });
        }
        catch (TargetInvocationException ex)
        {
            // 插件内部把异常吞成一句"异常"，这里退到特性兜底，比直接崩更好排查
            cfg = null;
            result.Warnings.Add("LoadTemlateConfig 抛异常：" +
                (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
        }

        // ③ 兜底：按 ParamSetting 特性构建
        if (cfg == null)
        {
            cfg = BuildFromAttributes(configType, settingType, props);
            result.BaseSource = "AttributeFallback";
            result.Warnings.Add("基准配置 " + options.BaseConfigCode +
                " 加载失败，已用 ParamSetting 特性兜底；JudgeResultSetting / DefectTypeSetting / Templates 为空，" +
                "非 InParams 相关接口可能抛空引用。");
        }
        else
        {
            result.BaseSource = "File";
        }

        IList settings = GetSettings(configType, cfg);
        result.TemplateParamCount = settings.Count;

        // ④ 套用外部 XML
        if (!string.IsNullOrEmpty(externalXmlPath))
        {
            if (!File.Exists(externalXmlPath))
                throw new FileNotFoundException("找不到外部 XML。", externalXmlPath);

            MethodInfo setValue = devType.GetMethod("SetValue", AllStatic);

            foreach (KeyValuePair<string, string> kv in ReadOverrides(externalXmlPath))
            {
                object hit = FindSetting(settingType, settings, kv.Key);

                if (hit != null)
                {
                    PropertyInfo mp;
                    if (props.TryGetValue(kv.Key, out mp) &&
                        !string.Equals(mp.Name, kv.Key, StringComparison.Ordinal))
                        result.Warnings.Add("Code 大小写与属性名不一致：" + kv.Key + " / " + mp.Name +
                            "（插件 GetValue 可能区分大小写，建议改成 " + mp.Name + "）");

                    // 用插件自己的 SetValue，而不是直接给属性赋值 —— 保持与插件写配置行为一致
                    setValue.Invoke(null, new object[] { settings, kv.Key, null, kv.Value });
                    result.Applied.Add(kv.Key + "=" + kv.Value);
                }
                else if (!props.ContainsKey(kv.Key))
                {
                    result.UnknownInXml.Add(kv.Key);   // 特性表里都没有，肯定是拼错
                }
                else if (options.AllowNewParam)
                {
                    object ns = Activator.CreateInstance(settingType);
                    settingType.GetProperty("Code").SetValue(ns, kv.Key, null);
                    settingType.GetProperty("Name").SetValue(ns, kv.Key, null);
                    settingType.GetProperty("Value").SetValue(ns, kv.Value, null);
                    settingType.GetProperty("IsUserEdit").SetValue(ns, true, null);
                    settings.Add(ns);
                    result.Added.Add(kv.Key + "=" + kv.Value);
                }
                else
                {
                    result.Warnings.Add("基准配置缺少参数 " + kv.Key + "，已跳过（AllowNewParam=false）。");
                }
            }
        }

        // ⑤ 写回唯一注入点
        FieldInfo fInConfig = baseParamsType.GetField("_InConfig", AllStatic);
        if (fInConfig == null) throw new MissingFieldException("BaseParams 中找不到 _InConfig。");
        fInConfig.SetValue(null, cfg);

        // ⑥ 回读校验（改值的和新增的都要验）
        if (options.VerifyAfterInject)
        {
            List<string> toVerify = new List<string>(result.Applied);
            toVerify.AddRange(result.Added);

            foreach (string item in toVerify)
            {
                int eq = item.IndexOf('=');
                string code = item.Substring(0, eq);
                string expected = item.Substring(eq + 1);

                PropertyInfo prop;
                if (!props.TryGetValue(code, out prop)) continue;

                object actual = prop.GetValue(null, null);
                if (!string.Equals(Convert.ToString(actual), expected, StringComparison.Ordinal))
                    result.VerifyFailed.Add(code + "：期望 [" + expected + "] 实际 [" + actual + "]");
            }
        }

        result.Success = result.VerifyFailed.Count == 0;
        result.Message = string.Format(
            "基准={0}({1}) 参数={2} 改值={3} 新增={4} 未识别={5} 校验失败={6}",
            options.BaseConfigCode, result.BaseSource, result.TemplateParamCount,
            result.Applied.Count, result.Added.Count,
            result.UnknownInXml.Count, result.VerifyFailed.Count);

        return result;
    }

    // ---- 以下是内部步骤，逐条对应上面的 ①～③ ----

    private static Assembly ResolveSdkAssembly(Assembly pluginAsm, InjectOptions options)
    {
        const string name = "DomainAOI.DataExport";

        // 1) 已加载的同名程序集直接复用 —— 保证注入用的类型和插件读的类型是同一份
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            if (a.GetName().Name == name) return a;

        // 2) 按目录探测：先 SdkDir，再插件目录，再应用程序目录
        List<string> dirs = new List<string>();
        if (!string.IsNullOrEmpty(options.SdkDir)) dirs.Add(options.SdkDir);
        dirs.Add(Path.GetDirectoryName(Path.GetFullPath(options.PluginDllPath)));
        dirs.Add(AppDomain.CurrentDomain.BaseDirectory);

        foreach (string d in dirs)
        {
            string p = Path.Combine(d, name + ".dll");
            if (!File.Exists(p)) continue;
            try { return Assembly.LoadFrom(p); }
            catch (BadImageFormatException) { }
        }

        // 3) 最后交给 AppDomain 的解析策略
        try { return Assembly.Load(name); }
        catch (FileNotFoundException) { }

        throw new FileNotFoundException(
            "找不到 " + name + ".dll，请指定 SdkDir，或确认插件目录下依赖齐全。");
    }

    private static Type FindType(Assembly asm, string shortName)
    {
        Type[] types;
        try
        {
            types = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // 依赖缺失时 GetTypes 只返回一部分，过滤 null 继续找
            List<Type> ok = new List<Type>();
            foreach (Type t in ex.Types)
                if (t != null) ok.Add(t);
            types = ok.ToArray();
        }
        foreach (Type t in types)
            if (t.Name == shortName) return t;
        return null;
    }

    private static object BuildFromAttributes(Type configType, Type settingType,
        Dictionary<string, PropertyInfo> props)
    {
        object cfg = Activator.CreateInstance(configType);
        IList list = GetSettings(configType, cfg);

        PropertyInfo codeProp = settingType.GetProperty("Code");
        PropertyInfo nameProp = settingType.GetProperty("Name");
        PropertyInfo valueProp = settingType.GetProperty("Value");
        PropertyInfo editProp = settingType.GetProperty("IsUserEdit");

        foreach (KeyValuePair<string, PropertyInfo> kv in props)
        {
            object attr = null;
            Attribute[] attrs = Attribute.GetCustomAttributes(kv.Value, false);
            foreach (Attribute a in attrs)
                if (a.GetType().Name == "ParamSettingAttribute") { attr = a; break; }
            if (attr == null) continue;

            Type at = attr.GetType();
            object code = at.GetProperty("Code").GetValue(attr, null);
            object nm = at.GetProperty("Name").GetValue(attr, null);
            object dv = at.GetProperty("DefaultValue").GetValue(attr, null);
            object iu = at.GetProperty("IsUserEdit").GetValue(attr, null);

            object s = Activator.CreateInstance(settingType);
            codeProp.SetValue(s, code != null && code.ToString() != "" ? code : kv.Key, null);
            nameProp.SetValue(s, nm, null);
            valueProp.SetValue(s, dv, null);
            editProp.SetValue(s, iu, null);
            list.Add(s);
        }
        return cfg;
    }

    private static IList GetSettings(Type configType, object cfg)
    {
        PropertyInfo prop = configType.GetProperty("TemplateParamSettings");
        object raw = prop.GetValue(cfg, null);
        if (raw == null)
        {
            // PropertyType 就是 List<TemplateParamSetting>，直接实例化，不要从泛型参数反推
            raw = Activator.CreateInstance(prop.PropertyType);
            prop.SetValue(cfg, raw, null);
        }
        // 必须返回非泛型 IList：List<T> 不实现 IList<object>（IList<T> 不是协变的），
        // 强转 (IList<object>) 会抛 InvalidCastException。
        return (IList)raw;
    }

    private static object FindSetting(Type settingType, IList settings, string code)
    {
        PropertyInfo codeProp = settingType.GetProperty("Code");
        foreach (object s in settings)
        {
            object v = codeProp.GetValue(s, null);
            if (v != null && string.Equals(v.ToString(), code, StringComparison.OrdinalIgnoreCase))
                return s;
        }
        return null;
    }

    /// <summary>
    /// 读外部 XML。三种写法都认，取 Code + 值：
    ///   1) 完整模板：&lt;TemplateParam&gt;&lt;Setting Code="X"&gt;值&lt;/Setting&gt;&lt;/TemplateParam&gt;
    ///   2) 扁平覆盖：&lt;Params&gt;&lt;Item Code="X"&gt;值&lt;/Item&gt;&lt;/Params&gt;
    ///   3) 极简：    &lt;Params&gt;&lt;MachineName&gt;值&lt;/MachineName&gt;&lt;/Params&gt;
    /// 判定规则：有 Code 属性就用 Code，否则用节点名。同名取最后一个。
    /// </summary>
    private static List<KeyValuePair<string, string>> ReadOverrides(string path)
    {
        var list = new List<KeyValuePair<string, string>>();
        var doc = new XmlDocument();
        doc.Load(path);

        foreach (XmlNode n in doc.SelectNodes("//*"))
        {
            if (n.NodeType != XmlNodeType.Element) continue;
            if (n.Attributes == null) continue;

            XmlAttribute codeAttr = n.Attributes["Code"];
            string code = codeAttr != null ? codeAttr.Value : n.Name;
            if (string.IsNullOrEmpty(code)) continue;

            // 只收叶子节点，避免把 <TemplateParam> 这种容器当成参数
            bool hasElementChild = false;
            foreach (XmlNode c in n.ChildNodes)
                if (c.NodeType == XmlNodeType.Element) { hasElementChild = true; break; }
            if (hasElementChild) continue;

            if (codeAttr == null && n.Name == "root") continue;

            string value = n.InnerText;
            if (value == null) value = "";

            for (int i = list.Count - 1; i >= 0; i--)
                if (string.Equals(list[i].Key, code, StringComparison.OrdinalIgnoreCase))
                    list.RemoveAt(i);

            list.Add(new KeyValuePair<string, string>(code, value));
        }
        return list;
    }
}
```

## 四、外部 XML 长什么样

### 写法 1：完整模板（直接复制现场 `DefaultMES.config` 改值）

```xml
<root>
  <TemplateParam>
    <Setting Code="MachineName" Name="设备名称" IsUserEdit="1">TEST-1234</Setting>
    <Setting Code="Operator"    Name="操作者"   IsUserEdit="1">zhangsan</Setting>
    <Setting Code="Side1"       Name="板面1"    IsUserEdit="1">A</Setting>
    <Setting Code="InterfaceUrl" Name="接口地址" IsUserEdit="1">http://127.0.0.1:7070/</Setting>
  </TemplateParam>
</root>
```

**注意**：写作法 1 时，**只需列你要改的那几行**，不必 65 项全抄。基准配置负责补全其余。

### 写法 2：扁平覆盖表

```xml
<Params>
  <Item Code="MachineName">TEST-1234</Item>
  <Item Code="Operator">zhangsan</Item>
</Params>
```

### 写法 3：极简（节点名即 Code）

```xml
<Params>
  <MachineName>TEST-1234</MachineName>
  <Operator>zhangsan</Operator>
</Params>
```

推荐写法 2：Code 显式、可读、能被 XSD 校验，也不会因为节点名笔误而静默变成"未知参数"。

## 五、和 `DllMethodInvoker.Invoke` 怎么配合

两个方法各管一段，调用顺序固定：

```csharp
// 第 1 步：注入配置（改的是插件静态状态）
InjectResult r = ConfigInjector.Inject(
    @"E:\WorkFile\Code\AOITestTool\TestData\aoi_overrides.xml",
    new InjectOptions
    {
        PluginDllPath   = @"D:\EYAOI\MesCustom\Bin\CustomDLL\DefaultMES\DefaultMES.SCR.dll",
        BaseConfigCode  = "DefaultMES",
        AllowNewParam   = true,
        VerifyAfterInject = true
    });

if (!r.Success) { txtLog.Text = r.Message + "\r\n" + string.Join("\r\n", r.VerifyFailed); return; }

// 第 2 步：造运行时对象（BarCodeTree，上次那张表）
object tree = BuildBarCodeTree(...);

// 第 3 步：照旧调用
object ret = DllMethodInvoker.Invoke(
    @"D:\EYAOI\MesCustom\Bin\CustomDLL\DefaultMES\DefaultMES.SCR.dll",
    "SpecialCaseRun.AOI", "GetBadMark", new object[] { tree });
```

**顺序不能反**：`GetBadMark` 内部读的是静态 `InParams`，注入必须在调用之前完成。

如果想把两步合成一次调用，再加一层门面即可，但**不建议**——注入结果你要单独看，
混进 `Invoke` 的返回值里就丢了。

## 六、八个必须踩对的坑

1. **`RefreshConfig()` 会冲掉注入。**
   `RefreshConfig()` 的实现是 `_ConfigName=""; _InConfig=LoadTemlateConfig(ConfigName)`，
   等于按文件名重新读盘。注入后任何触发 `RefreshConfig` 的动作都会让值回到文件里的值。
   Google 式的"注入了但读到旧值"，九成是这个原因。

2. **不要用 `App.InConfig` 或 `App.InConfigName`。**
   - `App.InConfig` 有 public setter，但 `BaseParams` 的 getter **完全不读它**，设了没用（假成功）；
   - `App.InConfigName` 的 setter 会顺带执行 `MesSettingService.SaveSettings`，**把当前激活配置写进现场设置**，
     属于污染行为，只读诊断时禁用。

3. **`InParams` 在插件 DLL 里，`BaseParams` 在 SDK 里。**
   `SpecialCaseRun.InParams : DomainAOI.DataExport.BaseParams`。
   `_InConfig` 是**基类的静态字段**，所以写它一次，所有继承它的 `InParams` 都生效。
   但如果插件自己定义了同名字段遮蔽，注入就会落空——`VerifyAfterInject` 能立刻发现。

4. **AOI 和 SPI 不能混着注。**
   两套 `SinicTek.SDK` 同名同版本、成员不同，CLR 先加载者胜。注入器里 `DomainAOI.DataExport`
   必须和当前插件由**同一份**程序集提供，否则注进去的类型和插件读的类型不是一个，
   表现为"注入成功、回读不一致"。切机型必须重启进程。

5. **bool 类参数只认字面量 `1`。**
   插件判定是 `GetValue(...) == "1"`。XML 写 `true` / `True` / `是` 一律变成 false，且**不报错**，
   表现为方法首行 `if(!IsEnableBarcodeCheck) return;` 直接静默返回。注入器不管这个语义，
   但你的外部 XML 里必须写 `1`。

6. **基准配置有 65 项，`InParams` 只有 64 个属性，而且其中 3 项对不上。**
   实测 diff（`DefaultMES.config` ↔ `SpecialCaseRun.InParams`）：

   | 差异 | Code |
   |---|---|
   | XML 有、属性无 | `EnCode`（值是 `UTF-8`，这是**模板编码**，不是客户参数）、`ExportDataPathRule-1`、`ExportDataPathRule-2` |
   | 属性有、XML 无 | `ExportDataPathRule1`、`ExportDataPathRule2`（少了横杠） |

   即 65 = 64 个属性 + `EnCode`，而 `ExportDataPathRule` 那两项因为命名带横杠，**永远取不到值**，
   导出路径规则只能退回默认。注入器遇到这种 XML 项会进 `UnknownInXml`，正好复现这个模板 bug。
   另外 `GetValue` 取**第一个**命中的 Code，重复 Code 时要断言出现次数 == 1。

7. **`LoadTemlateConfig` 只读硬编码目录。**
   `Path.Combine(CommonSystemConfig.MesCustomConfigBaseDirPath, ACode + ".config")`，
   即永远读 `D:\EYAOI\MesCustom\Bin\Config\Setting\`。**没有参数能指定别的路径。**
   所以"用外部 XML 换掉整份模板"只能走两条路：改内存（本文方案），
   或者把外部 XML 复制成该目录下的**新文件**（如 `AOITEST_TMP.config`）再 `LoadTemlateConfig("AOITEST_TMP")`。
   后者会写现场目录，只在你需要完整 `JudgeResultSetting` 覆盖时才用。

8. **现场 XML 里存在首尾空格。**
   实测 `UserName` 的值是 `"UserName  "`（尾部两个空格），`MachineID` 是 `"MachineID"`。
   插件 `GetValue` 不做 Trim，这些空格会原样进请求 body。注入器不替你改（改了就不是"真实复现"了），
   但应在 `Warnings` 里标出来，否则排查对方 MES 报"参数非法"时永远想到不这一层。

## 七、落地清单

| 步 | 内容 | 验证 |
|---|---|---|
| 1 | 新建 `ConfigInjector.cs`（反射版，无需项目引用） | 编译通过，无新 Reference |
| 2 | 加进 csproj 的 `<Compile Include>` | 项目里能看到该文件 |
| 3 | 手写一份 3 行 `aoi_overrides.xml`（MachineName/Operator/Side1） | 文件可被 XmlDocument 加载 |
| 4 | 界面加"注入配置"按钮，把 `InjectResult.Message` 打到日志 | 显示 `改值=3 校验失败=0` |
| 5 | 点现有 `GetBadMark` 按钮，看插件日志里的 `strJson` | body 里 `MachineName` == XML 里的值 |
| 6 | 故意把 XML 里 `MachineName` 改名成 `MachineNameX` | `UnknownInXml` 里有它，其余仍成功 |

第 5 步过了，就证明整条链路（XML → TemplateConfig → InParams → 请求实体）是通的。
