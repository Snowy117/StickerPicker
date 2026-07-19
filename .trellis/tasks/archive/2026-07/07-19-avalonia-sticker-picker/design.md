# Design: StickerPicker (MVP-A)

## 1. Goals & non-goals

**Goals**

- 本地表情库：导入、分类、搜索、网格浏览
- 快速发送：全局热键呼出 → 单击复制剪贴板 → 隐藏窗口
- Steam-like 无圆角 UI，克制动画
- 可配置数据根（默认 LocalAppData）

**Non-goals**

- SuzuEmojy 数据兼容、云同步、WEBM、浏览器扒图、完整 DnD、WinUI 1:1

## 2. Solution structure

使用官方模板：`dotnet new avalonia.mvvm -n StickerPicker -f net10.0 -m CommunityToolkit`（Avalonia 默认 12.1.x）。

解决方案格式：**`.slnx`**（.NET 10 默认 / `dotnet new sln -f slnx`）。

推荐布局（实现时可微调，原则：UI 与领域/IO 分离 + **deep modules**）：

```text
StickerPicker.slnx
src/
  StickerPicker/                 # Avalonia Desktop 宿主 + Views/ViewModels
    App.axaml
    Program.cs
    Views/
    ViewModels/
    Controls/                    # 自写控件（StickerTile 等）
    Themes/                      # Steam-like 样式覆盖（无圆角、色板）
    Platform/Windows/            # 热键、剪贴板 Win32 互操作（adapters）
  StickerPicker.Core/            # 无 UI 依赖的领域 + 存储
    Models/
    Abstractions/                # small interfaces (seams)
    Library/                     # deep IStickerLibrary adapter(s)
    Config/
    Paths/
tests/
  StickerPicker.Core.Tests/      # 测 library/config 接口行为，不测 UI
```

**起步即双项目**：ViewModel 只依赖 Core 抽象，不直接拼文件路径。

### Stack choices

| 选择 | 决策 | 理由 |
|------|------|------|
| UI | Avalonia 12.1.x Desktop | 用户指定 + 模板默认 |
| TFM | `net10.0` | 用户指定；Windows 互操作通过 `RuntimeInformation` / 条件编译 |
| MVVM | CommunityToolkit.Mvvm | 模板默认，源生成器友好 |
| 主题基座 | 官方 FluentTheme + 自写 Styles | 不引 FluentAvalonia；用样式去圆角、改色板 |
| DI | 轻量手写或 `Microsoft.Extensions.DependencyInjection` | 服务少；App 启动时组装配即可 |
| JSON | `System.Text.Json` | BCL，nullable 友好 |
| 图像 | Avalonia 解码 + 需要时 `SixLabors.ImageSharp`（哈希/静态帧） | 动图发送靠**文件路径**进剪贴板，不必自己播完整动画引擎 |
| 全局热键 | Win32 `RegisterHotKey` + 消息钩子 | Avalonia 无内置全局热键 |
| 托盘 | Avalonia `TrayIcon` | 官方支持 |
| 剪贴板（发送） | Win32 / Windows DataPackage 风格：`CF_HDROP`/`FileDrop` + `CF_DIB`/Bitmap | 聊天软件依赖文件路径才能发 GIF 原文件 |

## 3. Architecture

```text
┌─────────────────────────────────────────────────────────┐
│  Views (AXAML) + Themes (Steam-like)                    │
│  MainWindow | GalleryView | SettingsView | StickerTile  │
└───────────────────────────┬─────────────────────────────┘
                            │ bindings / commands
┌───────────────────────────▼─────────────────────────────┐
│  ViewModels (CommunityToolkit)                          │
│  MainViewModel | GalleryViewModel | SettingsViewModel   │
└───────────────────────────┬─────────────────────────────┘
                            │ interfaces
┌───────────────────────────▼─────────────────────────────┐
│  Core services                                          │
│  IStickerLibrary | IImportService | IConfigStore        │
│  IClipboardImageService | IHotkeyService | IAppPaths    │
└───────────────────────────┬─────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
   JSON + images/     Win32 Hotkey        Win32 Clipboard
   (data root)        (Windows only)      (Windows only)
```

### Design principles (deep modules, low coupling)

- **Deep modules**：小接口 + 厚实现。例如 `IStickerLibrary` 对外只暴露分类/查询/导入/移动等能力；扫描文件夹、哈希、JSON 原子写、路径规范化全部藏在实现内。
- **Seams where adapters actually vary**：
  - Core ↔ filesystem（library/config 实现可对测试用临时目录）
  - UI ↔ Core（ViewModels 只依赖接口）
  - UI ↔ OS（`IClipboardImageService` / `IHotkeyService`：Windows 实现 + 测试/非 Windows stub）
- **不引入假抽象**：没有第二实现就不为 DI 而拆接口；但 OS 与库 IO 从第一天就有测试/平台第二适配器，故值得成 seam。
- **Deletion test**：删掉 `IStickerLibrary` 后，文件夹扫描/去重/分类 CRUD 会散落到多个 ViewModel → 证明该模块有必要。
- **依赖方向**：`StickerPicker` → `StickerPicker.Core`；Core **零** Avalonia 引用。

