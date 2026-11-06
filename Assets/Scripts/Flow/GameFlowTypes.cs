using System;
using System.Collections.Generic;

/// <summary>
/// 全局流程用到的类型、常量和存档数据结构。
/// 这里只放"数据"，所有推进流程的逻辑都在 GameProgressManager 里。
/// </summary>

/// <summary>全局规则常量</summary>
public static class GameRules
{
    /// <summary>总幕数：三幕</summary>
    public const int ActCount = 4;
}

/// <summary>
/// 游戏的大阶段。整个流程：
/// None -> Prologue（开场剧情）-> Act x3（每幕：探索 -> 拼图 -> 音游）-> Ending（结尾）
/// </summary>
public enum GamePhase
{
    None,       // 没有存档 / 从未开始过
    Prologue,   // 开场剧情
    Act,        // 三幕中的某一幕，具体进度看 ActStage
    Ending      // 结尾（暂未实现，留空）
}

/// <summary>
/// 一幕内部的三个小阶段，固定顺序：探索 -> 拼图 -> 音游。
/// 拼图完成进入音游，音游完成进入下一幕。
/// </summary>
public enum ActStage
{
    Explore,    // 地图探索（一边探索一边凑拼图碎片）
    Puzzle,     // 拼图
    Rhythm      // 音游
}

/// <summary>单幕的通关记录</summary>
[Serializable]
public class ChapterProgress
{
    public bool puzzleCleared;  // 本幕拼图是否已过
    public bool rhythmCleared;  // 本幕音游是否已过

    // 本幕已收集的拼图碎片编号，按收集先后排列（编号 = PuzzleChapterConfig.pieces 的列表序号）
    // 旧档反序列化后可能为 null，读取后要调 EnsureCollectedOrder() 补空
    public List<int> collectedPieceOrder;

    /// <summary>整幕是否已通关（拼图 + 音游都过了）</summary>
    public bool IsCleared
    {
        get { return puzzleCleared && rhythmCleared; }
    }

    /// <summary>保证收集列表存在（反序列化后要调一次）</summary>
    public void EnsureCollectedOrder()
    {
        if (collectedPieceOrder == null)
        {
            collectedPieceOrder = new List<int>();
        }
    }

    /// <summary>是否已收集指定编号的碎片</summary>
    public bool HasCollectedPiece(int pieceIndex)
    {
        EnsureCollectedOrder();
        return collectedPieceOrder.Contains(pieceIndex);
    }

    /// <summary>追加收集一块碎片（按收集顺序排在末尾）；重复收集返回 false</summary>
    public bool AddCollectedPiece(int pieceIndex)
    {
        EnsureCollectedOrder();

        if (collectedPieceOrder.Contains(pieceIndex))
        {
            return false;
        }

        collectedPieceOrder.Add(pieceIndex);
        return true;
    }

    public void Reset()
    {
        puzzleCleared = false;
        rhythmCleared = false;
        EnsureCollectedOrder();
        collectedPieceOrder.Clear();
    }
}

/// <summary>
/// 落盘的存档数据，JsonUtility 直接序列化这个对象。
/// 想加要存的东西（比如音游分数）往里加 public 字段即可。
/// </summary>
[Serializable]
public class ProgressSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;    // 存档版本，改结构时 +1，旧档自动作废
    public bool hasSave;                    // 是否存在有效存档（决定主菜单要不要显示"继续游戏"）
    public GamePhase phase = GamePhase.None;
    public int actIndex = -1;               // 当前第几幕，0 起；不在幕内时为 -1
    public ActStage stage = ActStage.Explore;
    public ChapterProgress[] chapters;      // 每一幕的通关记录，长度 = GameRules.ActCount
    public long savedAtBinary;              // 存档时间（DateTime.ToBinary）
    public string savedAtText;              // 存档时间（人能看的，方便调试）

    /// <summary>保证 chapters 数组存在且长度正确（反序列化后一定要调一次）</summary>
    public void EnsureChapters()
    {
        if (chapters == null)
        {
            chapters = new ChapterProgress[GameRules.ActCount];
        }

        if (chapters.Length != GameRules.ActCount)
        {
            Array.Resize(ref chapters, GameRules.ActCount);
        }

        for (int i = 0; i < chapters.Length; i++)
        {
            if (chapters[i] == null)
            {
                chapters[i] = new ChapterProgress();
            }

            chapters[i].EnsureCollectedOrder();
        }
    }

    /// <summary>清空成"全新游戏"的状态（不写盘）</summary>
    public void Clear()
    {
        hasSave = false;
        phase = GamePhase.None;
        actIndex = -1;
        stage = ActStage.Explore;
        savedAtBinary = 0;
        savedAtText = string.Empty;

        EnsureChapters();

        for (int i = 0; i < chapters.Length; i++)
        {
            chapters[i].Reset();
        }
    }

    /// <summary>调试用：把当前进度描述成一句话</summary>
    public override string ToString()
    {
        if (!hasSave)
        {
            return "无存档";
        }

        switch (phase)
        {
            case GamePhase.Prologue:
                return "开场剧情";
            case GamePhase.Ending:
                return "结尾";
            case GamePhase.Act:
                return string.Format("第{0}幕-{1}", actIndex + 1, StageName(stage));
            default:
                return "未开始";
        }
    }

    public static string StageName(ActStage stage)
    {
        switch (stage)
        {
            case ActStage.Explore: return "探索";
            case ActStage.Puzzle: return "拼图";
            case ActStage.Rhythm: return "音游";
            default: return stage.ToString();
        }
    }
}
