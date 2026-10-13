# 桌面架构

应用使用 .NET 8 Windows Forms 管理布局，系统 Edge WebView2 承载官方 B 站登录页与直播页。登录、账号检查、直播和用户打开的网页弹窗共享浏览器环境。唯一第三方依赖为 `Microsoft.Web.WebView2`，版本固定于项目文件。

## 模块

| 文件 | 责任 |
| --- | --- |
| Program.cs | 桌面入口、运行参数、错误日志 |
| Core.cs | 登录响应规则、房号校验、房间与音量配置 |
| BrowserSession.cs | 共享会话、音量同步、官方弹窗、导航等待 |
| MonitorWindow.cs | 登录门禁、直播库、房间生命周期与布局操作 |
| RoomPane.cs | 播放页、视频／网页模式、音频选择、播放状态标题 |
| PlayerScripts.cs | 官方播放器布局与媒体音量同步 |
| RoomLibrary.cs | 封面、标题、状态列表与鼠标／键盘选择 |
| RoomInfo.cs | 官方房间资料解析与封面读取 |
| RoomWorkspace.cs | 共享边界缩放、位置交换、比例排列与网页展开恢复 |
| Ui.cs | 原生界面颜色、字体、按钮和音量滑杆 |

## 生命周期

启动读取 `.runtime/workspace.json`，加载官方登录页，并在同一会话中验证账号。仅当登录接口返回 `code=0`、`isLogin=true` 与有效 UID 时允许观看。开始观看前再次验证；登录失效或账号切换会关闭已有直播页。网络验证失败不清除浏览器会话，但暂停开启新房间。

每个选中的房间对应一个 WebView2 控件。改变比例、交换位置、展开官方网页再返回，均保留已有控件。移除或停止观看会释放控件；退出登录还会清除站点数据。

官方网页负责播放、弹幕和观看上报。应用仅检查 video 的播放状态以更新标题，不累计任务时长，也不拦截或生成观看上报。任务进度查看官方粉丝团页面。

房间资料通过现有账号检查视图串行读取；未观看房间不创建播放器。封面只读取官方 CDN 的 HTTPS 图片，替换、删除与关闭时释放图像资源。

## 目录与验证

- `src/DDMonitor/`：正式桌面源码。
- `tests/DDMonitor.Tests/`：独立的规则与原生控件回归测试。
- `scripts/build-desktop.ps1`：编译到 `.runtime/desktop/`。
- `.runtime/web-profile/`：浏览器会话和缓存。
- `.runtime/workspace.json`：房号与音量。
- `.runtime/desktop.log`：应用日志。

使用 README 中的命令运行离线测试。实际观看依赖开播状态、登录与官方网页响应；是否完成粉丝勋章任务以服务端任务面板为准。
