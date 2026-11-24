param([string]$OutputDirectory) # 可指定验证构建的输出目录

$ErrorActionPreference = 'Stop' # 出现 PowerShell 错误时立即终止构建
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' # 定位系统自带的 .NET Framework C# 编译器
if (-not (Test-Path -LiteralPath $compiler)) { # 先确认编译器文件存在
    throw 'The Windows .NET Framework C# compiler is missing.' # 编译器缺失时报告明确原因
} # 结束编译器检查

if (-not $OutputDirectory) { # 默认使用项目根目录下的发布目录
    $OutputDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'dist' # 从源码目录向上一层找到 dist
} # 结束默认输出目录设置
$outputDirectoryFullPath = [IO.Path]::GetFullPath($OutputDirectory) # 将输出目录转换为完整路径
New-Item -ItemType Directory -Path $outputDirectoryFullPath -Force | Out-Null # 创建缺失的输出目录
$output = Join-Path $outputDirectoryFullPath 'DeskToDo.exe' # 指定输出的可执行文件名称
$manifest = Join-Path $PSScriptRoot 'app.manifest' # 定位要嵌入 EXE 的 Windows 应用清单
$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File # 读取源码目录中的全部 C# 文件

$compilerArgs = @( # 每项作为一个独立的编译器参数传递
    '/nologo' # 省略编译器启动横幅
    '/target:winexe' # 生成双击启动时不打开控制台的桌面程序
    '/platform:anycpu' # 由 .NET Framework 按当前系统选择进程架构
    '/optimize+' # 启用发布构建优化
    '/codepage:65001' # 以 UTF-8 读取中文源码和界面文字
    '/reference:System.Runtime.Serialization.dll' # 引用 .NET Framework 的数据契约和 JSON 类型
    '/reference:System.Xml.dll' # 引用序列化接口依赖的 XML 类型
    "/win32manifest:$manifest" # 将 Windows 应用清单嵌入 EXE
    "/out:$output" # 指定编译结果的完整路径
) + $sources.FullName # 将各个源文件的完整路径追加到参数列表

& $compiler @compilerArgs # 使用准备好的参数执行编译
if ($LASTEXITCODE -ne 0) { # 检查编译器的退出状态
    throw 'DeskToDo build failed.' # 编译失败时报告错误并结束脚本
} # 结束构建结果检查
Write-Output $output # 输出生成的 EXE 的完整路径
