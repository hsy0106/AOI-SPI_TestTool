using System;
using System.Reflection;

namespace Plugin.Abstractions.Attribute
{
    /// <summary>
    /// 通信插件标记
    /// Host（PluginManager）反射扫描插件 DLL 时，只认带有该特性的类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class CommunicationPluginAttribute : System.Attribute
    {
        /// <summary>
        /// 插件唯一标识
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 插件名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 插件版本号
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// 功能描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 插件特性信息
        /// </summary>
        /// <param name="pluginType"></param>
        /// <returns></returns>
        public static CommunicationPluginAttribute From(Type pluginType)
        {
            return pluginType?.GetCustomAttribute<CommunicationPluginAttribute>();
        }
    }
}