### Key modules (interfaces)

| Module | Interface surface (small) | Hidden implementation |
|--------|---------------------------|------------------------|
| `IAppPaths` | `DataRoot`, `BootstrapPath`, `Resolve()` | LocalAppData 默认值、bootstrap 指针读写 |
| `IConfigStore` | `Load` / `Save` / 强类型 `AppConfig` | 默认合并、原子 JSON 写 |
| `IStickerLibrary` | 加载/刷新、列分类、查询贴纸、分类 CRUD、导入、移动到分类 | **文件夹扫描**、hashes、metadata、扩展名过滤、SHA-256 去重 |
| `IClipboardImageService` | `CopyImageFile(path)` | Win32 file drop + bitmap |
| `IHotkeyService` | `Register` / `Unregister` / `HotkeyPressed` | RegisterHotKey + WndProc |
| `IWindowChromeService` | Show/Hide/Activate/Topmost | Avalonia Window 细节 |

导入**并入** `IStickerLibrary`（或作为其内部 collaborator，不向 UI 暴露第二套存储 API），避免 ViewModel 同时编排 Library + Import 造成浅层编排。

Windows 实现放在 `Platform/Windows/`；非 Windows 可提供 no-op 或降级（MVP 不优化）。

## 4. Data model (folder categories + autonomous metadata)

### Source of truth

| Concern | Source of truth |
|---------|-----------------|
| 有哪些分类 | `library/*` **一级子目录** |
| 贴纸属于哪类 | 文件所在目录 |
| 贴纸字节 | 磁盘文件 |
| tags / 稳定 id / createdAt | `metadata.json`（可按 relative path 或 content hash 索引） |
| 去重 | `hashes.json`（SHA-256 → relative path） |
| 应用设置 | `config.json` |

用户可用资源管理器直接新建文件夹、移动图片；`IStickerLibrary.Refresh()` 重新扫描即可对齐 UI。

### Identifiers & files

- 物理路径：`library/{CategoryName}/{fileName}{ext}`
- 导入时文件名：保留原名；冲突则追加短后缀（实现细节藏在 library 内）
- 分类名：文件夹名；禁止路径分隔符与保留名；「全部」为虚拟 id `__all__`，非文件夹
- 可选未分类：`library/_inbox/` 或导入时要求当前选中真实分类（MVP 推荐：导入到**当前选中分类**；若在「全部」则导入到默认 `Inbox` 文件夹并自动创建）

### `metadata.json`

```json
{
  "version": 1,
  "stickers": {
    "library/cats/neko.png": {
      "relativePath": "library/cats/neko.png",
      "tags": ["happy", "猫"],
      "createdAt": "2026-07-19T00:00:00Z",
      "hash": "<sha256-hex>"
    }
  }
}
```

路径变更（移动/重命名）时 library 实现负责迁移 metadata 键；扫描时清理指向缺失文件的条目。

### 不再使用 `categories.json` membership

分类 CRUD = `Directory.Create` / `Move` / `Delete`（删除策略：拒绝非空或移到回收区 — MVP 选「非空则提示并要求先移走/确认删除文件」）。

### `order.json`（可选，MVP 可简化）

MVP 可用目录枚举顺序；若需稳定「导入追加在末尾」，可写：

```json
{
  "version": 1,
  "byCategory": {
    "cats": ["a.png", "b.gif"]
  }
}
```

### `hashes.json`

```json
{
  "version": 1,
  "hashes": {
    "<sha256-hex>": "library/cats/neko.png"
  }
}
```

哈希策略（MVP）：**文件字节 SHA-256**（对 GIF/WebP 动图安全）。像素级去重后续增强。

### `config.json`

```json
{
  "version": 1,
  "theme": "system",
  "alwaysOnTop": true,
  "hotkey": "Ctrl+Shift+E",
  "dataRoot": null,
  "thumbnailSize": 96,
  "window": { "x": 0, "y": 0, "width": 900, "height": 640 }
}
```

`dataRoot: null` 表示使用默认 `%LocalAppData%/StickerPicker/`。

### Bootstrap 配置

自定义 `dataRoot` 时，需要一个**固定位置**记住指针，否则切换后找不到数据：

- 推荐：`%LocalAppData%/StickerPicker/bootstrap.json` 仅存 `{ "dataRoot": "D:/path" }`
- 实际库内容在 `dataRoot` 下；`config.json` 仍在 dataRoot 内
- 切换数据根流程：选路径 → 可选「复制现有数据」→ 写 bootstrap → 重新加载库

## 5. UI design language (Steam-like)

### Visual tokens（实现时落到 ResourceDictionary）

- **无圆角**：`CornerRadius=0` 覆盖 Button/TextBox/Border/ScrollViewer 等
- **色板（暗色主）**：深灰面板 `#1b2838` / `#171a21` 感；强调色克制的蓝（如 `#66c0f4`）用于焦点/选中，非大面积渐变
- **亮色**：浅灰面板 + 深字；同样无圆角
- **密度**：紧凑边距；侧栏分类列表 + 顶栏搜索 + 主区虚拟化网格
- **动画**：主题切换、窗口显示可短 fade（≤150–200ms）；列表项不做夸张弹跳
- **控件**：`StickerTile`（缩略图+悬停边框）、`CategoryList`、设置表单可用原生控件 + 样式

