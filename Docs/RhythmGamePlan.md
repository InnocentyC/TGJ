# 音游（RhythmGameScene）设计与实现计划

> 7 轨下落式音游，仅点击键型。本文档是开发前的完整规划，作为后续实现的唯一依据。

---

## 一、需求概述

- 场景：`RhythmGameScene`，2D，7 条下落轨道，键位 A S D F J K L（已定义在 `GameSettings`）。
- 键型：只有"点击"一种，无长按、滑键。
- 谱面：CSV 文件（`Assets/Resources/Chart/Chart_Act1.csv`），每行 = 一个音符（时间秒, 轨道号），支持 `#` 注释和空行，程序读取后自行排序。
- 判定：Perfect / Great / Miss 三档，实时显示评级图片。
- 实时计分 + 连击，打完弹出结算 Panel，之后通过 `GameProgressManager.NotifyRhythmCompleted()` 回到主线流程。

## 二、总体架构

代码放在 `Assets/Scripts/Rhythm/`，按职责拆分：

| 类 | 职责 |
|---|---|
| `RhythmGameManager` | 总控。状态机 Ready → Playing → Result；串起各模块；对接主线流程 |
| `ChartLoader` | 读 CSV → `List<NoteData>`，过滤注释/非法行，按时间排序 |
| `RhythmConductor` | 唯一时间源。基于 `AudioSource.timeSamples` 计算歌曲进度，叠加偏移补偿 |
| `NoteData` | 纯数据：`hitTime`（double）、`lane`、`state`（未生成/在屏/已判定） |
| `NoteController` | 单个音符组件：按公式计算 Y 坐标、被判定后回池 |
| `NotePool` | 对象池：预热 N 个，耗尽自动扩容 |
| `JudgementManager` | 判定逻辑 + 每 lane 的 FIFO 队列 |
| `ScoreManager` | 分数、连击、Perfect/Great/Miss 计数、评级（S/A/B/C/D）、结算数据汇总 |
| `RhythmSceneUI` | 分数/连击显示、评级图片动画、结算 Panel |

核心原则：**逻辑跑在"歌曲时间"上，显示只是把时间映射成 Y 坐标**。
音符位置每帧由公式 `y = judgeLineY + (hitTime - songTime) * fallSpeed` 直接计算，
不做物理移动（无累积误差，帧率波动不影响视觉对齐）。

## 三、数据流

```
CSV → ChartLoader → List<NoteData>（按时间排序）
        │
RhythmConductor: songTime = timeSamples/frequency + (offsetMs + audioLatencyMs)/1000
        │
每帧：hitTime - songTime <= approachTime 的 note → 从池取出，进入所在 lane 的队列
        │
NoteController 按公式更新 Y
        │
玩家按下 lane i 的键 → JudgementManager 看 lane i 队头：
    · |hitTime - songTime| <= perfect 窗口 → Perfect
    · <= great 窗口 → Great
    · <= miss 窗口但提前过多 → 空按不消耗，不判定
    · 队头已超 miss 窗口 → 自动 Miss 出队（Update 中做，不等按键）
        │
结果 → ScoreManager 更新分数/连击 → UI 换评级图 + 播放按键音 → note 回池
```

**同轨道先进先出**：每个 lane 维护一个按时间排序的队列，按键只判队头，
天然保证"多个可点音符时优先判先落下的"。同时按的双押在不同 lane，互不冲突。

## 四、预制体与图片的取舍

- **Note 预制体（必须）**：一个 `Image` + `NoteController` 脚本。需要独立生命周期
  （激活/隐藏/回池）。**七个轨道外形完全相同，单个预制体共用一个 sprite**，
  只按 `laneXPositions` 摆放位置，不做换色/换图逻辑。
- **Perfect/Great/Miss 图片（不做预制体）**：场景里固定一个 `Image`，平时隐藏，
  判定时换 sprite + 淡出/缩放动画（协程 + CanvasGroup）。
- **轨道按键框**：7 个固定 `Image`，按下时切换颜色。
- **按键音**：一个 AudioSource 即可（切换 Clip 播放）。
  **音色跟音符走，不跟轨道走**：谱面第三列"音高"是打击音编号，
  Inspector 里有一个可自由增减的**打击音队列**（List，数量不定，按编号顺序排），
  命中音符时按其音高编号取队列里的 Clip 播放；空按用 0 号音兜底。
  **章节层**：每个谱面的这组音色不一样，换章即换整组；
  `keySoundVolume` 仍是全局设置。

