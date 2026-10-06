# 导出与分发说明

本文记录「导出 Windows 可执行版」时必须随 exe 一起放置的文件，以及背后的机制。
每次重新导出后，请按本文第 4 节核对一遍，否则导出的游戏会出现**卡牌全空、音乐不响、区域不生成**等静默故障。

---

## 1. res:// 的双源查找机制（核心）

Godot 的 `FileAccess` 打开 `res://` 路径时是**两级查找**，不是只查 PCK：

1. 先查 `.pck`（`PackedData::try_open_path`）；
2. **查不到就回退到真实文件系统**（`create_for_path` → `open_internal`），
   此时 `res://` 被替换为 `ProjectSettings.get_resource_path()`，
   在导出后的游戏里这个值就是 **exe 所在目录**。

> Godot 核心开发者 bruvzg：
> *"`res://` refers to both PCKs and resource folder in the real file system. PCKs (or ZIPs) are fully optional."*
> 出处：<https://github.com/godotengine/godot/issues/111164>

**推论**：只要把文件按 `res://` 的相对路径摆在 exe 旁边，即使它不在 `.pck` 里，游戏也能读到。
这是本项目分发 7 个 `.ini` 配置文件的依据（不是绕过机制，是 Godot 的正常行为）。

注意：`ResourceLoader.load()` 的资源加载**以 pck 优先**，所以散装的 `.tscn`、`.png` 不会顶掉 pck 里已打包的同名资源——它们既不会被读、也不需要放。

## 2. 为什么 .ini 不在 .pck 里

`export_presets.cfg` 的预设「柏林之路」用的是：

```ini
export_filter="all_resources"   # 只打包「资源」
include_filter=""               # 非资源文件过滤器为空
```

`.ini` 既没有 `.import` 文件、也不被 Godot 的资源加载器识别，因此在 `all_resources` 模式下**被排除在 pck 之外**。
若要改成打包进 pck，需在 `include_filter` 里补上路径后重新导出；本项目当前采用「散装随包」方案，**不改该配置**。

## 3. 必需文件清单

导出产物目录（本项目为 `easikard_out/`）必须包含以下**四类**内容：

| 类别 | 内容 | 缺失后果 |
|------|------|---------|
| 可执行文件 | `testGame.exe` | — |
| 资源包 | `testGame.pck` | 无场景、无贴图、无脚本 |
| **.NET 运行时目录** | `data_<程序集名>_windows_x86_64/`（约 200 个文件，约 93MB）。**当前是 `data_新建游戏项目_windows_x86_64/`**，改名后下次导出会变成 `data_road_to_berlin_windows_x86_64/` | **游戏直接崩溃**：`.NET: Assemblies not found` + `signal 11` |
| **7 个 .ini 配置** | 见下表 | 对应系统静默失效 |

### 7 个 .ini 的落位与读取点

| 文件（相对 exe 目录） | 读取位置 | 缺失后果 |
|----------------------|---------|---------|
| `bin/AreaPool.ini` | `bin/WorldMap.cs:285` | 区域任务池为空，地图不生成关卡 |
| `bin/deck.ini` | `bin/WorldMap.cs:352`、`bin/battlefield_.cs:5821` | 初始卡组为空 |
| `bin/event.ini` | `bin/WorldMap.cs:389` | 事件系统无内容 |
| `bin/setting.ini` | `bin/SettingsManager.cs:21` | 设置项为空 |
| `cards/card.ini` | `bin/WorldMap.cs:221`、`bin/battlefield_.cs:463` | **所有卡牌数据为空** |
| `cards/enemyTurn.ini` | `bin/WorldMap.cs:554`、`bin/battlefield_.cs:2235` | 敌方行动队列为空 |
| `configs/music.ini` | `core_logic/MusicManager.cs:13` | 无背景音乐 |

上述读取点全部使用 `Godot.FileAccess` + `res://` 前缀，因此都走第 1 节的双源查找。

### 不需要随包的文件

| 文件 | 说明 |
|------|------|
| `bin/timesList.ini` | 时点（timing）名称对照表，供卡牌作者查阅，**运行时不读取** |
| 各类 `.cs` / `.uid` / `.tscn` / `.png` / `.png.import` | 已编译进 `.pck` 或 `.dll`，散装副本不会被读取 |

