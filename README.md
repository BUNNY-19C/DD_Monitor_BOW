<div align="center">

# DD 监控室

**先登录，再把喜欢的 B 站直播放进同一个窗口。**

![Windows](https://img.shields.io/badge/Windows-desktop-0078D4)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4)
![WebView2](https://img.shields.io/badge/Edge-WebView2-0078D7)
![License](https://img.shields.io/badge/License-LGPL--2.1-blue)

扫码登录 · 多直播同看 · 封面直播库 · 联动布局 · 低音量观看

[快速开始](#快速开始) · [使用方法](#使用方法) · [开发与测试](#开发与测试) · [常见问题](#常见问题)

</div>

DD 监控室是一个 Windows 桌面直播观看工具。原项目已重建为 **.NET 8 Windows Forms + 系统 Edge WebView2**：官方网页负责登录、播放、弹幕和观看上报，桌面程序负责房间管理、窗口排列和声音控制。

## 功能

| 功能 | 说明 |
| --- | --- |
| 登录后观看 | 在官方登录页扫码、密码或短信登录；验证成功后开放房间选择 |
| 封面直播库 | 展示直播封面、标题、主播、房号与开播状态；支持鼠标和键盘勾选 |
| 多窗口播放 | 最多同时观看 16 个房间，自动排列或固定 1–4 列 |
| 联动缩放 | 拖动共享边界调整邻窗，保持窗口铺满工作区；末行均分整行 |
| 位置交换 | 拖动标题到高亮窗口后松开，交换位置并保留播放器 |
| 视频／网页切换 | 默认集中显示视频，按需展开完整网页发送弹幕、查看粉丝团任务 |
| 统一音量 | 首次音量为 15%，保存调整结果；同时只让一个房间出声 |
| 原生桌面界面 | 深色直播库、可收起侧栏、原生控件，复用系统浏览器内核 |

当前版本面向 Windows，已在 Windows x64 上验证。直接依赖只有一个固定版本的 WebView2 SDK。

## 快速开始

### 1. 安装环境

- [Git for Windows](https://git-scm.com/download/win)：用于克隆仓库；也可从 GitHub 的 **Code → Download ZIP** 下载源码。
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)：选择 Windows 对应架构的 **SDK**，用于首次编译。
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/#download-section)：未安装时使用 Evergreen 安装程序。

### 2. 获取源码并启动

在 PowerShell 中执行：

```powershell
git clone https://github.com/BUNNY-19C/DD_Monitor.git
cd DD_Monitor
.\start.cmd
```

首次启动会自动编译到 `.runtime/desktop/`，之后直接启动已生成的桌面程序。首次编译需要联网还原 WebView2 SDK。

### 3. 登录并选择直播间

1. 用哔哩哔哩 App 扫描官方登录页二维码，也可使用密码或短信登录。
2. 登录验证通过后，在左侧输入房号或 `https://live.bilibili.com/房号`，添加直播间。
3. 勾选想一起观看的房间，点击「开始观看」。

## 使用方法

### 管理直播间

直播库显示封面、直播标题、主播、房号和状态。长标题悬停可查看全文；滚轮浏览列表，方向键选择，空格切换勾选。点击顶部「直播库」可收起侧栏，扩大播放区域。

### 调整布局

- 顶部可选择自动排列或 1–4 列。
- 拖动左右共享边界，调整同一行的邻窗宽度。
- 拖动上下共享边界，调整相邻行的高度；拖动右下角可同时调整两个方向。
- 拖动标题到另一个窗口，目标高亮后松开即可交换位置。
- 「更多」提供放大、缩小与刷新。

主窗口改变大小后按当前比例重新铺满。自定义窗口比例与位置只保留在本次观看会话中，顶部布局菜单可恢复均分排列。

### 网页、弹幕与粉丝勋章

默认视频模式保持原始比例，留白为黑色，并隐藏网页外围内容、弹幕和礼物特效。点击格内「网页」展开官方直播页；窄窗口的入口在「更多」菜单中。

完整网页中可使用官方弹幕输入框、查看粉丝团观看任务和亲密度。点击「返回多窗口」恢复视频布局，使用原播放器继续观看。

**是否计入粉丝勋章观看任务，以官方任务面板的服务端结果为准。** 应用的“正在播放”状态只来自网页 video 元素，不作为任务已结算的证明。

### 控制声音

首次音量为 15%，调整后保存。滑杆支持鼠标、方向键和 Home／End。每格「声音／静音」切换出声房间，同时只让一个房间出声。

需要静音打开指定房间时：

```powershell
.\.runtime\desktop\DDMonitor.exe --muted --room 4788550 --room 81004
```

`--room` 可以重复使用；应用仍先验证登录，再打开房间。

## 本机数据

| 路径 | 内容 |
| --- | --- |
| `.runtime/web-profile/` | WebView2 管理的登录会话、站点数据与浏览器缓存 |
| `.runtime/workspace.json` | 已添加房号与全局音量 |
| `.runtime/desktop.log` | 应用运行和错误日志 |
| `.runtime/desktop/` | 本机编译产物 |

登录页和直播页共享会话。登录失效或切换账号时会关闭已有直播页；网络验证失败时暂停开启新房间。「退出登录」停止观看并清除本应用的站点数据。

`.runtime/` 与编译产物均被 Git 忽略。不要将包含登录会话的浏览器目录作为源码或便携包分享。

## 开发与测试

需要 Windows、.NET 8 SDK 和 WebView2 Runtime。

```powershell
# 编译桌面程序
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build-desktop.ps1

# 运行离线回归测试
dotnet run --project tests/DDMonitor.Tests/DDMonitor.Tests.csproj -c Release

# 启动
.\start.cmd
```

修改源码后，关闭正在运行的程序、重新编译，再启动 `start.cmd`。启动脚本在程序已存在时不会自动重新编译。

测试不访问真实登录资料，覆盖登录响应规则、直播间输入、封面地址、配置保存、联动缩放、位置交换、完整网页返回、悬停刷新和按钮文字对比度。测试失败返回非零退出码。

2026-10-13 本地验证：**77 项回归检查通过，Release 构建 0 警告、0 错误**；真实登录会话可正常验证。此记录只描述当次验证结果。

### 项目结构

```text
DD_Monitor/
├─ src/DDMonitor/             # 正式桌面程序
├─ tests/DDMonitor.Tests/     # 独立回归测试
├─ scripts/build-desktop.ps1  # 编译入口
├─ docs/rebuild.md            # 架构与生命周期
├─ DESIGN.md                  # 界面约定
├─ start.cmd                  # 本地启动入口
└─ LICENSE
```

架构说明见 [docs/rebuild.md](docs/rebuild.md)，界面约定见 [DESIGN.md](DESIGN.md)。旧 Python／Qt／VLC 实现已从当前版本移除，旧源码与提交历史可在[上游仓库](https://github.com/zhimingshenjun/DD_Monitor)查看。本仓库从精简后的桌面重建版本开始记录。

## 常见问题

**第一次启动提示找不到 `dotnet`？**
安装 .NET 8 SDK 后重新打开终端。只有 Runtime 的环境无法从源码编译。

**直播一直显示“等待播放”？**
先确认房间已开播，再切换完整网页查看官方提示；可在「更多」中刷新。登录状态可用顶部「检查登录」重新验证。

**为什么只能听到一个房间？**
应用采用单房间出声，避免多个直播声音叠加。点击目标房间的「声音」切换。

**重启后为什么没有恢复窗口大小？**
当前只保存房号和音量；自定义布局保留在观看会话中。

**支持 macOS 或 Linux 吗？**
当前重建版本使用 Windows Forms 和 WebView2，面向 Windows。

## 许可证

项目基于 [zhimingshenjun/DD_Monitor](https://github.com/zhimingshenjun/DD_Monitor) 重建，沿用 [GNU LGPL 2.1 许可证](LICENSE)。感谢原作者与贡献者。
