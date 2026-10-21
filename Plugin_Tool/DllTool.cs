using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Plugin_Tool
{
    public static class DllTool
    {

        public static MethodInfo GetMethod(string strMethod, string dll)
        {
            Assembly asm = Assembly.LoadFrom(dll);

            foreach (Type type in asm.GetTypes())
            {
                MethodInfo method = type.GetMethod(strMethod,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

                if (method != null)
                    return method;
            }

            return null;
        }

        public static object RunMethod(string strMethod, string dll, params object[] args)
        {
            MethodInfo method = GetMethod(strMethod, dll);
            if (method == null)
                throw new Exception("找不到方法：" + strMethod);

            //object instance = method.IsStatic ? null : Activator.CreateInstance(method.DeclaringType);
            object instance =  Activator.CreateInstance(method.DeclaringType);
            try
            {
                return method.Invoke(instance, args);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }
        ///// <summary>依赖搜索目录列表（客户 DLL 目录 + SDK 目录）</summary>
        //private static readonly List<string> _probeDirs = new List<string>();

        ///// <summary>目录 → { 真实程序集名 : 文件路径 } 缓存，一个目录只扫一次</summary>
        //private static readonly Dictionary<string, Dictionary<string, string>> _dirAssemblyMap =
        //    new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        ///// <summary>钩子只挂一次</summary>
        //private static bool _resolveHooked = false;

        ///// <summary>本目录扫描时的最后一条错误</summary>
        //public static string LastError { get; private set; }

        ///// <summary>
        ///// 注册依赖搜索目录。
        ///// 客户 DLL 一般单独放一个文件夹，DomainAOI.DataExport / SinicTek.SDK /
        ///// Newtonsoft.Json 这些依赖都在设备目录下，不登记就加载不了类型。
        ///// </summary>
        //public static void AddProbeDir(string dir)
        //{
        //    if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        //        return;

        //    lock (_probeDirs)
        //    {
        //        if (!_probeDirs.Contains(dir))
        //            _probeDirs.Add(dir);
        //    }

        //    HookAssemblyResolve();
        //}

        ///// <summary>
        ///// 在 AppDomain 上挂依赖解析。
        ///// 因为用 Assembly.Load(byte[]) 加载，CLR 不会去 dll 同目录找依赖，
        ///// 不挂这个钩子 GetTypes() 会因为依赖缺失大面积失败。
        ///// </summary>
        //private static void HookAssemblyResolve()
        //{
        //    lock (_probeDirs)
        //    {
        //        if (_resolveHooked) return;

        //        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        //        {
        //            // args.Name 形如 "Newtonsoft.Json, Version=..., Culture=..., PublicKeyToken=..."
        //            string simpleName = args.Name.Split(',')[0].Trim();

        //            lock (_probeDirs)
        //            {
        //                foreach (string dir in _probeDirs)
        //                {
        //                    // 1. 文件名 == 程序集名：Xxx.dll
        //                    string candidate = Path.Combine(dir, simpleName + ".dll");
        //                    if (File.Exists(candidate))
        //                    {
        //                        try { return Assembly.Load(File.ReadAllBytes(candidate)); }
        //                        catch { }
        //                    }

        //                    // 2. 文件名 != 程序集名：如 SinicTek.SDK.dll 的程序集名是 SinicTekAOI.SDK
        //                    string realPath;
        //                    if (GetDirAssemblyMap(dir).TryGetValue(simpleName, out realPath))
        //                    {
        //                        try { return Assembly.Load(File.ReadAllBytes(realPath)); }
        //                        catch { }
        //                    }
        //                }
        //            }

        //            // 3. 最后在主程序目录下找
        //            string baseCandidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, simpleName + ".dll");
        //            if (File.Exists(baseCandidate))
        //            {
        //                try { return Assembly.Load(File.ReadAllBytes(baseCandidate)); }
        //                catch { }
        //            }

        //            return null;
        //        };

        //        _resolveHooked = true;
        //    }
        //}

        ///// <summary>
        ///// 读目录下每个 dll 的真实程序集名，建映射并缓存。
        ///// 解决"文件名 ≠ 程序集名"导致的找不到依赖。
        ///// </summary>
        //private static Dictionary<string, string> GetDirAssemblyMap(string dir)
        //{
        //    lock (_dirAssemblyMap)
        //    {
        //        Dictionary<string, string> map;
        //        if (_dirAssemblyMap.TryGetValue(dir, out map))
        //            return map;

        //        map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        //        try
        //        {
        //            foreach (string f in Directory.GetFiles(dir, "*.dll"))
        //            {
        //                try
        //                {
        //                    // 只读元数据，不加载程序集
        //                    AssemblyName an = AssemblyName.GetAssemblyName(f);
        //                    if (an != null && !string.IsNullOrEmpty(an.Name) && !map.ContainsKey(an.Name))
        //                        map[an.Name] = f;
        //                }
        //                catch { }
        //            }
        //        }
        //        catch { }

        //        _dirAssemblyMap[dir] = map;
        //        return map;
        //    }
        //}
    }
}