## 五、配置项划分

### 5.1 存档配置（并入 `GameSettings`，JSON 存盘）

| 变量 | 类型/默认值 | 说明 |
|---|---|---|
| `rhythmOffsetMs` | float，0 | 已有。判定整体偏移（不做手动校准 UI，值直接写在配置里） |
| `audioLatencyMs` | float，0 | 音频输出延迟补偿（蓝牙耳机等硬件延迟） |
| `approachTime` | float，1.5 秒 | 下落速度：音符从出现点到判定线的时间，越小越快 |
| `keySoundVolume` | float，0.6 | 按键音效音量（实际输出乘 masterVolume） |
| `laneKeys`（已有）+ 设置面板键位调整 | KeyCode[7] | 存档里已存 7 轨键位，需在设置面板加"改键"入口：点击某轨道 → 等待玩家按下新键 → 写回 `laneKeys` 并存盘（含冲突检测：同一键不能绑两条轨道） |

`masterVolume`、`laneKeys` 已存在于 `GameSettings`，不重复添加。

### 5.2 Inspector 配置（挂在 `RhythmGameManager`）

判定参数：

| 变量 | 默认值 | 说明 |
|---|---|---|
| `perfectWindowMs` | 50 | Perfect 窗口（±毫秒） |
| `greatWindowMs` | 100 | Great 窗口 |
| `missWindowMs` | 150 | 超过自动 Miss；提前超此值 → 空按不消耗 |

对象池：

| 变量 | 默认值 | 说明 |
|---|---|---|
| `poolPreloadCount` | 20 | 预热数量 |
| `poolCanExpand` | true | 耗尽自动扩容（数量不写死的关键） |

场景引用（Inspector 拖拽）：

| 变量 | 说明 |
|---|---|
| `musicSource` | 歌曲 AudioSource |
| `keySoundSource` / `keySoundClip` | 按键音 |
| `judgeLineY` / `spawnY` | 判定线与出现点 Y 坐标 |
| `laneXPositions` | float[7]，各轨道 X 坐标 |
| `notePrefab` | Note 预制体 |
| UI 引用 | 分数 Text、连击 Text、评级 Image、结算 Panel |

结算参数：

| 变量 | 默认值 | 说明 |
|---|---|---|
| `endBufferSeconds` | 2.0 | 音乐播完后多久出结算（无音乐时：最后一个音符判定后） |
| `scorePerPerfect` / `scorePerGreat` | 1000 / 500 | 计分权重（连击加成系数另定） |

划分标准：玩家会调、要存盘 → `GameSettings`；只有开发者调 → Inspector；
以后若做"判定难度"选项，再把窗口参数挪进 `GameSettings`。

## 六、手感与延迟措施

1. **时间源用 `AudioSource.timeSamples / clip.frequency`**（采样级精度，与实际听到的声音同步），
   不用 `Time.time`。注意取值要 clamp 防止偶发不单调。
2. **位置由时间公式直接算**，不用 Translate 累积。
3. **判定比时间，不比距离**：距离 = 时间差 × 速度，直接比时间更精确且与分辨率无关。
4. **偏移补偿**：`songTime` 叠加 `rhythmOffsetMs + audioLatencyMs`（先不做校准 UI，值写配置文件）。

## 七、CSV 接口约定

- 路径：`Resources/Chart/Chart_<幕名>`。**每章谱面各不相同**，由章节数据 /
  `GameFlowConfig` 指定本章的谱面路径与**打击音队列**，`RhythmGameManager` 开局按当前章节加载。
- 格式：`时间,轨道,音高` 三列。**音高 = 打击音编号（Inspector 打击音队列的下标），
  与轨道解绑**——同轨道可打不同音色，同音色可出现在不同轨道；同按的两行可共用同一音高。
- 解析：`Resources.Load<TextAsset>` 读入（随包发布更稳）；
  逐行 Trim → 跳过 `#` 与空行 → 按 `,` 分割 →
  `double.Parse(时间, CultureInfo.InvariantCulture)`（**必须 InvariantCulture**，防小数点兼容问题）→
  `int.Parse(轨道)` → 越界/非法行打 warning 跳过 → 按时间排序。
