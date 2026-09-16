using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ECController.Models;
using ECController.Services;

namespace ECController.Config
{
    /// <summary>解析结果：包含数据以及解析过程中遇到的问题。</summary>
    public class ParseResult
    {
        public List<EcGroup> Groups { get; set; }

        /// <summary>格式有问题的行说明，用于提示用户而不是静默忽略。</summary>
        public List<string> Warnings { get; set; }

        /// <summary>文件本身是否成功读到。</summary>
        public bool FileFound { get; set; }

        public ParseResult()
        {
            Groups = new List<EcGroup>();
            Warnings = new List<string>();
        }
    }

    /// <summary>
    /// 解析 Data\ec ADDRESS.txt。
    ///
    /// 格式：
    ///     # 注释
    ///     [功能组名]
    ///
    ///     模式名
    ///     地址=数据        （十六进制，不带 0x）
    ///
    /// 地址与数据都用两位十六进制书写，例如 78=AA。
    /// </summary>
    public static class AddressParser
    {
        /// <summary>默认配置文件相对路径。</summary>
        public static string DefaultRelativePath
        {
            get { return Path.Combine("Data", "ec ADDRESS.txt"); }
        }

        /// <summary>默认配置文件的完整路径（exe 同级的 Data 目录）。</summary>
        public static string DefaultPath
        {
            get
            {
                return Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    DefaultRelativePath);
            }
        }

        /// <summary>
        /// 按默认路径加载。
        /// </summary>
        public static ParseResult Load()
        {
            return Load(DefaultPath);
        }

        /// <summary>
        /// 加载并解析指定文件。永不抛异常——所有问题都体现在
        /// <see cref="ParseResult.Warnings"/> 里。
        /// </summary>
        public static ParseResult Load(string file)
        {
            ParseResult result = new ParseResult();

            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                Logger.Error("EC 配置文件不存在：" + file);
                result.FileFound = false;
                return result;
            }

            result.FileFound = true;

            string[] lines;

            try
            {
                // 显式指定 UTF-8 并容忍 BOM；用户可能用记事本另存过
                lines = File.ReadAllLines(file, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error("读取 EC 配置文件失败：" + file, ex);
                result.Warnings.Add("读取配置文件失败：" + ex.Message);
                result.FileFound = false;
                return result;
            }

            EcGroup currentGroup = null;
            EcMode currentMode = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                string line = raw.Trim();

                if (line.Length == 0)
                    continue;

                if (line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                // [功能组]
                if (line.StartsWith("[") && line.EndsWith("]") && line.Length > 2)
                {
                    string name = line.Substring(1, line.Length - 2).Trim();

                    if (name.Length == 0)
                    {
                        result.Warnings.Add(
                            string.Format("第 {0} 行：功能组名为空，已跳过。", i + 1));
                        continue;
                    }

                    currentGroup = new EcGroup { Name = name };
                    result.Groups.Add(currentGroup);
                    currentMode = null;

                    continue;
                }

                // 地址=数据
                if (line.Contains("="))
                {
                    if (currentMode == null)
                    {
                        result.Warnings.Add(string.Format(
                            "第 {0} 行：'{1}' 出现在任何模式之前，已忽略。",
                            i + 1,
                            line));

                        continue;
                    }

                    EcItem item;
                    string itemError;

                    if (TryParseItem(line, out item, out itemError))
                    {
                        currentMode.Items.Add(item);
                    }
                    else
                    {
                        result.Warnings.Add(string.Format(
                            "第 {0} 行：'{1}' 格式错误（{2}），已忽略。",
                            i + 1,
                            line,
                            itemError));
                    }

                    continue;
                }

                // 模式名
                if (currentGroup == null)
                {
                    result.Warnings.Add(string.Format(
                        "第 {0} 行：'{1}' 出现在任何 [功能组] 之前，已忽略。",
                        i + 1,
                        line));

                    continue;
                }

                currentMode = new EcMode { Name = line };
                currentGroup.Modes.Add(currentMode);
            }

            // 结构完整性检查：空组、空模式通常意味着配置写漏了
            foreach (EcGroup group in result.Groups)
            {
                if (group.Modes.Count == 0)
                {
                    result.Warnings.Add(
                        "功能组 [" + group.Name + "] 下没有任何模式。");
                }

                foreach (EcMode mode in group.Modes)
                {
                    if (mode.Items.Count == 0)
                    {
                        result.Warnings.Add(string.Format(
                            "模式 [{0}] / {1} 下没有任何地址。",
                            group.Name,
                            mode.Name));
                    }
                }
            }

            foreach (string warning in result.Warnings)
                Logger.Warn("配置解析：" + warning);

            Logger.Info(string.Format(
                "配置解析完成：{0} 个功能组，{1} 条提示。",
                result.Groups.Count,
                result.Warnings.Count));

            return result;
        }

        /// <summary>
        /// 解析 "78=AA" 这种行。地址与值都接受 1~2 位十六进制。
        /// </summary>
        private static bool TryParseItem(string line, out EcItem item, out string error)
        {
            item = null;
            error = null;

            // 只按第一个等号切分，容忍值里出现多个 '='
            int eq = line.IndexOf('=');

            string left = line.Substring(0, eq).Trim();
            string right = line.Substring(eq + 1).Trim();

            if (left.Length == 0)
            {
                error = "地址为空";
                return false;
            }

            if (right.Length == 0)
            {
                error = "数据为空";
                return false;
            }

            byte address;
            byte value;

            if (!TryParseHexByte(left, out address))
            {
                error = "地址 '" + left + "' 不是合法的两位十六进制";
                return false;
            }

            if (!TryParseHexByte(right, out value))
            {
                error = "数据 '" + right + "' 不是合法的两位十六进制";
                return false;
            }

            item = new EcItem
            {
                Address = address,
                Value = value
            };

            return true;
        }

        /// <summary>
        /// 十六进制字节解析。接受 "78"、"0x78"、"0X78" 三种写法。
        /// </summary>
        private static bool TryParseHexByte(string text, out byte value)
        {
            value = 0;

            if (string.IsNullOrEmpty(text))
                return false;

            string hex = text.Trim();

            if (hex.StartsWith("0x") || hex.StartsWith("0X"))
                hex = hex.Substring(2);

            // 超过两位就没有意义了，视为错误而不是截断
            if (hex.Length == 0 || hex.Length > 2)
                return false;

            return byte.TryParse(
                hex,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
