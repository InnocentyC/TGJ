using UnityEngine;

/// <summary>
/// 挂在"拼图完成确认弹窗"预制体根节点上（替代/伴随 PopupPanel）。
/// 确认弹窗是独立预制体，按钮的 OnClick 没法直接拖到主拼图弹窗里的 PuzzleCompletionFlow，
/// 所以由本脚本向上查找（GetComponentInParent）确认弹窗实例的父级里的 PuzzleCompletionFlow
/// 来执行"进入音游"。
///
/// 按钮绑定：OnClick → PuzzleCompleteConfirmPopup.Confirm
/// </summary>
public class PuzzleCompleteConfirmPopup : PopupPanel
{
    /// <summary>确认弹窗"进入音游"按钮绑这个</summary>
    public void Confirm()
    {
        PuzzleCompletionFlow flow = GetComponentInParent<PuzzleCompletionFlow>();

        if (flow != null)
        {
            flow.ConfirmToRhythm();
        }
        else
        {
            Debug.LogWarning("[PuzzleCompleteConfirmPopup] 父级里找不到 PuzzleCompletionFlow，无法跳转音游");
        }
    }

    /// <summary>关闭按钮绑这个：只关弹窗，不推进流程</summary>
    public void Close()
    {
        Hide();
    }
}
