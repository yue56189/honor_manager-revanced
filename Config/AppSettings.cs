using System;
using System.Collections.Generic;
using ECController.Models;

namespace ECController.Config
{
    /// <summary>
    /// 用户设置。只描述"用户想怎么运行程序"，
    /// 与 EC 配置（Data\ec ADDRESS.txt 里定义了哪些模式）严格分开。
    /// </summary>
    public class AppSettings
    {
        /// <summary>单次登录最多应用多少个功能组，防止配置文件被写坏后无上限。</summary>
        public const int MaxBootSelections = 16;

        /// <summary>是否开机自启（写入 HKCU Run 注册表项）。</summary>
        public bool AutoStart { get; set; }

        /// <summary>登录后是否自动应用下面的选择（总开关）。</summary>
        public bool ApplyOnBoot { get; set; }

        /// <summary>关闭/最小化窗口时是否隐藏到托盘而不是退出。</summary>
        public bool Tray { get; set; }

        /// <summary>
        /// 登录自动应用时要套用的选择，每个功能组最多一项。
        /// 顺序即应用顺序。
        /// </summary>
        public List<BootSelection> BootSelections { get; private set; }

        /// <summary>每次 EC 端口操作后的等待毫秒数。</summary>
        public int WaitTime { get; set; }

        /// <summary>开机自动应用前额外等待的秒数，等系统 EC 服务就绪。</summary>
        public int BootDelaySeconds { get; set; }

        /// <summary>
        /// 旧版配置里的单一选择。只用于迁移（读到旧 config.json 时转成
        /// BootSelections 的一项），不再写出。
        /// </summary>
        public BootSelection LegacySelection { get; set; }

        /// <summary>旧版配置里的"是否自动应用"开关，只用于迁移。</summary>
        public bool LegacyApplyOnBoot { get; set; }

        public AppSettings()
        {
            AutoStart = false;
            ApplyOnBoot = false;
            Tray = false;
            BootSelections = new List<BootSelection>();
            WaitTime = 5;
            BootDelaySeconds = 5;
        }

        /// <summary>实际会写 EC 的项数（总开关没开时为 0）。</summary>
        public int EffectiveBootCount
        {
            get { return ApplyOnBoot ? BootSelections.Count : 0; }
        }

        /// <summary>某个功能组下已保存的模式名；没有则返回 null。</summary>
        public string GetBootMode(string group)
        {
            foreach (BootSelection s in BootSelections)
            {
                if (string.Equals(s.Group, group, StringComparison.Ordinal))
                    return s.Mode;
            }

            return null;
        }

        /// <summary>设置某功能组的开机应用模式；mode 为空表示移除该项。</summary>
        public void SetBootMode(string group, string mode)
        {
            if (string.IsNullOrEmpty(group))
                return;

            for (int i = 0; i < BootSelections.Count; i++)
            {
                if (!string.Equals(BootSelections[i].Group, group, StringComparison.Ordinal))
                    continue;

                if (string.IsNullOrEmpty(mode))
                    BootSelections.RemoveAt(i);
                else
                    BootSelections[i].Mode = mode;

                return;
            }

            if (!string.IsNullOrEmpty(mode))
                BootSelections.Add(new BootSelection(group, mode));
        }

        public void ClearBootSelections()
        {
            BootSelections.Clear();
        }

        /// <summary>
        /// 收敛非法值。配置文件被手工改坏时也能安全使用。
        /// 同时完成旧版 -> 新版迁移。
        /// </summary>
        public void Normalize()
        {
            if (WaitTime < 0 || WaitTime > 1000)
                WaitTime = 5;

            if (BootDelaySeconds < 0 || BootDelaySeconds > 300)
                BootDelaySeconds = 5;

            // 迁移：旧配置只有 BootGroup/BootMode + ApplyOnBoot。
            // 旧配置里 ApplyOnBoot 是独立开关，照搬其取值——用户当时没开就保持没开，
            // 不要因为存在 BootGroup/BootMode 就凭空打开。
            if (BootSelections.Count == 0 && LegacySelection != null && LegacySelection.IsValid)
            {
                BootSelections.Add(LegacySelection);
                ApplyOnBoot = LegacyApplyOnBoot;
            }

            LegacySelection = null;

            // 去掉残缺项，并按功能组去重（一个组只可能应用一个模式）
            List<BootSelection> cleaned = new List<BootSelection>();
            List<string> seen = new List<string>();

            foreach (BootSelection s in BootSelections)
            {
                if (s == null || !s.IsValid)
                    continue;

                s.Group = s.Group.Trim();
                s.Mode = s.Mode.Trim();

                if (s.Group.Length == 0 || s.Mode.Length == 0)
                    continue;

                if (seen.Contains(s.Group))
                    continue;

                seen.Add(s.Group);
                cleaned.Add(s);
            }

            if (cleaned.Count > MaxBootSelections)
                cleaned.RemoveRange(MaxBootSelections, cleaned.Count - MaxBootSelections);

            BootSelections = cleaned;
        }

        public AppSettings Clone()
        {
            AppSettings copy = (AppSettings)MemberwiseClone();
            copy.BootSelections = new List<BootSelection>();

            foreach (BootSelection s in BootSelections)
                copy.BootSelections.Add(new BootSelection(s.Group, s.Mode));

            return copy;
        }

        /// <summary>与另一份设置是否等价（用于判断界面上的修改是否已保存）。</summary>
        public bool SameAs(AppSettings other)
        {
            if (other == null)
                return false;

            if (AutoStart != other.AutoStart ||
                ApplyOnBoot != other.ApplyOnBoot ||
                Tray != other.Tray ||
                WaitTime != other.WaitTime ||
                BootDelaySeconds != other.BootDelaySeconds)
            {
                return false;
            }

            if (BootSelections.Count != other.BootSelections.Count)
                return false;

            for (int i = 0; i < BootSelections.Count; i++)
            {
                if (!BootSelections[i].Equals(other.BootSelections[i]))
                    return false;
            }

            return true;
        }
    }
}
