namespace ECController
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblFunction = new System.Windows.Forms.Label();
            this.cmbGroup = new ECController.Theme.FluentComboBox();
            this.lblmodes = new System.Windows.Forms.Label();
            this.cmbMode = new ECController.Theme.FluentComboBox();
            this.cardPreview = new ECController.Theme.FluentCard();
            this.dgvPreview = new ECController.Theme.FluentDataGridView();
            this.ColAddress = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColTarget = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColCurrent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnRead = new ECController.Theme.FluentButton();
            this.cardOptions = new ECController.Theme.FluentCard();
            this.chkAutoStart = new ECController.Theme.FluentCheckBox();
            this.chkApplyBoot = new ECController.Theme.FluentCheckBox();
            this.lblBootMode = new System.Windows.Forms.Label();
            this.cmbBootMode = new ECController.Theme.FluentComboBox();
            this.lblBootDelay = new System.Windows.Forms.Label();
            this.numBootDelay = new ECController.Theme.FluentNumericUpDown();
            this.lblDelayUnit = new System.Windows.Forms.Label();
            this.chkTray = new ECController.Theme.FluentCheckBox();
            this.pnlActions = new System.Windows.Forms.Panel();
            this.btnApply = new ECController.Theme.FluentButton();
            this.btnRestore = new ECController.Theme.FluentButton();
            this.btnExit = new ECController.Theme.FluentButton();
            this.lblTitle = new System.Windows.Forms.Label();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblDivider = new System.Windows.Forms.Label();
            this.cardPreview.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvPreview)).BeginInit();
            this.cardOptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numBootDelay)).BeginInit();
            this.pnlActions.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            //
            // lblTitle
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.BackColor = System.Drawing.Color.Transparent;
            this.lblTitle.Font = ECController.Theme.FluentTheme.TitleFont;
            this.lblTitle.ForeColor = ECController.Theme.FluentTheme.TextPrimary;
            this.lblTitle.Location = new System.Drawing.Point(20, 16);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(180, 26);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "EC 配置控制器";
            //
            // lblDivider
            //
            this.lblDivider.BackColor = ECController.Theme.FluentTheme.CardBorder;
            this.lblDivider.Location = new System.Drawing.Point(20, 52);
            this.lblDivider.Name = "lblDivider";
            this.lblDivider.Size = new System.Drawing.Size(508, 1);
            this.lblDivider.TabIndex = 1;
            //
            // lblFunction
            //
            this.lblFunction.AutoSize = true;
            this.lblFunction.BackColor = System.Drawing.Color.Transparent;
            this.lblFunction.Font = ECController.Theme.FluentTheme.CaptionFont;
            this.lblFunction.ForeColor = ECController.Theme.FluentTheme.TextSecondary;
            this.lblFunction.Location = new System.Drawing.Point(20, 68);
            this.lblFunction.Name = "lblFunction";
            this.lblFunction.Size = new System.Drawing.Size(38, 17);
            this.lblFunction.TabIndex = 2;
            this.lblFunction.Text = "功能";
            //
            // cmbGroup
            //
            this.cmbGroup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbGroup.Font = ECController.Theme.FluentTheme.BodyFont;
            this.cmbGroup.FormattingEnabled = true;
            this.cmbGroup.Location = new System.Drawing.Point(20, 89);
            this.cmbGroup.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cmbGroup.Name = "cmbGroup";
            this.cmbGroup.Size = new System.Drawing.Size(508, 29);
            this.cmbGroup.TabIndex = 3;
            this.cmbGroup.SelectedIndexChanged += new System.EventHandler(this.cmbGroup_SelectedIndexChanged);
            //
            // lblmodes
            //
            this.lblmodes.AutoSize = true;
            this.lblmodes.BackColor = System.Drawing.Color.Transparent;
            this.lblmodes.Font = ECController.Theme.FluentTheme.CaptionFont;
            this.lblmodes.ForeColor = ECController.Theme.FluentTheme.TextSecondary;
            this.lblmodes.Location = new System.Drawing.Point(20, 128);
            this.lblmodes.Name = "lblmodes";
            this.lblmodes.Size = new System.Drawing.Size(38, 17);
            this.lblmodes.TabIndex = 4;
            this.lblmodes.Text = "模式";
            //
            // cmbMode
            //
            this.cmbMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbMode.Font = ECController.Theme.FluentTheme.BodyFont;
            this.cmbMode.FormattingEnabled = true;
            this.cmbMode.Location = new System.Drawing.Point(20, 149);
            this.cmbMode.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cmbMode.Name = "cmbMode";
            this.cmbMode.Size = new System.Drawing.Size(508, 29);
            this.cmbMode.TabIndex = 5;
            this.cmbMode.SelectedIndexChanged += new System.EventHandler(this.cmbMode_SelectedIndexChanged);
            //
            // cardPreview
            //
            this.cardPreview.Controls.Add(this.dgvPreview);
            this.cardPreview.Location = new System.Drawing.Point(20, 192);
            this.cardPreview.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cardPreview.Name = "cardPreview";
            this.cardPreview.Padding = new System.Windows.Forms.Padding(16, 46, 16, 16);
            this.cardPreview.Size = new System.Drawing.Size(508, 200);
            this.cardPreview.TabIndex = 6;
            this.cardPreview.Title = "即将写入 EC";
            //
            // dgvPreview
            //
            this.dgvPreview.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvPreview.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvPreview.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColAddress,
            this.ColTarget,
            this.ColCurrent,
            this.ColStatus});
            this.dgvPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvPreview.Location = new System.Drawing.Point(16, 46);
            this.dgvPreview.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.dgvPreview.Name = "dgvPreview";
            this.dgvPreview.Size = new System.Drawing.Size(476, 138);
            this.dgvPreview.TabIndex = 0;
            //
            // ColAddress
            //
            this.ColAddress.HeaderText = "地址";
            this.ColAddress.MinimumWidth = 6;
            this.ColAddress.Name = "ColAddress";
            this.ColAddress.ReadOnly = true;
            //
            // ColTarget
            //
            this.ColTarget.HeaderText = "目标值";
            this.ColTarget.MinimumWidth = 6;
            this.ColTarget.Name = "ColTarget";
            this.ColTarget.ReadOnly = true;
            //
            // ColCurrent
            //
            this.ColCurrent.HeaderText = "当前值";
            this.ColCurrent.MinimumWidth = 6;
            this.ColCurrent.Name = "ColCurrent";
            this.ColCurrent.ReadOnly = true;
            //
            // ColStatus
            //
            this.ColStatus.HeaderText = "状态";
            this.ColStatus.MinimumWidth = 6;
            this.ColStatus.Name = "ColStatus";
            this.ColStatus.ReadOnly = true;
            //
            // btnRead
            //
            this.btnRead.Accent = false;
            this.btnRead.Location = new System.Drawing.Point(424, 400);
            this.btnRead.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnRead.Name = "btnRead";
            this.btnRead.Size = new System.Drawing.Size(104, 34);
            this.btnRead.TabIndex = 7;
            this.btnRead.Text = "读取当前 EC";
            this.btnRead.Click += new System.EventHandler(this.btnRead_Click);
            //
            // cardOptions
            //
            this.cardOptions.Controls.Add(this.chkAutoStart);
            this.cardOptions.Controls.Add(this.chkApplyBoot);
            this.cardOptions.Controls.Add(this.lblBootMode);
            this.cardOptions.Controls.Add(this.cmbBootMode);
            this.cardOptions.Controls.Add(this.lblBootDelay);
            this.cardOptions.Controls.Add(this.numBootDelay);
            this.cardOptions.Controls.Add(this.lblDelayUnit);
            this.cardOptions.Controls.Add(this.chkTray);
            this.cardOptions.Location = new System.Drawing.Point(20, 446);
            this.cardOptions.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cardOptions.Name = "cardOptions";
            this.cardOptions.Padding = new System.Windows.Forms.Padding(16, 46, 16, 16);
            this.cardOptions.Size = new System.Drawing.Size(508, 154);
            this.cardOptions.TabIndex = 8;
            this.cardOptions.Title = "启动与运行";
            //
            // chkAutoStart
            //
            this.chkAutoStart.Location = new System.Drawing.Point(20, 50);
            this.chkAutoStart.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.chkAutoStart.Name = "chkAutoStart";
            this.chkAutoStart.Size = new System.Drawing.Size(128, 24);
            this.chkAutoStart.TabIndex = 0;
            this.chkAutoStart.Text = "开机启动";
            this.chkAutoStart.CheckedChanged += new System.EventHandler(this.chkAutoStart_CheckedChanged);
            //
            // chkApplyBoot
            //
            this.chkApplyBoot.Location = new System.Drawing.Point(20, 82);
            this.chkApplyBoot.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.chkApplyBoot.Name = "chkApplyBoot";
            this.chkApplyBoot.Size = new System.Drawing.Size(128, 24);
            this.chkApplyBoot.TabIndex = 1;
            this.chkApplyBoot.Text = "登录自动应用";
            this.chkApplyBoot.CheckedChanged += new System.EventHandler(this.chkApplyBoot_CheckedChanged);
            //
            // lblBootMode
            //
            this.lblBootMode.AutoSize = true;
            this.lblBootMode.BackColor = System.Drawing.Color.Transparent;
            this.lblBootMode.Font = ECController.Theme.FluentTheme.CaptionFont;
            this.lblBootMode.ForeColor = ECController.Theme.FluentTheme.TextSecondary;
            this.lblBootMode.Location = new System.Drawing.Point(160, 53);
            this.lblBootMode.Name = "lblBootMode";
            this.lblBootMode.Size = new System.Drawing.Size(68, 17);
            this.lblBootMode.TabIndex = 2;
            this.lblBootMode.Text = "自动应用";
            //
            // cmbBootMode
            //
            this.cmbBootMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbBootMode.Font = ECController.Theme.FluentTheme.BodyFont;
            this.cmbBootMode.FormattingEnabled = true;
            this.cmbBootMode.Location = new System.Drawing.Point(236, 49);
            this.cmbBootMode.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cmbBootMode.Name = "cmbBootMode";
            this.cmbBootMode.Size = new System.Drawing.Size(256, 29);
            this.cmbBootMode.TabIndex = 3;
            this.cmbBootMode.SelectedIndexChanged += new System.EventHandler(this.cmbBootMode_SelectedIndexChanged);
            //
            // lblBootDelay
            //
            this.lblBootDelay.AutoSize = true;
            this.lblBootDelay.BackColor = System.Drawing.Color.Transparent;
            this.lblBootDelay.Font = ECController.Theme.FluentTheme.CaptionFont;
            this.lblBootDelay.ForeColor = ECController.Theme.FluentTheme.TextSecondary;
            this.lblBootDelay.Location = new System.Drawing.Point(160, 86);
            this.lblBootDelay.Name = "lblBootDelay";
            this.lblBootDelay.Size = new System.Drawing.Size(68, 17);
            this.lblBootDelay.TabIndex = 4;
            this.lblBootDelay.Text = "启动延迟";
            //
            // numBootDelay
            //
            this.numBootDelay.Font = ECController.Theme.FluentTheme.BodyFont;
            this.numBootDelay.Location = new System.Drawing.Point(236, 82);
            this.numBootDelay.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.numBootDelay.Maximum = new decimal(new int[] { 300, 0, 0, 0 });
            this.numBootDelay.Name = "numBootDelay";
            this.numBootDelay.Size = new System.Drawing.Size(60, 28);
            this.numBootDelay.TabIndex = 5;
            this.numBootDelay.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.numBootDelay.Value = new decimal(new int[] { 5, 0, 0, 0 });
            this.numBootDelay.ValueChanged += new System.EventHandler(this.numBootDelay_ValueChanged);
            //
            // lblDelayUnit
            //
            this.lblDelayUnit.AutoSize = true;
            this.lblDelayUnit.BackColor = System.Drawing.Color.Transparent;
            this.lblDelayUnit.Font = ECController.Theme.FluentTheme.CaptionFont;
            this.lblDelayUnit.ForeColor = ECController.Theme.FluentTheme.TextSecondary;
            this.lblDelayUnit.Location = new System.Drawing.Point(304, 86);
            this.lblDelayUnit.Name = "lblDelayUnit";
            this.lblDelayUnit.Size = new System.Drawing.Size(98, 17);
            this.lblDelayUnit.TabIndex = 6;
            this.lblDelayUnit.Text = "秒后再应用";
            //
            // chkTray
            //
            this.chkTray.Location = new System.Drawing.Point(20, 114);
            this.chkTray.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.chkTray.Name = "chkTray";
            this.chkTray.Size = new System.Drawing.Size(150, 24);
            this.chkTray.TabIndex = 7;
            this.chkTray.Text = "最小化到托盘";
            this.chkTray.CheckedChanged += new System.EventHandler(this.chkTray_CheckedChanged);
            //
            // pnlActions
            //
            this.pnlActions.BackColor = System.Drawing.Color.Transparent;
            this.pnlActions.Controls.Add(this.btnApply);
            this.pnlActions.Controls.Add(this.btnRestore);
            this.pnlActions.Controls.Add(this.btnExit);
            this.pnlActions.Location = new System.Drawing.Point(20, 612);
            this.pnlActions.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.pnlActions.Name = "pnlActions";
            this.pnlActions.Size = new System.Drawing.Size(508, 40);
            this.pnlActions.TabIndex = 9;
            //
            // btnApply
            //
            this.btnApply.Accent = true;
            this.btnApply.Location = new System.Drawing.Point(184, 3);
            this.btnApply.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(100, 34);
            this.btnApply.TabIndex = 0;
            this.btnApply.Text = "应用";
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            //
            // btnRestore
            //
            this.btnRestore.Accent = false;
            this.btnRestore.Location = new System.Drawing.Point(292, 3);
            this.btnRestore.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnRestore.Name = "btnRestore";
            this.btnRestore.Size = new System.Drawing.Size(100, 34);
            this.btnRestore.TabIndex = 1;
            this.btnRestore.Text = "恢复默认";
            this.btnRestore.Click += new System.EventHandler(this.btnRestore_Click);
            //
            // btnExit
            //
            this.btnExit.Accent = false;
            this.btnExit.Location = new System.Drawing.Point(400, 3);
            this.btnExit.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(100, 34);
            this.btnExit.TabIndex = 2;
            this.btnExit.Text = "退出";
            this.btnExit.Click += new System.EventHandler(this.btnExit_Click);
            //
            // statusStrip1
            //
            this.statusStrip1.BackColor = ECController.Theme.FluentTheme.WindowBackground;
            this.statusStrip1.Font = ECController.Theme.FluentTheme.BodyFont;
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 688);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(20, 0, 20, 0);
            this.statusStrip1.SizingGrip = false;
            this.statusStrip1.Size = new System.Drawing.Size(548, 26);
            this.statusStrip1.TabIndex = 10;
            //
            // toolStripStatusLabel1
            //
            this.toolStripStatusLabel1.ForeColor = ECController.Theme.FluentTheme.TextSecondary;
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(38, 20);
            this.toolStripStatusLabel1.Text = "就绪";
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = ECController.Theme.FluentTheme.WindowBackground;
            this.ClientSize = new System.Drawing.Size(548, 684);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblDivider);
            this.Controls.Add(this.lblFunction);
            this.Controls.Add(this.cmbGroup);
            this.Controls.Add(this.lblmodes);
            this.Controls.Add(this.cmbMode);
            this.Controls.Add(this.cardPreview);
            this.Controls.Add(this.btnRead);
            this.Controls.Add(this.cardOptions);
            this.Controls.Add(this.pnlActions);
            this.Controls.Add(this.statusStrip1);
            this.DoubleBuffered = true;
            this.Font = ECController.Theme.FluentTheme.BodyFont;
            this.ForeColor = ECController.Theme.FluentTheme.TextPrimary;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "EC Controller";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.cardPreview.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvPreview)).EndInit();
            this.cardOptions.ResumeLayout(false);
            this.cardOptions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numBootDelay)).EndInit();
            this.pnlActions.ResumeLayout(false);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblDivider;
        private System.Windows.Forms.Label lblFunction;
        private ECController.Theme.FluentComboBox cmbGroup;
        private System.Windows.Forms.Label lblmodes;
        private ECController.Theme.FluentComboBox cmbMode;
        private ECController.Theme.FluentCard cardPreview;
        private ECController.Theme.FluentDataGridView dgvPreview;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColAddress;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColTarget;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColCurrent;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColStatus;
        private ECController.Theme.FluentButton btnRead;
        private ECController.Theme.FluentCard cardOptions;
        private ECController.Theme.FluentCheckBox chkAutoStart;
        private ECController.Theme.FluentCheckBox chkApplyBoot;
        private System.Windows.Forms.Label lblBootMode;
        private ECController.Theme.FluentComboBox cmbBootMode;
        private System.Windows.Forms.Label lblBootDelay;
        private ECController.Theme.FluentNumericUpDown numBootDelay;
        private System.Windows.Forms.Label lblDelayUnit;
        private ECController.Theme.FluentCheckBox chkTray;
        private System.Windows.Forms.Panel pnlActions;
        private ECController.Theme.FluentButton btnApply;
        private ECController.Theme.FluentButton btnRestore;
        private ECController.Theme.FluentButton btnExit;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
    }
}
