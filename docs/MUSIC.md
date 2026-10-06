# 全局音乐管理

## 方案

项目使用autoload的`MusicManager`实现全局不间断背景音乐。`MusicManager`常驻一个`AudioStreamPlayer`，场景切换不会中断播放。

**切换场景不会掐断当前曲目**：请求新槽位时只入队，等当前曲目自然播完再切。详见下节。

## 文件

- `core_logic/MusicManager.cs`：全局音乐管理器。
- `configs/music.ini`：BGM槽位配置。

## 配置

`configs/music.ini`的`[music]`段，**每个键是一个槽位**，`LoadConfig()`遍历该段全部键，因此新增槽位不需要改代码：

```ini
[music]
start_menu=res://path/to/menu.mp3
world_map=res://path/to/world.mp3
battle=res://path/to/battle.mp3,res://path/to/battle_alt.mp3
```

规则：

| 规则 | 说明 |
|------|------|
| 一槽多曲 | 值可用英文逗号分隔多首曲目，播放时随机抽取一首；只写一首即固定播放它 |
| 不连续重复 | 多曲槽位抽到正在播放的那首时会重新抽，避免连续两遍同一首 |
| 空值 | 留空（或只有空白）的槽位不进入槽位表，`HasSlot()`对其返回false |
| 槽位名 | 忽略大小写，`battleBGM_DonBend`与`battleBGM_donbend`等价 |
| 注释符 | **只能用分号`;`**。`bin/iniHandler.cs`的解析器只把`;`开头当注释，含`=`的`#`行会被当成一个键名 |

## 切换时机：播完再切

`PlaySlot()`**不会立即换曲**。当前有曲目在播时，新槽位只记入`pendingSlot`，真正换曲发生在曲末：

| 情形 | 行为 |
|------|------|
| 当前无曲目在播（首次进场景、已停止） | 立即起播，不排队 |
| 请求的槽位就是正在播的那个 | 撤销排队，继续把这首放完 |
| 请求别的槽位 | 排队；连续切换时后者覆盖前者，记住最后一次请求 |
| 曲末，队列非空 | 切到排队的槽位 |
| 曲末，队列为空 | 从**当前槽位**再随机取一首续播（单曲槽位即等于原来的循环） |

实现要点：

- 订阅`AudioStreamPlayer.Finished`驱动曲末判断。该信号只在流**非循环**时触发。
- 因此`DisableBuiltinLoop()`在起播前关掉三种格式的内建循环（`AudioStreamMP3.Loop`、`AudioStreamOggVorbis.Loop`、`AudioStreamWav.LoopMode`），循环改由曲末回调重新起播实现。这同时修掉了旧版"只有MP3设了循环、ogg/wav播完即静音"的问题。
- 关掉内建循环意味着曲末重新起播处会有一个极短的接缝，这是换取"能在曲末精确切槽位"的代价。
- 槽位的曲目因此被当作播放列表使用：`battle`槽位配了 12 首，曲末会在其中随机续播，而不是把同一首放到底。

### 战斗专属BGM

槽位名前缀`battleBGM_`加敌人预设名（即`cards/enemyTurn.ini`的section名）即为该战斗的专属BGM：

```ini
battleBGM_DonBend=res://assest/music/配乐2.mp3
battleBGM_berlin_final_battle=res://assest/music/配乐3.mp3,res://assest/music/配乐4.mp3
```

`battlefield_`进场时调用`PlayBattleSlot(BattleStateManager.ResolveEnemyPreset())`：该槽位有配置就播专属曲，**没配置就自动回退到通用`battle`槽位**，因此只为部分战斗配曲是安全的，其余战斗照常播放通用战斗曲。

专属槽位由`[music]`段的通用键解析自动产生，`LoadConfig()`中没有任何`battleBGM_`专用分支。

## 场景接入

- `StartMenu` 请求 `start_menu`
- `WorldMap` 请求 `world_map`
- `battlefield_` 请求 `battleBGM_<敌人预设名>`，回退 `battle`

`Store`、`ChooseMission`、`EventScene`、`PostBattleReward` 不主动切歌，会继承当前场景音乐。

## API

- `MusicManager.Instance?.PlaySlot("start_menu")` — 播放指定槽位
- `MusicManager.Instance?.PlayBattleSlot(enemyPreset)` — 播放战斗BGM，含专属槽位回退
- `MusicManager.Instance?.HasSlot("battleBGM_X")` — 槽位是否配置了曲目
- `MusicManager.Instance?.PickSfx("button")` — 只**取**一条音效（`AudioStream`），播放器由调用方管
- `MusicManager.Instance?.PlaySfx("button")` — **响一声就走**：自己抽、自己播，调用方不用管节点
- `MusicManager.Instance?.StopMusic()`
- `MusicManager.Instance?.SetVolumeDb(-6f)`
- 常量：`MusicManager.BattleBgmPrefix`（`"battleBGM_"`）、`MusicManager.BattleSlot`（`"battle"`）

