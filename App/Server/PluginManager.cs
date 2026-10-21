using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Plugin.Abstractions.Attribute;
using Plugin.Abstractions.Interface;

namespace App.Server
{
    /// <summary>
    /// 插件管理器：反射加载
    /// </summary>
    public sealed class PluginManager : IDisposable
    {
        private readonly string _pluginDir;
        private readonly List<ICommunicationPlugin> _plugins = new List<ICommunicationPlugin>();
        private readonly object _sync = new object();


        public IReadOnlyList<ICommunicationPlugin> Plugins
        {
            get {   return _plugins.ToList(); } 
        }

        public PluginManager(string pluginDir)
        {
            if (string.IsNullOrEmpty(pluginDir))
                throw new ArgumentNullException(nameof(pluginDir));

            _pluginDir = pluginDir;
            Directory.CreateDirectory(_pluginDir);

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }


        public IList<PluginLoadResult> LoadAll(IPluginContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var results = new List<PluginLoadResult>();

          
            foreach (var dll in Directory.GetFiles(_pluginDir, "*.dll"))
            {
                try
                {
                    if (IsHostAssembly(Path.GetFileNameWithoutExtension(dll)))
                    {
                        results.Add(new PluginLoadResult { FilePath = dll, Message = "HOST已加载，跳过" });
                        continue;
                    }
                    if (PluginLoader.Discover(dll).Count == 0)
                        continue;  

                    results.Add(LoadOne(dll, context));
                }
                catch (Exception ex)
                {
                    results.Add(new PluginLoadResult { FilePath = dll, Message = ex.Message });
                }
            }
            return results;
        }

        private PluginLoadResult LoadOne(string dll, IPluginContext context)
        {
            ICommunicationPlugin plugin = PluginLoader.LoadFrom(dll);
            if (plugin == null)
                return new PluginLoadResult { FilePath = dll, Message = "未发现插件类型" };

            plugin.Initialize(context);

            lock (_sync) { _plugins.Add(plugin); }

            return new PluginLoadResult { Success = true, FilePath = dll, Plugin = plugin };
        }



        public void Start(string id)
        {
            var p = Find(id); if (p != null) p.Start();
        }

        public void Stop(string id)
        {
            var p = Find(id); if (p != null) p.Stop();
        }

        public void StopAll()
        {
            foreach (var p in Plugins) { try { p.Stop(); } catch { } }
        }

        private ICommunicationPlugin Find(string id)
        {
            // 插件 Id 从 [CommunicationPlugin] 特性反射读取
            return Plugins.FirstOrDefault(p => CommunicationPluginAttribute.From(p.GetType())?.Id == id);
        }



        /// <summary>测试打印插件信息</summary>
        public string ScanReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("扫描插件目录: " + _pluginDir);
            sb.AppendLine(new string('=', 60));

            string[] dlls;
            try { dlls = Directory.GetFiles(_pluginDir, "*.dll"); }
            catch (Exception ex)
            {
                sb.AppendLine("读取目录失败: " + ex.Message);
                return sb.ToString();
            }

            if (dlls.Length == 0)
            {
                sb.AppendLine("(Plugin目录为空)");
                return sb.ToString();
            }

            foreach (var dll in dlls)
            {
                sb.AppendLine();
                sb.AppendLine("程序集: " + Path.GetFileName(dll));
                try
                {
                    var infos = PluginLoader.Discover(dll);   // 只读扫描源目录，不实例化
                    if (infos.Count == 0)
                    {
                        sb.AppendLine("  (未发现插件类型)");
                        continue;
                    }
                    foreach (var info in infos)
                    {
                        sb.AppendLine("插件类型: " + info.TypeName);
                        sb.AppendLine("Id=" + info.Id + " Name=" + info.Name + " Version=" + (info.Version ?? ""));
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine("  加载失败: " + ex.Message);
                }
            }
            return sb.ToString();
        }


        /// <summary>
        /// 是否已被Host加载
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static bool IsHostAssembly(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Any(a =>
                string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 依赖解析：CLR 找不到程序集时回调这里。
        /// </summary>
        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            StopAll();
        }
    }
}
