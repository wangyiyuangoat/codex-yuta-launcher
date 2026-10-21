# Codex Yuta Launcher

为 Windows 版 Codex 制作的非官方乙骨忧太与里香主题启动动画。

戒指在暗场中发光，人物缓缓睁眼、里香显现；结尾保留人物、里香和刀光的轻微分层视差，再渐隐露出 Codex。当前版本 **v2.1.0**，时长约 **9.2 秒**。

![动画封面](docs/cover.png)

[观看预览](docs/preview.mp4) · [下载最新版本](https://github.com/wangyiyuangoat/codex-yuta-launcher/releases/latest) · [完整安装说明](docs/INSTALL.md)

## 把链接发给 Codex

复制下面这句话，发给运行在你 Windows 电脑上的 Codex：

> 请帮我下载并安装这个 Windows 版 Codex 启动动画：https://github.com/wangyiyuangoat/codex-yuta-launcher ，先阅读 README，安装后创建独立桌面快捷方式。

仓库包含源码、图片素材和本地安装脚本。Codex 可以检查脚本后下载并安装；安装时会在本机编译启动器。

## 手动安装

1. 从 [最新 Release](https://github.com/wangyiyuangoat/codex-yuta-launcher/releases/latest) 下载 `Codex-Yuta-Launcher-v2.1.0.zip` 并解压，也可以克隆本仓库。
2. 在解压后的项目目录打开 PowerShell，执行：

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1
   ```

3. 双击桌面新建的 **Codex - Yuta** 快捷方式。

需要 Windows 10/11、可用的 .NET Framework 4.x WPF 组件，以及已安装的 Codex 桌面应用。安装脚本不安装依赖、不要求管理员权限，也不会自动打开 Codex。非默认安装方式可指定 `-AppId`，详见 [安装说明](docs/INSTALL.md)。

## 使用方式

- 启动时播放动画，同时打开 Codex。
- 按 **Esc / 空格** 或点击动画可跳过。
- 动画窗口会匹配检测到的 Codex 窗口大小和位置。
- 安装仅创建独立快捷方式，保留官方快捷方式；不修改官方应用文件。
- 蓝色应用配色是可选的手动导入主题，详见 [安装说明](docs/INSTALL.md#可选蓝色主题)。

## 卸载

在项目目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Uninstall.ps1
```

需要同时删除本项目安装文件时添加 `-RemoveFiles`。卸载脚本依据安装清单识别本项目文件；不会卸载 Codex。

## 项目内容

```text
source/Launcher.cs      原生 WPF 启动器源码
source/build.ps1        本地构建脚本
assets/                动画素材与图标
Install.ps1            安装、构建和独立快捷方式
Uninstall.ps1          按安装清单卸载
prompts/               图像生成提示词
docs/                  预览、封面与安装说明
```

这是个人制作的非官方同人动画，与 OpenAI 或原作权利方无隶属或背书关系。角色及相关作品权利归各自权利方所有；本仓库不对第三方内容授予额外权利。
