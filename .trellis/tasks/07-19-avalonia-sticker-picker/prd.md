# Avalonia Sticker Picker (SuzuEmojy-like)

## Goal

用 **Avalonia + .NET 10 + C# 14（nullable enabled）** 从零实现本地表情包管理与快速发送工具 **StickerPicker**（能力参考 [SuzuEmojy](https://github.com/IxinorTyan/SuzuEmojy)，不兼容其数据格式），让用户能快速搜索、分类整理、一键复制发送收藏的表情图。

## Branding

| 项 | 值 |
|----|-----|
| 产品名 / 程序集 / AppData 文件夹 | `StickerPicker` |
| 窗口标题 / 托盘提示 | `StickerPicker` |
| 默认数据根 | `%LocalAppData%/StickerPicker/` |

## Background

本仓库当前为空壳（仅 Trellis / agent 配置），需从零建立解决方案。

参考产品（Python / PySide6）能力摘要，仅作产品行为参考：

- 全局快捷键呼出、分类/搜索、导入去重、剪贴板双格式发送、托盘常驻、主题、JSON 本地存储
- 发送：图片以 **文件 URL + 位图** 写入剪贴板，便于 QQ/微信识别动图
- 导入格式参考：png/jpg/gif/webp（MVP 不含 webm 转换）

本机环境：**.NET SDK 10.0.109**；Avalonia 模板默认 **12.1.0**、**CommunityToolkit.Mvvm**、`net10.0`。

## Requirements

### Platform & stack

- R1. 目标平台：**Windows 10/11**（第一优先）。
- R2. 技术栈：Avalonia 最新稳定（规划基线 12.1.x）、.NET 10、C# 14、`<Nullable>enable</Nullable>`。
- R3. 本地数据优先，无需账号/云同步（MVP）。

### MVP-A product capabilities

- R4. 表情图库：网格展示缩略图，支持 Ctrl+滚轮缩放。
- R5. 分类：**以文件系统文件夹为分类**（创建/重命名/删除分类 = 文件夹操作）；用户也可在资源管理器中直接整理文件夹后由应用识别；图片归属 = 所在文件夹。
- R6. 搜索：按 tag/关键词实时过滤。
- R7. 导入：本地多选文件/文件夹导入 + 哈希去重。
- R8. 发送：单击表情 → 文件路径 + 位图双格式写入剪贴板（Windows）→ **自动隐藏主窗口**。
- R9. 全局热键显示/隐藏主窗口（Windows）；默认 **Ctrl+Shift+E**，设置中可修改。
- R10. 系统托盘常驻，关闭窗口不退出；托盘可显示/退出。
- R11. 基础设置：热键、主题（明/暗/跟随系统）、窗口置顶、数据目录。
- R12. 应用可 `dotnet run` / 发布后独立运行。
- R13. 数据目录：**默认 `%LocalAppData%/StickerPicker/`**；设置可切换自定义/便携路径；可整体复制迁移。
- R14. UI：**无圆角 Steam-like 工业简洁风**；克制动画；允许自写控件；不以 Fluent/WinUI 1:1 为目标。

### Deferred（非 MVP-A）

- D1. 浏览器 HTML/URL 扒图、剪贴板网络图导入
- D2. WEBM → 动图转换
- D3. 完整拖拽排序 / 跨分类 DnD / 批量操作打磨
- D4. 列表模式双布局
- D5. 高清悬停预览浮窗
- D6. 数据迁移向导、完整设置页打磨
- D7. Fluent 云母 / WinUI 1:1 复刻（与 Steam-like 方向不一致）

## Data layout

物理根可配置；默认 `%LocalAppData%/StickerPicker/`。

**分类模型（已确认）**：分类 = `library/` 下的一级文件夹；贴纸文件在对应文件夹内。不使用独立的 `categories.json` membership 表作为真相源。

```text
<data-root>/
  library/                 # 表情库根
    <CategoryName>/        # 每个文件夹 = 一个分类
      <sticker files>
  metadata.json            # tags / id / hash 等非路径元数据（按文件相对路径或 id 索引）
  hashes.json              # 去重索引
  order.json               # 可选：展示顺序（MVP 可用文件系统顺序 + 导入追加）
  config.json              # 热键、主题、置顶、窗口几何等
bootstrap.json             # 仅在默认 LocalAppData 位置：自定义 dataRoot 指针
```

- 虚拟分类「全部」：聚合 `library/**` 下全部图片，不对应物理文件夹。
- 用户在资源管理器增删/重命名文件夹或移动图片后，应用通过扫描库根同步视图（启动时 + 导入后 + 可选手动刷新）。
- `config.json` 持久化：热键、主题、置顶、窗口几何等。Schema **自主设计**，不兼容 SuzuEmojy。

## Acceptance Criteria

- [ ] 可在 Windows 上 `dotnet run` 启动 Avalonia 应用。
- [ ] 能本地导入图片（含去重提示），按**文件夹分类**浏览，按关键词/tag 过滤；在资源管理器中整理文件夹后应用能识别。
- [ ] 单击表情可将图片复制到剪贴板（文件+图像双格式），并自动隐藏主窗口。
- [ ] 默认 `Ctrl+Shift+E` 可显示/隐藏主窗口；热键可在设置中修改并持久化。
- [ ] 托盘可显示主窗口 / 退出应用；关闭窗口不退出。
- [ ] 主题与置顶设置可持久化。
- [ ] 默认数据目录为 `%LocalAppData%/StickerPicker/`；支持切换自定义/便携路径。
- [ ] nullable 全开，无无意义的 null 压制。
- [ ] UI 无圆角倾向，Steam-like 简洁，动画克制。

## Out of scope（MVP-A）

- 账号体系 / 云同步 / 多设备协作
- 插件市场 / 表情商店
- 完整 1:1 复刻 SuzuEmojy UI 像素 / Fluent 圆角云母风格
- 夸张动效或游戏化视觉
- 非 Windows 平台的热键/剪贴板深度优化
- SuzuEmojy `data/` 格式兼容与导入器（仅作能力参考）
- Deferred 项 D1–D7

## Open Questions

（规划阶段已收敛，无阻塞项。实现中细节见 `design.md`。）

## Notes

- 复杂任务：`design.md` + `implement.md` 完成后，用户批准再 `task.py start`。
- 实现前注入 `.trellis/spec/`（当前多为 To fill，以本任务 design 为准并在实现中回填 spec）。
