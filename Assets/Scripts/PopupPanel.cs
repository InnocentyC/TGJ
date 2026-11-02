using UnityEngine;

// 弹窗面板的状态：已隐藏 / 正在弹出 / 已显示 / 正在隐藏
public enum PopupState
{
    Hidden,   // 已隐藏，物体处于 SetActive(false)
    Showing,  // 正在弹出（播放出现动画的阶段）
    Shown,    // 已完全显示，可以交互
    Hiding    // 正在隐藏（播放消失动画的阶段）
}

/// <summary>
/// 挂在 Puzzle / Settings 两个弹窗预制体的根节点上，负责维护"弹出 / 隐藏"状态。
/// </summary>
public class PopupPanel : MonoBehaviour
{
    // 当前状态，外部只读
    public PopupState State { get; private set; } = PopupState.Hidden;

    // 是否处于显示中（正在弹出 或 已显示 都算）
    public bool IsShowing
    {
        get { return State == PopupState.Showing || State == PopupState.Shown; }
    }

    // 弹出面板（子类可重写，用于打开前刷新界面）
    public virtual void Show()
    {
        // 先标记正在弹出：这里可以放淡入/缩放动画，动画结束后再置为 Shown
        State = PopupState.Showing;
        gameObject.SetActive(true);
        State = PopupState.Shown;
    }

    // 隐藏面板（子类可重写，用于关闭时保存数据）
    public virtual void Hide()
    {
        // 先标记正在隐藏：这里可以放淡出动画，动画结束后再置为 Hidden
        State = PopupState.Hiding;
        gameObject.SetActive(false);
        State = PopupState.Hidden;
    }

    // 开关：显示中则隐藏，已隐藏则弹出
    public virtual void Toggle()
    {
        if (IsShowing)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }
}
