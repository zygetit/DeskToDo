using System; // 导入异常类型和数值工具
using System.Drawing; // 导入窗体字体、颜色和大小类型
using System.Windows.Forms; // 导入对话框、数据表格和确认框类型

namespace DeskToDo // 将删除历史对话框放在应用命名空间
{ // 开始应用命名空间
    internal sealed class RecentlyDeletedDialog : Form // 提供打开、还原和永久删除已删除条目的界面
    { // 开始定义最近删除窗口
        private const int FileNameColumn = 1; // 文件名位于序号之后
        private const int DeletedDateColumn = 2; // 删除日期位于文件名之后
        private readonly EntryStore store; // 与主窗口共享的主清单和删除历史存储
        private readonly DataGridView grid; // 显示序号、文件名和删除日期
        private readonly Button open; // 打开已删除条目的快捷方式
        private readonly Button restore; // 将选中的条目移回主清单
        private readonly Button purge; // 发起需要确认的永久删除

        private int SelectedIndex // 读取选中条目在删除历史中的位置
        { // 开始读取选区
            get { return grid.SelectedRows.Count == 1 ? grid.SelectedRows[0].Index : -1; } // 只有唯一选中行才能执行单条操作
        } // 完成读取选区

        public RecentlyDeletedDialog(EntryStore store, Font font, bool allowMultiSelect = false) // 接收主窗口的测试多选设置
        { // 开始初始化最近删除窗口
            this.store = store; // 保存主窗口传入的存储对象
            Text = "最近删除"; // 设置窗口标题
            Font = font; // 采用主窗口的字体
            AutoScaleMode = AutoScaleMode.Font; // 让控件按系统字体度量缩放
            StartPosition = FormStartPosition.Manual; // 加载时在主窗口所在屏幕的可用区域内定位
            ShowInTaskbar = false; // 使用主窗口的任务栏入口
            MinimizeBox = false; // 保持模态窗口可见，避免隐藏主窗口操作入口
            ClientSize = new Size(540, 360); // 设置默认内容区域大小
            MinimumSize = new Size(410, 240); // 保留容纳列表和按钮的最小空间

            var layout = new TableLayoutPanel // 建立列表与操作按钮的两行布局
            { // 开始设置布局
                Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 2 // 填满窗口并留出四周边距
            }; // 完成布局设置
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // 让唯一一列占满窗口宽度
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 将剩余高度分给列表
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 依据按钮高度布局操作栏
            Controls.Add(layout); // 将布局容器加入对话框

            grid = new DataGridView // 创建只读的删除历史列表
            { // 开始设置删除历史表格
                Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = allowMultiSelect, RowHeadersVisible = false, // 仅在测试开关开启时允许选中多行
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, // 将增删交给窗口的显式操作
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White, // 整行选择并使用清晰的列表背景
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, // 首次显示前测量表头并固定高度
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AccessibleName = "最近删除记录" // 分配列宽并为辅助技术提供名称
            }; // 完成表格设置
            grid.EnableHeadersVisualStyles = false; // 让 Windows 主题不覆盖表头的显示样式
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(232, 239, 246); // 按主清单的样式区分表头和记录区
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor; // 选中记录时保持表头背景色不变
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = grid.ColumnHeadersDefaultCellStyle.ForeColor; // 选中记录时保持表头文字颜色不变
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft; // 让列标题居左并垂直居中
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 5, 8, 5); // 让标题文字上下都留有足够空间
            grid.RowTemplate.Height = TextRenderer.MeasureText("最近删除", Font).Height + 12; // 根据字体高度给行内容留白
            grid.Columns.Add(MainForm.MakeRowNumberColumn()); // 最左侧显示历史记录的位置
            grid.Columns.Add(MainForm.MakeColumn("文件名", 65, 150)); // 显示拖入来源的原文件名
            grid.Columns.Add(MainForm.MakeColumn("删除日期", 35, 110)); // 显示移出主清单的日期
            grid.SelectionChanged += delegate { UpdateButtons(); }; // 选区变化时同步操作可用状态
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) // 允许双击历史中的单元格打开该条目
            { // 开始处理历史双击
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && SelectedIndex == e.RowIndex) // 只打开唯一选中的数据行
                    OpenEntry(e.RowIndex); // 打开双击行而不改动记录
            }; // 完成历史双击绑定
            layout.Controls.Add(grid, 0, 0); // 将表格放在第一行

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false }; // 在表格下方横排操作按钮
            open = MainForm.MakeButton("打开", delegate { OpenEntry(SelectedIndex); }); // 点击后通过原快捷方式打开当前项
            restore = MainForm.MakeButton("还原", RestoreSelected); // 点击后把记录移回主清单
            purge = MainForm.MakeButton("永久删除", ConfirmAndPurge); // 点击后先提示确认，再执行永久删除
            Button close = MainForm.MakeButton("关闭", Close); // 关闭窗口并回到主清单
            toolbar.Controls.AddRange(new Control[] { open, restore, purge, close }); // 按操作顺序加入按钮
            layout.Controls.Add(toolbar, 0, 1); // 放置底部操作栏
            CancelButton = close; // 允许使用 Esc 关闭窗口
            RefreshRows(); // 加载最近删除列表，初始不选中任何记录
            ActiveControl = close; // 避免表格在打开时因取得焦点而自动选中首行
        } // 结束最近删除窗口初始化

        protected override void OnLoad(EventArgs e) // 首次显示前计算对话框位置
        { // 开始定位窗口
            grid.ColumnHeadersHeight = Math.Max(grid.Columns[FileNameColumn].HeaderCell.PreferredSize.Height, // 按当前字号和边距测量文件名表头
                grid.Columns[DeletedDateColumn].HeaderCell.PreferredSize.Height); // 采用较高的数据列标题尺寸
            Rectangle workArea = Screen.FromControl(Owner ?? this).WorkingArea; // 按主窗口所在显示器避开任务栏
            Rectangle anchor = Owner == null ? workArea : Owner.Bounds; // 优先以主窗口为居中参照
            int x = anchor.Left + (anchor.Width - Width) / 2; // 计算水平居中的目标坐标
            int y = anchor.Top + (anchor.Height - Height) / 2; // 计算垂直居中的目标坐标
            Location = new Point( // 必要时把窗口移回可用区域，保留完整标题栏
                Math.Max(workArea.Left, Math.Min(x, workArea.Right - Width)), // 限制窗口左侧和右侧的位置
                Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - Height))); // 限制窗口顶部和底部的位置
            base.OnLoad(e); // 继续处理 WinForms 的加载事件
        } // 完成窗口定位

        protected override void OnShown(EventArgs e) // 窗口首次显示时清除控件初始化生成的选区
        { // 开始处理首次显示
            ClearRowSelection(); // 等待用户选中记录后再高亮数据行
            base.OnShown(e); // 继续处理 WinForms 的显示事件
        } // 结束处理首次显示

        private void RefreshRows() // 根据最新的删除历史重新绘制列表
        { // 开始刷新历史行
            grid.Rows.Clear(); // 移除现有界面行以保持索引一致
            foreach (DeletedEntry item in store.Deleted) // 按最近删除的顺序显示每条记录
            { // 开始填充一条历史行
                string fileName = Entry.SourceName(item.Entry.SourcePath); // 提取原始文件名，不显示完整路径
                int row = grid.Rows.Add(grid.Rows.Count + 1, fileName, item.DeletedDate); // 根据历史记录的显示位置编号
                grid.Rows[row].Cells[FileNameColumn].ToolTipText = fileName; // 悬停时可查看被截断的完整文件名
            } // 完成填充一条历史行
            grid.Columns[MainForm.RowNumberColumn].Visible = grid.Rows.Count > 0; // 空列表隐藏整个序号列

            ClearRowSelection(); // 刷新列表后不自动选中其他记录
        } // 结束刷新历史行

        private void ClearRowSelection() // 清除表格的当前单元格和行选区
        { // 开始清除表格选区
            grid.CurrentCell = null; // 避免活动单元格使所在行自动高亮
            grid.ClearSelection(); // 清除所有被选中的行
            UpdateButtons(); // 没有选中的记录时禁用针对条目的操作
        } // 结束清除表格选区

        private void UpdateButtons() // 根据是否选中条目更新按钮状态
        { // 开始更新按键状态
            open.Enabled = restore.Enabled = SelectedIndex >= 0; // 打开和还原要求唯一选中行
            purge.Enabled = grid.SelectedRows.Count > 0; // 有选中记录时允许发起永久删除并检查上限
        } // 完成更新按键状态

        private void OpenEntry(int index) // 从最近删除中打开指定条目
        { // 开始请求 Windows 打开条目
            if (index >= 0) // 忽略缺少选中项的操作
                RunAction("无法打开快捷方式。", delegate { store.OpenDeleted(index); }); // 只打开目标，不还原记录也不更新时间
        } // 完成打开历史条目

        private void RestoreSelected() // 还原当前选中的条目
        { // 开始处理还原请求
            int index = SelectedIndex; // 读取待还原行的位置
            if (index < 0) // 检查是否选择了条目
                return; // 没有条目时直接返回

            RunAction("无法还原条目。", delegate // 如果数据写入失败则保留原列表
            { // 开始提交还原
                if (store.Restore(index)) // 主清单有空位时还原所选条目
                    RefreshRows(); // 从最近删除视图去掉已还原条目
                else // 主清单已满时保留所选历史条目
                    MainForm.ShowListFull(this); // 使用与拖入相同的提示
            }); // 完成提交还原
        } // 结束处理还原请求

        private void ConfirmAndPurge() // 为按钮和 Delete 键提供统一的确认流程
        { // 开始处理永久删除请求
            int[] indices = MainForm.GetDeletionSelection(grid, this); // 检查上限并读取待删除的历史行
            if (indices.Length == 0) // 没有可提交的删除批次时结束
                return; // 保留这些历史记录

            string target = indices.Length == 1 // 单条删除显示文件名，批量删除显示数量
                ? "“" + Entry.SourceName(store.Deleted[indices[0]].Entry.SourcePath) + "”" // 使用列表中的原文件名
                : "选中的 " + indices.Length + " 条记录"; // 说明本次会永久删除多少条记录
            string message = "将从最近删除中永久移除" + target + "。\n\n此操作无法撤销，源文件会保留。"; // 告知确认后的影响
            DialogResult result = MessageBox.Show(this, message, "确认永久删除", // 显示单条或整批确认框
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2); // 默认选择否，避免误删
            if (result != DialogResult.Yes) // 只有用户选择是才允许继续
                return; // 拒绝或关闭确认框时保留历史条目

            RunAction("无法永久删除记录。", delegate // 为持久化错误提供提示
            { // 开始提交永久删除
                bool cleaned = store.PermanentlyRemove(indices); // 一次移除所选历史并清理程序保存的快捷方式
                RefreshRows(); // 根据已提交数据更新历史列表
                if (!cleaned) // 检查是否因占用或权限而无法清理副本
                    MessageBox.Show(this, "记录已移除，但应用保存的快捷方式未能清理。", Text, // 说明记录已经移除及副本清理失败
                        MessageBoxButtons.OK, MessageBoxIcon.Warning); // 显示普通的结果提示
            }); // 完成提交永久删除
        } // 结束处理永久删除请求

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) // 拦截历史列表中用于操作条目的键盘命令
        { // 开始处理对话框快捷键
            if (keyData == Keys.Delete) // Delete 与“永久删除”使用同一确认流程
            { // 开始处理 Delete 按键
                ConfirmAndPurge(); // 弹窗确认后才永久删除
                return true; // 阻止表格执行默认删除命令
            } // 完成处理 Delete 按键
            if (keyData == Keys.Enter && grid.ContainsFocus) // 列表有焦点时可按 Enter 打开
            { // 开始处理 Enter 按键
                OpenEntry(SelectedIndex); // 打开选中记录且不还原
                return true; // 防止 Enter 切换到其他数据行
            } // 完成处理 Enter 按键
            return base.ProcessCmdKey(ref msg, keyData); // 其他按键按 WinForms 默认规则处理
        } // 结束处理对话框快捷键

        private void RunAction(string message, Action action) // 执行操作并展示来自存储或 Windows 的错误
        { // 开始执行带错误提示的操作
            try { action(); } // 调用打开、还原或永久删除处理
            catch (Exception error) // 保持窗口可用并向用户报告失败原因
            { // 开始显示操作错误
                MessageBox.Show(this, message + "\n\n" + error.Message, Text, // 同时显示操作和具体失败原因
                    MessageBoxButtons.OK, MessageBoxIcon.Warning); // 提供关闭提示的确定按钮
            } // 完成显示操作错误
        } // 结束执行带错误提示的操作
    } // 结束定义最近删除窗口
} // 结束应用命名空间
