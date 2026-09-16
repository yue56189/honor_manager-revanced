using System;
using System.Drawing;
using System.Windows.Forms;

namespace ECController.Services
{
    /// <summary>
    /// 系统托盘图标与右键菜单。
    ///
    /// 菜单项由外部通过回调注入，托盘本身不认识 EC —— 这样
    /// 主窗体换逻辑时不用改这里。
    /// </summary>
    public sealed class TrayManager : IDisposable
    {
        private NotifyIcon _icon;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _modeItem;
        private bool _disposed;

        /// <summary>双击托盘图标或点击"显示主界面"时触发。</summary>
        public event EventHandler ShowMainFormRequested;

        /// <summary>点击"重新应用"时触发。</summary>
        public event EventHandler ReapplyRequested;

        /// <summary>点击"退出"时触发。返回后由主窗体负责真正的关闭流程。</summary>
        public event EventHandler ExitRequested;

        /// <summary>托盘图标是否已经可见。</summary>
        public bool IsVisible
        {
            get { return _icon != null && _icon.Visible; }
        }

        public TrayManager()
        {
            _menu = new ContextMenuStrip();

            ToolStripMenuItem showItem = new ToolStripMenuItem("显示主界面");
            showItem.Font = new Font(showItem.Font, FontStyle.Bold);
            showItem.Click += delegate
            {
                EventHandler handler = ShowMainFormRequested;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            };

            _modeItem = new ToolStripMenuItem("当前模式：--");
            _modeItem.Enabled = false;

            ToolStripMenuItem reapplyItem = new ToolStripMenuItem("重新应用");
            reapplyItem.Click += delegate
            {
                EventHandler handler = ReapplyRequested;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate
            {
                EventHandler handler = ExitRequested;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            };

            _menu.Items.Add(showItem);
            _menu.Items.Add(_modeItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(reapplyItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(exitItem);

            _icon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "EC Controller",
                ContextMenuStrip = _menu,
                Visible = false
            };

            _icon.DoubleClick += delegate
            {
                EventHandler handler = ShowMainFormRequested;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            };
        }

        /// <summary>显示/隐藏托盘图标。</summary>
        public void SetVisible(bool visible)
        {
            if (_icon != null)
                _icon.Visible = visible;
        }

        /// <summary>更新菜单里显示的当前模式。</summary>
        public void SetCurrentMode(string modeName)
        {
            if (_modeItem == null)
                return;

            _modeItem.Text = "当前模式：" +
                (string.IsNullOrEmpty(modeName) ? "--" : modeName);
        }

        /// <summary>弹出气泡提示。失败（如系统禁用通知）时静默忽略。</summary>
        public void Notify(string title, string text, ToolTipIcon icon)
        {
            if (_icon == null)
                return;

            try
            {
                _icon.BalloonTipTitle = title;
                _icon.BalloonTipText = text;
                _icon.BalloonTipIcon = icon;
                _icon.ShowBalloonTip(3000);
            }
            catch
            {
                // 通知不可用时忽略
            }
        }

        /// <summary>
        /// 取图标：优先用 exe 自身嵌入的图标，取不到则退回系统默认图标。
        /// 这样即使没配 .ico 也不会崩。
        /// </summary>
        private static Icon LoadIcon()
        {
            try
            {
                Icon extracted = Icon.ExtractAssociatedIcon(StartupManager.ExecutablePath);

                if (extracted != null)
                    return extracted;
            }
            catch
            {
                // 落到下面的默认图标
            }

            return SystemIcons.Application;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_icon != null)
            {
                _icon.Visible = false;
                _icon.Dispose();
                _icon = null;
            }

            if (_menu != null)
            {
                _menu.Dispose();
                _menu = null;
            }
        }
    }
}
