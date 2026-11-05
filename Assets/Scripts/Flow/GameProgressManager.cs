using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局进度监督器（唯一）。整个游戏的流程只有它说了算：
///
///   开始界面 --点开始--> 开场剧情 --> 第1幕(探索 -> 拼图 -> 音游)
///            --> 第2幕(...) --> 第3幕(...) --> 结尾
///
/// 职责：
///   1. 记录"现在进行到哪一步"（大阶段 + 第几幕 + 幕内小阶段）；
///   2. 每一步完成时由各玩法通知它，它决定下一步并切场景；
///   3. 进度实时落盘，退出游戏后再进来能接着玩（ContinueGame）；
///   4. 给章节选择界面提供"哪些章节能选 / 通关没有"的查询与跳转。
///
/// 用法：不用手动往场景里放，代码会在启动时自己创建（DontDestroyOnLoad）。
/// 想改默认场景映射就建一个 Assets/Resources/GameFlowConfig.asset。
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameProgressManager : MonoBehaviour
{
    /// <summary>章节选择界面用的单章状态</summary>
    public struct ChapterStatus
    {
        public int index;           // 0 起
        public string displayName;  // 章节名
        public bool unlocked;       // 是否已解锁（可选）
        public bool cleared;        // 是否已通关
        public bool isCurrent;      // 是否是当前进行中的章节
    }

    private const string ConfigResourcePath = "GameFlowConfig";

    private static GameProgressManager instance;

    [Tooltip("可选：不放在 Resources 下时，把 GameFlowConfig 资产拖到这里")]
    [SerializeField] private GameFlowConfig configOverride;

    private GameFlowConfig config;
    private ProgressSaveData data;

    // ---------------- 事件（界面/玩法可以订阅） ----------------

    /// <summary>任何进度变化后触发（UI 刷新用）</summary>
    public static event Action OnProgressChanged;

    /// <summary>阶段/幕/小阶段变化后触发</summary>
    public static event Action<GamePhase, int, ActStage> OnStageChanged;

    /// <summary>准备切场景时触发，参数是目标场景名（可在这里接转场动画/淡入淡出）</summary>
    public static event Action<string> OnBeforeSceneLoad;

    // ---------------- 生命周期 ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInit()
    {
        EnsureInstance();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        LoadConfig();
        LoadProgress();

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    // 退出 / 切后台时兜底存一次，保证"退出后可继续"
    private void OnApplicationQuit()
    {
        SaveProgress();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveProgress();
        }
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            SaveProgress();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 场景加载完广播一次，方便界面刷新"当前是第几幕/哪个阶段"
        RaiseProgressChanged();
    }

    // ---------------- 单例 ----------------

    /// <summary>全局唯一访问入口</summary>
    public static GameProgressManager Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    /// <summary>是否已经存在实例（不想触发自动创建时用这个判断）</summary>
    public static bool HasInstance
    {
        get { return instance != null; }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindObjectOfType<GameProgressManager>();

        if (instance == null)
        {
            GameObject go = new GameObject("GameProgressManager");
            instance = go.AddComponent<GameProgressManager>();
        }
    }

    // ---------------- 当前进度查询 ----------------

    /// <summary>是否有存档（主菜单靠它决定要不要显示"继续游戏"）</summary>
    public bool HasSave
    {
        get { return data != null && data.hasSave; }
    }

    /// <summary>原始存档数据（只读用途，改完记得 SaveProgress）</summary>
    public ProgressSaveData Data
    {
        get { return data; }
    }

    public GamePhase Phase
    {
        get { return data == null ? GamePhase.None : data.phase; }
    }

    /// <summary>当前第几幕，0 起；不在幕内时 -1</summary>
    public int ActIndex
    {
        get { return data == null ? -1 : data.actIndex; }
    }

    /// <summary>当前第几幕，1 起；不在幕内时 0</summary>
    public int ActNumber
    {
        get { return ActIndex + 1; }
    }

    /// <summary>当前幕内小阶段</summary>
    public ActStage Stage
    {
        get { return data == null ? ActStage.Explore : data.stage; }
    }

    /// <summary>当前阶段应该在的场景名（没配就是空字符串）</summary>
    public string CurrentSceneName
    {
        get { return ResolveSceneName(data == null ? GamePhase.None : data.phase, ActIndex, Stage); }
    }

    /// <summary>当前是否在指定幕</summary>
    public bool IsInAct(int actIndex)
    {
        return Phase == GamePhase.Act && ActIndex == actIndex;
    }

    /// <summary>当前是否在指定小阶段</summary>
    public bool IsAtStage(ActStage stage)
    {
        return Phase == GamePhase.Act && Stage == stage;
    }

    // ---------------- 流程入口 ----------------

    /// <summary>开始新游戏：清掉旧档，从开场剧情开始</summary>
    public void StartNewGame()
    {
        data.Clear();
        data.hasSave = true;
        data.phase = GamePhase.Prologue;
        data.actIndex = -1;
        data.stage = ActStage.Explore;

        SaveProgress();
        Debug.Log("[GameProgressManager] 开始新游戏 -> 开场剧情");
        EnterCurrentStage(false);
    }

    /// <summary>继续游戏：读档回到上次那一步。没有存档时退化为开始新游戏。</summary>
    public void ContinueGame()
    {
        if (!HasSave)
        {
            Debug.LogWarning("[GameProgressManager] 没有存档，改为开始新游戏");
            StartNewGame();
            return;
        }

        Debug.Log("[GameProgressManager] 继续游戏 -> " + data);
        EnterCurrentStage(true);
    }

    /// <summary>回到开始界面（不清档）</summary>
    public void BackToStartMenu()
    {
        LoadScene(config.GetPhaseScene(GamePhase.None));
    }

    // ---------------- 背包接口（拼图碎片收集，由对话/探索系统调用） ----------------

    /// <summary>
    /// 收集一块拼图碎片（追加到该章收集顺序末尾，立即落盘）。
    /// 只允许在对应幕的探索中收集；重复收集返回 false。
    /// pieceIndex 对应 PuzzleChapterConfig.pieces 的列表序号。
    /// </summary>
    public bool CollectPiece(int actIndex, int pieceIndex)
    {
        if (Phase != GamePhase.Act)
        {
            Debug.LogWarning("[GameProgressManager] 当前不在幕内，无法收集碎片");
            return false;
        }

        if (actIndex != ActIndex)
        {
            Debug.LogWarning("[GameProgressManager] 只能收集当前章节（第" + ActNumber + "幕）的碎片");
            return false;
        }

        if (actIndex < 0 || actIndex >= data.chapters.Length)
        {
            return false;
        }

        bool added = data.chapters[actIndex].AddCollectedPiece(pieceIndex);

        if (added)
        {
            SaveProgress();
            RaiseProgressChanged();
            Debug.Log("[GameProgressManager] 第" + (actIndex + 1) + "幕收集碎片 #" + pieceIndex);
        }

        return added;
    }

    /// <summary>某章是否已收集指定编号的碎片</summary>
    public bool HasPiece(int actIndex, int pieceIndex)
    {
        if (actIndex < 0 || actIndex >= data.chapters.Length)
        {
            return false;
        }

        return data.chapters[actIndex].HasCollectedPiece(pieceIndex);
    }

    /// <summary>某章已收集碎片的编号列表（按收集先后排列，只读）</summary>
    public IReadOnlyList<int> GetCollectedPieces(int actIndex)
    {
        if (actIndex < 0 || actIndex >= data.chapters.Length)
        {
            return Array.Empty<int>();
        }

        data.chapters[actIndex].EnsureCollectedOrder();
        return data.chapters[actIndex].collectedPieceOrder;
    }

    /// <summary>某章已收集的碎片数量</summary>
    public int GetCollectedCount(int actIndex)
    {
        if (actIndex < 0 || actIndex >= data.chapters.Length)
        {
            return 0;
        }

        data.chapters[actIndex].EnsureCollectedOrder();
        return data.chapters[actIndex].collectedPieceOrder.Count;
    }

    // ---------------- 各阶段完成通知（由玩法系统调用） ----------------

    /// <summary>开场剧情播完 -> 进入第 1 幕的地图探索</summary>
    public void NotifyPrologueFinished()
    {
        data.hasSave = true;
        data.phase = GamePhase.Act;
        data.actIndex = 0;
        data.stage = ActStage.Explore;

        Commit("开场剧情结束 -> 第1幕探索");
    }

    /// <summary>地图探索完成（碎片凑齐/决定开拼图）-> 进入拼图</summary>
    public void NotifyExploreFinished()
    {
        if (Phase != GamePhase.Act)
        {
            return;
        }

        data.stage = ActStage.Puzzle;
        Commit("探索完成 -> 拼图");
    }

    /// <summary>拼图完成 -> 进入本幕音游</summary>
    public void NotifyPuzzleCompleted()
    {
        ChapterProgress chapter = CurrentChapter;

        if (chapter == null)
        {
            return;
        }

        chapter.puzzleCleared = true;
        data.stage = ActStage.Rhythm;

        Commit("拼图完成 -> 音游");
    }

    /// <summary>音游完成 -> 下一幕；三幕都过完 -> 结尾</summary>
    public void NotifyRhythmCompleted()
    {
        ChapterProgress chapter = CurrentChapter;

        if (chapter == null)
        {
            return;
        }

        chapter.rhythmCleared = true;

        if (data.actIndex + 1 < config.ActCount)
        {
            data.actIndex++;
            data.stage = ActStage.Explore;
            Commit(string.Format("音游完成 -> 第{0}幕探索", data.actIndex + 1));
        }
        else
        {
            data.phase = GamePhase.Ending;
            data.actIndex = -1;
            data.stage = ActStage.Explore;
            Commit("三幕全部完成 -> 结尾");
        }
    }

    /// <summary>结尾播完（结尾内容没定，先留接口）：清档并回开始界面</summary>
    public void NotifyEndingFinished(bool clearSave = true)
    {
        if (clearSave)
        {
            DeleteSave();
        }

        BackToStartMenu();
    }

    // ---------------- 章节选择 ----------------

    /// <summary>第 index 幕是否已解锁：第 1 幕永远解锁，之后要前一幕通关</summary>
    public bool IsChapterUnlocked(int actIndex)
    {
        if (actIndex < 0 || actIndex >= Mathf.Min(config.ActCount, data.chapters.Length))
        {
            return false;
        }

        if (actIndex == 0)
        {
            return true;
        }

        return data.chapters[actIndex - 1].IsCleared;
    }

    /// <summary>第 index 幕是否已通关</summary>
    public bool IsChapterCleared(int actIndex)
    {
        if (actIndex < 0 || actIndex >= data.chapters.Length)
        {
            return false;
        }

        return data.chapters[actIndex].IsCleared;
    }

    /// <summary>章节选择界面要的一整份数据</summary>
    public ChapterStatus[] GetChapterStatuses()
    {
        int count = config.ActCount;
        ChapterStatus[] result = new ChapterStatus[count];

        for (int i = 0; i < count; i++)
        {
            result[i] = new ChapterStatus
            {
                index = i,
                displayName = config.GetChapterName(i),
                unlocked = IsChapterUnlocked(i),
                cleared = IsChapterCleared(i),
                isCurrent = IsInAct(i)
            };
        }

        return result;
    }

    /// <summary>
    /// 章节选择：直接跳到第 index 幕。
    /// 默认从地图探索开始；传 ActStage.Puzzle / Rhythm 可以直达幕内某一步。
    /// 返回 false 表示章节没解锁，跳不了。
    /// </summary>
    public bool StartChapter(int actIndex, ActStage stage = ActStage.Explore)
    {
        if (!IsChapterUnlocked(actIndex))
        {
            Debug.LogWarning("[GameProgressManager] 第" + (actIndex + 1) + "幕未解锁，无法进入");
            return false;
        }

        data.hasSave = true;
        data.phase = GamePhase.Act;
        data.actIndex = actIndex;
        data.stage = stage;

        SaveProgress();
        RaiseProgressChanged();

        Debug.Log(string.Format("[GameProgressManager] 章节选择 -> 第{0}幕 {1}", actIndex + 1, ProgressSaveData.StageName(stage)));

        // 强制重载：就算和目标场景同名也要重开一次，保证是干净的一幕
        EnterCurrentStage(true);
        return true;
    }

    /// <summary>三幕是否全部通关（可以用来解锁结尾/ extras）</summary>
    public bool AllChaptersCleared
    {
        get
        {
            for (int i = 0; i < data.chapters.Length; i++)
            {
                if (!data.chapters[i].IsCleared)
                {
                    return false;
                }
            }

            return true;
        }
    }

    // ---------------- 存档 ----------------

    /// <summary>立即把当前进度写盘</summary>
    public void SaveProgress()
    {
        if (data == null || !data.hasSave)
        {
            return;
        }

        ProgressSaveSystem.Save(data);
    }

    /// <summary>删档（会同时清掉内存里的进度）</summary>
    public void DeleteSave()
    {
        data.Clear();
        ProgressSaveSystem.Delete();
        RaiseProgressChanged();
        Debug.Log("[GameProgressManager] 存档已清除");
    }

    // ---------------- 调试辅助 ----------------

    /// <summary>
    /// 手动把进度改到指定位置（直接从一个场景起 Play 调试时用）。
    /// </summary>
    public void AssumeStage(GamePhase phase, int actIndex, ActStage stage, bool save = false)
    {
        data.hasSave = true;
        data.phase = phase;
        data.actIndex = actIndex;
        data.stage = stage;

        if (save)
        {
            SaveProgress();
        }

        RaiseProgressChanged();
    }

    /// <summary>重新加载当前阶段所在场景（比如"重试本幕"）</summary>
    public void ReloadCurrentStage()
    {
        EnterCurrentStage(true);
    }

    // ---------------- 内部实现 ----------------

    private void LoadConfig()
    {
        config = configOverride;

        if (config == null)
        {
            config = Resources.Load<GameFlowConfig>(ConfigResourcePath);
        }

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GameFlowConfig>();
        }

        config.EnsureValid();
    }

    private void LoadProgress()
    {
        data = ProgressSaveSystem.Load();

        if (data == null)
        {
            data = new ProgressSaveData();
            data.Clear();
        }

        data.EnsureChapters();

        // 存档和配置的幕数不一致时纠正一下，防止越界
        int actCount = Mathf.Min(config.ActCount, data.chapters.Length);

        if (data.phase == GamePhase.Act && (data.actIndex < 0 || data.actIndex >= actCount))
        {
            data.actIndex = 0;
            data.stage = ActStage.Explore;
        }
    }

    /// <summary>当前幕的记录；不在幕内或越界时返回 null</summary>
    private ChapterProgress CurrentChapter
    {
        get
        {
            if (Phase != GamePhase.Act || data.chapters == null)
            {
                return null;
            }

            if (data.actIndex < 0 || data.actIndex >= data.chapters.Length)
            {
                return null;
            }

            return data.chapters[data.actIndex];
        }
    }

    /// <summary>存盘 + 广播 + 进入对应场景</summary>
    private void Commit(string log)
    {
        SaveProgress();
        Debug.Log("[GameProgressManager] " + log);
        RaiseProgressChanged();
        EnterCurrentStage(false);
    }

    private void RaiseProgressChanged()
    {
        if (OnProgressChanged != null)
        {
            OnProgressChanged();
        }

        if (OnStageChanged != null)
        {
            OnStageChanged(Phase, ActIndex, Stage);
        }
    }

    /// <summary>按当前进度进入该去的场景</summary>
    private void EnterCurrentStage(bool forceReload)
    {
        string sceneName = CurrentSceneName;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[GameProgressManager] 当前阶段（" + data + "）还没配场景，停在原地。" +
                             "到 GameFlowConfig 里把对应场景名填上就会自动切过去。");
            return;
        }

        LoadScene(sceneName, forceReload);
    }

    private string ResolveSceneName(GamePhase phase, int actIndex, ActStage stage)
    {
        if (phase == GamePhase.Act)
        {
            return config.GetStageScene(actIndex, stage);
        }

        return config.GetPhaseScene(phase);
    }

    private void LoadScene(string sceneName, bool forceReload = false)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        if (!GameFlowConfig.IsSceneAvailable(sceneName))
        {
            Debug.LogWarning("[GameProgressManager] 场景 [" + sceneName +
                             "] 不可用（没配或没加进 Build Settings），本次不切场景");
            return;
        }

        if (OnBeforeSceneLoad != null)
        {
            OnBeforeSceneLoad(sceneName);
        }

        string current = SceneManager.GetActiveScene().name;

        if (!forceReload && current == sceneName)
        {
            // 已经在这个场景里了（比如拼图就是探索场景内的弹窗），不用重载
            Debug.Log("[GameProgressManager] 已在场景 [" + sceneName + "]，不重复加载");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
