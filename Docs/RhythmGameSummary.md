# 音游开发总结（学习笔记）

> 对 RhythmGameScene 从零到完成的复盘：架构设计、关键技术、踩坑记录。
> 配套设计文档见 `Docs/RhythmGamePlan.md`。代码在 `Assets/Scripts/Rhythm/`。

---

## 一、最终架构

7 轨下落式音游，仅点击键型。按职责拆成 9 个文件：

| 模块 | 文件 | 职责 |
|---|---|---|
| 总控 | `RhythmGameManager` | 状态机 Ready→Playing→Result、章节配置、主循环、输入、音效 |
| 谱面 | `ChartLoader` | CSV → `NoteData` 列表 |
| 数据 | `NoteData` | 音符纯数据 + 判定档位枚举 |
| 时间 | `RhythmConductor` | 唯一时间源，提供 `SongTime` / `RawTime` |
| 池 | `NotePool` | 音符对象池（预热 + 自动扩容） |
| 池对象 | `NoteController` | 单个音符的壳，只管数据绑定 |
| 判定 | `JudgementManager` | 每 lane FIFO 队列 + 三档判定 + 自动 Miss |
| 计分 | `ScoreManager` | 分数/连击/三档计数/准确率/S~D 评级 |
| UI | `RhythmSceneUI` | 分数/连击刷新（判定弹出功能已注释备用） |
| 编辑器 | `RhythmGameManagerEditor` | Inspector 章节摘要显示 |

**核心原则：逻辑跑在"歌曲时间"上，显示只是把时间映射成 Y 坐标。**

```
CSV → NoteData 列表（按时间排序）
Conductor.SongTime（每帧）
  ├─ 生成：hitTime - songTime <= approachTime 的音符入池、进 lane 队列
  ├─ 显示：y = 判定线y + (hitTime - songTime) × fallSpeed
  ├─ 输入：按键只判 lane 队头 → 计分 → 回池
  └─ 收尾：音符打完 + 音乐播完 → 结算 → NotifyRhythmCompleted()
```

## 二、技术要点（值得记住的）

### 1. 时间源：`AudioSource.timeSamples`，不用 `Time.time`

```csharp
raw = (double)musicSource.timeSamples / musicSource.clip.frequency;
```

- 精度到采样级，且**和耳朵听到的声音天然同步**——音游手感的根基；
- `Time.time` 与音频播放互相独立，帧率一抖视觉就和音乐脱节；
- 保险措施：记录 `lastRawTime`，发现时间倒退就钳住（`timeSamples` 偶发不单调）；
- 音乐开始前用 `AudioSettings.dspTime - scheduledStartTime` 算负数进度，
  起播用 `PlayScheduled` 精确调度，两条时间线在起播点无缝衔接。

### 2. 位置用公式直接算，不用 Translate 累积

```csharp
float y = judgeLineY + (float)(note.hitTime - songTime) * fallSpeed;
transform.localPosition = new Vector3(laneX, y, 0f);
```

- 每帧从时间**重算**位置，误差不累积；帧率波动只影响精度不影响正确性；
- 对比错误做法 `transform.Translate(velocity * deltaTime)`：deltaTime 误差逐帧累积，掉帧时音符"变慢"；
- 判定也比时间不比距离：`|songTime - hitTime|` 与窗口比，和分辨率、下落速度完全解耦。

### 3. 偏移补偿链

```
SongTime = RawTime - (rhythmOffsetMs + audioLatencyMs) / 1000
```

- `audioLatencyMs`：硬件输出延迟（蓝牙耳机），客观补偿；
- `rhythmOffsetMs`：玩家个人手感偏移，主观补偿；
- 正偏移 = 判定整体往后推。全部存在 `GameSettings`（JSON 存档），代码里不写死。

### 4. 对象池

- `Queue<NoteController>` 预热 20 个；`Get()` 空了按需扩容（**数量不写死**）；
- 回池三件事：数据解绑（`Unbind`）、`SetActive(false)`、入队；
- **谁把音符从 lane 队列移除？** 判定管理器。池只管 GameObject 的生死，
  数据结构（队列）的增删由判定层负责——职责分清，回池时序就不会出 bug。

### 5. 同轨道先进先出（FIFO）

- 谱面按时间排序后，每个 lane 一个 `Queue<NoteData>`，入队顺序 = 落下顺序；
- 按键**只看队头**："多个音符可点时优先判先落下的"自动成立；
- 三种情况的边界划分（`delta = songTime - hitTime`，正 = 按晚了）：
  - `|delta| <= Perfect窗口` → Perfect；
  - `<= Great窗口` → Great；
  - `在 Miss 窗口内但超出 Great` → Miss（消耗）；
  - `delta < -Miss窗口`（按太早）→ **空按不消耗**，等音符再落；
  - 队头 `delta > Miss窗口` 还没按 → 自动 Miss 出队（每帧检查，不等按键）。

### 6. CSV 解析的三个细节

```csharp
double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out t);
```

- **`InvariantCulture` 必须写**：部分系统区域小数点解析规则不同，不写会崩；
- **UTF-8 BOM**：CSV 首行可能带 `\uFEFF` 不可见字符，解析前要剥掉；
- 非法行 `Debug.LogWarning` 后跳过而不是中断——策划手滑不该让游戏崩；
- 读完后统一 `Sort by hitTime`，谱面行序写错也能跑；
- 用 `Resources.Load<TextAsset>` 而非文件 IO，随包发布不丢文件。

### 7. 章节配置：嵌套可序列化类

```csharp
[System.Serializable]
public class ChapterSetup {
    public string chartResourcePath;
    public AudioClip musicClip;
    public List<AudioClip> keySoundClips;   // 数量每章随意
}
public List<ChapterSetup> chapters;         // 加几项就是几章
```

