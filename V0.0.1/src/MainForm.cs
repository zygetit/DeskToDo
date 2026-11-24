using System; // 导入委托、异常和数值工具
using System.Collections.Generic; // 导入用于收集导入错误的列表类型
using System.Drawing; // 导入颜色、尺寸和文本绘制所需的类型
using System.Linq; // 收集和排序已选数据行
using System.Windows.Forms; // 导入 WinForms 窗口、控件和拖放事件类型

namespace DeskToDo // 将主窗体放在应用命名空间
{ // 开始此代码块：将主窗体放在应用命名空间
    internal sealed class MainForm : Form // 定义桌面清单的主窗口
    { // 开始此代码块：定义桌面清单的主窗口
        internal const int RowNumberColumn = 0; // 两个列表都在最左侧显示行序号
        private const int NameColumn = 1; // 名称列位于序号之后
        private const int UpdatedDateColumn = 2; // 更新时间位于名称之后
        private const int SourcePathColumn = 3; // 原始位置位于最右侧
        private static readonly Color Accent = Color.FromArgb(24, 88, 142); // 设定拖拽插入线的强调色
        private static readonly Color Ink = Color.FromArgb(29, 43, 57); // 设定标题及正文颜色
        private static readonly Color Muted = Color.FromArgb(82, 101, 118); // 设定提示文字颜色
        private static readonly Color Canvas = Color.FromArgb(244, 247, 250); // 设定窗体背景颜色

        private readonly EntryStore store; // 持有负责文件和条目持久化的对象
        private readonly DataGridView grid; // 持有显示名称及源路径的表格
        private readonly Label status; // 持有显示用法和条目数量的状态提示
        private readonly Button dismissStatus; // 关闭底部的历史淘汰提示
        private readonly Button open; // 持有打开当前条目的按钮
        private readonly Button rename; // 持有修改当前条目名称的按钮
        private readonly Button remove; // 持有删除当前条目的按钮
        private readonly Timer scrollTimer; // 持有靠近表格边缘拖拽时使用的滚动定时器

        private Rectangle dragBounds; // 保存鼠标开始拖动前允许移动的阈值矩形
        private int pressedRow = -1; // 记录鼠标按下所在行，负数表示未按在数据行上
        private int draggedRow = -1; // 记录正在拖动的行，负数表示没有内部拖拽
        private int dropIndex = -1; // 记录预计插入位置，负数表示没有有效落点
        private int pendingExpiredCount; // 保存最近一次尚未关闭的历史淘汰数量

        private int SelectedIndex // 统一读取当前选中条目的表格索引
        { // 开始此代码块：统一读取当前选中条目的表格索引
            get { return grid.SelectedRows.Count == 1 ? grid.SelectedRows[0].Index : -1; } // 只有唯一选中行才能执行单条操作
        } // 结束此代码块：统一读取当前选中条目的表格索引

        public MainForm(EntryStore store) // 设置主窗口并绑定界面事件
        { // 开始此代码块：设置主窗口并绑定界面事件
            this.store = store; // 将传入的存储对象保留给窗口事件
            Text = Program.DisplayName; // 在系统标题栏显示应用名称
            StartPosition = FormStartPosition.CenterScreen; // 启动时将主窗口置于屏幕中央
            ClientSize = new Size(720, 470); // 设置主窗口的初始内容区域大小
            MinimumSize = new Size(460, 300); // 限制最小窗口尺寸以保留可用表格空间
            Font = new Font("Microsoft YaHei UI", 9F); // 使用支持中文的系统界面字体
            AutoScaleMode = AutoScaleMode.Font; // 按界面字体度量缩放窗口和控件
            BackColor = Canvas; // 应用窗体的背景色
            ForeColor = Ink; // 应用窗体的默认前景文字色

            var layout = new TableLayoutPanel // 创建用于摆放界面各部分的布局容器
            { // 开始此代码块：创建用于摆放界面各部分的布局容器
                Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 10), // 填满主窗口并在四周留出内边距
                ColumnCount = 1, RowCount = 4 // 用一列四行摆放标题、工具栏、表格和状态
            }; // 结束此代码块：创建用于摆放界面各部分的布局容器
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // 让唯一一列占据全部可用宽度
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 让标题行根据内容自动决定高度
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 让工具栏行根据按钮高度自动决定高度
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 将剩余窗口高度分配给表格
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 让底部状态行根据文字高度自动布局
            Controls.Add(layout); // 把布局容器加入主窗口

            var introduction = new Label // 创建窗口内容区域的简介标签
            { // 开始配置简介标签
                Text = "一款轻量级的桌面ToDoList小工具", AutoSize = true, Margin = new Padding(0, 0, 0, 8), // 保留指定文案并为下方工具栏留出间距
                Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Muted // 左对齐并沿用窗口的普通 9 磅字体
            }; // 完成配置简介标签
            layout.Controls.Add(introduction, 0, 0); // 将简介放在主界面首行

