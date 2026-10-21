# AOITestTool

AOI / SPI 设备 **MES 接口联调与插件方法模拟触发工具**（.NET Framework 4.6.1 / WinForms）。

现场 AOI、SPI 设备通过客户定制插件 DLL（`DefaultMES.SCR.dll`、`SHENZHEN_TWSC.SPISCR.dll` 等）
与客户 MES 交互。接口一旦不通，只能在设备上盲调，插件内部还会把异常吞掉，表现为"点了没反应"。
本工具把这些插件加载到一个独立进程里，用界面按需触发插件方法，并把
**客户参数（XML）→ 插件 `InParams` → 请求实体 → HTTP Body** 这条链路完整暴露出来。

---

## 一、功能概览

| 能力 | 说明 |
|---|---|
| 插件方法调用 | 按 `程序集.类.方法` 反射调用，自动匹配参数签名，异常原样抛出 |
| 客户参数注入 | 把一份 XML 的参数值注入插件，让插件后续调用使用这份值 |
| IL 数据载入 | 读取 AOI 输出的 IL json，还原成 `BoardPCB` / `BarCodeTree` 运行时对象 |
| XML 配置浏览 | 以 `Code / Name / Value / IsUserEdit` 四列展示现场模板参数 |
| 调用日志 | 注入摘要、方法返回值（JSON）、异常堆栈，逐条输出到界面日志区 |

---

## 二、仓库结构

```
Main.sln
├─ AOITest/                 ← 主程序（WinForms），本工具的入口
│   ├─ Form1.cs             界面与按钮事件
│   ├─ AOI/
│   │   ├─ ConfigInjector.cs   XML 客户参数注入
│   │   ├─ IlJsonLoader.cs     IL json 解析（含流式写入重试）
│   │   └─ AOITest.cs          插件方法调用封装
│   ├─ Enum/AOIMethod.cs      可触发的方法清单
│   ├─ IL/IlPcbResult.cs      IL json 实体定义
│   └─ bin/Debug/Config/      运行配置（config.ini + 测试用 XML 副本）
├─ App/                      ← 通用服务层（XML 配置读取、日志、SQLite 等）
├─ Plugin_Tool/              ← 反射工具（DllMethodInvoker / DllTool）
├─ Plugin.Abstractions/      ← 插件契约（IEvent / PlcInteractive 等）
├─ Plugin_Test/              ← 测试流程插件
├─ Plugin_SECS/              ← SECS/HSMS 协议插件（Passive，默认 127.0.0.1:5000）
├─ Plugin_WebAPI/            ← WebAPI 插件
├─ Plugin_WebServer/         ← WebServer 插件（占位）
├─ Ref/                      ← 厂商 SDK 二进制引用（见下方说明，必须入库）
├─ Docs/                     ← 设计文档与踩坑记录
└─ packages/                 ← NuGet 包还原目录（不入库）
```

> **`Ref/` 为什么入库**：`DomainAOI.DataExport.dll`、`QuickMiddle.MES.SDK.dll`、`SinicTek.SDK.dll`、
> `Aspose.Cells.DLL` 等都是厂商随设备下发的二进制，没有 NuGet 来源。缺少任意一个都无法编译，
> 所以 `.gitignore` **不忽略** `Ref/*.dll`。

---

## 三、环境要求

- Visual Studio 2017 及以上（需勾选 **.NET Framework 4.6.1 开发工具**）
- 老式 csproj（`Compile` 逐文件列出）——**新增 .cs 文件必须手工加进 csproj**，否则不参与编译
- 首次打开先执行 NuGet 还原（依赖落在解决方案级 `packages\`）
- 部分插件路径在 SDK 里是**硬编码**的（如 `D:\EYAOI\MesCustom\Bin\Config\Setting`），
  因此完整联调需要本机存在 `D:\EYAOI` / `D:\EYSPI` 现场目录；纯界面操作不受影响

---

## 四、构建与运行

```bash
# 1. 还原 NuGet（或在 VS 里右键解决方案 → 还原 NuGet 包）
nuget restore Main.sln

# 2. 生成 AOITest
msbuild Main.sln /t:AOITest /p:Configuration=Debug

# 3. 运行
AOITest\bin\Debug\AOITest.exe
```

程序启动时读取 **`<程序目录>\Config\config.ini`**，所以必须从 `bin\Debug` 运行，
不能把 exe 单独拷走。

---

## 五、配置（`AOITest\bin\Debug\Config\config.ini`）

```ini
[Server]
ConfigPath = D:\EYAOI\MesCustom\Bin\Config\Setting\DefaultMES.config
ModelTpye   = AOI
AOIDllPath  = D:\EYAOI\MesCustom\Bin\CustomDLL
SPIDllPath  = D:\EYSPI\Bin\CustomDLL
```

| 键 | 作用 |
|---|---|
| `ConfigPath` | 界面上表格展示、并用于注入的 XML。可写**绝对路径**，也可写相对程序目录的路径（如 `Config\TestXml\aoi_test.config`）；写目录时自动取其中第一个 `.config` |
| `ModelTpye` | `AOI` / `SPI`，决定插件目录用哪个 |
| `AOIDllPath` / `SPIDllPath` | 插件 DLL 的搜索目录 |

⚠️ **`ModelTpye` 和 `ConfigPath` 必须属于同一机型**。AOI 模式配 SPI 的
`SHENZHEN_TWSC.config`，注入时会大量"未识别"（字段对不上），界面上看起来像注入失败。

⚠️ **改了 `config.ini` 要重启程序**——`Form1_Load` 才读取它。

---

## 六、客户参数注入是怎么工作的

插件里的 `InParams` 是**只读查询视图**，不是数据容器：

```csharp
// 插件 DLL 内部（约等价的写法）
public static string MachineName =>
    DevelopService.GetValue(BaseParams.InConfig.TemplateParamSettings, "MachineName");
