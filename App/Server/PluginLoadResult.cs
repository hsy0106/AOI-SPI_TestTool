using System;
using System.IO;
using Plugin.Abstractions.Interface;

namespace App.Server
{
    /// <summary>
    /// 单个插件文件的加载结果（LoadAll 返回用）。
    /// </summary>
    public sealed class PluginLoadResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public ICommunicationPlugin Plugin { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return (Success ? "OK  " : "FAIL") + " " + Path.GetFileName(FilePath) + "  " + Message;
        }
    }
}
