using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 拼图弹窗控制器：挂在每章拼图弹窗预制体根节点上（继承 PopupPanel 的显隐行为）。
///
/// Show 时：
///   1. 读当前章节（GameProgressManager.ActIndex）的碎片收集记录；
///   2. 按"收集顺序"把尚未吸附的碎片实例化到待摆放区（托盘），未收集的不生成；
///   3. 已吸附（锁定）的碎片保持挂在槽位上不动——关闭弹窗后摆放进度保留，
///      场景重载（含章节选择重进）后弹窗实例销毁重建，拼图自然从头开始。
///
/// 配套：托盘容器挂 LayoutGroup 自动排序；槽位预摆在 PuzzleManager 所在子树下。
/// </summary>
public class PuzzlePopupController : PopupPanel
{
    [Header("拼图配置")]
    [Tooltip("本章拼图配置（碎片列表、结果图、碎片模板）")]
    public PuzzleChapterConfig config;

    [Header("弹窗结构")]
    [Tooltip("待摆放区容器（挂 LayoutGroup 自动排序）；留空则找子物体里名字含 Tray 的节点")]
    public Transform trayRoot;

    [Tooltip("拼图管理器；留空则自动从子物体找")]
    public PuzzleManager puzzleManager;

    // 本弹窗生成过的碎片（含已锁定挂在槽位上的），用于二次打开时区分"保留 / 重新生成"
    private readonly List<CustomPuzzleImage> spawnedPieces = new List<CustomPuzzleImage>();

    private void Awake()
    {
        if (puzzleManager == null)
        {
            puzzleManager = GetComponentInChildren<PuzzleManager>(true);
        }

        if (trayRoot == null)
        {
            trayRoot = FindChildByNameContains(transform, "Tray");
        }
    }

    public override void Show()
    {
        // 拼图已经拼完：保持现状直接显示（结果图和点击切换还活着），不再重新生成
        if (puzzleManager != null && puzzleManager.IsPuzzleCompleted)
        {
            base.Show();
            return;
        }

        PreparePuzzle();
        base.Show();
    }

    public override void Hide()
    {
        // 不做任何复位：已吸附的碎片留在槽位、托盘里的碎片留在托盘，下次打开接着拼
        base.Hide();
    }

    // 按背包数据准备拼图：注入配置、清理未吸附的旧碎片、按收集顺序补生成
    private void PreparePuzzle()
    {
        if (config == null)
        {
            Debug.LogWarning("[PuzzlePopup] 未配置 PuzzleChapterConfig");
            return;
        }

        if (puzzleManager == null)
        {
            Debug.LogWarning("[PuzzlePopup] 找不到 PuzzleManager");
            return;
        }

        if (trayRoot == null)
        {
            Debug.LogWarning("[PuzzlePopup] 找不到待摆放区容器 trayRoot");
            return;
        }

        // 注入本章配置（总块数、结果图、收集槽位、清空碎片注册表）
        puzzleManager.Setup(config);

        // 当前章节与收集顺序（弹窗所在探索场景对应一章，取的就是当前进度里的章）
        List<int> collected = new List<int>();
        if (GameProgressManager.HasInstance)
        {
            int actIndex = GameProgressManager.Instance.ActIndex;
            IReadOnlyList<int> order = GameProgressManager.Instance.GetCollectedPieces(actIndex);
            collected.AddRange(order);
        }
        else
        {
            Debug.LogWarning("[PuzzlePopup] 找不到 GameProgressManager，按空背包处理");
        }

        // 先清掉托盘里未吸附的旧碎片（它们要按最新收集顺序重排）；
        // 已锁定（吸附在槽位上）的保留不动
        for (int i = spawnedPieces.Count - 1; i >= 0; i--)
        {
            CustomPuzzleImage piece = spawnedPieces[i];

            if (piece == null)
            {
                spawnedPieces.RemoveAt(i);
                continue;
            }

            if (piece.IsLocked)
            {
                continue;
            }

            puzzleManager.UnregisterPiece(piece);
            Destroy(piece.gameObject);
            spawnedPieces.RemoveAt(i);
        }

        if (config.pieceTemplate == null)
        {
            Debug.LogWarning("[PuzzlePopup] 配置里没有碎片预制体模板 pieceTemplate");
            return;
        }

        // 按收集顺序补生成尚未吸附的碎片
        foreach (int pieceIndex in collected)
        {
            // 该编号的碎片已经吸附在槽位上：跳过，避免同一块出现两份
            if (FindLockedPiece(pieceIndex) != null)
            {
                continue;
            }

            // 配置越界（存档里的编号配置里没有）：跳过并提示
            if (pieceIndex < 0 || pieceIndex >= config.pieces.Length || config.pieces[pieceIndex] == null)
            {
                Debug.LogWarning("[PuzzlePopup] 收集记录里的碎片编号 #" + pieceIndex + " 在配置中不存在，跳过");
                continue;
            }

            CustomPuzzleImage piece = Instantiate(config.pieceTemplate, trayRoot)
                .GetComponent<CustomPuzzleImage>();

            if (piece == null)
            {
                Debug.LogError("[PuzzlePopup] 碎片预制体模板根节点上没有 CustomPuzzleImage 组件");
                return;
            }

            piece.Setup(puzzleManager, pieceIndex, pieceIndex, config.pieces[pieceIndex].image);
            puzzleManager.RegisterPiece(piece);
            spawnedPieces.Add(piece);
        }
    }

    // 在已生成的碎片里找指定编号且已锁定的那块
    private CustomPuzzleImage FindLockedPiece(int pieceIndex)
    {
        foreach (CustomPuzzleImage piece in spawnedPieces)
        {
            if (piece != null && piece.IsLocked && piece.pieceIndex == pieceIndex)
            {
                return piece;
            }
        }

        return null;
    }

    // 在子层级里找名字包含关键字的第一个节点
    private static Transform FindChildByNameContains(Transform parent, string keyword)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Contains(keyword))
            {
                return child;
            }

            Transform found = FindChildByNameContains(child, keyword);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
