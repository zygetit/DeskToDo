using System.Drawing; // 导入字体、大小和边距所需的绘图类型
using System.Windows.Forms; // 导入窗体、文本框和布局控件

namespace DeskToDo // 将对话框放在应用命名空间
{ // 开始此代码块：将对话框放在应用命名空间
    internal sealed class RenameDialog : Form // 定义用于修改条目显示名称的模态窗口
    { // 开始此代码块：定义用于修改条目显示名称的模态窗口
        private readonly TextBox name; // 持有显示名称输入框

        public string EntryName { get { return name.Text; } } // 读取用户在输入框中的文字

        public RenameDialog(string currentName, Font font) // 根据当前名称和主窗口字体初始化对话框
        { // 开始此代码块：根据当前名称和主窗口字体初始化对话框
            Text = "重命名条目"; // 设置对话框标题
            Font = font; // 沿用主窗口传入的界面字体
            AutoScaleMode = AutoScaleMode.Font; // 按系统字体度量缩放控件
            FormBorderStyle = FormBorderStyle.FixedDialog; // 使用固定尺寸的对话框边框
            StartPosition = FormStartPosition.CenterParent; // 将对话框居中在所属窗口上
            ShowInTaskbar = false; // 不为对话框单独创建任务栏按钮
            MinimizeBox = false; // 禁用最小化按钮
            MaximizeBox = false; // 禁用最大化按钮
            ClientSize = new Size(370, 114); // 设置初始客户区大小

            var layout = new TableLayoutPanel // 建立包含说明、输入框和按钮的布局
            { // 开始此代码块：建立包含说明、输入框和按钮的布局
                Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 // 填满窗口并预留边距，使用一列三行
            }; // 结束此代码块：建立包含说明、输入框和按钮的布局
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // 让唯一一列占满可用宽度
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 让名称标签行按所需高度布局
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 让输入框行按所需高度布局
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 让按钮行使用剩余高度
            Controls.Add(layout); // 将布局挂到对话框

            layout.Controls.Add(new Label { Text = "显示名称", AutoSize = true }, 0, 0); // 在第一行添加显示名称标签
            name = new TextBox { Text = currentName, Dock = DockStyle.Top, AccessibleName = "显示名称" }; // 初始化文本框为当前名称，并提供无障碍名称
            layout.Controls.Add(name, 0, 1); // 把输入框放到第二行

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; // 创建由右向左排列按钮的容器
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true }; // 创建取消按钮并返回取消结果
            var save = new Button { Text = "保存", DialogResult = DialogResult.OK, AutoSize = true }; // 创建保存按钮并返回确认结果
            buttons.Controls.AddRange(new Control[] { cancel, save }); // 将保存和取消操作放在同一个按钮栏
            layout.Controls.Add(buttons, 0, 2); // 把按钮栏放到最后一行

            name.TextChanged += delegate { save.Enabled = !string.IsNullOrWhiteSpace(name.Text); }; // 名称只含空白时禁用保存按钮
            AcceptButton = save; // 允许按 Enter 确认保存
            CancelButton = cancel; // 允许按 Esc 取消编辑
            ActiveControl = name; // 打开对话框时将焦点放入名称输入框
            name.SelectAll(); // 初始选中整个名称，方便直接输入替换
        } // 结束此代码块：根据当前名称和主窗口字体初始化对话框
    } // 结束此代码块：定义用于修改条目显示名称的模态窗口
} // 结束此代码块：将对话框放在应用命名空间