            var toolbar = new FlowLayoutPanel // 创建按顺序摆放操作按钮的工具栏
            { // 开始此代码块：创建按顺序摆放操作按钮的工具栏
                Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 0, 0, 8) // 窗口较窄时允许按钮换行
            }; // 结束此代码块：创建按顺序摆放操作按钮的工具栏
            open = MakeButton("打开", OpenSelected); // 建立打开按钮并绑定打开当前项的操作
            rename = MakeButton("重命名", RenameSelected); // 建立重命名按钮并绑定名称编辑操作
            remove = MakeButton("删除条目", RemoveSelected); // 建立删除按钮并绑定当前项的移除操作
            Button recent = MakeButton("最近删除", ShowRecentlyDeleted); // 提供最近删除的入口，即使主清单为空也可使用
            var admin = new CheckBox // 用可按下的按钮切换当前会话的测试多选模式
            { // 开始设置管理员权限按钮
                Text = "管理员权限", Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter, // 主工具栏显示指定的按钮名称
                AutoSize = true, MinimumSize = new Size(76, 30), Margin = new Padding(0, 0, 6, 0), // 保持工具栏按钮间距一致
                AccessibleDescription = "开启或关闭两个列表的测试多选" // 说明开关的作用
            }; // 完成管理员权限按钮设置
            toolbar.Controls.AddRange(new Control[] { open, rename, remove, recent, admin }); // 将主清单操作、删除历史和测试开关加入工具栏
            layout.Controls.Add(toolbar, 0, 1); // 把工具栏放在标题下方

