# Implement: StickerPicker (MVP-A)

## Preconditions

- [ ] User approved `prd.md` + `design.md` + this file
- [ ] Run `python3 ./.trellis/scripts/task.py start avalonia-sticker-picker` (or current task name)
- [ ] Load `trellis-before-dev` and inject available specs (many are To fill — follow `design.md`)
- [ ] Windows 10/11 真机可用于热键/剪贴板/托盘验收（当前环境有 .NET 10 SDK）

## Validation commands (throughout)

```bash
dotnet build StickerPicker.slnx
dotnet test StickerPicker.slnx
dotnet run --project src/StickerPicker
```

发布（后期）：

```bash
dotnet publish src/StickerPicker -c Release -r win-x64 --self-contained false
```

## Phase checklist

### P0 — Solution skeleton

1. `dotnet new sln -n StickerPicker -f slnx` → `StickerPicker.slnx`
2. `dotnet new avalonia.mvvm -n StickerPicker -o src/StickerPicker -f net10.0 -m CommunityToolkit`
3. `dotnet new classlib -n StickerPicker.Core -o src/StickerPicker.Core -f net10.0`
4. `dotnet new xunit -n StickerPicker.Core.Tests -o tests/StickerPicker.Core.Tests -f net10.0`
5. 项目引用、Nullable enable、ImplicitUsings；Core 无 Avalonia 引用
6. 删掉模板样例噪音，保留可运行空壳窗口标题 `StickerPicker`
7. 确认 `dotnet build StickerPicker.slnx` 通过

**Done when**: 空应用可启动，`.slnx` 结构符合 design。

### P1 — Paths, config, folder library IO

1. `IAppPaths`：默认 `%LocalAppData%/StickerPicker/` + `bootstrap.json` 指针
2. `IConfigStore`：读写 `config.json`，默认合并，原子写
3. Models：`Sticker`, `Category`（文件夹分类）、`ImportResult`
4. **Deep** `IStickerLibrary`：扫描 `library/*` 文件夹作分类；metadata/hashes；内存缓存 + `Refresh`
5. 单元测试：空库初始化、创建分类文件夹、损坏 JSON 恢复

**Done when**: Core 测试绿；无 UI 也能创建数据根与分类文件夹。

### P2 — Import + dedupe (on library)

1. `IStickerLibrary.ImportAsync`：文件/文件夹、扩展名过滤、SHA-256、复制进目标分类目录、跳过重复
2. 导入结果 DTO：`Imported / Duplicates / Failed`
3. 测试：临时目录 fixture 覆盖重复与成功路径；导入后文件出现在对应文件夹

**Done when**: Core 导入测试绿。

### P3 — UI shell (Steam-like)

1. Themes：去圆角、暗/亮资源、强调色
2. MainWindow 布局：侧栏分类 + 搜索 + 网格宿主 + 状态栏
3. `GalleryViewModel`：分类列表、过滤集合、缩略图尺寸（Ctrl+Wheel）
4. `StickerTile` 控件：绑定缩略图路径、单击命令
5. 虚拟化：优先 `ItemsRepeater` 或虚拟化 `ListBox`/`ItemsControl` 方案（按 Avalonia 12 可用控件选型）
6. 设置页骨架：主题、置顶、热键文本、数据目录

**Done when**: 假数据或空库下 UI 可导航，视觉无圆角。

### P4 — Wire library to UI

1. 启动加载库 → 绑定网格
2. 分类 CRUD（创建/重命名/删除文件夹）对话框；支持 Refresh 同步磁盘
3. 搜索实时过滤
4. 导入按钮 → 系统文件/文件夹选择器 → 刷新网格 + 结果提示
5. 图片归属分类（MVP：导入到当前分类文件夹；「移动到分类」= 移动文件）

**Done when**: 手工导入真实图片并可按分类/搜索浏览。

### P5 — Windows clipboard send

1. `IClipboardImageService` Windows 实现（文件 drop + bitmap）
2. 单击 Sticker → 复制 → Hide 主窗口
3. 失败提示
4. **真机**：QQ/微信粘贴 PNG 与 GIF

**Done when**: 至少一种聊天软件可粘贴静态与动图（GIF）。

### P6 — Hotkey + tray + lifecycle

1. `TrayIcon` 菜单：显示 / 设置 / 退出；关闭窗口 = Hide
2. `IHotkeyService`：RegisterHotKey + UI 线程 toggle
3. 设置改热键并持久化；冲突提示
4. 置顶绑定 `Topmost`
5. 单实例（可选但推荐）：二次启动激活已有窗口

**Done when**: 热键与托盘满足 PRD 验收。

### P7 — Data root switch

1. 设置中选择自定义目录
2. 写 `bootstrap.json`；可选复制迁移
3. 重新加载库；错误回滚指针

**Done when**: 换目录后重启仍指向新根。

### P8 — Polish & gate

1. 窗口几何记忆
2. 克制 show/hide 动画（可选）
3. README：运行方式、数据目录、热键
4. `dotnet build` / `dotnet test` 全绿
5. 对照 PRD Acceptance Criteria 勾选
6. 有价值的约定回写 `.trellis/spec/`（directory structure、nullable、服务边界）

## Spike order (risk-first)

若时间紧，在 P3 前插入半日 spike：

1. Win32 全局热键在 Avalonia 窗口上的最小 demo
2. 剪贴板 FileDrop + Bitmap 最小 demo → 微信/QQ 粘贴

失败则调整 `Platform/Windows` 方案，不阻塞 Core 开发。

## Risky files / rollback

| Area | Risk | Rollback |
|------|------|----------|
| Clipboard P/Invoke | 聊天软件不识别 | 保留接口，换实现（如第三方包） |
| Hotkey hook | 消息吃掉/崩溃 | 隔离到 `WindowsHotkeyService`，可禁用热键仍用托盘 |
| 原子 JSON 写 | 中断导致空文件 | temp+replace；启动备份 |
| 主题全局 `CornerRadius` | 部分控件异常 | 按控件类型精确 Setter |

## Sub-agent context manifests

实现前确保 `implement.jsonl` / `check.jsonl` 含真实条目（非 `_example`）：

- `prd.md`, `design.md`, `implement.md`
- 现有 spec 索引（即使 To fill，作为回写目标）
- `research/windows-platform-notes.md`（热键/剪贴板结论）

## Definition of done (MVP-A)

- [ ] PRD 全部 Acceptance Criteria 满足或有文档化的已知限制
- [ ] `dotnet build` + `dotnet test` 通过
- [ ] 无大面积 `!` / null 压制
- [ ] 用户可在 Windows 完成：导入 → 搜索 → 热键呼出 → 单击发送 → 聊天粘贴
