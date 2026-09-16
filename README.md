# Honor Manager Revanced

通过读写笔记本 **EC（Embedded Controller）** 寄存器来切换性能模式、调整电源策略的
Windows 桌面工具。界面使用 **Fluent Design** 风格自绘，无第三方 UI 依赖。

> 本项目基于 WinRing0 提供的底层端口访问能力。**请先阅读文末「许可证」，**
> 它决定了你可以如何使用与再分发本软件。

---

## 功能

| 功能 | 说明 |
|---|---|
| EC 读写 | 通过 WinRing0 驱动直接读写 EC 寄存器，不依赖厂商软件 |
| 配置驱动 | 所有可用模式由 `Data\ec ADDRESS.txt` 定义，改文本即可扩展，无需改代码 |
| 写入校验 | 写入后自动回读校验，逐地址显示 `✔ 一致` / `✘ 不一致` |
| 登录自动应用 | 登录后按设定延迟自动套用指定模式，静默运行不打扰 |
| 开机启动 | 写入 HKCU `Run` 项，无需管理员即可开关 |
| 托盘常驻 | 最小化到托盘，支持「显示主界面 / 重新应用 / 退出」 |
| 驱动自愈 | 服务 `ImagePath` 失效时自动修正，驱动缺失时给出可操作的排查提示 |
| 日志 | 按天写入 `Logs\yyyy-MM-dd.log`，出错可直接复制给他人排查 |

---

## 运行要求

- Windows 10 / 11，x64
- **必须以管理员身份运行**（已通过 `app.manifest` 声明 `requireAdministrator`，
  双击即会弹 UAC）
- .NET Framework 4.7.2（系统自带）

---

## 目录结构

```
├── ECController.csproj        主工程（非 SDK 风格，供 msbuild 使用）
├── Program.cs                 入口：单实例、提权、静默模式
├── MainForm.cs / .Designer.cs 主界面
├── DriverErrorDialog.cs       初始化失败详情
├── RestoreDialog.cs           「恢复默认」含义选择
├── app.manifest               requireAdministrator + dpiAware
├── Config/
│   ├── AppSettings.cs         设置数据模型与默认值
│   ├── SettingsManager.cs     手写 JSON 读写（无第三方依赖）
│   └── AddressParser.cs       解析 Data\ec ADDRESS.txt
├── Driver/
│   ├── DriverLoader.cs        驱动路径解析、服务安装/修复、错误提示
│   └── EcAccess.cs            EC 协议时序（端口 0x62 / 0x66）
├── Models/                    EcGroup / EcMode / EcItem
├── Services/
│   ├── EcService.cs           业务层：读取、写入、应用、校验
│   ├── Logger.cs              按天滚动日志
│   ├── StartupManager.cs      开机启动（注册表，与 EC 逻辑解耦）
│   └── TrayManager.cs         托盘图标与菜单
├── Theme/                     Fluent 自绘控件
│   ├── FluentTheme.cs         配色、字体、圆角度量
│   ├── FluentControls.cs      按钮 / 卡片 / 复选 / 单选 / 下拉 / 文本框
│   └── FluentDataGridView.cs  Fluent 风格表格
├── Data/
│   └── ec ADDRESS.txt         ← EC 配置定义（开发者/用户可改）
├── Drivers/
│   └── WinRing0x64.sys        内核驱动（随包分发）
├── libs/
│   └── System.Resources.Extensions.dll
├── tools/                     辅助脚本（编译、校验、截图）
└── winring0/                  第三方 WinRing0 源码（GPL-3.0）
```

---

## 两条配置线的分工

这是本项目最重要的设计约定，两者职责**不重叠**：

| 文件 | 谁维护 | 定义什么 |
|---|---|---|
| `Data\ec ADDRESS.txt` | 开发者 / 高级用户 | **有哪些 EC 配置**（功能组、模式、地址与目标值） |
| `config.json` | 程序 / 用户界面 | **用户想怎么运行**（开机启动、托盘、自动应用哪个模式、延迟） |

`config.json` 首次运行不存在时使用内置默认值，只有用户改动设置后才会落盘。

### `Data\ec ADDRESS.txt` 格式

```
# 以 # 或 // 开头的是注释

[性能模式]          ← 功能组

智能模式            ← 模式名
78=00               ← 地址=数据（十六进制，与 0x78=0x00 等价）
79=A0

Hunter3
78=AA
79=AC
```