每次实际切换曲目都会打印带时间与代码位置的日志，便于确认抽中了哪一首。

## 已知限制

- **同时只能播放一首**：只有一个`AudioStreamPlayer`，不支持BGM叠加环境音。
- **切换有等待**：进入战斗后可能要等世界地图的曲子放完才会响战斗曲，最长等于一首曲子的时长。这是"不掐断当前曲目"的直接代价。
- **切点无淡入淡出**：换曲发生在曲末的自然边界，但边界处仍是硬切，没有交叉淡化。

## 音效槽位（`[sfx]` 段）

`configs/music.ini` 除 `[music]` 外还有一个 `[sfx]` 段，**格式完全相同**（每个键一个槽位、
值可用英文逗号分隔多条、每次从那几条里随机抽一条），区别只在语义：

| | `[music]` | `[sfx]` |
|---|---|---|
| 用途 | 背景音乐 | 一次性音效 |
| 调度 | 有「播完再切」，切换不掐断当前曲目 | 无调度，每次播放各抽一条 |
| 抽取 | 多曲时避开正在播的那首 | 抽到同一条也没关系（爆炸连着响同一条很自然） |
| 接口 | `PlaySlot(slot)` | `PickSfx(slot)` → 返回 `AudioStream`，由调用方自行播放；<br>`PlaySfx(slot)` → **放一次就完**，不用调用方管节点 |

`PickSfx` 带缓存，同一条音频只读一次盘；槽位缺失或加载失败时返回 `null`，
调用方保持原有音效即可（不会变成没声音）。

**`PickSfx` 与 `PlaySfx` 怎么选**：需要在**自己场景里**控制播放器（要调音量、
要等它播完 `await`）时用 `PickSfx`；只是「响一声就走」用 `PlaySfx`。

`PlaySfx` 的声部挂在 **`MusicManager` 自己身上**（autoload，常驻），不是调用方所在的场景——
按键音最典型的用法就是「按下去 → 立刻切场景」，挂场景里的话节点会跟着 `QueueFree`，
声音刚起个头就被掐掉。多个声部轮换，连点也不会让后一声掐掉前一声；音量走 `SFX` 总线，
和场景里的音效是同一条（设置界面调的就是它）。

当前槽位：

