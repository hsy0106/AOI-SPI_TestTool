using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugin.Abstractions.Interface;

namespace App.Server
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
    }

    public static class LogServer
    {
        public static event Action<LogEntry> LogWritten;

        private static readonly ILogger _logger;

        static LogServer()
        {
            try
            {
                string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

     

                _logger = new LoggerConfiguration()
                     .MinimumLevel.Debug()
                     .WriteTo.File(
                         //path: Path.Combine(logDirectory, $"{DateTime.Now:yyyyMMdd}.txt"), // 直接生成日期文件
                         path: Path.Combine(logDirectory, "log.txt"),
                         rollingInterval: RollingInterval.Day, // 每天一个新文件
                         restrictedToMinimumLevel: LogEventLevel.Information,
                         outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                     )
                     .WriteTo.Console()
                     .CreateLogger();
            }
            catch (Exception ex)
            {

                throw new Exception("日志系统初始化失败: " + ex.Message, ex);
            }
        }

        public static void Info(string message) => Emit(LogLevel.Info, message, null);
        public static void Warn(string message) => Emit(LogLevel.Warn, message, null);
        public static void Error(string message, Exception ex = null) => Emit(LogLevel.Error, message, ex);
        public static void Debug(string message) => Emit(LogLevel.Debug, message, null);


        private static void Emit(LogLevel level, string message, Exception ex)
        {
            try
            {
                switch (level)
                {
                    case LogLevel.Debug: _logger.Debug(message); break;
                    case LogLevel.Info: _logger.Information(message); break;
                    case LogLevel.Warn: _logger.Warning(message); break;
                    case LogLevel.Error: _logger.Error(ex, message); break;
                }
            }
            catch {  }

            try
            {
                LogWritten?.Invoke(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message
                });
            }
            catch {  }
        }
    }
}