using System;
using System.IO;
using System.Text;

namespace ECController.Services
{
    /// <summary>
    /// 极简日志服务：按天写入 Logs\yyyy-MM-dd.log。
    /// 线程安全，且自身永不抛异常——日志失败不能影响主流程。
    /// </summary>
    public static class Logger
    {
        private static readonly object Sync = new object();
        private static string _logDir;

        /// <summary>日志根目录，默认为 exe 同级的 Logs 目录。</summary>
        public static string LogDirectory
        {
            get
            {
                if (_logDir == null)
                {
                    _logDir = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Logs");
                }
                return _logDir;
            }
            set { _logDir = value; }
        }

        public static void Info(string message)
        {
            Write("INFO ", message);
        }

        public static void Warn(string message)
        {
            Write("WARN ", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Error(string message, Exception ex)
        {
            if (ex == null)
            {
                Write("ERROR", message);
                return;
            }

            Write("ERROR", message +
                Environment.NewLine +
                "        " + ex.GetType().Name + ": " + ex.Message +
                (string.IsNullOrEmpty(ex.StackTrace)
                    ? string.Empty
                    : Environment.NewLine + "        " + ex.StackTrace.Replace(
                        Environment.NewLine,
                        Environment.NewLine + "        ")));
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(LogDirectory);

                    string file = Path.Combine(
                        LogDirectory,
                        DateTime.Now.ToString("yyyy-MM-dd") + ".log");

                    string line = string.Format(
                        "{0}  {1}  {2}{3}",
                        DateTime.Now.ToString("HH:mm:ss.fff"),
                        level,
                        message ?? string.Empty,
                        Environment.NewLine);

                    File.AppendAllText(file, line, Encoding.UTF8);
                }
            }
            catch
            {
                // 日志写入失败时静默忽略，绝不影响 EC 读写主流程。
            }
        }
    }
}
