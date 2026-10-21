using AOITest.IL;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AOITest.AOI
{
    public static class IlJsonLoader
    {
        //public static string ILJsonFile = @"D:\EYAOI\FILE-POSTDATA\IL";
        public static string ILJsonFile = Path.Combine(Application.StartupPath, "IL");
        /// <summary>加载目录下所有 json，返回解析成功的对象列表。</summary>
        /// 
        public static IlPcbResult Load()
        {
            string file = Directory.GetFiles(ILJsonFile, "*.json", SearchOption.TopDirectoryOnly)
                                   .FirstOrDefault();

            if (file == null) return null;   // 目录下还没有 json

            string json = File.ReadAllText(file);

            return JsonConvert.DeserializeObject<IlPcbResult>(json);
        }
        public static List<IlPcbResult> LoadAll(string dir = null)
        {
            dir = string.IsNullOrWhiteSpace(dir) ? ILJsonFile : dir;
            var list = new List<IlPcbResult>();

            if (!Directory.Exists(dir)) return list;

            foreach (string file in Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
            {
                var obj = LoadOne(file);
                if (obj != null) list.Add(obj);
            }
            return list;
        }

        /// <summary>解析单个 json 文件；失败返回 null，不抛异常。</summary>
        public static IlPcbResult LoadOne(string file)
        {
            string json = ReadTextWithRetry(file);   // 文件可能正在被写入
            if (json == null) return null;

            try
            {
                return JsonConvert.DeserializeObject<IlPcbResult>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[IL] 解析失败 " + Path.GetFileName(file) + " : " + ex.Message);
                return null;
            }
        }

        /// <summary>解析 + 归档：成功后把文件移到 Backup 目录，避免重复处理。</summary>
        public static List<IlPcbResult> LoadAndArchive(string dir = null, bool deleteAfter = false)
        {
            dir = string.IsNullOrWhiteSpace(dir) ? ILJsonFile : dir;
            var list = new List<IlPcbResult>();
            if (!Directory.Exists(dir)) return list;

            string backup = Path.Combine(dir, "Backup");
            if (!deleteAfter) Directory.CreateDirectory(backup);

            foreach (string file in Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
            {
                IlPcbResult obj = LoadOne(file);
                if (obj == null) continue;

                list.Add(obj);

                try
                {
                    if (deleteAfter)
                        File.Delete(file);
                    else
                        File.Move(file, Path.Combine(backup, Path.GetFileName(file)));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[IL] 归档失败 " + file + " : " + ex.Message);
                }
            }
            return list;
        }

        /// <summary>带重试读取：AOI 写文件时是流式写入，刚触发事件时文件常还不完整。</summary>
        private static string ReadTextWithRetry(string file, int retry = 5, int waitMs = 300)
        {
            for (int i = 0; i < retry; i++)
            {
                try
                {
                    // FileShare.None 打开成功 = 没有别的进程在写
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
                    using (var sr = new StreamReader(fs, System.Text.Encoding.UTF8))
                        return sr.ReadToEnd();
                }
                catch (IOException)
                {
                    if (i == retry - 1)
                    {
                        Console.WriteLine("[IL] 文件被占用，读取失败：" + file);
                        return null;
                    }
                    Thread.Sleep(waitMs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[IL] 读取异常：" + ex.Message);
                    return null;
                }
            }
            return null;
        }
    }
}
