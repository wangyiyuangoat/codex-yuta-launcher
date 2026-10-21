# 安装与使用

本项目为 Windows 本地启动器：先显示动画，同时启动已安装的 Codex 桌面应用，随后动画自动关闭。它不替换 Codex，不修改官方应用文件。

## 环境

- Windows 10/11。
- 可用的 .NET Framework 4.x、WPF 组件和框架自带的 C# 编译器。
- 已安装并能正常打开的 Codex 桌面应用。

安装不需要管理员权限，也不会自动安装运行时或其他依赖。若缺少环境，脚本会给出错误信息；应先处理错误再继续。

## 获取项目

从 [最新 Release](https://github.com/wangyiyuangoat/codex-yuta-launcher/releases/latest) 下载 `Codex-Yuta-Launcher-v2.1.0.zip`，解压后进入包含 `Install.ps1` 的目录。

也可以使用 Git：

```powershell
git clone https://github.com/wangyiyuangoat/codex-yuta-launcher.git
Set-Location .\codex-yuta-launcher
```

请先阅读 `Install.ps1`，确认将要复制的文件、安装位置及快捷方式操作，再执行脚本。

## 安装

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1
```

默认安装到当前用户的 `%LOCALAPPDATA%\CodexYutaLauncher`，在本机编译启动器，并创建独立的 **Codex - Yuta** 桌面快捷方式。官方 Codex 快捷方式保留。默认安装完成后不会自动播放动画或打开 Codex。

`-ExecutionPolicy Bypass` 只用于本次 PowerShell 进程，不会永久修改系统执行策略。

### 可选参数

| 参数 | 用途 |
| --- | --- |
| `-InstallDir` | 指定本项目的安装目录，建议使用独立的新目录。 |
| `-AppId` | 指定 Codex 的 Microsoft Store 应用标识（AUMID）。 |
| `-Preview` | 安装完成后播放一次动画预览。 |
| `-NoShortcut` | 不创建桌面快捷方式，适合检查或测试安装。 |

例如，指定安装目录：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -InstallDir "$env:LOCALAPPDATA\CodexYutaLauncher"
```

如果安装器无法找到你的 Codex，可先查看本机开始菜单应用标识：

```powershell
Get-StartApps | Where-Object { $_.Name -match 'Codex' }
```

将确认属于 Codex 桌面应用的 `AppID` 传入安装器：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1 -AppId '在这里填写确认后的 AppID'
```

不要使用 Codex 命令行可执行文件来代替桌面应用标识。安装器支持的目标是 Windows 桌面应用。

## 播放

双击 **Codex - Yuta** 快捷方式。动画约 9.2 秒，**Esc / 空格 / 点击**可跳过。

启动器会尝试使用 Codex 窗口的位置、尺寸与 DPI。应用尚未出现时可能先使用回退窗口尺寸，随后匹配检测到的窗口。多窗口、最小化状态或不同显示器的 DPI 会影响可检测到的窗口。

动画结束后启动器退出；Codex 独立运行。

## 可选蓝色主题

动画本身已使用蓝色配色。若希望 Codex 应用外观也接近动画，可手动导入 [Cool-Blue-Theme.txt](Cool-Blue-Theme.txt) 中的完整主题字符串。

在 Codex 的设置中打开外观设置，找到深色主题的导入入口并粘贴。入口名称可能因应用版本不同而略有差异。安装器不会自动修改应用设置。

该主题文件包含默认字体设置。若希望保留现有字体，只需在外观设置中手动使用下列颜色：

| 项目 | 颜色 |
| --- | --- |
| 背景 | `#07111F` |
| 文字 | `#E7F3FF` |
| 强调色 | `#66D9FF` |

## 卸载

使用项目提供的卸载脚本：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Uninstall.ps1
```

默认移除本项目创建的独立快捷方式。需要同时移除本项目安装文件时：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Uninstall.ps1 -RemoveFiles
```

如果安装时指定过 `-InstallDir`，卸载时也传入同一目录。卸载依赖项目安装清单；不要手工将其他个人文件放入安装目录。Codex 桌面应用和官方快捷方式不属于卸载范围。

## 常见问题

**提示找不到 Codex：**先确认能从开始菜单打开桌面应用，再按上文检查 `AppID`，用 `-AppId` 重新安装。

**提示找不到编译器或 WPF：**本项目需要 Windows 的 .NET Framework 4.x 环境。查看脚本报错所列路径；安装脚本不会自行下载依赖。

**只想看动画：**运行安装后的 `Preview.cmd`，或者安装时添加 `-Preview`。

**需要检查源码：**入口在 `source/Launcher.cs`，构建方式在 `source/build.ps1`，图像生成提示词在 `prompts/`。
