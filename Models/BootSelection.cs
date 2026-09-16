namespace ECController.Models
{
    /// <summary>
    /// 「登录后应用」的一项选择：某个功能组下的某个模式。
    ///
    /// 为什么需要它而不是单个 (组, 模式) 对：
    /// ec ADDRESS.txt 里的功能组是彼此独立的（性能模式写 0x78/0x79，
    /// 电池管理写 0xE4/0xE5），登录后可以同时应用两个组各一个模式。
    /// </summary>
    public class BootSelection
    {
        /// <summary>功能组名，对应配置文件的 [功能组]。</summary>
        public string Group { get; set; }

        /// <summary>模式名。</summary>
        public string Mode { get; set; }

        public BootSelection()
        {
            Group = string.Empty;
            Mode = string.Empty;
        }

        public BootSelection(string group, string mode)
        {
            Group = group ?? string.Empty;
            Mode = mode ?? string.Empty;
        }

        /// <summary>组名与模式名都非空才是一个可用项。</summary>
        public bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(Group) && !string.IsNullOrEmpty(Mode);
            }
        }

        public override string ToString()
        {
            return Group + " / " + Mode;
        }

        public override bool Equals(object obj)
        {
            BootSelection other = obj as BootSelection;

            if (other == null)
                return false;

            return string.Equals(Group, other.Group, System.StringComparison.Ordinal)
                && string.Equals(Mode, other.Mode, System.StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return Group.GetHashCode() ^ (Mode.GetHashCode() << 1);
        }
    }
}
