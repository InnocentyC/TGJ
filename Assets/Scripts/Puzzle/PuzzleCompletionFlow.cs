using UnityEngine;

/// <summary>
/// 拼图完成流程触发器：挂在拼图弹窗预制体内（PuzzleManager 附近）。
///
/// 职责：
///   1. 监听 PuzzleManager 的结果图淡入结束事件（onImageTransitionComplete），
///      弹出"拼图完成"确认弹窗（预制体由美术/用户在 Unity 里搭建，本脚本只负责触发与跳转）；
///   2. 确认弹窗上的"进入音游"按钮在 Inspector 的 OnClick 里绑 ConfirmToRhythm()，
///      点击后调 GameProgressManager.NotifyPuzzleCompleted()，由流程管理器切到本章音游场景。
///
/// 已通关章节重进重拼：照常弹出确认弹窗并跳音游（重复置 puzzleCleared 无副作用）。
/// </summary>
public class PuzzleCompletionFlow : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("拼图管理器；留空则自动从子物体找")]
    public PuzzleManager puzzleManager;

    [Tooltip("拼图完成确认弹窗预制体（根节点挂 PopupPanel，界面在 Unity 里搭）")]
    public PopupPanel completionPopupPrefab;

    [Tooltip("确认弹窗的挂载位置；留空则挂到本脚本所在物体下")]
    public Transform popupParent;

    private PopupPanel completionPopup;  // 已创建的确认弹窗实例（懒加载复用）

    private void Awake()
    {
        if (puzzleManager == null)
        {
            puzzleManager = GetComponentInChildren<PuzzleManager>(true);
        }

        if (popupParent == null)
        {
            popupParent = transform;
        }
    }

    private void OnEnable()
    {
        if (puzzleManager != null)
        {
            puzzleManager.onImageTransitionComplete.AddListener(HandleTransitionComplete);
        }
    }

    private void OnDisable()
    {
        if (puzzleManager != null)
        {
            puzzleManager.onImageTransitionComplete.RemoveListener(HandleTransitionComplete);
        }
    }

    // 结果图淡入播完：弹出确认弹窗（若没配预制体则打日志提示，方便搭 UI 前调试）
    private void HandleTransitionComplete()
    {
        if (completionPopupPrefab == null)
        {
            Debug.LogWarning("[PuzzleCompletionFlow] 未指定确认弹窗预制体 completionPopupPrefab");
            return;
        }

        ShowCompletionPopup();
    }

    /// <summary>显示确认弹窗（懒加载复用；也可由外部/按钮直接调用）</summary>
    public void ShowCompletionPopup()
    {
        if (completionPopupPrefab == null)
        {
            Debug.LogWarning("[PuzzleCompletionFlow] 未指定确认弹窗预制体 completionPopupPrefab");
            return;
        }

        if (completionPopup == null)
        {
            completionPopup = Instantiate(completionPopupPrefab, popupParent);
        }

        completionPopup.Show();
    }

    /// <summary>
    /// 确认弹窗"进入音游"按钮绑这个：通知流程管理器拼图完成，自动存档并切到本章音游场景。
    /// </summary>
    public void ConfirmToRhythm()
    {
        if (completionPopup != null)
        {
            completionPopup.Hide();
        }

        if (GameProgressManager.HasInstance)
        {
            GameProgressManager.Instance.NotifyPuzzleCompleted();
        }
        else
        {
            Debug.LogWarning("[PuzzleCompletionFlow] 找不到 GameProgressManager，无法推进流程");
        }
    }

    /// <summary>确认弹窗的关闭按钮可绑这个：只关弹窗，不推进流程</summary>
    public void CloseCompletionPopup()
    {
        if (completionPopup != null)
        {
            completionPopup.Hide();
        }
    }
}
