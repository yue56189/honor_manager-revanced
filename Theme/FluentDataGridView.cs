using System;
using System.Drawing;
using System.Windows.Forms;

namespace ECController.Theme
{
    /// <summary>
    /// Fluent 化 DataGridView：无边框、轻网格线、扁平表头、选中行浅蓝。
    /// 状态列的文字颜色由 MainForm 通过 CellFormatting 设置，这里只负责底色与线条。
    /// </summary>
    public class FluentDataGridView : DataGridView
    {
        public FluentDataGridView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

            BackgroundColor = FluentTheme.CardBackground;
            BorderStyle = BorderStyle.None;
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            GridColor = FluentTheme.GridLine;
            EnableHeadersVisualStyles = false;

            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            ColumnHeadersDefaultCellStyle.BackColor = FluentTheme.GridHeaderBackground;
            ColumnHeadersDefaultCellStyle.ForeColor = FluentTheme.TextSecondary;
            ColumnHeadersDefaultCellStyle.SelectionBackColor = FluentTheme.GridHeaderBackground;
            ColumnHeadersDefaultCellStyle.SelectionForeColor = FluentTheme.TextSecondary;
            ColumnHeadersDefaultCellStyle.Font = FluentTheme.SubtitleFont;
            ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
            ColumnHeadersHeight = 38;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            DefaultCellStyle.BackColor = FluentTheme.CardBackground;
            DefaultCellStyle.ForeColor = FluentTheme.TextPrimary;
            DefaultCellStyle.SelectionBackColor = FluentTheme.GridSelection;
            DefaultCellStyle.SelectionForeColor = FluentTheme.TextPrimary;
            DefaultCellStyle.Font = FluentTheme.BodyFont;
            DefaultCellStyle.Padding = new Padding(10, 0, 10, 0);

            AllowUserToAddRows = false;
            AllowUserToDeleteRows = false;
            AllowUserToResizeRows = false;
            AllowUserToResizeColumns = false;
            AllowUserToOrderColumns = false;
            ReadOnly = true;
            MultiSelect = false;
            RowHeadersVisible = false;
            SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            RowTemplate.Height = 34;
            ShowCellToolTips = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // 表头默认选中态会带走主题色，需在句柄建立后强制一次
            foreach (DataGridViewColumn c in Columns)
                c.HeaderCell.Style.BackColor = FluentTheme.GridHeaderBackground;
        }

        /// <summary>绘制表头下沿分隔线（Fluent 表头只有一条基线）。</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ColumnHeadersVisible && ColumnCount > 0)
            {
                int y = ColumnHeadersHeight - 1;
                using (Pen p = new Pen(FluentTheme.GridLine, 1f))
                    e.Graphics.DrawLine(p, 0, y, Width, y);
            }
        }
    }
}
