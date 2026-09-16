using System;
using System.Globalization;
using System.IO;
using System.Text;
using ECController.Services;

namespace ECController.Config
{
    /// <summary>
    /// config.json 的读写。手写极小 JSON 序列化，避免引入第三方依赖
    /// （项目是 .NET Framework 4.7.2 且不带 NuGet 还原）。
    ///
    /// 文件与 exe 同目录。损坏时自动备份为 config.json.bad 并回退默认值，
    /// 保证程序永远能启动。
    /// </summary>
    public static class SettingsManager
    {
        private const string FileName = "config.json";

        private const string BackupSuffix = ".bad";

        /// <summary>配置文件完整路径。</summary>
        public static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    FileName);
            }
        }

        /// <summary>
        /// 加载设置。文件不存在或损坏时返回默认值（损坏的文件会被备份）。
        /// 本方法不抛异常。
        /// </summary>
        public static AppSettings Load()
        {
            string path = SettingsPath;

            if (!File.Exists(path))
            {
                Logger.Info("config.json 不存在，使用默认设置。");
                return new AppSettings();
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                AppSettings settings = Parse(json);
                settings.Normalize();

                Logger.Info("已加载 config.json。");

                return settings;
            }
            catch (Exception ex)
            {
                Logger.Error("config.json 解析失败，回退默认设置。", ex);
                BackupBrokenFile(path);

                return new AppSettings();
            }
        }

        /// <summary>
        /// 保存设置。写入失败返回 false 并记录日志，不抛异常。
        /// 先写临时文件再替换，避免写到一半断电产生半个文件。
        /// </summary>
        public static bool Save(AppSettings settings)
        {
            if (settings == null)
                return false;

            settings.Normalize();

            string path = SettingsPath;
            string temp = path + ".tmp";

            try
            {
                File.WriteAllText(temp, Serialize(settings), new UTF8Encoding(false));

                if (File.Exists(path))
                    File.Delete(path);

                File.Move(temp, path);

                Logger.Info("已保存 config.json。");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("保存 config.json 失败。", ex);

                try
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
                catch
                {
                    // 清理失败无需处理
                }

                return false;
            }
        }

        /// <summary>把损坏的配置文件改名为 config.json.bad，保留现场供排查。</summary>
        private static void BackupBrokenFile(string path)
        {
            try
            {
                string backup = path + BackupSuffix;

                if (File.Exists(backup))
                    File.Delete(backup);

                File.Move(path, backup);

                Logger.Warn("已把损坏的配置备份为 " + Path.GetFileName(backup));
            }
            catch (Exception ex)
            {
                Logger.Error("备份损坏的 config.json 失败。", ex);
            }
        }

        #region 极简 JSON 序列化

        /// <summary>序列化为带缩进的 JSON，方便用户手工编辑。</summary>
        public static string Serialize(AppSettings s)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("{");
            sb.AppendLine("    \"AutoStart\": " + Bool(s.AutoStart) + ",");
            sb.AppendLine("    \"ApplyOnBoot\": " + Bool(s.ApplyOnBoot) + ",");
            sb.AppendLine("    \"Tray\": " + Bool(s.Tray) + ",");
            sb.AppendLine("    \"BootGroup\": " + Quote(s.BootGroup) + ",");
            sb.AppendLine("    \"BootMode\": " + Quote(s.BootMode) + ",");
            sb.AppendLine("    \"WaitTime\": " + s.WaitTime.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("    \"BootDelaySeconds\": " + s.BootDelaySeconds.ToString(CultureInfo.InvariantCulture));
            sb.Append("}");

            return sb.ToString();
        }

        /// <summary>
        /// 解析 JSON。只认本类自己写出的那些键，未知键忽略，
        /// 缺失键保留默认值——所以用户手改出多余字段也不会出问题。
        /// </summary>
        public static AppSettings Parse(string json)
        {
            AppSettings s = new AppSettings();

            if (string.IsNullOrEmpty(json))
                return s;

            // 去掉最外层花括号后按行扫描
            string body = json.Replace("{", string.Empty).Replace("}", string.Empty);

            foreach (string rawLine in body.Split('\n'))
            {
                string line = rawLine.Trim();

                if (line.Length == 0)
                    continue;

                // 去掉行尾逗号
                if (line.EndsWith(","))
                    line = line.Substring(0, line.Length - 1).Trim();

                int colon = line.IndexOf(':');

                if (colon <= 0)
                    continue;

                string key = Unquote(line.Substring(0, colon).Trim());
                string value = line.Substring(colon + 1).Trim();

                switch (key)
                {
                    case "AutoStart":
                        s.AutoStart = ParseBool(value, false);
                        break;

                    case "ApplyOnBoot":
                        s.ApplyOnBoot = ParseBool(value, false);
                        break;

                    case "Tray":
                        s.Tray = ParseBool(value, false);
                        break;

                    case "BootGroup":
                        s.BootGroup = Unquote(value);
                        break;

                    case "BootMode":
                        s.BootMode = Unquote(value);
                        break;

                    case "WaitTime":
                        s.WaitTime = ParseInt(value, 5);
                        break;

                    case "BootDelaySeconds":
                        s.BootDelaySeconds = ParseInt(value, 5);
                        break;
                }
            }

            return s;
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static bool ParseBool(string value, bool fallback)
        {
            if (string.IsNullOrEmpty(value))
                return fallback;

            value = Unquote(value).Trim().ToLowerInvariant();

            if (value == "true" || value == "1" || value == "yes")
                return true;

            if (value == "false" || value == "0" || value == "no")
                return false;

            return fallback;
        }

        private static int ParseInt(string value, int fallback)
        {
            int result;

            if (int.TryParse(
                Unquote(value),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result))
            {
                return result;
            }

            return fallback;
        }

        private static string Quote(string value)
        {
            if (value == null)
                value = string.Empty;

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string Unquote(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            value = value.Trim();

            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = value.Substring(1, value.Length - 2);

            return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        #endregion
    }
}
