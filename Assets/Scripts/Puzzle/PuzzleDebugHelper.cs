using UnityEngine;

/// <summary>
/// 拼图调试助手（临时用，正式版可删）：挂在拼图弹窗预制体根节点上。
/// 运行后在 Inspector 里右键组件标题，用 Context Menu 模拟"对话中收集到碎片"，
/// 免去搭对话系统就能测拼图弹窗。
/// </summary>
public class PuzzleDebugHelper : MonoBehaviour
{
    [Tooltip("点一次'收集下一块'会收集的碎片编号起点（一般用默认 0 自动往后找）")]
    public int debugStartIndex = 0;

    /// <summary>收集第一块尚未收集的碎片</summary>
    [ContextMenu("收集下一块碎片")]
    private void CollectNextPiece()
    {
        if (!GameProgressManager.HasInstance)
        {
            Debug.LogWarning("[PuzzleDebug] 找不到 GameProgressManager");
            return;
        }

        GameProgressManager progress = GameProgressManager.Instance;
        int actIndex = progress.ActIndex;

        for (int i = debugStartIndex; i < GameRules.ActCount * 64; i++)
        {
            if (!progress.HasPiece(actIndex, i))
            {
                progress.CollectPiece(actIndex, i);
                return;
            }
        }
    }

    /// <summary>把本章拼图碎片全部收集齐（测"拼完"流程用）</summary>
    [ContextMenu("收集全部碎片")]
    private void CollectAllPieces()
    {
        PuzzlePopupController popup = GetComponent<PuzzlePopupController>();

        if (popup == null || popup.config == null || popup.config.pieces == null)
        {
            Debug.LogWarning("[PuzzleDebug] 弹窗上没有 PuzzlePopupController 或未配置章节 config");
            return;
        }

        if (!GameProgressManager.HasInstance)
        {
            Debug.LogWarning("[PuzzleDebug] 找不到 GameProgressManager");
            return;
        }

        GameProgressManager progress = GameProgressManager.Instance;
        int actIndex = progress.ActIndex;

        for (int i = 0; i < popup.config.pieces.Length; i++)
        {
            progress.CollectPiece(actIndex, i);
        }

        Debug.Log("[PuzzleDebug] 已收集第" + (actIndex + 1) + "幕全部 " + popup.config.pieces.Length + " 块碎片");
    }

    /// <summary>清空本章收集记录（重新测收集流程用）</summary>
    [ContextMenu("清空本章碎片记录")]
    private void ClearCollectedPieces()
    {
        if (!GameProgressManager.HasInstance)
        {
            return;
        }

        GameProgressManager progress = GameProgressManager.Instance;
        var data = progress.Data;
        var chapter = data.chapters[progress.ActIndex];

        chapter.EnsureCollectedOrder();
        chapter.collectedPieceOrder.Clear();
        progress.SaveProgress();
        Debug.Log("[PuzzleDebug] 已清空第" + (progress.ActIndex + 1) + "幕碎片记录");
    }
}