### Main window layout

```text
┌──────────────────────────────────────────────┐
│ Title bar (standard or light custom)         │
├────────────┬─────────────────────────────────┤
│ Categories │ Search ................. [Import]│
│            │ ┌────┐ ┌────┐ ┌────┐ ┌────┐     │
│  全部      │ │    │ │    │ │    │ │    │     │
│  分类A     │ └────┘ └────┘ └────┘ └────┘     │
│  + 新建    │  virtualized wrap panel grid    │
│            │  Ctrl+Wheel → thumbnail size    │
├────────────┴─────────────────────────────────┤
│ status: N stickers · last import …           │
└──────────────────────────────────────────────┘
```

设置：独立视图或二级页（热键捕获、主题、置顶、数据目录浏览）。

## 6. Critical Windows behaviors

### 6.1 Clipboard send

目标：QQ/微信粘贴动图时走**文件**而非压成静态位图。

实现要点：

1. 设置 `DataObject` 含：
   - 文件列表（本地绝对路径，`CF_HDROP` / `Shell IDList` 视 API 选择）
   - 位图兜底（静态预览；GIF 首帧可接受）
2. 成功后调用窗口隐藏（不退出进程）
3. 失败时 UI 提示，不隐藏

Avalonia 自带 Clipboard 可能不足以设文件 drop；MVP 使用 Windows 专用实现（`OleSetClipboard` / `SetClipboardData` 封装库或 P/Invoke）。需在真机对 QQ/微信验证。

### 6.2 Global hotkey

1. 启动时 `RegisterHotKey`（默认 Ctrl+Shift+E）
2. 消息循环：`WndProc` hook（Avalonia Win32 窗口）或隐藏 message-only 窗口
3. 回调切回 UI 线程：若窗口可见则 Hide，否则 Show + Activate + 可选聚焦搜索框
4. 设置变更：Unregister 旧 → Register 新 → 持久化
5. 冲突：注册失败时提示用户改键

### 6.3 Tray

- `TrayIcon`：左键显示/激活；菜单：显示、设置、退出
- `ShutdownMode`：不因关主窗口退出；仅托盘「退出」或显式 `Shutdown`
- 关闭按钮 → Hide

### 6.4 Theme

- `system` / `light` / `dark` 映射到 Avalonia `ThemeVariant` + 自定义资源
- 跟随系统：订阅 `ActualThemeVariant` / platform 设置

## 7. Import pipeline (into folder category)

```text
paths → expand folders → filter extensions
  → resolve target category folder (current selection / Inbox)
  → for each file:
      hash = SHA256(bytes)
      if hash in hashes → skip (duplicate)
      else copy into library/{Category}/, update metadata + hashes
  → report: imported / skippedDuplicates / failed
```

支持扩展名：`.png .jpg .jpeg .gif .webp`（MVP）。

UI：文件选择器 + 文件夹选择器；结果 toast/对话框。
从「导入文件夹」导入时：默认**拍平**进当前分类（不递归创建子分类树）；递归镜像目录树可作为后续增强。

## 8. Search & filter

- 当前分类 membership ∩ 搜索串
- 搜索：`tags` 与文件名/id 的大小写不敏感子串；多关键词可用空格 AND（MVP 简单 contains 即可）
- 实时：SearchBox `TextChanged` → 过滤 in-memory 列表（库规模数千级可接受；虚拟化网格）

## 9. Error handling & persistence

- 所有 JSON 读写：原子写（写 temp + replace）
- 损坏 JSON：备份坏文件 + 重建空结构，日志记录
- 导入单文件失败不中断整批
- 用户可见错误：简短中文消息（产品 UI 中文可接受；代码/注释英文按仓库规范）

## 10. Testing strategy

| 层 | 测什么 |
|----|--------|
| Core.Tests | 哈希去重、import 结果、分类 membership、搜索过滤、config 默认合并 |
| 手工 | 热键、托盘、QQ/微信粘贴 GIF、主题、数据目录切换 |

自动化 UI 测试 MVP 不做。

## 11. Risks & mitigations

| Risk | Mitigation |
|------|------------|
| 剪贴板双格式聊天软件不认 | 优先验证 FileDrop；参考 SuzuEmojy 双写策略；真机测 QQ/微信 |
| 全局热键与消息循环集成 | 尽早做 spike（Day 1–2 技术竖切） |
| 大图库卡顿 | `ItemsControl` + 虚拟化；缩略图解码缓存/限尺寸 |
| AppData vs 便携切换丢数据 | bootstrap 指针 + 可选复制迁移；切换前确认 |
| 规格目录 To fill | 实现中沉淀约定到 `.trellis/spec/` |

## 12. Future extension points

- `IImportSource` 扩展网络 URL / 剪贴板 HTML（D1）
- 像素哈希（更强去重）
- 排序 DnD 写回 `order.json`（D3）
- 预览浮窗（D5）
