using System;
using System.Drawing;
using System.Windows.Forms;
using ECController.Models;
using ECController.Theme;

namespace ECController
{
    /// <summary>
    /// "恢复默认"的含义选择框。
    ///
    /// 这里有三个容易混淆的概念，必须让用户明确选一个：
    ///   A. 恢复 EC 到配置文件里的第一个模式
    ///   B. 把当前 EC 寄存器写回指定值（需要用户提供地址与值）
    ///   C. 恢复软件设置（config.json）
    /// </summary>
    internal sealed class RestoreDialog : Form
    {
        public enum RestoreChoice
        {
            None,
            DefaultMode,
            SoftwareSettings
        }

        /// <summary>用户选择的恢复方式。</summary>
        public RestoreChoice Choice { get; private set; }

        public RestoreDialog(EcGroup currentGroup, EcMode currentMode)
        {
            Choice = RestoreChoice.None;

            BuildUi(currentGroup, currentMode);
        }

        private void BuildUi(EcGroup currentGroup, EcMode currentMode)
        {
            Text = "恢复默认";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Font = FluentTheme.BodyFont;
            BackColor = FluentTheme.WindowBackground;
            ForeColor = FluentTheme.TextPrimary;
            ClientSize = new Size(516, 366);
            DoubleBuffered = true;

            Label title = new Label
            {
                Text = "恢复默认",
                Location = new Point(24, 20),
                Size = new Size(300, 28),
                Font = FluentTheme.TitleFont,
                ForeColor = FluentTheme.TextPrimary,
                BackColor = Color.Transparent
            };

            Label divider = new Label
            {
                Location = new Point(24, 56),
                Size = new Size(468, 1),
                BackColor = FluentTheme.CardBorder
            };

            Label header = new Label
            {
                Text = "「恢复默认」有三种不同含义，请选择你要执行的操作：",
                Location = new Point(24, 70),
                Size = new Size(468, 22),
                ForeColor = FluentTheme.TextSecondary,
                BackColor = Color.Transparent
            };

            FluentRadioButton optionA = new FluentRadioButton
            {
                Text = "A. 恢复 EC 到配置中的默认模式（第一个功能组的第一个模式）",
                Location = new Point(24, 100),
                Size = new Size(468, 24),
                Checked = true
            };

            Label hintA = new Label
            {
                Text = "会向 EC 寄存器写入值，属于硬件层面的修改。",
                Location = new Point(48, 126),
                Size = new Size(444, 20),
                Font = FluentTheme.CaptionFont,
                ForeColor = FluentTheme.TextDisabled,
                BackColor = Color.Transparent
            };

            FluentRadioButton optionC = new FluentRadioButton
            {
                Text = "C. 恢复软件设置（开机启动 / 登录自动应用 / 托盘）",
                Location = new Point(24, 162),
                Size = new Size(468, 24)
            };

            Label hintC = new Label
            {
                Text = "只改 config.json 与注册表，不碰任何 EC 寄存器。",
                Location = new Point(48, 188),
                Size = new Size(444, 20),
                Font = FluentTheme.CaptionFont,
                ForeColor = FluentTheme.TextDisabled,
                BackColor = Color.Transparent
            };

            FluentCard noteCard = new FluentCard
            {
                Location = new Point(24, 218),
                Size = new Size(468, 78),
                Padding = new Padding(14, 12, 14, 12)
            };

            Label noteB = new Label
            {
                Text = "说明：把 EC 寄存器恢复到任意指定值（方案 B）需要你提供地址和值，"
                     + "请直接在 Data\\ec ADDRESS.txt 里新增一个模式，然后在主界面选择它并应用。",
                Dock = DockStyle.Fill,
                Font = FluentTheme.CaptionFont,
                ForeColor = FluentTheme.TextSecondary,
                BackColor = Color.Transparent
            };
            noteCard.Controls.Add(noteB);

            FluentButton ok = new FluentButton
            {
                Text = "确定",
                Accent = true,
                Location = new Point(300, 312),
                Size = new Size(92, 34),
                DialogResult = DialogResult.OK
            };

            FluentButton cancel = new FluentButton
            {
                Text = "取消",
                Location = new Point(400, 312),
                Size = new Size(92, 34),
                DialogResult = DialogResult.Cancel
            };

            ok.Click += delegate
            {
                Choice = optionA.Checked
                    ? RestoreChoice.DefaultMode
                    : RestoreChoice.SoftwareSettings;
            };

            Controls.Add(title);
            Controls.Add(divider);
            Controls.Add(header);
            Controls.Add(optionA);
            Controls.Add(hintA);
            Controls.Add(optionC);
            Controls.Add(hintC);
            Controls.Add(noteCard);
            Controls.Add(ok);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