            grid = new DataGridView // 创建只读的桌面文件列表表格
            { // 开始此代码块：创建只读的桌面文件列表表格
                Dock = DockStyle.Fill, Margin = Padding.Empty, ReadOnly = true, MultiSelect = false, // 填满布局行并限制每次只选中一项
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToOrderColumns = false, // 将增删和排序交给本应用的操作逻辑
                AllowUserToResizeRows = false, RowHeadersVisible = false, BackgroundColor = Color.White, // 固定行高、隐藏行头，并设定列表背景色
                BorderStyle = BorderStyle.FixedSingle, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, // 保留外边框，并用横线分隔数据行
                GridColor = Color.FromArgb(226, 232, 238), SelectionMode = DataGridViewSelectionMode.FullRowSelect, // 设置分隔线颜色，并以整行作为选中单位
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, // 让两列按配置权重填满可用宽度
                AccessibleName = "桌面文件清单" // 设置辅助技术可读取的表格名称
            }; // 结束此代码块：创建只读的桌面文件列表表格
            grid.RowTemplate.Height = TextRenderer.MeasureText("名称", Font).Height + 12; // 根据字体高度计算行高，再预留上下空间
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize; // 让表头高度按字体和内容自动调整
            grid.EnableHeadersVisualStyles = false; // 禁用主题对表头颜色的覆盖
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(232, 239, 246); // 设置表头背景色
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink; // 设置表头文字颜色
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor; // 表头随选区变化时仍显示相同背景
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(215, 233, 248); // 设置选中行的背景色
            grid.DefaultCellStyle.SelectionForeColor = Ink; // 保持选中行的文字对比度
            grid.DefaultCellStyle.Padding = new Padding(6, 0, 4, 0); // 为单元格文字增加左右留白
            grid.Columns.Add(MakeRowNumberColumn()); // 最左侧显示当前行的位置
            grid.Columns.Add(MakeColumn("名称", 31, 100)); // 显示可编辑的名称
            grid.Columns.Add(MakeColumn("更新时间", 19, 105)); // 显示固定格式的日期
            grid.Columns.Add(MakeColumn("原始位置", 50, 150)); // 显示来源路径
            grid.SelectionChanged += delegate { UpdateButtons(); }; // 选中项变化时同步更新按钮状态
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) // 监听表格单元格的双击
            { // 开始此代码块：监听表格单元格的双击
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0) // 只处理数据行和数据列中的双击
                    OpenSelected(); // 交给统一的打开操作执行
            }; // 结束此代码块：监听表格单元格的双击
            grid.KeyDown += GridKeyDown; // 监听表格中可用的快捷键
            grid.MouseDown += GridMouseDown; // 鼠标按下时记录可能开始拖动的行
            grid.MouseMove += GridMouseMove; // 鼠标移动时检测是否超过开始拖拽的阈值
            grid.MouseUp += delegate { pressedRow = -1; }; // 松开鼠标后清除等待拖动的状态
            grid.DragLeave += delegate { ClearDropPosition(); }; // 鼠标带着拖拽物离开表格时清除反馈
            grid.Paint += PaintGrid; // 在表格重绘时绘制空列表提示和插入线
            layout.Controls.Add(grid, 0, 2); // 将表格放到布局的第三行

            var statusBar = new FlowLayoutPanel // 将状态文字和关闭按钮排列在同一行
            { // 开始配置底部状态区域
                AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0, 8, 0, 0) // 贴合内容并留出上方间距
            }; // 完成底部状态区域设置
            status = new Label { AutoSize = true, ForeColor = Muted, Anchor = AnchorStyles.Left, Margin = Padding.Empty }; // 让状态文字在行内垂直居中
            dismissStatus = new Button // 为历史淘汰提示提供可点击的关闭按钮
            { // 开始配置关闭按钮
                Text = "×", AccessibleName = "关闭提示", Visible = false, // 只在有待关闭的提示时显示
                Size = new Size(24, 22), Margin = new Padding(6, 0, 0, 0) // 在提示右侧留出间距并保持可点击面积
            }; // 完成关闭按钮设置
            dismissStatus.Click += delegate // 点击后清除待关闭的历史淘汰提示
            { // 开始恢复常规状态
                pendingExpiredCount = 0; // 标记当前提示已关闭
                UpdateStatus(); // 恢复当前条目数量和操作提示
            }; // 完成恢复常规状态
            statusBar.Controls.AddRange(new Control[] { status, dismissStatus }); // 将文字和关闭按钮加入同一状态区域
            layout.Controls.Add(statusBar, 0, 3); // 将状态区域放到布局最后一行
            admin.CheckedChanged += delegate { SetTestSelection(admin.Checked); }; // 开关变化时立即更新主清单的选择方式

            scrollTimer = new Timer { Interval = 120 }; // 以 120 毫秒为间隔检查拖拽时的自动滚动
            scrollTimer.Tick += ScrollWhileDragging; // 绑定定时滚动处理函数
            RegisterDropTargets(this); // 给主窗口及其子控件注册文件接收事件
            RefreshRows(-1, 0); // 使用已保存数据填充初始表格，初始不选中任何项
        } // 结束此代码块：设置主窗口并绑定界面事件

        internal static Button MakeButton(string text, Action action) // 为两个清单窗口创建一致的操作按钮
        { // 开始此代码块：按指定文字及点击行为创建标准工具栏按钮
            var button = new Button // 创建一个自动适应文字长度的按钮
            { // 开始此代码块：创建一个自动适应文字长度的按钮
                Text = text, AutoSize = true, MinimumSize = new Size(76, 30), Margin = new Padding(0, 0, 6, 0) // 设置按钮最小尺寸，并在右侧留出间距
            }; // 结束此代码块：创建一个自动适应文字长度的按钮
            button.Click += delegate { action(); }; // 点击按钮时调用传入的操作
            return button; // 返回可加入工具栏的按钮
        } // 结束此代码块：按指定文字及点击行为创建标准工具栏按钮

        internal static DataGridViewTextBoxColumn MakeColumn(string title, float weight, int minWidth) // 为两个清单窗口创建不可排序的文本列
        { // 开始此代码块：根据表头、宽度比例和最小宽度构造文本列
            return new DataGridViewTextBoxColumn // 返回适合清单显示的文本列
            { // 开始此代码块：返回适合清单显示的文本列
                HeaderText = title, FillWeight = weight, MinimumWidth = minWidth, // 设置表头及分配表格宽度所需的属性
                SortMode = DataGridViewColumnSortMode.NotSortable // 停用点击表头排序，以保留手工排列的顺序
            }; // 结束此代码块：返回适合清单显示的文本列
        } // 结束此代码块：根据表头、宽度比例和最小宽度构造文本列

        internal static DataGridViewTextBoxColumn MakeRowNumberColumn() // 为两个列表创建一致的序号列
        { // 开始配置序号列
            var column = new DataGridViewTextBoxColumn // 以固定窄列显示列表位置
            { // 开始设置序号列属性
                HeaderText = "序号", Width = 54, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, // 给三位数序号预留空间
                SortMode = DataGridViewColumnSortMode.NotSortable, Resizable = DataGridViewTriState.False, // 序号与显示位置绑定
                Visible = false // 起初隐藏，填充第一条数据时显示
            }; // 完成设置序号列属性
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter; // 将数字居中显示
            column.DefaultCellStyle.Padding = Padding.Empty; // 让较窄的序号列保留完整数字宽度
            return column; // 返回已配置的序号列
        } // 完成序号列创建

        private void RefreshRows(int selected, int firstVisible) // 根据存储重新生成列表并尽量恢复界面位置
        { // 开始此代码块：根据存储重新生成列表并尽量恢复界面位置
            grid.Rows.Clear(); // 先清除原有表格数据行
            foreach (Entry entry in store.Entries) // 按已保存的顺序遍历条目
            { // 开始此代码块：按已保存的顺序遍历条目
                int row = grid.Rows.Add(grid.Rows.Count + 1, entry.Name, entry.UpdatedDate, entry.SourcePath); // 序号取当前显示位置，不写入记录
                grid.Rows[row].Cells[NameColumn].ToolTipText = entry.Name; // 鼠标悬停时可查看完整的显示名称
                grid.Rows[row].Cells[SourcePathColumn].ToolTipText = entry.SourcePath; // 鼠标悬停时可查看完整的来源路径
            } // 结束此代码块：按已保存的顺序遍历条目
            grid.Columns[RowNumberColumn].Visible = grid.Rows.Count > 0; // 空列表隐藏整个序号列

            if (selected >= 0 && selected < grid.RowCount) // 确认准备恢复的选中索引仍在范围内
            { // 开始此代码块：确认准备恢复的选中索引仍在范围内
                grid.CurrentCell = grid.Rows[selected].Cells[NameColumn]; // 将键盘焦点单元格设到所选行的名称列
                grid.Rows[selected].Selected = true; // 选中对应数据行
                if (firstVisible >= 0 && firstVisible < grid.RowCount && selected >= firstVisible // 核实原首个可见行仍有效且不在选中行下方
                    && selected - firstVisible < grid.DisplayedRowCount(false)) // 确认恢复原滚动位置后仍能完整显示选中行
                    grid.FirstDisplayedScrollingRowIndex = firstVisible; // 将滚动位置恢复到原首个可见行
            } // 结束此代码块：确认准备恢复的选中索引仍在范围内
            else // 处理没有可恢复选中项的情况
            { // 开始此代码块：处理没有可恢复选中项的情况
                grid.CurrentCell = null; // 清除当前活动单元格
                grid.ClearSelection(); // 清除表格可能自动建立的选区
            } // 结束此代码块：处理没有可恢复选中项的情况
            UpdateStatus(); // 根据条目数量和测试开关刷新底部提示
            UpdateButtons(); // 同步启用或禁用对选中项的操作
            grid.Invalidate(); // 请求重绘表格，以更新空列表或拖拽反馈
        } // 结束此代码块：根据存储重新生成列表并尽量恢复界面位置

        private void SetTestSelection(bool enabled) // 切换主清单的测试多选模式
        { // 开始切换选择方式
            if (!enabled && grid.SelectedRows.Count > 1) // 退出模式时清除已有的多行选区
                grid.ClearSelection(); // 等待用户重新选中要操作的单条记录
            grid.MultiSelect = enabled; // 让表格使用当前模式对应的单选或多选行为
            UpdateButtons(); // 同步单条操作按钮状态
            UpdateStatus(); // 显示当前模式对应的使用提示
        } // 完成选择方式切换

        private void UpdateStatus() // 显示当前模式和条目数量
        { // 开始更新底部提示
            dismissStatus.Visible = pendingExpiredCount > 0; // 仅在显示淘汰提示时提供关闭按钮
            if (pendingExpiredCount > 0) // 列表刷新时继续显示尚未关闭的淘汰提示
            { // 开始显示淘汰信息
                status.Text = string.Format("最近删除已满，已移除最早的 {0} 条记录", pendingExpiredCount); // 显示最近一次删除实际淘汰的条数
                return; // 等待用户关闭后再恢复常规状态
            } // 完成显示淘汰信息
            if (grid.MultiSelect) // 测试模式开启时说明多选方式
                status.Text = string.Format("{0} 项  ·  测试多选已开启  ·  Ctrl / Shift 选行", grid.RowCount); // 显示测试状态和选择键
            else // 单选模式显示原有操作提示
                status.Text = grid.RowCount == 0 ? "可一次拖入多个文件或文件夹" // 空列表提示可以批量拖入
                    : string.Format("{0} 项  ·  可拖动排序  ·  F2 重命名", grid.RowCount); // 有记录时提示单条排序和重命名
        } // 完成底部提示更新

        private void UpdateButtons() // 根据选区统一刷新工具栏状态
        { // 开始此代码块：根据选区统一刷新工具栏状态
            open.Enabled = rename.Enabled = SelectedIndex >= 0; // 打开和重命名要求唯一选中行
            remove.Enabled = grid.SelectedRows.Count > 0; // 有选中行即可发起删除并检查上限
        } // 结束此代码块：根据选区统一刷新工具栏状态

        private void GridKeyDown(object sender, KeyEventArgs e) // 处理表格接收到的操作快捷键
        { // 开始此代码块：处理表格接收到的操作快捷键
            switch (e.KeyData) // 通过完整键值识别不带修饰键的命令
            { // 开始此代码块：通过完整键值识别不带修饰键的命令
                case Keys.Enter: OpenSelected(); break; // 按 Enter 打开选中条目
                case Keys.F2: RenameSelected(); break; // 按 F2 编辑选中条目的显示名称
                case Keys.Delete: RemoveSelected(); break; // 按 Delete 移除选中条目
                default: return; // 未匹配的键继续由表格正常处理
            } // 结束此代码块：通过完整键值识别不带修饰键的命令
            e.SuppressKeyPress = true; // 防止已处理的快捷键再触发表格默认动作
        } // 结束此代码块：处理表格接收到的操作快捷键

        private void OpenSelected() // 打开选中的快捷方式
        { // 开始此代码块：打开选中的快捷方式
            int index = SelectedIndex; // 获取当前选中行的索引
            if (index < 0) // 检查是否存在选中条目
                return; // 没有选中项时结束操作

            RunAction("无法打开快捷方式。请检查目标是否仍可访问。", delegate // 单独报告打开快捷方式失败
            { // 开始打开所选条目
                store.Open(index); // 将快捷方式交给 Windows Shell，失败时停止后续更新
                RunAction("已发出打开请求，但无法保存更新时间。", delegate // 单独报告打开后的日期保存失败
                { // 开始保存并显示新的日期
                    store.RecordOpened(index); // 按本地日期持久化此次打开
                    grid.Rows[index].Cells[UpdatedDateColumn].Value = store.Entries[index].UpdatedDate; // 刷新本行的更新时间单元格
                }); // 结束日期更新
            }); // 结束打开条目
        } // 结束此代码块：打开选中的快捷方式

        private void RenameSelected() // 通过模态对话框修改选中条目名称
        { // 开始此代码块：通过模态对话框修改选中条目名称
            int index = SelectedIndex; // 获取当前选中条目的索引
            if (index < 0) // 检查表格是否有选中项
                return; // 无选中项时不显示对话框

            using (var dialog = new RenameDialog(store.Entries[index].Name, Font)) // 以当前显示名称和窗口字体构建编辑对话框
            { // 开始此代码块：以当前显示名称和窗口字体构建编辑对话框
                if (dialog.ShowDialog(this) == DialogResult.OK) // 只有用户确认保存时才应用新名称
                { // 开始此代码块：只有用户确认保存时才应用新名称
                    RunAction("无法保存名称。", delegate // 将持久化和界面更新包装在统一的错误提示中
                    { // 开始此代码块：将持久化和界面更新包装在统一的错误提示中
                        store.Rename(index, dialog.EntryName); // 持久化新的显示名称
                        grid.Rows[index].Cells[NameColumn].Value = store.Entries[index].Name; // 保存成功后刷新这一行名称单元格
                        grid.Rows[index].Cells[NameColumn].ToolTipText = store.Entries[index].Name; // 同步更新该名称的悬停提示
                    }); // 结束此代码块：将持久化和界面更新包装在统一的错误提示中
                } // 结束此代码块：只有用户确认保存时才应用新名称
            } // 结束此代码块：以当前显示名称和窗口字体构建编辑对话框
            grid.Focus(); // 编辑完成后把焦点交回列表
        } // 结束此代码块：通过模态对话框修改选中条目名称

        private void RemoveSelected() // 从清单移除当前选中项
        { // 开始此代码块：从清单移除当前选中项
            int[] indices = GetDeletionSelection(grid, this); // 检查条数并取得选中的行索引
            if (indices.Length == 0) // 没有可提交的删除批次时结束
                return; // 保留当前记录和选区

            RunAction("无法删除条目。", delegate // 执行移除，并统一处理保存失败提示
            { // 开始此代码块：执行移除，并统一处理保存失败提示
                int firstVisible = grid.FirstDisplayedScrollingRowIndex; // 记录当前滚动位置以便重绘后恢复
                int expiredCount = store.Remove(indices); // 提交删除并取得被自动淘汰的历史记录数
                if (expiredCount > 0) // 本次淘汰旧记录时更新待关闭提示
                    pendingExpiredCount = expiredCount; // 保存本次实际淘汰的条数
                RefreshRows(Math.Min(indices[0], store.Entries.Count - 1), firstVisible); // 刷新主清单并选中最靠近删除位置的剩余记录
                grid.Focus(); // 将键盘焦点交回表格
            }); // 结束此代码块：执行移除，并统一处理保存失败提示
        } // 结束此代码块：从清单移除当前选中项

        internal static int[] GetDeletionSelection(DataGridView grid, IWin32Window owner) // 为两个列表共用删除选区和条数检查
        { // 开始检查删除选区
            if (grid.SelectedRows.Count > EntryStore.DeleteBatchLimit) // 只在选中超过三十条时提示
            { // 开始显示条数超限提示
                MessageBox.Show(owner, EntryStore.DeleteBatchLimitMessage, Program.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information); // 显示指定的提示文案
                return new int[0]; // 阻止整批删除
            } // 结束显示条数超限提示
            return grid.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index).OrderBy(index => index).ToArray(); // 按显示顺序记录已选行索引
        } // 完成删除选区检查

        private void ShowRecentlyDeleted() // 在独立对话框中查看和处理删除历史
        { // 开始显示最近删除
            int previousCount = store.Entries.Count; // 记下还原前的条目数量
            using (var dialog = new RecentlyDeletedDialog(store, Font, grid.MultiSelect)) // 将当前测试模式传给最近删除窗口
                dialog.ShowDialog(this); // 等待关闭后再更新主清单

            if (store.Entries.Count > previousCount) // 只有手动还原才会使主清单增加
            { // 开始刷新还原结果
                RefreshRows(previousCount, -1); // 显示并选中第一条已还原记录
                grid.Focus(); // 把焦点交回主清单
            } // 完成主清单刷新
        } // 结束最近删除窗口流程

        private void RegisterDropTargets(Control control) // 为窗口及子控件递归注册拖放接收能力
        { // 开始此代码块：为窗口及子控件递归注册拖放接收能力
            control.AllowDrop = true; // 允许当前控件接收 Windows 拖拽数据
            control.DragEnter += DragOverTarget; // 指针进入控件时判断拖放效果
            control.DragOver += DragOverTarget; // 指针在控件中移动时更新拖放效果
            control.DragDrop += DropOnTarget; // 在释放拖拽物时执行导入或排序
            foreach (Control child in control.Controls) // 遍历当前控件的所有直接子控件
                RegisterDropTargets(child); // 让子控件也能接收文件或传递拖放反馈
        } // 结束此代码块：为窗口及子控件递归注册拖放接收能力

        private void GridMouseDown(object sender, MouseEventArgs e) // 记录可能开始行拖拽的鼠标按下事件
        { // 开始此代码块：记录可能开始行拖拽的鼠标按下事件
            pressedRow = -1; // 清除上次尚未开始的拖拽行
            if (e.Button != MouseButtons.Left || (grid.MultiSelect && (ModifierKeys & (Keys.Control | Keys.Shift)) != 0)) // 留出 Ctrl 和 Shift 的多选手势
                return; // 不将多选手势识别为开始拖拽

            int row = grid.HitTest(e.X, e.Y).RowIndex; // 根据鼠标坐标找出对应的数据行
            if (row < 0) // 检查是否点中了表头或空白区域
                return; // 未点中有效数据行时结束处理

            pressedRow = row; // 记住这次可能要拖动的数据行
            Size size = SystemInformation.DragSize; // 读取 Windows 定义的拖拽开始阈值
            dragBounds = new Rectangle(e.X - size.Width / 2, e.Y - size.Height / 2, size.Width, size.Height); // 以按下点为中心计算允许轻微移动的范围
        } // 结束此代码块：记录可能开始行拖拽的鼠标按下事件

        private void GridMouseMove(object sender, MouseEventArgs e) // 在鼠标移动事件中尝试开始拖拽
        { // 开始此代码块：在鼠标移动事件中尝试开始拖拽
            if (pressedRow < 0 || e.Button != MouseButtons.Left || dragBounds.Contains(e.Location)) // 要求左键按在数据行上且移动距离超过阈值
                return; // 未满足启动条件时继续等待
            if (grid.MultiSelect && SelectedIndex < 0) // 多行选区不参与单条拖动排序
                return; // 保留当前选区

            draggedRow = pressedRow; // 将按下行记作正式拖动的条目
            pressedRow = -1; // 清除等待状态，避免重复启动拖拽
            try // 在启动拖拽后确保清理反馈和状态
            { // 开始此代码块：在启动拖拽后确保清理反馈和状态
                grid.DoDragDrop(new DataObject("DeskToDo.ListRow", "move"), DragDropEffects.Move); // 用本应用定义的数据格式发起移动拖拽
            } // 结束此代码块：在启动拖拽后确保清理反馈和状态
            finally // 拖拽完成或取消时统一清理
            { // 开始此代码块：拖拽完成或取消时统一清理
                draggedRow = -1; // 清除正在拖动的行索引
                ClearDropPosition(); // 停止自动滚动并移除插入线
            } // 结束此代码块：拖拽完成或取消时统一清理
        } // 结束此代码块：在鼠标移动事件中尝试开始拖拽

        private void DragOverTarget(object sender, DragEventArgs e) // 计算鼠标悬停位置允许的拖放操作
        { // 开始此代码块：计算鼠标悬停位置允许的拖放操作
            e.Effect = DragDropEffects.None; // 默认显示禁止放置，直到识别出支持的数据
            if (e.Data == null) // 检查事件是否带有拖放数据
                return; // 没有数据时保持禁止放置

            if (draggedRow >= 0 && sender == grid && e.Data.GetDataPresent("DeskToDo.ListRow")) // 将带内部格式的表格行拖拽识别为排序
            { // 开始此代码块：将带内部格式的表格行拖拽识别为排序
                Point point = grid.PointToClient(new Point(e.X, e.Y)); // 将屏幕坐标转换为表格内部坐标
                SetDropPosition(GetInsertionIndex(point)); // 根据鼠标所在位置更新插入线
                if (dropIndex >= 0) // 检查该位置能否插入行
                { // 开始此代码块：检查该位置能否插入行
                    e.Effect = DragDropEffects.Move; // 通知 Windows 当前拖放效果是移动清单条目
                    scrollTimer.Start(); // 开启定时器以检查是否需要自动滚动
                } // 结束此代码块：检查该位置能否插入行
            } // 结束此代码块：将带内部格式的表格行拖拽识别为排序
            else if (e.Data.GetDataPresent(DataFormats.FileDrop) && (e.AllowedEffect & DragDropEffects.Copy) != 0) // 检查是否由外部提供文件路径且允许复制
            { // 开始此代码块：检查是否由外部提供文件路径且允许复制
                e.Effect = DragDropEffects.Copy; // 用复制效果接收路径，确保来源不会被移动
            } // 结束此代码块：检查是否由外部提供文件路径且允许复制
        } // 结束此代码块：计算鼠标悬停位置允许的拖放操作

        private void DropOnTarget(object sender, DragEventArgs e) // 处理放下文件或清单行的操作
        { // 开始此代码块：处理放下文件或清单行的操作
            if (e.Data == null) // 检查拖放事件是否包含数据
                return; // 无数据时结束拖放处理

            if (draggedRow >= 0 && sender == grid && e.Data.GetDataPresent("DeskToDo.ListRow")) // 判断是否把内部条目放回表格
            { // 开始此代码块：判断是否把内部条目放回表格
                int insertion = GetInsertionIndex(grid.PointToClient(new Point(e.X, e.Y))); // 以释放时的指针位置计算最终插入索引
                ClearDropPosition(); // 移除拖拽期间的视觉和滚动反馈
                if (insertion >= 0) // 只在释放到合法落点时提交排序
                { // 开始此代码块：只在释放到合法落点时提交排序
                    RunAction("无法保存排序。", delegate // 在统一错误提示的保护下保存顺序
                    { // 开始此代码块：在统一错误提示的保护下保存顺序
                        int firstVisible = grid.FirstDisplayedScrollingRowIndex; // 记录表格首个可见行以尽量保持滚动位置
                        RefreshRows(store.Move(draggedRow, insertion), firstVisible); // 持久化移动后的顺序，并选中被移动的行
                    }); // 结束此代码块：在统一错误提示的保护下保存顺序
                } // 结束此代码块：只在释放到合法落点时提交排序
                return; // 内部拖拽处理完毕，不继续按外部文件导入
            } // 结束此代码块：判断是否把内部条目放回表格

            var paths = e.Data.GetData(DataFormats.FileDrop) as string[]; // 从系统拖放数据读取文件系统路径数组
            if (paths == null || (e.AllowedEffect & DragDropEffects.Copy) == 0) // 确认拖入的是受支持的文件数据且来源允许复制
                return; // 忽略无效数据或不能以复制方式接收的拖放

            RunAction("无法保存拖入的项目。", delegate // 按统一错误提示方式执行外部导入
            { // 开始此代码块：按统一错误提示方式执行外部导入
                int firstAdded = store.Entries.Count; // 记录新批次在列表中的起始位置
                var failures = new List<string>(); // 创建用于收集各路径失败详情的列表
                bool limitReached; // 接收整次拖入是否因空间不足被拒绝
                int added = store.Import(paths, failures, out limitReached); // 检查整次拖入的容量并取得成功数量
                if (limitReached) // 空间不足时只显示对应的容量提示
                { // 开始显示拖入被拒绝的提示
                    if (paths.Length > 1) // 批量拖入时提示减少文件数量
                        MessageBox.Show(this, "列表可用空间不足，请减少选取文件数！", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); // 显示批量拖入的指定文案
                    else // 单文件拖入已满的列表时沿用原提示
                        ShowListFull(this); // 提示主清单已满
                    return; // 结束本次拖入处理
                } // 完成容量提示
                if (added > 0) // 至少有一项成功时刷新可见列表
                { // 开始此代码块：至少有一项成功时刷新可见列表
                    RefreshRows(firstAdded, -1); // 将列表选中位置移动到第一条新记录
                    grid.Focus(); // 把焦点设为列表，方便立即进行键盘操作
                } // 结束此代码块：至少有一项成功时刷新可见列表
                if (failures.Count > 0) // 检查本批是否包含导入失败的项目
                { // 开始此代码块：检查本批是否包含导入失败的项目
                    string summary = string.Format("{0} 项未能添加。\n\n{1}", failures.Count, // 构造包含失败总数和详细示例的摘要
                        string.Join("\n\n", failures.GetRange(0, Math.Min(3, failures.Count)))); // 最多列出三个失败示例，以避免对话框过长
                    MessageBox.Show(this, summary, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); // 显示部分或全部导入失败的提示
                } // 结束此代码块：检查本批是否包含导入失败的项目
            }); // 结束此代码块：按统一错误提示方式执行外部导入
        } // 结束此代码块：处理放下文件或清单行的操作

        internal static void ShowListFull(IWin32Window owner) // 让拖入和还原使用同一条满额提示
        { // 开始显示满额提示
            MessageBox.Show(owner, "可记录列表已满！", Program.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information); // 显示指定的提示文案
        } // 结束显示满额提示

        private int GetInsertionIndex(Point point) // 计算鼠标指向的行间插入索引
        { // 开始此代码块：计算鼠标指向的行间插入索引
            var hit = grid.HitTest(point.X, point.Y); // 检测鼠标是否位于数据单元格或表格空白处
            if (hit.Type == DataGridViewHitTestType.Cell) // 当命中数据单元格时根据上下半行决定落点
            { // 开始此代码块：当命中数据单元格时根据上下半行决定落点
                Rectangle row = grid.GetRowDisplayRectangle(hit.RowIndex, false); // 读取该行在表格客户区中的矩形坐标
                return point.Y < row.Top + row.Height / 2 ? hit.RowIndex : hit.RowIndex + 1; // 上半行插到本行之前，下半行插到本行之后
            } // 结束此代码块：当命中数据单元格时根据上下半行决定落点

            if (point.Y >= grid.ColumnHeadersHeight && grid.ClientRectangle.Contains(point) // 检查位置是否在表头下方且仍位于表格内部
                && hit.Type == DataGridViewHitTestType.None) // 只有非单元格、非滚动条的空白区视为追加目标
                return grid.RowCount; // 将放到表格尾部空白的行追加到末尾
            return -1; // 对表头、滚动条等无效区域返回负索引
        } // 结束此代码块：计算鼠标指向的行间插入索引

        private void SetDropPosition(int index) // 更新排序拖拽的插入线位置
        { // 开始此代码块：更新排序拖拽的插入线位置
            if (dropIndex == index) // 判断落点与上次记录是否相同
                return; // 落点没变时跳过重复重绘
            dropIndex = index; // 记录新的插入索引
            grid.Invalidate(); // 请求表格重绘以显示或清除插入线
        } // 结束此代码块：更新排序拖拽的插入线位置

        private void ClearDropPosition() // 清除排序拖拽的反馈和定时器
        { // 开始此代码块：清除排序拖拽的反馈和定时器
            scrollTimer.Stop(); // 停止检查自动滚动
            SetDropPosition(-1); // 将插入位置重设为无效
        } // 结束此代码块：清除排序拖拽的反馈和定时器

        private void ScrollWhileDragging(object sender, EventArgs e) // 在内部拖拽靠近表格边缘时滚动
        { // 开始此代码块：在内部拖拽靠近表格边缘时滚动
            Point point = grid.PointToClient(Cursor.Position); // 把系统鼠标位置转为表格客户区坐标
            int first = grid.FirstDisplayedScrollingRowIndex; // 取得当前第一个可见行
            if (!grid.ClientRectangle.Contains(point) || first < 0 || point.Y < grid.ColumnHeadersHeight) // 检查鼠标是否在有效区域且表格中有可见行
                return; // 无法从当前位置滚动时结束本次检查

            const int edge = 20; // 将靠近上下边缘的触发距离设为 20 像素
            int next = first; // 默认维持首个可见行
            if (point.Y < grid.ColumnHeadersHeight + edge && first > 0) // 鼠标接近上边缘且上方还有数据时允许向上滚
                next--; // 将第一个可见行向前移动一行
            else if (point.Y >= grid.ClientSize.Height - edge && first + grid.DisplayedRowCount(false) < grid.RowCount) // 鼠标接近下边缘且后面还有数据时允许向下滚
                next++; // 将第一个可见行向后移动一行

            if (next != first) // 只在滚动目标变化时更新表格
            { // 开始此代码块：只在滚动目标变化时更新表格
                grid.FirstDisplayedScrollingRowIndex = next; // 应用新的滚动位置
                SetDropPosition(GetInsertionIndex(point)); // 用滚动后的行位置重新计算插入线
                grid.Invalidate(); // 请求重绘插入线和可见内容
            } // 结束此代码块：只在滚动目标变化时更新表格
        } // 结束此代码块：在内部拖拽靠近表格边缘时滚动

        private void PaintGrid(object sender, PaintEventArgs e) // 绘制表格空态说明和拖拽插入线
        { // 开始此代码块：绘制表格空态说明和拖拽插入线
            if (grid.RowCount == 0) // 空列表时绘制居中的拖入提示
            { // 开始此代码块：空列表时绘制居中的拖入提示
                Rectangle area = grid.ClientRectangle; // 取得表格的客户区矩形
                area.Y += grid.ColumnHeadersHeight; // 将绘制区域上边移到表头下方
                area.Height -= grid.ColumnHeadersHeight; // 从绘制区域的高度中扣除表头
                TextRenderer.DrawText(e.Graphics, "把桌面文件拖到这里", Font, area, Muted, // 用主窗口字体和提示文字色绘制说明
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); // 在剩余表格区域水平和垂直居中
            } // 结束此代码块：空列表时绘制居中的拖入提示
            else if (dropIndex >= 0) // 非空列表且存在有效落点时绘制插入线
            { // 开始此代码块：非空列表且存在有效落点时绘制插入线
                bool afterLast = dropIndex == grid.RowCount; // 判断是追加到最后一行之后，还是插到某行之前
                Rectangle row = grid.GetRowDisplayRectangle(afterLast ? dropIndex - 1 : dropIndex, false); // 选择用于定位插入线的那一行
                int y = afterLast ? row.Bottom : row.Top; // 追加时使用行下边缘，否则使用目标行上边缘
                if (y >= grid.ColumnHeadersHeight && y <= grid.ClientSize.Height) // 仅在插入线处于可见的表格内容区时绘制
                { // 开始此代码块：仅在插入线处于可见的表格内容区时绘制
                    using (var pen = new Pen(Accent, 2)) // 创建绘制完成后可自动释放的画笔
                        e.Graphics.DrawLine(pen, 1, y, row.Right - 1, y); // 横跨数据区域绘制两像素宽的插入线
                } // 结束此代码块：仅在插入线处于可见的表格内容区时绘制
            } // 结束此代码块：非空列表且存在有效落点时绘制插入线
        } // 结束此代码块：绘制表格空态说明和拖拽插入线

        private void RunAction(string message, Action action) // 让界面操作使用一致的异常提示
        { // 开始此代码块：让界面操作使用一致的异常提示
            try { action(); } // 尝试执行调用方提供的操作
            catch (Exception error) // 捕获操作中抛出的异常
            { // 开始此代码块：捕获操作中抛出的异常
                MessageBox.Show(this, message + "\n\n" + error.Message, Text, // 将友好的操作提示和具体异常内容一并显示
                    MessageBoxButtons.OK, MessageBoxIcon.Warning); // 显示带确定按钮和警告图标的消息框
            } // 结束此代码块：捕获操作中抛出的异常
        } // 结束此代码块：让界面操作使用一致的异常提示

        protected override void Dispose(bool disposing) // 释放主窗口及其持有的资源
        { // 开始此代码块：释放主窗口及其持有的资源
            if (disposing && scrollTimer != null) // 判断是否应释放托管资源且定时器已初始化
                scrollTimer.Dispose(); // 释放拖拽滚动定时器
            base.Dispose(disposing); // 委托 WinForms 释放其余窗体资源
        } // 结束此代码块：释放主窗口及其持有的资源
    } // 结束此代码块：定义桌面清单的主窗口
} // 结束此代码块：将主窗体放在应用命名空间
