using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ECController.Config;
using ECController.Driver;
using ECController.Models;
using ECController.Services;
using ECController.Theme;

namespace ECController
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// 界面上正在编辑的设置。改动先落在这里，"保存配置"才写盘。
        /// </summary>
        private readonly AppSettings _settings;

        /// <summary>上次成功保存的快照，用来判断"有没有未保存的更改"。</summary>
        private AppSettings _savedSettings;

        private readonly TrayManager _tray = new TrayManager();

        private EcService _ec;

        private ParseResult _parse;

        private List<EcGroup> _groups = new List<EcGroup>();

        private EcGroup _currentGroup;

        private EcMode _currentMode;

        /// <summary>配置里每个功能组在"启动后应用"区里对应的一行。</summary>
        private readonly List<BootGroupRow> _bootRows = new List<BootGroupRow>();

        /// <summary>防止"退出中"被托盘菜单重复触发。</summary>
        private bool _exiting;

        /// <summary>真正的退出（区别于最小化到托盘）。</summary>
        private bool _reallyExit;

        /// <summary>程序正在执行耗时 EC 操作或批量改控件状态，忽略控件事件的保存/脏标记。</summary>
        private bool _suppressSettingEvents;

        /// <summary>底纹用的窗口位置，用来判断窗口是否真的移动了。</summary>
        private Point _backdropOrigin = new Point(int.MinValue, int.MinValue);

        public MainForm(AppSettings settings)
        {
            _settings = settings ?? new AppSettings();
            _savedSettings = _settings.Clone();

            InitializeComponent();

            _tray.ShowMainFormRequested += OnTrayShow;
            _tray.ReapplyRequested += OnTrayReapply;
            _tray.ExitRequested += OnTrayExit;
        }

        #region 生命周期

        private void MainForm_Load(object sender, EventArgs e)
        {
            Text = string.Format(
                "EC Controller {0}",
                AssemblyVersionText);

            string effect = Mica.Apply(Handle, false, _settings.MicaTitleBar);

            Logger.Info("窗口特效：" + effect);

            LoadConfiguration();
            LoadSettingsIntoUi();
            InitializeEc();

            _tray.SetVisible(_settings.Tray);

            Logger.Info(string.Format(
                "字体：{0}（首选微软雅黑{1}）；开机启动={2}；登录自动应用={3}（{4} 项）。",
                FluentTheme.FontFamilyName,
                FluentTheme.UsingPreferredFont ? "已生效" : "不可用，回退",
                _settings.AutoStart,
                _settings.ApplyOnBoot,
                _settings.BootSelections.Count));
        }

        /// <summary>
        /// 客户区背景。
        ///
        /// 这里曾经贴一张"壁纸强模糊 + 浅色光罩"的位图来模拟 Mica。已改成纯色垂直渐变，
        /// 原因是实测它太贵（PerfProbe，Windows 11 26200 / DPI 125%）：
        ///   · 合成一张 548x804 的底纹要 49.5 ms，而窗口每移动 8px 就会重做一次
        ///     → 拖动窗口 87 ms/帧，约 7 FPS，肉眼可见地卡
        ///   · 位图 1:1 贴回来还要再花 7.1 ms/帧
        /// 渐变则是一次填充，且移动、缩放窗口都不需要重算，观感差别很小。
        /// </summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 渐变端点必须按整个客户区算，填充范围才是当前要重画的那块；
            // 否则局部重绘时颜色会跳。见 FluentTheme.PaintWindowBackground。
            FluentTheme.PaintWindowBackground(e.Graphics, ClientRectangle, e.ClipRectangle);
        }

        /// <summary>
        /// 版本号文本。优先读 exe 的文件版本（与 csproj/AssemblyInfo 一致），
        /// 读不到时退回程序集版本。这样标题栏在发布版里显示真实版本。
        /// </summary>
        private static string AssemblyVersionText
        {
            get
            {
                try
                {
                    string path = StartupManager.ExecutablePath;
                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);

                    Version v = new Version(info.FileVersion ?? "0.0.0");

                    return string.Format("v{0}.{1}.{2}", v.Major, v.Minor, v.Build);
                }
                catch
                {
                    // 落到程序集版本
                }

                Version asm = typeof(MainForm).Assembly.GetName().Version;

                return asm == null
                    ? string.Empty
                    : string.Format("v{0}.{1}.{2}", asm.Major, asm.Minor, asm.Build);
            }
        }

        /// <summary>读取 ec ADDRESS.txt 并填充功能/模式下拉框，同时重建启动应用行。</summary>
        private void LoadConfiguration()
        {
            _parse = AddressParser.Load();

            _groups = _parse.Groups;

            cmbGroup.Items.Clear();

            foreach (EcGroup group in _groups)
                cmbGroup.Items.Add(group.Name);

            BuildBootGroupRows();

            if (cmbGroup.Items.Count > 0)
            {
                // 主动触发一次联动，而不是依赖 SelectedIndexChanged ——
                // 下拉框本来就是空的时候 SelectedIndex 从 -1 变 0 才会触发事件，
                // 若之前已经选中过 0，再次赋 0 不会触发，模式列表就会是空的。
                cmbGroup.SelectedIndex = 0;
                PopulateModesForCurrentGroup();
            }
            else
            {
                _currentGroup = null;
                _currentMode = null;
                cmbMode.Items.Clear();
                dgvPreview.Rows.Clear();

                SetStatus("未找到任何 EC 配置，请检查 Data\\ec ADDRESS.txt");
            }

            // 格式问题要提示用户，而不是静默跳过
            if (_parse.Warnings.Count > 0)
            {
                string message = _parse.FileFound
                    ? "配置文件有以下问题，相关行已被忽略：\n\n"
                    : "配置文件读取失败。\n\n";

                message += string.Join("\n", _parse.Warnings.ToArray());

                MessageBox.Show(
                    this,
                    message,
                    "配置解析提示",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        /// <summary>初始化驱动与 EC，失败时给可操作的提示。</summary>
        private void InitializeEc()
        {
            try
            {
                _ec = new EcService(_settings.WaitTime);
                _ec.Initialize();

                SetStatus("EC 已连接");
                SetEcControlsEnabled(true);
            }
            catch (Exception ex)
            {
                SetStatus("EC 未连接");
                SetEcControlsEnabled(false);

                Logger.Error("EC 初始化失败。", ex);

                ShowEcInitFailure(ex);
            }
        }

        /// <summary>
        /// 驱动初始化失败的详细提示：给出原因和可执行的排查步骤，
        /// 而不是只丢一个错误码。
        /// </summary>
        private void ShowEcInitFailure(Exception ex)
        {
            DriverLoadException driverEx = ex as DriverLoadException;

            string text;

            if (driverEx != null)
            {
                text = "驱动初始化失败。\n\n" +
                    "原因：" + driverEx.Message;
            }
            else
            {
                text = "EC 通信初始化失败。\n\n" +
                    "原因：" + ex.Message;
            }

            using (DriverErrorDialog dialog = new DriverErrorDialog(text, ex))
            {
                dialog.ShowDialog(this);
            }
        }

        private void SetEcControlsEnabled(bool enabled)
        {
            btnApply.Enabled = enabled;
            btnRead.Enabled = enabled;

            // 只读控件始终可用，方便用户先看配置
            cmbGroup.Enabled = enabled;
            cmbMode.Enabled = enabled;

            if (!enabled)
                dgvPreview.Rows.Clear();
        }

        private void SetStatus(string text)
        {
            toolStripStatusLabel1.Text = text;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        /// <summary>释放 EC 句柄。</summary>
        private void ReleaseEc()
        {
            if (_ec != null)
            {
                _ec.Dispose();
                _ec = null;
            }
        }

        #endregion

        #region 选择联动

        private void cmbGroup_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbGroup.SelectedIndex < 0 || cmbGroup.SelectedIndex >= _groups.Count)
            {
                _currentGroup = null;
                _currentMode = null;
                cmbMode.Items.Clear();
                dgvPreview.Rows.Clear();
                return;
            }

            _currentGroup = _groups[cmbGroup.SelectedIndex];

            PopulateModesForCurrentGroup();
        }

        /// <summary>
        /// 按 _currentGroup 重建模式下拉框。
        /// 与选中事件分开，便于配置加载后主动调用。
        /// </summary>
        private void PopulateModesForCurrentGroup()
        {
            cmbMode.Items.Clear();

            if (_currentGroup == null)
            {
                _currentMode = null;
                dgvPreview.Rows.Clear();
                return;
            }

            foreach (EcMode mode in _currentGroup.Modes)
                cmbMode.Items.Add(mode.Name);

            if (cmbMode.Items.Count > 0)
            {
                cmbMode.SelectedIndex = 0;

                // 同上：赋 0 不一定触发事件，这里直接同步一次
                _currentMode = _currentGroup.Modes.Count > 0
                    ? _currentGroup.Modes[0]
                    : null;

                RefreshPreview();
            }
            else
            {
                _currentMode = null;
                dgvPreview.Rows.Clear();
            }
        }

        private void cmbMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_currentGroup == null)
                return;

            if (cmbMode.SelectedIndex < 0 ||
                cmbMode.SelectedIndex >= _currentGroup.Modes.Count)
            {
                return;
            }

            _currentMode = _currentGroup.Modes[cmbMode.SelectedIndex];

            RefreshPreview();
        }

        /// <summary>用当前模式的地址重建表格（当前值/状态先留空）。</summary>
        private void RefreshPreview()
        {
            dgvPreview.Rows.Clear();

            if (_currentMode == null)
                return;

            foreach (EcItem item in _currentMode.Items)
            {
                dgvPreview.Rows.Add(
                    string.Format("0x{0:X2}", item.Address),
                    string.Format("0x{0:X2}", item.Value),
                    "--",
                    "未读取");
            }
        }

        #endregion

        #region 读取 / 应用

        private async void btnRead_Click(object sender, EventArgs e)
        {
            if (!EnsureModeSelected())
                return;

            await RunEcOperationAsync("正在读取 EC...", delegate
            {
                List<EcItemResult> results = _ec.Verify(_currentMode);
                UpdateGridFromResults(results);

                int mismatch = 0;

                foreach (EcItemResult result in results)
                {
                    if (result.HasActual && !result.Matches)
                        mismatch++;
                }

                return mismatch == 0
                    ? "读取完成，当前值与目标值一致"
                    : string.Format("读取完成，{0} 个地址与目标值不一致", mismatch);
            });
        }

        private async void btnApply_Click(object sender, EventArgs e)
        {
            if (!EnsureModeSelected())
                return;

            DialogResult confirm = MessageBox.Show(
                this,
                string.Format(
                    "确定要把 EC 设置为 {0} / {1} 吗？\n\n共 {2} 个寄存器将被写入，写入后会立即回读校验。",
                    _currentGroup == null ? "-" : _currentGroup.Name,
                    _currentMode.Name,
                    _currentMode.Items.Count),
                "确认应用",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            ApplyResult applyResult = null;

            await RunEcOperationAsync("正在应用并校验...", delegate
            {
                applyResult = _ec.Apply(_currentMode);
                UpdateGridFromResults(applyResult.Items);

                // 应用成功时把当前模式记进托盘菜单
                if (applyResult.Success)
                    _tray.SetCurrentMode(_currentMode.Name);

                return applyResult.Message;
            });

            if (applyResult == null)
                return;

            if (applyResult.Success)
            {
                MessageBox.Show(
                    this,
                    applyResult.Message,
                    "应用成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    this,
                    applyResult.Message + "\n\n详细过程已写入 Logs 目录下的日志。",
                    "应用未完全成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// 统一的耗时操作包装：禁用按钮、切换忙碌光标、捕获异常、
        /// 恢复界面。EC 读写是阻塞的串口式操作，放后台线程避免界面假死。
        /// </summary>
        private async Task RunEcOperationAsync(string busyText, Func<string> operation)
        {
            bool previousApply = btnApply.Enabled;
            bool previousRead = btnRead.Enabled;

            btnApply.Enabled = false;
            btnRead.Enabled = false;

            string previousStatus = toolStripStatusLabel1.Text;
            Cursor previousCursor = Cursor;

            Cursor = Cursors.WaitCursor;
            SetStatus(busyText);

            try
            {
                string resultStatus = await Task.Run(operation);

                SetStatus(resultStatus ?? previousStatus);
            }
            catch (Exception ex)
            {
                Logger.Error("EC 操作失败。", ex);

                SetStatus("操作失败");

                MessageBox.Show(
                    this,
                    "EC 操作失败：\n\n" + ex.Message +
                    "\n\n详细过程已写入 Logs 目录下的日志。",
                    "操作失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = previousCursor;

                btnApply.Enabled = previousApply;
                btnRead.Enabled = previousRead;
            }
        }

        /// <summary>把读写结果刷进表格的"当前值/状态"两列。</summary>
        private void UpdateGridFromResults(List<EcItemResult> results)
        {
            if (results == null)
                return;

            for (int i = 0; i < results.Count && i < dgvPreview.Rows.Count; i++)
            {
                EcItemResult result = results[i];
                DataGridViewRow row = dgvPreview.Rows[i];

                if (result.HasActual)
                {
                    row.Cells[2].Value = string.Format("0x{0:X2}", result.Actual);

                    if (result.Matches)
                    {
                        row.Cells[3].Value = "✔ 一致";
                        row.Cells[3].Style.ForeColor = FluentTheme.Success;
                    }
                    else
                    {
                        row.Cells[3].Value = "✘ 不一致";
                        row.Cells[3].Style.ForeColor = FluentTheme.Error;
                    }
                }
                else if (result.Error != null)
                {
                    row.Cells[2].Value = "--";
                    row.Cells[3].Value = "✘ 失败";
                    row.Cells[3].Style.ForeColor = FluentTheme.Error;
                }
                else
                {
                    row.Cells[2].Value = "--";
                    row.Cells[3].Value = "未读取";
                    row.Cells[3].Style.ForeColor = FluentTheme.TextSecondary;
                }
            }
        }

        private bool EnsureModeSelected()
        {
            if (_currentMode != null)
                return true;

            MessageBox.Show(
                this,
                "请先选择一个功能组和模式。",
                "提示",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return false;
        }

        private void btnRestore_Click(object sender, EventArgs e)
        {
            // "恢复默认"含义容易混淆，这里明确让用户选择恢复什么
            using (RestoreDialog dialog = new RestoreDialog(_currentGroup, _currentMode))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                switch (dialog.Choice)
                {
                    case RestoreDialog.RestoreChoice.DefaultMode:
                        RestoreToDefaultMode();
                        break;

                    case RestoreDialog.RestoreChoice.SoftwareSettings:
                        RestoreSoftwareSettings();
                        break;
                }
            }
        }

        /// <summary>方案 A：把 EC 恢复到配置文件中定义的默认模式（第一个功能组的第一个模式）。</summary>
        private void RestoreToDefaultMode()
        {
            EcMode target = FindFirstMode();

            if (target == null)
            {
                MessageBox.Show(
                    this,
                    "配置文件里没有可用于恢复的默认模式。",
                    "无法恢复",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            DialogResult confirm = MessageBox.Show(
                this,
                string.Format(
                    "将把 EC 恢复到配置中的第一个模式：{0}。\n\n确定继续吗？",
                    target.Name),
                "恢复默认模式",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            if (_ec == null || !_ec.IsReady)
            {
                MessageBox.Show(
                    this,
                    "EC 尚未连接，无法执行恢复。",
                    "无法恢复",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            try
            {
                ApplyResult result = _ec.Apply(target);

                SetStatus(result.Success
                    ? "已恢复默认模式：" + target.Name
                    : "恢复默认模式未完全成功");

                _tray.SetCurrentMode(target.Name);

                MessageBox.Show(
                    this,
                    result.Message,
                    result.Success ? "恢复成功" : "恢复未完全成功",
                    MessageBoxButtons.OK,
                    result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Logger.Error("恢复默认模式失败。", ex);

                MessageBox.Show(
                    this,
                    "恢复默认模式失败：\n\n" + ex.Message,
                    "恢复失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private EcMode FindFirstMode()
        {
            foreach (EcGroup group in _groups)
            {
                if (group.Modes.Count > 0)
                    return group.Modes[0];
            }

            return null;
        }

        /// <summary>方案 C：把软件设置恢复为默认值。</summary>
        private void RestoreSoftwareSettings()
        {
            DialogResult confirm = MessageBox.Show(
                this,
                "将把软件设置恢复为默认值：\n\n" +
                "    开机启动         = 关闭\n" +
                "    启动后应用       = 关闭\n" +
                "    各功能组的选择   = 清空\n" +
                "    最小化到托盘     = 关闭\n\n" +
                "注意：这不会修改任何 EC 寄存器。确定继续吗？",
                "恢复软件设置",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            _settings.AutoStart = false;
            _settings.ApplyOnBoot = false;
            _settings.Tray = false;
            _settings.ClearBootSelections();
            _settings.BootDelaySeconds = 5;
            _settings.WaitTime = 5;

            LoadSettingsIntoUi();

            _tray.SetVisible(false);

            // 复原动作也走同一条保存链路，用户能看到注册表是否真的同步了
            SaveAndReport();

            SetStatus("软件设置已恢复默认");
        }

        #endregion

        #region 启动后应用：每个功能组一行

        /// <summary>配置里一个功能组在"启动后应用"区里对应的一行。</summary>
        private sealed class BootGroupRow
        {
            public string GroupName;
            public FluentCheckBox Check;
            public FluentComboBox Combo;
            public EcGroup Group;
        }

        /// <summary>
        /// 按 Data\ec ADDRESS.txt 里定义的功能组动态生成行。
        /// 这样追加 [电池管理] 之类的组不用改代码——配置驱动的原则。
        /// </summary>
        private void BuildBootGroupRows()
        {
            for (int i = pnlBootGroups.Controls.Count - 1; i >= 0; i--)
            {
                Control old = pnlBootGroups.Controls[i];
                pnlBootGroups.Controls.RemoveAt(i);
                old.Dispose();
            }

            _bootRows.Clear();

            foreach (EcGroup group in _groups)
            {
                BootGroupRow row = new BootGroupRow();
                row.Group = group;
                row.GroupName = group.Name;

                row.Check = new FluentCheckBox();
                row.Check.Text = group.Name;
                row.Check.AutoSize = false;
                row.Check.CheckedChanged += BootRow_Changed;

                row.Combo = new FluentComboBox();
                row.Combo.DropDownStyle = ComboBoxStyle.DropDownList;

                foreach (EcMode mode in group.Modes)
                    row.Combo.Items.Add(mode.Name);

                if (row.Combo.Items.Count > 0)
                    row.Combo.SelectedIndex = 0;

                row.Combo.Enabled = false;
                row.Combo.SelectedIndexChanged += BootRow_Changed;

                pnlBootGroups.Controls.Add(row.Check);
                pnlBootGroups.Controls.Add(row.Combo);

                _bootRows.Add(row);
            }

            LayoutBootGroupRows();
        }

        private void LayoutBootGroupRows()
        {
            int y = 0;
            int rowHeight = 34;

            foreach (BootGroupRow row in _bootRows)
            {
                // 行高按控件实际高度推，避免 DPI 缩放后文字被截
                rowHeight = Math.Max(row.Check.Height, row.Combo.PreferredHeight) + 8;

                row.Check.Location = new Point(0, y + 2);
                row.Check.Size = new Size(170, row.Check.Height);

                row.Combo.Location = new Point(174, y);
                row.Combo.Size = new Size(
                    Math.Max(120, pnlBootGroups.Width - 174),
                    row.Combo.PreferredHeight);

                y += rowHeight;
            }

            pnlBootGroups.Height = Math.Max(rowHeight, y);

            lblBootGroupsHint.Visible = _bootRows.Count > 0;

            if (_bootRows.Count == 0)
                pnlBootGroups.Height = 0;

            LayoutForm();
        }

        /// <summary>
        /// 卡片与下方按钮跟随动态行数一起长高。
        /// 全部用相对位置算，DPI 缩放后也不会错位。
        /// </summary>
        private void LayoutForm()
        {
            cardOptions.Height = pnlBootGroups.Bottom + FluentTheme.CardPadding;

            pnlSave.Top = cardOptions.Bottom + FluentTheme.Gap;
            pnlActions.Top = pnlSave.Bottom + FluentTheme.Gap;

            int bottom = pnlActions.Bottom + FluentTheme.Gap + statusStrip1.Height;

            if (ClientSize.Height != bottom)
                ClientSize = new Size(ClientSize.Width, bottom);
        }

        /// <summary>某一行的勾选或模式发生变化：同步到内存设置并标记未保存。</summary>
        private void BootRow_Changed(object sender, EventArgs e)
        {
            if (_suppressSettingEvents)
                return;

            SyncBootRowsToSettings();
            UpdateBootRowsEnabled();
            MarkDirty();
        }

        /// <summary>把界面上的勾选/模式收集进 _settings.BootSelections。</summary>
        private void SyncBootRowsToSettings()
        {
            _settings.ClearBootSelections();

            foreach (BootGroupRow row in _bootRows)
            {
                if (!row.Check.Checked)
                    continue;

                string mode = row.Combo.SelectedItem == null
                    ? null
                    : row.Combo.SelectedItem.ToString();

                if (string.IsNullOrEmpty(mode))
                    continue;

                _settings.BootSelections.Add(new BootSelection(row.GroupName, mode));
            }
        }

        /// <summary>总开关关闭时，所有行都不可交互。</summary>
        private void UpdateBootRowsEnabled()
        {
            bool on = chkApplyBoot.Checked;

            foreach (BootGroupRow row in _bootRows)
            {
                row.Check.Enabled = on;
                row.Combo.Enabled = on && row.Check.Checked;
            }

            lblBootGroupsHint.Visible = _bootRows.Count > 0;

            if (on && CountCheckedRows() == 0 && _bootRows.Count > 0)
                lblBootGroupsHint.Text = "已启用，但没有勾选任何功能组 —— 保存后登录时不会套用任何配置。";
            else
                lblBootGroupsHint.Text = "每个功能组可分别选择要套用的模式：";
        }

        private int CountCheckedRows()
        {
            int n = 0;

            foreach (BootGroupRow row in _bootRows)
            {
                if (row.Check.Checked)
                    n++;
            }

            return n;
        }

        #endregion

        #region 设置项

        /// <summary>把 _settings 反映到界面控件上。</summary>
        private void LoadSettingsIntoUi()
        {
            _suppressSettingEvents = true;

            try
            {
                chkAutoStart.Checked = _settings.AutoStart;
                chkApplyBoot.Checked = _settings.ApplyOnBoot;
                chkTray.Checked = _settings.Tray;
                numBootDelay.Value = Clamp(
                    _settings.BootDelaySeconds,
                    (int)numBootDelay.Minimum,
                    (int)numBootDelay.Maximum);

                foreach (BootGroupRow row in _bootRows)
                {
                    string mode = _settings.GetBootMode(row.GroupName);
                    bool selected = !string.IsNullOrEmpty(mode) && row.Combo.Items.Count > 0;

                    if (selected)
                    {
                        int index = row.Combo.Items.IndexOf(mode);

                        if (index < 0)
                        {
                            // 配置里记的模式已从 ec ADDRESS.txt 删掉：保留组选中，
                            // 但选回落第一个模式，并在状态栏说明。
                            Logger.Warn(string.Format(
                                "配置里记录的 [{0}] / {1} 已不存在，已改为第一个可用模式。",
                                row.GroupName,
                                mode));

                            index = 0;
                        }

                        row.Combo.SelectedIndex = index;
                    }

                    row.Check.Checked = selected;
                }
            }
            finally
            {
                _suppressSettingEvents = false;
            }

            // 勾了总开关但一个组都没选（比如旧配置迁移过来只剩空列表）：
            // 默认把第一个功能组选上，避免"看起来开了其实什么都没做"。
            if (_settings.ApplyOnBoot && CountCheckedRows() == 0 && _bootRows.Count > 0)
            {
                _suppressSettingEvents = true;

                try
                {
                    _bootRows[0].Check.Checked = true;
                }
                finally
                {
                    _suppressSettingEvents = false;
                }

                SyncBootRowsToSettings();
            }

            UpdateBootRowsEnabled();
            UpdateSaveState();
        }

        private void chkAutoStart_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressSettingEvents)
                return;

            _settings.AutoStart = chkAutoStart.Checked;

            MarkDirty();
        }

        private void chkApplyBoot_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressSettingEvents)
                return;

            _settings.ApplyOnBoot = chkApplyBoot.Checked;

            bool hasCheckedRow = CountCheckedRows() > 0;

            _suppressSettingEvents = true;

            try
            {
                if (_settings.ApplyOnBoot && !hasCheckedRow && _bootRows.Count > 0)
                    _bootRows[0].Check.Checked = true;

                if (!_settings.ApplyOnBoot)
                {
                    foreach (BootGroupRow row in _bootRows)
                        row.Check.Checked = false;
                }
            }
            finally
            {
                _suppressSettingEvents = false;
            }

            SyncBootRowsToSettings();
            UpdateBootRowsEnabled();
            MarkDirty();
        }

        private void chkTray_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressSettingEvents)
                return;

            _settings.Tray = chkTray.Checked;
            _tray.SetVisible(_settings.Tray);

            MarkDirty();
        }

        private void numBootDelay_ValueChanged(object sender, EventArgs e)
        {
            if (_suppressSettingEvents)
                return;

            _settings.BootDelaySeconds = (int)numBootDelay.Value;

            MarkDirty();
        }

        /// <summary>界面上的改动还没写盘时用一个显眼的标记提示。</summary>
        private void MarkDirty()
        {
            UpdateSaveState();
        }

        private bool IsDirty
        {
            get { return !_settings.SameAs(_savedSettings); }
        }

        private void UpdateSaveState()
        {
            if (IsDirty)
            {
                lblSaveState.ForeColor = FluentTheme.Warning;

                // 明确折行：这行字不短，靠 Label 自动换行会随 DPI/字体变化而截断
                lblSaveState.Text =
                    "有未保存的更改 —— 请点「保存配置」\n" +
                    "将写入 config.json 并同步开机启动项";
            }
            else
            {
                lblSaveState.ForeColor = FluentTheme.TextSecondary;
                lblSaveState.Text = "设置与 config.json 一致";
            }
        }

        /// <summary>
        /// 「保存配置」：写 config.json -> 同步 HKCU Run -> 回读校验，
        /// 三项结果分别反馈，不做"看起来成功了"的模糊提示。
        /// </summary>
        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            SyncBootRowsToSettings();
            SaveAndReport();
        }

        private void SaveAndReport()
        {
            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;

            try
            {
                // 1) 写 config.json
                bool configOk = SettingsManager.Save(_settings);

                // 2) 同步开机启动项，并回读校验
                bool startupOk = StartupManager.Apply(_settings.AutoStart);
                string actual = StartupManager.ReadCommand();

                bool allOk = IsSaveReportClean(configOk, startupOk, _settings, actual);
                List<string> lines = BuildSaveReport(_settings, configOk, startupOk, actual);

                if (allOk)
                    _savedSettings = _settings.Clone();

                UpdateSaveState();

                SetStatus(allOk ? "配置已保存" : "配置保存未完全成功");

                string title = allOk ? "配置已保存" : "配置保存未完全成功";

                MessageBox.Show(
                    this,
                    string.Join("\n", lines.ToArray()),
                    title,
                    MessageBoxButtons.OK,
                    allOk ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = previous;
            }
        }

        /// <summary>
        /// 组装「保存配置」的反馈文本。
        ///
        /// 刻意做成静态纯函数：不碰界面、不碰注册表、不弹窗，
        /// 这样"保存后的反馈"本身可以被自测覆盖（三种状态：写盘、开机启动项、登录后行为），
        /// 而不是只能靠人眼看一遍。
        /// </summary>
        internal static List<string> BuildSaveReport(
            AppSettings settings,
            bool configOk,
            bool startupOk,
            string actualCommand)
        {
            List<string> lines = new List<string>();

            if (configOk)
            {
                lines.Add("config.json　写入成功");
                lines.Add("　　" + SettingsManager.SettingsPath);
            }
            else
            {
                lines.Add("config.json　写入失败，详见 Logs\\ 下的日志");
            }

            if (!startupOk)
            {
                lines.Add("开机启动项　写入失败，详见 Logs\\ 下的日志");
            }
            else if (settings.AutoStart)
            {
                lines.Add("开机启动项　已写入 HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run");
                lines.Add("　　ECController = " + (actualCommand ?? "(读回为空)"));
                lines.Add(StartupCommandMatches(actualCommand)
                    ? "　　回读校验：与预期一致"
                    : "　　回读校验：与预期不一致！");
            }
            else
            {
                lines.Add("开机启动项　已移除（注册表里已无 ECController 项）");
            }

            if (!settings.ApplyOnBoot)
            {
                lines.Add("登录后　不自动应用任何配置");
            }
            else if (settings.BootSelections.Count == 0)
            {
                lines.Add("登录后　已启用自动应用，但没有勾选任何功能组 —— 实际不会写入 EC");
            }
            else
            {
                StringBuilder sb = new StringBuilder();

                foreach (BootSelection sel in settings.BootSelections)
                {
                    if (sb.Length > 0)
                        sb.Append("、");

                    sb.Append(sel.ToString());
                }

                lines.Add(string.Format(
                    "登录后　{0} 秒后应用：{1}",
                    settings.BootDelaySeconds,
                    sb.ToString()));
            }

            return lines;
        }

        /// <summary>三步（写盘 / 注册表 / 回读）是否全部成功。与 BuildSaveReport 配对。</summary>
        internal static bool IsSaveReportClean(
            bool configOk,
            bool startupOk,
            AppSettings settings,
            string actualCommand)
        {
            if (!configOk || !startupOk)
                return false;

            // 关闭开机启动时不看回读（Disable 后本来就该是空）
            if (!settings.AutoStart)
                return true;

            return StartupCommandMatches(actualCommand);
        }

        private static bool StartupCommandMatches(string actualCommand)
        {
            return string.Equals(
                actualCommand,
                StartupManager.ExpectedCommand,
                StringComparison.Ordinal);
        }

        #endregion

        #region 关闭 / 托盘

        private void btnExit_Click(object sender, EventArgs e)
        {
            RequestExit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 勾了托盘且不是真的要退出 -> 隐藏而不关闭
            if (!_reallyExit && _settings.Tray &&
                e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;

                Hide();
                _tray.SetVisible(true);

                return;
            }

            // 用户直接关窗口时别把改动悄悄丢了
            if (!_reallyExit && e.CloseReason == CloseReason.UserClosing && IsDirty)
            {
                DialogResult answer = MessageBox.Show(
                    this,
                    "设置还有未保存的更改。\n\n要不要先保存再退出？",
                    "未保存的更改",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (answer == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }

                if (answer == DialogResult.Yes)
                {
                    SyncBootRowsToSettings();
                    SaveAndReport();

                    // 保存链路整体失败时不退出，让用户看到问题
                    if (IsDirty)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }

            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _tray.Dispose();
            ReleaseEc();

            Logger.Info("===== EC Controller 退出 =====");

            base.OnFormClosed(e);
        }

        /// <summary>请求真正退出程序。</summary>
        private void RequestExit()
        {
            if (_exiting)
                return;

            _exiting = true;
            _reallyExit = true;

            Close();
        }

        private void OnTrayShow(object sender, EventArgs e)
        {
            ShowFromTray();
        }

        private void ShowFromTray()
        {
            Show();

            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;

            Activate();
            BringToFront();
        }

        private void OnTrayReapply(object sender, EventArgs e)
        {
            if (!EnsureModeSelected())
                return;

            // 复用界面的应用按钮逻辑
            btnApply_Click(this, EventArgs.Empty);
        }

        private void OnTrayExit(object sender, EventArgs e)
        {
            RequestExit();
        }

        #endregion
    }
}
