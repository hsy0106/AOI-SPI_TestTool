using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Plugin.Abstractions.Attribute;
using Plugin.Abstractions.Interface;

namespace App.Server
{

    public static class PluginLoader
    {
        /// <summary>
        /// 找出带[CommunicationPlugin]类型的插件
        /// </summary>
        /// <param name="asm"></param>
        /// <returns></returns>
        public static Type FindPluginType(Assembly asm)
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 插件依赖缺失时 GetTypes 会部分失败，取出可用部分继续找
                types = ex.Types.Where(t => t != null).ToArray();
            }

            return types.FirstOrDefault(t =>
                t.IsClass && !t.IsAbstract && t.IsPublic &&
                typeof(ICommunicationPlugin).IsAssignableFrom(t) &&
                t.GetCustomAttribute<CommunicationPluginAttribute>() != null);
        }

        /// <summary>
        /// 加载指定 DLL 并创建插件实例
        /// </summary>
        public static ICommunicationPlugin LoadFrom(string dll)
        {
            Assembly asm = Assembly.LoadFrom(dll);
            Type type = FindPluginType(asm);
            if (type == null) return null;
            return (ICommunicationPlugin)Activator.CreateInstance(type);
        }

        /// <summary>
        /// 扫描一个 DLL 中所有插件类型
        /// </summary>
        public static List<PluginDiscoverInfo> Discover(string dll)
        {
            var result = new List<PluginDiscoverInfo>();
            Assembly asm = Assembly.LoadFrom(dll);

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

            foreach (var t in types)
            {
                if (t == null || !t.IsClass || t.IsAbstract || !t.IsPublic) continue;
                if (!typeof(ICommunicationPlugin).IsAssignableFrom(t)) continue;

                var attr = t.GetCustomAttribute<CommunicationPluginAttribute>();
                if (attr == null) continue;

                result.Add(new PluginDiscoverInfo
                {
                    Dll = dll,
                    TypeName = t.FullName,
                    Id = attr.Id,
                    Name = attr.Name,
                    Version = attr.Version,
                    Description = attr.Description
                });
            }
            return result;
        }
    }

    public sealed class PluginDiscoverInfo
    {
        public string Dll { get; set; }
        public string TypeName { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
    }
}