- 地址与数据都是十六进制，接受 `78` / `0x78` / `0X78`
- 文件缺失或格式出错**不会崩溃**，会在界面与日志中给出提示

---

## 构建

需要 .NET SDK（提供 `dotnet msbuild`）或 Visual Studio 2022。

```bash
# 命令行（Debug / Release）
python tools/build2.py ECController.csproj Release Rebuild
```

产物：`bin\Release\ECController.exe`

发布包目录结构：

```
ECController.exe
ECController.exe.config
config.json            ← 首运行自动生成
Data\ec ADDRESS.txt
Drivers\WinRing0x64.sys
Logs\
```

驱动查找顺序：先找 `Drivers\WinRing0x64.sys`，找不到再回退到 exe 同目录。

> 注意：`.resx` 中含图片资源，编译需要 `System.Resources.Extensions`。
> 本项目通过 `libs\` 下的 DLL 直接引用（而非 NuGet 还原），因此**无需联网即可编译**。

---

## 命令行参数

| 参数 | 用途 |
|---|---|
| （无） | 启动主界面 |
| `--apply-only` | 静默套用配置中的模式，不显示主界面（供登录自启使用） |
| `--show-ui` | 强制显示主界面 |

程序使用互斥体 `Global\ECController_SingleInstance` 保证单实例。

---

## 已知问题与设计取舍

### EC 端口存在并发串扰，读数必须连续一致才可信

EC 的命令/状态口 `0x66` 与数据口 `0x62` 是**全局共享资源**，系统 ACPI EC 驱动与
厂商电源管理软件都在用。我们走 WinRing0 直接读端口，绕过了操作系统的锁，
因此会和它们抢总线。

实测一次写入后连续读 4 次 `0x79`，可能得到：

```
写 0x79 = 0xAA  ->  读回 0x5A  0xAA  0xAA  0x5B
                        ↑错    ↑对   ↑对    ↑错
```

**结论**：单次读数不可信，写入本身是成功的。因此 `EcService.ReadStable()` 要求
**连续 2 次读到相同值**才认可该读数，最多尝试 10 次。这样既不会把成功的写入误判
为失败，也不会用「期望值」去筛读数——后者会把偶尔碰巧相等的错误读数当成成功，
是不诚实的做法。

### 「恢复默认」有三种含义

界面上把它们明确分开，避免误解：

- **A. 恢复 EC 到配置中的默认模式** —— 会写 EC 寄存器，属于硬件层面修改
- **B. 把 EC 恢复到任意指定值** —— 需自行在 `ec ADDRESS.txt` 里加一个模式
- **C. 恢复软件设置** —— 只重置 `config.json` 与注册表，不碰 EC

---

## 日志

`Logs\yyyy-MM-dd.log`，UTF-8，按天滚动。日志模块自身永不抛异常，
不会因为磁盘满或权限问题拖垮主流程。

初始化失败时可直接在错误对话框中点「复制错误信息」，其中包含系统版本、
运行路径、异常类型与调用栈。

---

## 许可证

**本项目按 GPL-3.0 分发**，见 `LICENSE`。

原因是本项目依赖并分发 [WinRing0](https://github.com/GermanAizek/WinRing0)
（`winring0/` 目录为其源码，`Drivers\WinRing0x64.sys` 为其编译产物），
而 WinRing0 采用 **GPL-3.0** 这一强 copyleft 许可。GPL-3.0 要求：
以整体形式分发时，整个组合作品须同样以 GPL-3.0 授权并提供完整源码。

因此：

- 你可以自由使用、修改、再分发本项目，但**衍生作品也必须以 GPL-3.0 开源**
- 若你需要闭源商业使用，必须**移除 WinRing0 相关部分**，改用其它授权兼容的
  内核驱动方案（例如自行签署的驱动，或改走系统的 ACPI EC 接口）

---

## 第三方

| 组件 | 许可 | 说明 |
|---|---|---|
| [WinRing0](https://github.com/GermanAizek/WinRing0) | GPL-3.0 | 提供 Ring0 端口 I/O |
| System.Resources.Extensions | MIT | .NET 官方组件，仅编译期需要 |

---

## 免责声明

直接读写 EC 寄存器属于**未经厂商授权的底层操作**，可能影响风扇转速、功耗墙、
充电策略等硬件行为。请确认你了解所写入地址的含义后再操作。
因使用本软件造成的任何后果由使用者自行承担。