## 4. 重新导出后的核对步骤

1. 在导出目录放置 `testGame.exe`、`testGame.pck`；
2. 从项目根目录复制 `data_<程序集名>_windows_x86_64/` 整个文件夹过去（目录名随程序集名变，**不要照抄旧名字**）；
3. 按第 3 节表格复制 7 个 `.ini`，**保持 `bin/`、`cards/`、`configs/` 三级目录结构**；
4. 跑一次冒烟测试（第 5 节）。

`.ini` 内容如有更新，需重新复制覆盖——导出目录里的是**副本**，不会自动跟随项目变化。

## 5. 验证方法

### 冒烟测试（命令行，无需交互）

在导出目录下执行：

```bash
timeout 40 ./testGame.exe --headless --quit-after 180
```

正常应输出一行 `MusicManager.cs: PlaySlot(start_menu) -> res://assest/music/xxx.mp3`。
这一行证明 `configs/music.ini` 被成功读取（该文件不在 pck 中，只能来自散装回退）。

### A/B 对照实验结论（2026-09-19 实测）

| 条件 | 结果 |
|------|------|
| 缺 `data_<程序集名>_windows_x86_64/`（实测时名为 `data_新建游戏项目_windows_x86_64/`） | `ERROR: .NET: Assemblies not found` → `Program crashed with signal 11` |
| 有该目录、缺 `configs/music.ini` | 不再输出 `PlaySlot` 行，音乐系统失效 |
| 两者齐备 | 正常启动并输出 `PlaySlot` 行 |

测试环境：Godot v4.5.stable.mono.official。

## 6. 改名注意事项（改项目名 / 程序集名时必读）

程序集名散落在**三个必须一起改**的地方，漏改任何一处，游戏启动时都找不到 C# 程序集：

| # | 位置 | 说明 |
|---|------|------|
| 1 | `road_to_berlin.csproj`（**文件名**） | Godot.NET.Sdk 在没有显式 `<AssemblyName>` 时，**用 csproj 的文件名作程序集名** |
| 2 | `road_to_berlin.sln` | 文件名 ＋ 内部 `Project(...) = "名字", "名字.csproj"` 这一行 |
| 3 | `project.godot` 的 `dotnet/project/assembly_name` | **Godot 靠它去找 `.dll`**。与 1 对不上就是 `Assemblies not found` → `signal 11` |

另有**一个可以单独改**、与程序集无关的：

- `project.godot` 的 `application/config/name` —— 窗口标题与编辑器里显示的项目名。
  本项目当前为 `柏林之路`（中文显示名），而文件与程序集用英文 `road_to_berlin`。

**改完必须做的三件事**：

1. `dotnet build road_to_berlin.csproj` —— 确认新程序集名生成正确（本项目实测 0 错误）；
2. **开一次 Godot 编辑器** —— `project/assembly_name` 变了，`.godot/mono/` 里的旧程序集
   缓存需要重建；旧的 `新建游戏项目.dll` 会残留在 `.godot/mono/temp/bin/` 下
   （该目录在 `.gitignore` 内，不影响版本库，但留着容易看花眼）；
3. **重新导出** —— 导出会生成 `data_road_to_berlin_windows_x86_64/`。
   **旧的 `data_新建游戏项目_windows_x86_64/` 在此之前仍被现有的 `testGame.exe` 使用，
   确认新导出能跑之前不要删它。**

> 本次改名的实际改动：`新建游戏项目.csproj` → `road_to_berlin.csproj`、
> `新建游戏项目.sln` → `road_to_berlin.sln`（＋内部引用）、
> `assembly_name` → `road_to_berlin`、`config/name` → `柏林之路`。
> **源码零改动**——`bin/*.cs` 里从未出现过项目名。

## 7. 注意事项

- **禁止修改 `.gitignore`**：`testGame.exe`、`testGame.pck`、`testGame.tmp` 已被排除在版本库外，这是既定约定。
- 导出产物目录（如 `easikard_out/`）位于项目仓库**之外**，不受 git 管理。
- 散装 `.ini` 之所以生效，依赖 `res://` 回退到 exe 目录。若将来改用 `include_filter` 把 ini 打进 pck，则 pck 内的版本会**优先命中**，散装副本将被忽略——两种方案不要混用而不自知。
