using System; // 导入异常和环境信息类型
using System.IO; // 导入路径拼接工具
using System.Runtime.Versioning; // 导入目标框架声明类型
using System.Windows.Forms; // 导入 WinForms 应用和对话框类型

[assembly: TargetFramework(".NETFramework,Version=v4.8")] // 声明应用面向 .NET Framework 4.8

namespace DeskToDo // 将应用入口放在 DeskToDo 命名空间
{ // 开始此代码块：将应用入口放在 DeskToDo 命名空间
    internal static class Program // 集中处理应用启动和退出
    { // 开始此代码块：集中处理应用启动和退出
        internal const string DisplayName = "DeskToDo"; // 统一窗口和启动提示中显示的应用名称
        [STAThread] // 让主线程使用 STA，以支持 Windows 拖放和 Shell COM
        private static void Main() // 定义 Windows 程序的启动入口
        { // 开始此代码块：定义 Windows 程序的启动入口
            Application.EnableVisualStyles(); // 启用 Windows 控件的视觉样式
            Application.SetCompatibleTextRenderingDefault(false); // 使用新版默认文本渲染方式

            string directory = Path.Combine( // 拼接当前用户的数据目录
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskToDo"); // 取当前用户的 LocalAppData 并附加应用目录名
            try // 捕获数据目录和界面初始化错误
            { // 开始此代码块：捕获数据目录和界面初始化错误
                using (var store = new EntryStore(directory)) // 打开列表存储，退出时释放文件锁
                using (var window = new MainForm(store)) // 创建主窗口并在退出时释放窗口资源
                    Application.Run(window); // 启动消息循环，直至用户关闭主窗口
            } // 结束此代码块：捕获数据目录和界面初始化错误
            catch (Exception error) // 接收启动过程抛出的异常
            { // 开始此代码块：接收启动过程抛出的异常
                MessageBox.Show("无法启动" + DisplayName + "。\n\n" + error.Message, DisplayName, // 提示用户启动失败并展示具体原因
                    MessageBoxButtons.OK, MessageBoxIcon.Error); // 为启动错误提供确定按钮和错误图标
            } // 结束此代码块：接收启动过程抛出的异常
        } // 结束此代码块：定义 Windows 程序的启动入口
    } // 结束此代码块：集中处理应用启动和退出
} // 结束此代码块：将应用入口放在 DeskToDo 命名空间
