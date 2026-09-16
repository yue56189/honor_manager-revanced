using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ECController.Config;
using ECController.Driver;
using ECController.Models;
using ECController.Services;

namespace ECController
{
    public partial class MainForm : Form
    {
        private readonly AppSettings _settings;

        private readonly TrayManager _tray = new TrayManager();

        private EcService _ec;

        private ParseResult _parse;

        private List<EcGroup> _groups = new List<EcGroup>();

        private EcGroup _currentGroup;

        private EcMode _currentMode;

        /// <summary>防止"退出中"被托盘菜单重复触发。</summary>
        private bool _exiting;

        /// <summary>真正的退出（区别于最小化到托盘）。</summary>
        private bool _reallyExit;

        /// <summary>程序正在执行耗时 EC 操作，忽略勾选框变更的保存。</summary>
        private bool _suppressSettingEvents;

        public MainForm(AppSettings settings)
        {
            _settings = settings ?? new AppSettings();

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

            LoadSettingsIntoUi();
            LoadConfiguration();
            InitializeEc();

            _tray.SetVisible(_settings.Tray);
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

        /// <summary>把 settings 反映到界面控件上。</summary>
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
            }
            finally
            {
                _suppressSettingEvents = false;
            }
        }

        /// <summary>读取 ec ADDRESS.txt 并填充功能/模式下拉框。</summary>
        private void LoadConfiguration()
        {
            _parse = AddressParser.Load();

            _groups = _parse.Groups;

            cmbGroup.Items.Clear();

            foreach (EcGroup group in _groups)
                cmbGroup.Items.Add(group.Name);

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

            // 自动应用模式列表同样依赖 _groups，必须在配置读完后立刻填充
            RefreshBootModeChoices();

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
                        row.Cells[3].Style.ForeColor = Theme.FluentTheme.Success;
                    }
                    else
                    {
                        row.Cells[3].Value = "✘ 不一致";
                        row.Cells[3].Style.ForeColor = Theme.FluentTheme.Error;
                    }
                }
                else if (result.Error != null)
                {
                    row.Cells[2].Value = "--";
                    row.Cells[3].Value = "✘ 失败";
                    row.Cells[3].Style.ForeColor = Theme.FluentTheme.Error;
                }
                else
                {
                    row.Cells[2].Value = "--";
                    row.Cells[3].Value = "未读取";
                    row.Cells[3].Style.ForeColor = Theme.FluentTheme.TextSecondary;
                }
            }
        }

        private bool EnsureModeSelected()
        {
            if (_currentMode != null)
                return true;

            MessageBox.Show(
                this,
                "请先选择一个功能和模式。",
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
                "    开机启动     = 关闭\n" +
                "    登录自动应用 = 关闭\n" +
                "    最小化到托盘 = 关闭\n\n" +
                "注意：这不会修改任何 EC 寄存器。确定继续吗？",
                "恢复软件设置",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            _settings.AutoStart = false;
            _settings.ApplyOnBoot = false;
            _settings.Tray = false;
            _settings.BootGroup = string.Empty;
            _settings.BootMode = string.Empty;
            _settings.BootDelaySeconds = 5;
            _settings.WaitTime = 5;

            StartupManager.Disable();

            LoadSettingsIntoUi();
            RefreshBootModeChoices();
            SaveSettings();

            _tray.SetVisible(false);

            SetStatus("软件设置已恢复默认");

            MessageBox.Show(
                this,
                "软件设置已恢复为默认值。",
                "完成",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        #endregion

        #region 设置项

        private void chkAutoStart_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressSettingEvents)
                return;

            bool enabled = chkAutoStart.Checked;

            if (!StartupManager.Apply(enabled))
            {
                MessageBox.Show(
                    this,
                    "写入开机启动项失败，详见日志。",
                    "设置失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                // 回滚勾选状态以反映真实情况
                _suppressSettingEvents = true;
                chkAutoStart.Checked = StartupManager.IsEnabled();
                _suppressSettingEvents = false;

                return;
            }

            _settings.AutoStart = enabled;
            SaveSettings();
            SetStatus(enabled ? "已启用开机启动" : "已关闭开机启动");
        }

        private void chkApplyBoot_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressSettingEvents)
                return;

            _settings.ApplyOnBoot = chkApplyBoot.Checked;

            RefreshBootModeChoices();
            SaveSettings();

            SetStatus(chkApplyBoot.Checked
                ? "已启用登录自动应用"
                : "已关闭登录自动应用");
        }

        private void chkTray_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressSettingEvents)
                return;

            _settings.Tray = chkTray.Checked;
            _tray.SetVisible(_settings.Tray);

            SaveSettings();

            SetStatus(chkTray.Checked
                ? "已启用最小化到托盘"
                : "已关闭最小化到托盘");
        }

        private void cmbBootMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressSettingEvents)
                return;

            string groupName;
            EcMode mode = ResolveBootSelection(out groupName);

            if (mode == null)
                return;

            _settings.BootGroup = groupName;
            _settings.BootMode = mode.Name;

            SaveSettings();
        }

        private void numBootDelay_ValueChanged(object sender, EventArgs e)
        {
            if (_suppressSettingEvents)
                return;

            _settings.BootDelaySeconds = (int)numBootDelay.Value;
            SaveSettings();
        }

        /// <summary>
        /// 重建"自动应用模式"下拉框。列表是"功能组 / 模式"的扁平组合，
        /// 因为自动应用需要同时知道组和模式。
        /// </summary>
        private void RefreshBootModeChoices()
        {
            _suppressSettingEvents = true;

            try
            {
                cmbBootMode.Items.Clear();

                foreach (EcGroup group in _groups)
                {
                    foreach (EcMode mode in group.Modes)
                    {
                        cmbBootMode.Items.Add(
                            new BootModeEntry(group.Name, mode.Name));
                    }
                }

                cmbBootMode.Enabled = chkApplyBoot.Checked &&
                    cmbBootMode.Items.Count > 0;

                // 禁用状态下 WinForms 会把下拉框画成灰蒙蒙一片，
                // 看起来像"空的"。这里同步给出提示文字，让用户明白为什么不能选。
                if (!chkApplyBoot.Checked)
                {
                    cmbBootMode.Items.Insert(0, DisabledBootModeHint);

                    // 必须真的选中占位项，否则禁用状态下框里什么都不显示
                    cmbBootMode.SelectedIndex = 0;
                }

                // 选中已保存的那一项
                int index = FindBootEntryIndex();

                if (index >= 0)
                {
                    cmbBootMode.SelectedIndex = index;
                }
                else if (chkApplyBoot.Checked && cmbBootMode.Items.Count > 0)
                {
                    // 跳过占位提示项（勾选时本来就不该有占位项，这里只做保护）
                    cmbBootMode.SelectedIndex =
                        cmbBootMode.Items[0] is BootModeEntry ? 0 : 1;
                }
            }
            finally
            {
                _suppressSettingEvents = false;
            }
        }

        /// <summary>未启用自动应用时下拉框里的占位文字。</summary>
        private const string DisabledBootModeHint = "（先勾选\"登录自动应用\"）";

        private int FindBootEntryIndex()
        {
            for (int i = 0; i < cmbBootMode.Items.Count; i++)
            {
                BootModeEntry entry = cmbBootMode.Items[i] as BootModeEntry;

                // 占位提示项不是 BootModeEntry，自然被跳过
                if (entry == null)
                    continue;

                if (entry.GroupName == _settings.BootGroup &&
                    entry.ModeName == _settings.BootMode)
                {
                    return i;
                }
            }

            return -1;
        }

        private EcMode ResolveBootSelection(out string groupName)
        {
            groupName = null;

            BootModeEntry entry = cmbBootMode.SelectedItem as BootModeEntry;

            // 选中的是占位提示项时视为未选择
            if (entry == null)
                return null;

            groupName = entry.GroupName;

            foreach (EcGroup group in _groups)
            {
                if (group.Name != entry.GroupName)
                    continue;

                foreach (EcMode mode in group.Modes)
                {
                    if (mode.Name == entry.ModeName)
                        return mode;
                }
            }

            return null;
        }

        /// <summary>保存设置到 config.json。失败提示一次但不打断操作。</summary>
        private void SaveSettings()
        {
            if (!SettingsManager.Save(_settings))
                SetStatus("设置保存失败，详见日志");
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

    /// <summary>
    /// "自动应用模式"下拉框的条目：同时携带功能组与模式名，
    /// 因为 EC 配置里模式名可能在不同组下重名。
    /// </summary>
    internal sealed class BootModeEntry
    {
        public string GroupName { get; private set; }

        public string ModeName { get; private set; }

        public BootModeEntry(string groupName, string modeName)
        {
            GroupName = groupName;
            ModeName = modeName;
        }

        public override string ToString()
        {
            return GroupName + " / " + ModeName;
        }
    }
}
