using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace App
{
    public static class AppConfig
    {
        //插件固定路径
        public static string PluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
    }
}
