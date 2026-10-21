using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App.Server
{

    public static class JsonConfigServer
    {
        private static readonly object _lock = new object();
        // 配置文件放在插件文件夹下与主程序无关
        private static readonly string ConfigDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Config");
        private static readonly string ConfigFile =
            Path.Combine(ConfigDir, "PluginConfig.json");

        
        public static string ConfigPath => ConfigFile;

        static JsonConfigServer()
        {

            try
            {
                if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                if (!File.Exists(ConfigFile))
                    File.WriteAllText(ConfigFile, "{ }", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogServer.Warn("初始化 JSON 配置失败:" + ex.ToString());
            }
        }

        /// <summary>读取某个 section 的强类型配置；不存在或损坏时返回 T 的默认实例。</summary>
        public static T GetSection<T>(string section) where T : class, new()
        {
            try
            {
                lock (_lock)
                {
                    var root = Load();
                    if (root.TryGetPropertyValue(section, out var node) && node is JsonObject obj)
                        return obj.Deserialize<T>() ?? new T();
                }
            }
            catch (Exception ex)
            {
                LogServer.Warn("读取配置节 [" + section + "] 异常:" + ex.Message);
            }
            return new T();
        }

      
        public static void SetSection(string section, object value)
        {
            if (value == null) return;
            try
            {
                lock (_lock)
                {
                    var root = Load();
                    root[section] = JsonSerializer.SerializeToNode(value, value.GetType());
                    Save(root);
                }
            }
            catch (Exception ex)
            {
                LogServer.Warn("写入配置节 [" + section + "] 异常:" + ex.Message);
            }
        }

        //根据key读取
        public static string Read(string section, string key, string defaultValue = "")
        {
            try
            {
                lock (_lock)
                {
                    var root = Load();
                    if (root.TryGetPropertyValue(section, out var node) && node is JsonObject sec &&
                        sec.TryGetPropertyValue(key, out var val) && val is JsonValue jv)
                    {
                        var s = jv.TryGetValue<string>(out var str) ? str : null;
                        if (s != null) return s;
                    }
                }
            }
            catch (Exception ex)
            {
                LogServer.Warn("JSON 配置读取异常:" + ex.ToString());
            }
            return defaultValue;
        }

    
        public static int ReadInt(string section, string key, int defaultValue = 0)
        {
            string val = Read(section, key);
            return int.TryParse(val, out int result) ? result : defaultValue;
        }

      
        public static bool Write(string section, string key, string value)
        {
            try
            {
                lock (_lock)
                {
                    var root = Load();
                    if (!(root.TryGetPropertyValue(section, out var node) && node is JsonObject sec))
                    {
                        sec = new JsonObject();
                        root[section] = sec;
                    }
                    sec[key] = value;
                    Save(root);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogServer.Warn("写入 JSON 配置异常:" + ex.ToString());
                return false;
            }
        }

        
        private static JsonObject Load()
        {
            if (!File.Exists(ConfigFile)) return new JsonObject();
            string json = File.ReadAllText(ConfigFile, Encoding.UTF8);
            try
            {
                return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            }
            catch
            {

                return new JsonObject();
            }
        }

     
        private static void Save(JsonObject root)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ConfigFile, root.ToJsonString(options), Encoding.UTF8);
        }
    }
}