- 可参考现有 `DialogueCsvParser` 的代码风格。

## 八、结算流程

1. 所有 note 出队完毕且**音乐播完后 `endBufferSeconds` 秒**（用音乐的原始时间判断）→ 进入 Result；
   没配音乐时退回旧逻辑：最后一个音符判定完 `endBufferSeconds` 秒。
2. `ScoreManager` 汇总结算数据：`score`、`combo`（最大连击）、`perfect` / `great` / `miss` 计数，
   以及综合评级 **S / A / B / C / D**（结算界面用图片显示）。
3. 评级的具体划分标准（按分数还是按准确率、各档阈值）**待与策划确定**，代码上
   留成可配置的阈值参数即可，先按占位逻辑实现。
4. 弹结算 Panel（风格参照现有 `PopupPanel`），确认后调
   `GameProgressManager.NotifyRhythmCompleted()`，按现有流程回主线。

## 九、实现顺序（每步可独立验证）

1. `ChartLoader` + 调试日志 → 确认谱面读对
2. `RhythmConductor` + 音频播放 + 调试显示 songTime
3. Note 预制体 + 对象池 + 公式下落 → **关键里程碑：下落视觉与音乐对齐，手感在此定型**
4. 按键判定 + lane FIFO 队列
5. 分数 / 连击 / 评级图 UI
6. 结算 Panel + 对接 `GameProgressManager`

## 十、未做 / 后续可扩展

- 手动校准流程（设置里只留偏移值输入）
- Fast/Late 显示、打击粒子特效（独立小对象池）
- 长按 / 滑键等键型
- 判定难度选项

---

## 十一、实现说明（已落地）

代码在 `Assets/Scripts/Rhythm/` 下：`NoteData`、`ChartLoader`、`RhythmConductor`、
`NoteController`、`NotePool`、`JudgementManager`、`ScoreManager`、`RhythmSceneUI`、`RhythmGameManager`。
`GameSettings` 新增 `audioLatencyMs` / `approachTime` / `keySoundVolume` 三个存档项。

### 场景搭建（美术场景已就绪）

1. 把 `RhythmGameManager` 挂到美术搭好的音游场景物体上。
2. Inspector 拖引用：`scoreText` / `comboText`（分数、连击文本）、
   `judgementImage`（判定弹出图）、`notePrefab`（音符预制体）、`noteParent`（音符容器）。
3. 对齐布局数值：`judgeLineY`（判定线 Y）、`spawnY`（出现点 Y）、`laneXPositions[7]`（各轨道中心 X，Canvas 局部坐标）。
4. 拖三档判定图 `perfectSprite` / `greatSprite` / `missSprite`（弹出时换图 + 淡出）。
5. 配歌曲 `musicClip`、按键音 `laneKeyClips[7]`，运行。

结算面板暂未做：打完后 **Console 打 log**（评级/分数/连击/三档计数/准确率），
延迟 `resultAdvanceDelay` 秒后自动调 `GameProgressManager.NotifyRhythmCompleted()` 回主线；
等结算 Panel 做好后把自动推进改为按钮触发。

### 默认数值（后续直接改 Inspector / 配置，不用动代码）

| 项 | 默认值 | 位置 |
|---|---|---|
| 判定窗口 Perfect / Great / Miss | ±50 / ±100 / ±150 ms | Inspector |
| 计分 | Perfect 1000、Great 500，连击加成封顶 +20% | 代码内（BasePoint） |
| 评级 S/A/B/C 阈值 | 准确率 95% / 90% / 80% / 70%，其余 D（待策划定稿） | Inspector `rankThresholds` |
| 准确率 | Perfect=1、Great=0.5、Miss=0 加权 | 代码内 |
| 下落时间 approachTime | 1.5 秒 | GameSettings 存档 |
| 按键音量 / 音频延迟 / 判定偏移 | 0.6 / 0 / 0 | GameSettings 存档 |
| 池预热 | 20 个，可自动扩容 | Inspector |

### 每章换谱换音色

`RhythmGameManager.chartResourcePath` 留空时按当前幕号自动取 `Chart/Chart_Act{幕号}`；
也可手动指定。`laneKeyClips[7]` 即本章的 7 轨音色，每章场景各配各的。
没配音色时内置 7 个不同音高的合成音（五声音阶），方便先跑通测试。