| 槽位 | 用途 | 调用方 |
|------|------|--------|
| `dead` | 单位阵亡的爆炸音效（`assest/爆炸3.wav`～`爆炸21.wav`，下划线开头的未采用版本不列入） | `battlefield_.PlayDeadSound()`，槽位名常量 `DeadSfxSlot` |
| `flyby` | 飞掠音效（`assest/飞机飞过_单位.wav`） | `FlyingEffect`（`effects/flying_effect.tscn` 的 `SfxSlot`）；`AirStrikeEffect` 继承它，共用这一声 |
| `严冬` / `战略重心` / `红色旗帜` / `嘿` / `阿嘿` / `朱可夫` / `拉伸` / `预备役` | 卡牌语音（`assest/同名.wav`） | `SoundEffect`，槽位由卡上的 `playEffect = sfx(槽位名)` 给出。清单与对照表见 `CONFIG.md` 的「卡牌语音」 |
| `咚` | **指令卡默认音**（`assest/咚.wav`） | 同上，但**卡上不用写**——指令卡没写 `playEffect` 时由 `battlefield_.PlayCardEffect` 兜底 |
| `research_1` / `research_2` / `research_3` | **研发卡三档**（`assest/MACHINE_Air_Compressor_*.wav` / `TOOL_Wrench_Long_RR1_stereo.wav` / `..._RR2_stereo.wav`） | 三家（美/苏/英）**共用**同一档的槽位——素材是按档给的，不是按国家。档位按卡面 `price`：3 / 6 / 9。见 `CONFIG.md` 的「研发卡三档怎么对上的」 |
| `stalins_organ` / `manhattan` | **两张终极指令卡**（`assest/AU_StalinsOrgan_03.wav` / `AU_Order_ManhattanProj_02.wav`） | 挂在「斯大林管风琴」与「曼哈顿计划」上，**含各自的 cost 0 衍生卡**（同一个显示名 → 同一个音） |
| `infantry_small` / `infantry_medium` / `infantry_large` | **步兵进场音**（`assest/AU_Infantry_*.wav`） | 同上，卡上也不用写——步兵按 `attack + defence` 分三档自动选 |
| `tank_small` / `tank_medium` / `tank_large` | **坦克与火炮进场音**（`assest/Tank_Light/Medium/Heavy_Move_Fx.wav`） | 同上，同一套三档规则，只是换一套素材 |
| `plane_deploy` / `plane_flyby` | **飞机（战斗机与轰炸机）的部署 / 移动音**（`assest/AU_depl_Fighter_small_01~02` / `AU_Flyby_Fighter_small_v2_01~02`） | 同上，卡上不用写，不分档。**两条按时机分**、互不通用，挂载点 `battlefield_.PlaneFallbackEffect` |
| `button` | **按键音**（`assest/General_button2.wav`） | `bin/UiClickSound.cs`。`AttachAll(root)` 递归挂一个界面里的**所有**按钮，`Attach(button)` 挂单个（给运行时才建的按钮）。目前：主菜单、任务选择面板、世界地图、商店、卡组查看器、战斗的「下一回合」与「卡组」；商店里点卡片购买走 `Play()`（那不是 Button） |
| `katyusha_into_pos` | **喀秋莎进场/移动**（`assest/AU_Rocket_Art_Katyusha_IntoPos_01.wav`） | 挂在喀秋莎的 `playEffect` 上——**进场与移动共用这一行**（见 `CONFIG.md` 的「单位『移动』时的音」） |
| `katyusha_fire` | **喀秋莎开火**（`assest/AU_Rocket_Art_Katyusha_fire_02.wav`） | 挂在喀秋莎的 `attackEffect` 上。带了它的攻击特效**不再叠通用机枪声**（`sfx` 在 `NoFiringSoundNames` 里） |
| `draw` | **抽卡音**（`assest/Draw_One_A~E.wav`，**5 条变体**） | 卡牌从牌堆被抽走时由 `battlefield_.PlayDrawSound()` 放。同一段写了 5 个文件、逗号分隔，靠 `[sfx]` 段本来就有的「**随机抽一条**」实现变化——不用写任何抽签代码 |
| `tank_cannon_medium` / `tank_cannon_large` | **坦克开火音**（`assest/AU_Tank_cannon_medium_fire_01~04` / `_heavy_fire_01~03`） | 炮弹**出膛**那一刻由 `battlefield_.TankCannonSoundEffect()` 放。素材只有中/大两套，**小的也归 medium** |
| `tank_cannon_impact` | **坦克命中音**（`assest/AU_Tank_cannon_impact_01~03`） | 炮弹**飞抵目标之后**由特效自己放。**不分档** |
| `artillery_{small\|medium\|large}_fire` | **火炮开火音**（`assest/AU_Artillery_{档}_fire_01a~d`） | 与坦克同一时机，但**三档齐全**、与坦克不同源 |
| `artillery_{small\|medium\|large}_impact` | **火炮命中音**（`assest/AU_Artillery_{档}_impact_01a~c`） | 与坦克同一时机，**三档齐全** |

> 坦克/火炮这一组是**两声**：**开火**在出膛那一刻（`Attack()` 里），**命中**在炮弹飞到之后
> （`BulletEffect` 里）。槽位怎么算、为什么命中音走「特效名的参数」而不是场景 Export，
> 见 `CONFIG.md` 的「坦克与火炮的炮声」。档位判据是共用的 `SizeTier`。

> 这几类素材**卡牌上都不需要写任何东西**，加新卡时自动就有声音。
> 档位边界见 `battlefield_` 的 `DeploySound*Max`，槽位前缀见 `InfantryVoicePrefix` / `TankVoicePrefix`，
> 完整规则见 `CONFIG.md` 的「没写 playEffect 时的兜底」。
>
> 注意区分三个名字很像的槽位：
> - **`plane_flyby`**：飞机（战斗机与轰炸机）**在场上挪位置**时的音；
> - **`plane_deploy`**：同两种飞机**部署**时的音（与上一条**不是**同一条）；
> - **`flyby`**：**飞掠攻击特效**（`flying` / `airstrike`）起飞时的那一声。
>
> 前两个是「单位换位置」，第三个是「攻击演出」，时机完全不同、素材也各用各的。

> `[sfx]` 的键**可以是中文**：`iniHandler` 只取 `=` 前的整段文本做键，不过滤非 ASCII。
> 已实测 `严冬`、`预备役` 等中文槽位都能取到。

> 素材约定：`assest/` 下**下划线开头**的音频是「未采用版本」，不要写进配置。

## 音量分类

- `Master`：所有声音最终汇入此Bus，受主音量修正。
- `Music`：`MusicManager`播放的背景音乐。
- `SFX`：战斗、卡牌等游戏效果音。
- `UI`：按钮等界面声音，目前预留给后续UI音效。

设置菜单使用0至100的滑块修改各Bus音量，0会静音。滑块上下限、默认值与步长由`bin/setting.ini`配置。