```

64 个属性全部 `static` + 无 setter，每次读取都按名字去 `BaseParams._InConfig` 查表。
所以注入点只有一个：**替换 / 修改 `BaseParams._InConfig` 指向的 `TemplateConfig`**。

`ConfigInjector.Inject()` 做的事：

1. 定位插件依赖的 `DomainAOI.DataExport` 程序集
2. `DevelopService.LoadTemlateConfig(基准 ConfigCode)` 取一份完整基准配置
3. 按 `Code` 把外部 XML 的值写进 `TemplateParamSettings`（用插件自己的 `SetValue`）
4. 反射写回 `BaseParams._InConfig`
5. 回读校验：把 `InParams` 的取值和期望值逐个比对

返回值是 `InjectResult` 而非 `bool`——注入最常见的失败不是抛异常，而是
**"没报错，但值没进去"**，`VerifyFailed` 能把这类情况显式暴露出来。

### 五个必须踩对的点

| # | 坑 | 后果 |
|---|---|---|
| 1 | `ConfigCode` 必须等于 `Config\Setting\` 下的文件名（不含 `.config`） | 传错 → 参数全空 |
| 2 | `LoadTemlateConfig` 对**不存在的** Code **不返回 null**，而是返回只含 `EnCode` 一项的空壳 | 判空失效，注入落进残缺配置 |
| 3 | bool 型参数只认字面量 `"1"`；`true` / `是` 一律当 false | 方法首行 `if (!IsEnableXxx) return;` 静默返回 |
| 4 | `BaseParams.RefreshConfig()` 会重新读盘，把注入冲掉 | 注入了却读到旧值 |
| 5 | `App.InConfig` 有 setter 但没人读；`App.InConfigName` 会**写盘**污染现场设置 | 一个无效，一个有副作用 |

**两个都不能用**，只能操作 `BaseParams`。

### 验证注入是否真的生效

1. 启动程序，点注入按钮，日志应出现
   `[注入] 基准=DefaultMES 参数=65 改值=N 未识别=0 校验失败=0`
2. 点 `GetBadMark` / `BarCodeCheck`，看插件自己写出的日志里 `strJson` 的字段值
3. 若 `改值=0`：`ConfigPath` 指向的文件就是基准配置本身——等于"用现场值覆盖现场值"，
   验证不出效果。改用 `Config\TestXml\aoi_test.config` 这类副本再试（现场文件不要改）

---

## 七、日志与现场目录

| 位置 | 内容 |
|---|---|
| 界面日志区 | 注入摘要、方法返回值 JSON、异常 |
| `D:\EYAOI\DataExport\DefaultMES.txt` | AOI 插件自己记录的 `Url` / `strJson` / `retHtml` |
| `D:\EYSPI\Bin\SPILogs\<线体>\<方法> yyyyMMdd.log` | SPI 插件日志 |

**插件日志比本工具日志更接近真相**：请求体和回包都在里面，排接口问题优先看它。

---

## 八、已知限制

1. **AOI 与 SPI 不能在同一进程里测**。两套 `SinicTek.SDK` 同名同版本、成员不同，
   CLR 先加载者胜，切换机型必须重启程序（或另做 AppDomain 隔离）。
2. **`ConfigInjector` 当前强类型引用 AOI 版 `DomainAOI.DataExport`**。
   先在 AOI 上验证；SPI 侧依赖同名另一份构建，运行期可能 `MissingMethodException`。
3. `ExportDataPathRule-1` / `-2` 是模板自身的 bug（XML 里有横杠，属性名没有），
   插件永远取不到这两项值，注入器修不了。
4. 注入是**内存级**的，不落盘；程序重启后回到文件里的值。

---

## 九、文档

| 文档 | 内容 |
|---|---|
| `Docs/插件接口模拟触发方案.md` | 插件方法清单、接口路径、模拟触发的分层结构 |
| `Docs/客户参数契约设计.md` | XML ↔ `InParams` 契约校验、`ParamSetting` 特性机制 |
| `Docs/外部XML注入方法设计.md` | 注入方法设计、IL 实证、8 个坑 |
| `Docs/代码修改步骤.md` | 注入器的落地步骤、验证顺序、回退方法 |

---

## 十、常见问题

**Q：点了按钮没反应，日志也没输出？**
A：先看注入摘要里的 `基准` 和 `参数` 数量。`参数=1` 说明基准配置是空壳（见坑 #2）；
`改值=0 未识别=100` 说明 `ConfigPath` 和 `ModelTpye` 不是同一机型。

**Q：编译报"找不到类型或命名空间"？**
A：新增的 `.cs` 文件没有加进 `AOITest.csproj` 的 `<Compile Include="..." />`。

**Q：`IlJsonLoader.Load()` 返回 null？**
A：`bin\Debug\IL\` 下没有 `*.json`，或文件正在被 AOI 写入（loader 会重试 5 次 × 300ms）。

**Q：注入成功但请求体里还是旧值？**
A：调用链中间有人调了 `RefreshConfig()`，或插件的 `IsReadBadMarkFile=1` 走了本地 `.stf` 文件而非 HTTP。
