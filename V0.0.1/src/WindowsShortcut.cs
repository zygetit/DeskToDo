using System; // 导入 IntPtr 等基础类型
using System.IO; // 导入工作目录的路径处理类型
using System.Runtime.InteropServices; // 导入 COM 接口声明与内存互操作类型
using System.Runtime.InteropServices.ComTypes; // 导入用于保存快捷方式的 IPersistFile 接口

namespace DeskToDo // 将 Windows Shell 调用放在应用命名空间
{ // 开始此代码块：将 Windows Shell 调用放在应用命名空间
    internal static class WindowsShortcut // 集中实现生成 Windows 快捷方式的功能
    { // 开始此代码块：集中实现生成 Windows 快捷方式的功能
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")] // 用系统 CLSID 声明 Shell Link COM 类
        private class ShellLink { } // 作为实例化 Windows Shell Link 对象的托管声明

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046")] // 用系统 IID 标识支持 Unicode 的 Shell Link 接口
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] // 声明该接口从 IUnknown 继承
        private interface IShellLinkW // 按原生接口顺序列出快捷方式操作
        { // 开始此代码块：按原生接口顺序列出快捷方式操作
            // 以下声明必须保持 IShellLinkW 原生虚函数表的顺序
            void GetPath(IntPtr file, int capacity, IntPtr findData, uint flags); // 读取目标路径和关联文件信息
            void GetIDList(out IntPtr itemIdList); // 读取目标的 Shell 项目 ID 列表
            void SetIDList(IntPtr itemIdList); // 设置目标的 Shell 项目 ID 列表
            void GetDescription(IntPtr text, int capacity); // 读取快捷方式描述
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text); // 设置快捷方式描述并传递 UTF-16 字符串
            void GetWorkingDirectory(IntPtr directory, int capacity); // 读取目标启动时使用的工作目录
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory); // 设置目标启动时使用的工作目录
            void GetArguments(IntPtr arguments, int capacity); // 读取目标启动参数
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments); // 设置要传给目标的启动参数
            void GetHotkey(out short hotkey); // 读取关联的键盘快捷键
            void SetHotkey(short hotkey); // 设置关联的键盘快捷键
            void GetShowCmd(out int showCommand); // 读取启动时使用的窗口显示命令
            void SetShowCmd(int showCommand); // 设置启动时使用的窗口显示命令
            void GetIconLocation(IntPtr iconPath, int capacity, out int iconIndex); // 读取图标路径和图标索引
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex); // 设置快捷方式图标及其索引
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved); // 为 Shell 提供相对路径信息
            void Resolve(IntPtr owner, uint flags); // 请求 Windows Shell 解析快捷方式目标
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path); // 设置快捷方式目标的完整路径
        } // 结束此代码块：按原生接口顺序列出快捷方式操作

        public static void Create(string target, string destination, bool isDirectory) // 为文件或目录保存一个真实的 .lnk 文件
        { // 开始此代码块：为文件或目录保存一个真实的 .lnk 文件
            var link = (IShellLinkW)new ShellLink(); // 创建系统 Shell Link 对象并取得 Unicode 接口
            try // 确保后续 COM 调用失败时也会释放对象
            { // 开始此代码块：确保后续 COM 调用失败时也会释放对象
                link.SetPath(target); // 让新快捷方式指向原始文件或目录
                string workingDirectory = isDirectory ? target : Path.GetDirectoryName(target); // 目录使用自身作为工作目录，文件使用其父目录
                if (!string.IsNullOrEmpty(workingDirectory)) // 确认已经取得有效的工作目录
                    link.SetWorkingDirectory(workingDirectory); // 把工作目录写入快捷方式对象
                ((IPersistFile)link).Save(destination, true); // 通过 IPersistFile 将 COM 对象保存为 .lnk
            } // 结束此代码块：确保后续 COM 调用失败时也会释放对象
            finally // 结束快捷方式创建时执行资源清理
            { // 开始此代码块：结束快捷方式创建时执行资源清理
                Marshal.ReleaseComObject(link); // 释放本次创建的 Shell Link COM 对象
            } // 结束此代码块：结束快捷方式创建时执行资源清理
        } // 结束此代码块：为文件或目录保存一个真实的 .lnk 文件
    } // 结束此代码块：集中实现生成 Windows 快捷方式的功能
} // 结束此代码块：将 Windows Shell 调用放在应用命名空间