- Inspector 原生支持嵌套 List，就是"数目不定的矩阵"；
- 运行时按 `GameProgressManager.ActIndex` 选 `chapters[act]`，
  不在幕内（直接开场景测试）用 `sceneTestChapterIndex` 兜底；
- 自定义 Editor（`Assets/Editor/` 下，`[CustomEditor]` + `DrawDefaultInspector()`）
  在面板顶部加 HelpBox 显示章节摘要和漏配警告——**编辑器脚本别碰运行时单例**，
  否则单例可能在编辑模式下误创建场景对象。

### 8. 场景坐标：标记点代替手填数值

- 判定线/出现点/7 条轨道中心，全用场景里摆空物体 + Inspector 拖 Transform；
- 运行时 `noteParent.InverseTransformPoint(marker.position)` 换算成音符所在坐标系——
  **不用关心像素还是米、PPU 是多少、Canvas 是什么模式**；
- 音符 parent 在 Canvas 下时，`localPosition` 就是 Canvas 局部坐标，与标记点同一参照系。

## 三、性能：两板斧 + 一个认知

### 1. UGUI Canvas 重建（这次卡顿的元凶）

UGUI 中**任何** UI 元素变动（包括每帧改 `localPosition`），整个 Canvas 下所有 UI
都要重新合批（`SendWillRenderCanvases`）。音符每帧都在动，和美术的大 Canvas
挤在一起 = 每帧重建整个界面。

**解法**：动态元素放独立子 Canvas。运行时在 `noteParent` 下建一个
`NotesCanvas`（挂 `Canvas` 组件的空子节点），音符全放它底下——
每帧只重建 20 个音符的小画布。

### 2. 帧率上限

```csharp
QualitySettings.vSyncCount = 0;
Application.targetFrameRate = 120;
```

默认锁 60（垂直同步），高刷屏上不丝滑。做成 Inspector 可调，
设 0 = 回系统默认。

### 3. 认知：Editor ≠ 真机

编辑器本身有巨大开销，Editor 里卡 ≠ 打包卡。调手感问题先打包验证；
真要查，用 Profiler 看 `UI.Canvas.SendWillRenderCanvases` 这类条目，
别凭感觉猜。Profiler 内存大快照本身就能把编辑器搞挂（见踩坑）。

## 四、踩坑记录（按发生顺序）

| # | 现象 | 原因 | 解法 / 教训 |
|---|---|---|---|
| 1 | CS0246 找不到 `Image` | `RhythmGameManager` 少 `using UnityEngine.UI;` | 新文件用到 UGUI 必带这个 using |
| 2 | CS1503 类型转换错误 | 把 `NoteData` 传给了要 `NoteController` 的 `pool.Release()` | 数据类和视图类分清；回池传 controller 且判空 |
| 3 | TMP 文本拖不进槽 | 字段类型是旧版 `Text`，TMP 组件是 `TMP_Text`，类型不兼容 | 同一字段同时暴露 `Text` 和 `TMP_Text` 两对，运行时 TMP 优先 |
| 4 | 判定图被 Canvas 盖住（白色方块/不可见） | 自动生成的弹图挂在世界空间物体下，被 Screen Space - Overlay Canvas 整个盖住 | 动态生成的 UI 必须挂在 Canvas 层级内，位置用 `InverseTransformPoint` 换算 |
| 5 | 判定图永远是白方块 | `ui.Setup(三张sprite)` 在判定图**创建之前**执行，绑定到了 null 上；图出生后没人管，无 sprite 无 CanvasGroup 常亮 | **初始化顺序**：先创建对象，再注入引用/配置。依赖注入前对象必须已确定 |
| 6 | Editor 内存耗尽（OOM） | 编辑器内存压力 + 可能的错误刷屏 + 全速渲染 | 查 Console 有无每帧报错；`targetFrameRate` 回 60 对照测试；重启编辑器；音频 Load Type 用 Streaming |
| 7 | Profiler 卡死无响应 | 编辑器假死，只能杀进程 | 代码都在磁盘上不会丢；场景手动改动若未 Ctrl+S 会丢；杀进程后异常就删 `Library` 重建缓存（只删缓存！） |

**贯穿的教训**：
- 编译错误 90% 是 using 和类型不匹配，先读报错行号；
- "东西不显示"三连查：层级（被谁盖住）→ 引用（绑上没有）→ 时序（绑的时机对不对）；
- 改字段结构后，Inspector 上旧的拖拽引用会被重置，要重新拖。

## 五、设计与代码的几个取舍

- **Note 做预制体 + 池，评级图/按键框用场景固定节点**：需要独立生命周期的才值得池化；
- **判定按时间不按像素**：分辨率无关、参数好调（毫秒）；
- **玩家调的进存档，开发者调的进 Inspector**：`GameSettings`（JSON）vs Inspector 分工清晰；
- **数值全部不写死**：判定窗口、池大小、帧率、评级阈值全 Inspector 可调，没配音色/音乐都有兜底（合成音/系统时钟），保证"任何情况下能跑"；
- **功能注释而非删除**：判定弹出图被砍需求后整块 `/* */` 注释并加标记，策划反悔可一键恢复。

## 六、留给后续的扩展点

- 结算 Panel（目前 Console log + 自动推进，`resultAdvanceDelay` 秒后调 `NotifyRhythmCompleted()`）
- 评级 S/A/B/C/D 阈值待策划定稿（Inspector `rankThresholds`，现占位 95/90/80/70）
- 手动校准流程、Fast/Late 显示、打击粒子特效
- 长按/滑键键型、判定难度选项
