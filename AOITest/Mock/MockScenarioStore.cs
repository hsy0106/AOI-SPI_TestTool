using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AOITest.Mock
{
    /// <summary>
    /// 场景仓库：扫描一个目录下的 *.json，每个文件一个场景。
    ///
    /// 文件格式：
    /// {
    ///   "name": "NG",
    ///   "routes": {
    ///     "/BarCodeCheck": { "status": 200, "delayMs": 0, "note": "条码校验不过",
    ///                        "body": { "Result": "NG", "EventCode": 1, "Message": "barcode not found" } }
    ///   }
    /// }
    ///
    /// 只需要写"要改的那几条"，没写的自动继承 Default 场景（约定 Default 同时是兜底场景）。
    /// 这样切场景 = 换一个 name，不用维护 8 条完整清单。
    /// </summary>
    public class MockScenarioStore
    {
        public const string DefaultScenarioName = "Default";

        private readonly Dictionary<string, MockScenario> _scenarios =
            new Dictionary<string, MockScenario>(StringComparer.OrdinalIgnoreCase);

        /// <summary>实际扫描的目录</summary>
        public string DirectoryPath { get; private set; }

        /// <summary>加载时跳过的文件及原因，界面上可以提示出来</summary>
        public List<string> LoadErrors { get; private set; }

        public MockScenarioStore()
        {
            LoadErrors = new List<string>();
        }

        public IEnumerable<string> ScenarioNames
        {
            get { return _scenarios.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase); }
        }

        public bool HasScenario(string name)
        {
            return !string.IsNullOrEmpty(name) && _scenarios.ContainsKey(name);
        }

        /// <summary>扫描目录，重建全部场景。目录不存在就只保留内置兜底场景。</summary>
        public void Load(string dir)
        {
            DirectoryPath = dir;
            _scenarios.Clear();
            LoadErrors.Clear();

            // 内置兜底：任何接口都回 {"Result":"OK","EventCode":0,"Message":"mock"}
            var builtin = new MockScenario { Name = DefaultScenarioName };
            foreach (string route in KnownRoutes)
            {
                builtin.Routes[route] = new MockResponse
                {
                    Body = "{\"Result\":\"OK\",\"EventCode\":0,\"Message\":\"mock\"}",
                    FromScenario = "builtin",
                    Note = "内置兜底"
                };
            }
            _scenarios[DefaultScenarioName] = builtin;

            if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir))
                return;

            foreach (string file in System.IO.Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    MockScenario sc = LoadOne(file);
                    if (sc != null) _scenarios[sc.Name] = sc;
                }
                catch (Exception ex)
                {
                    LoadErrors.Add(Path.GetFileName(file) + " → " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 取一个"已合并"的场景：先铺 Default，再用 name 覆盖。
        /// name 为空或不存在时，只返回 Default。
        /// </summary>
        public MockScenario Resolve(string name)
        {
            var merged = new MockScenario { Name = name };

            MockScenario baseSc;
            if (_scenarios.TryGetValue(DefaultScenarioName, out baseSc))
            {
                foreach (var kv in baseSc.Routes)
                    merged.Routes[kv.Key] = Clone(kv.Value, baseSc.Name);
            }

            if (string.IsNullOrEmpty(name) || name.Equals(DefaultScenarioName, StringComparison.OrdinalIgnoreCase))
                return merged;

            MockScenario over;
            if (!_scenarios.TryGetValue(name, out over))
                return merged;   // 场景不存在就退回 Default，不抛异常

            foreach (var kv in over.Routes)
                merged.Routes[kv.Key] = Clone(kv.Value, over.Name);

            return merged;
        }

        private static MockResponse Clone(MockResponse src, string scenarioName)
        {
            // 复制一份，避免调用方改动污染仓库
            return new MockResponse
            {
                StatusCode = src.StatusCode,
                ContentType = src.ContentType,
                Body = src.Body,
                DelayMs = src.DelayMs,
                Note = src.Note,
                FromScenario = string.IsNullOrEmpty(src.FromScenario) ? scenarioName : src.FromScenario
            };
        }

        private static MockScenario LoadOne(string file)
        {
            string text = File.ReadAllText(file);
            JObject root = JObject.Parse(text);

            var sc = new MockScenario();
            sc.Name = root.Value<string>("name");
            if (string.IsNullOrEmpty(sc.Name))
                sc.Name = Path.GetFileNameWithoutExtension(file);

            JObject routes = root["routes"] as JObject;
            if (routes == null)
                throw new InvalidDataException("缺少 routes 节点");

            foreach (JProperty p in routes.Properties())
            {
                sc.Routes[p.Name] = MockResponse.FromJson(p.Value);
            }

            return sc;
        }

        /// <summary>
        /// 已知接口段（来自插件 IL 常量扫描）。
        /// 只用于生成内置兜底场景，不影响路由匹配——文件里写别的路径一样生效。
        /// </summary>
        public static readonly string[] KnownRoutes =
        {
            "/LoginCheck",
            "/ParamsCheck",
            "/BarCodeCheck",
            "/GetBarCode",
            "/GetBadMark",
            "/MesOutPut",
            "/UpdateDeviceStatus",
            "/EquipStatus",
            "/SwitchJob",
            "/DevCmd"
        };
    }
}
