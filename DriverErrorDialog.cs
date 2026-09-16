using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ECController.Theme;

namespace ECController
{
    /// <summary>
    /// 驱动/EC 初始化失败时的详细错误对话框。
    /// 给用户可读的原因 + 排查步骤，并提供"复制错误信息"以便求助。
    /// </summary>
    internal sealed class DriverErrorDialog : Form
    {
        private readonly string _detailText;
        private readonly Exception _exception;

        public DriverErrorDialog(string detailText, Exception exception)
        {
            _detailText = detailText ?? string.Empty;
            _exception = exception;

            BuildUi();
        }

        private void BuildUi()
        {
            Text = "EC Controller - 初始化失败";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Font = FluentTheme.BodyFont;
            BackColor = FluentTheme.WindowBackground;
            ForeColor = FluentTheme.TextPrimary;
            ClientSize = new Size(620, 470);
            MinimumSize = new Size(520, 400);
            DoubleBuffered = true;

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = FluentTheme.WindowBackground,
                Padding = new Padding(24, 20, 24, 20)
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));

            // 标题：主标题 + 一句次要说明，模拟 Fluent 的 ContentDialog 头部
            Panel headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            Label title = new Label
            {
                Text = "无法访问 EC",
                Location = new Point(0, 0),
                Size = new Size(560, 28),
                Font = FluentTheme.TitleFont,
                ForeColor = FluentTheme.Error,
                BackColor = Color.Transparent
            };

            Label subtitle = new Label
            {
                Text = "驱动或 EC 设备初始化失败，请按下方提示逐项排查。",
                Location = new Point(2, 28),
                Size = new Size(560, 20),
                Font = FluentTheme.CaptionFont,
                ForeColor = FluentTheme.TextSecondary,
                BackColor = Color.Transparent
            };

            headerPanel.Controls.Add(title);
            headerPanel.Controls.Add(subtitle);

            FluentTextBox detail = new FluentTextBox
            {
                Dock = DockStyle.Fill,
                Text = _detailText
            };

            // 放进卡片里，用卡片的圆角浅描边替代原生 TextBox 的深色方框
            FluentCard detailCard = new FluentCard
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 12, 8, 12)
            };
            detailCard.Controls.Add(detail);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 12, 0, 0)
            };

            FluentButton ok = new FluentButton
            {
                Text = "确定",
                Accent = true,
                Size = new Size(92, 34),
                DialogResult = DialogResult.OK
            };

            FluentButton copy = new FluentButton
            {
                Text = "复制错误信息",
                Size = new Size(136, 34)
            };

            copy.Click += delegate { CopyDetailToClipboard(copy); };

            buttons.Controls.Add(ok);
            buttons.Controls.Add(copy);

            layout.Controls.Add(headerPanel, 0, 0);
            layout.Controls.Add(detailCard, 0, 1);
            layout.Controls.Add(buttons, 0, 2);

            Controls.Add(layout);

            // 让"确定"先拿到焦点，避免只读文本框一上来就被高亮
            ActiveControl = ok;
            AcceptButton = ok;
            CancelButton = ok;
        }

        private void CopyDetailToClipboard(Button source)
        {
            try
            {
                Clipboard.SetText(BuildCopyText());

                source.Text = "已复制";
                source.Enabled = false;
            }
            catch
            {
                source.Text = "复制失败";
            }
        }

        /// <summary>复制内容包含环境信息，方便排查。</summary>
        private string BuildCopyText()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("EC Controller 初始化失败");
            sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("系统：" + Environment.OSVersion.VersionString +
                " (" + (Environment.Is64BitOperatingSystem ? "64 位" : "32 位") + ")");
            sb.AppendLine("路径：" + AppDomain.CurrentDomain.BaseDirectory);
            sb.AppendLine();

            sb.AppendLine("【提示内容】");
            sb.AppendLine(_detailText);
            sb.AppendLine();

            if (_exception != null)
            {
                sb.AppendLine("【异常类型】");
                sb.AppendLine(_exception.GetType().FullName);
                sb.AppendLine();

                sb.AppendLine("【异常消息】");
                sb.AppendLine(_exception.Message);
                sb.AppendLine();

                if (!string.IsNullOrEmpty(_exception.StackTrace))
                {
                    sb.AppendLine("【调用栈】");
                    sb.AppendLine(_exception.StackTrace);
                }
            }

            return sb.ToString();
        }
    }
}
