using UnityEngine;

/// <summary>
/// WalkingScene 的界面按钮控制。挂到 Canvas（或任意物体）上，
/// PuzzleButton 的 OnClick 绑 OnPuzzleButtonClick()，SettingsButton 绑 OnSettingsButtonClick()。
/// 弹窗预制体是懒加载的：第一次点才 Instantiate，之后复用同一个实例。
/// </summary>
public class WalkingSceneUI : MonoBehaviour
{
    [Header("弹窗预制体")]
    public PopupPanel puzzlePopupPrefab;    // Puzzle 预制体（根节点上要有 PopupPanel 组件）
    public PopupPanel settingsPopupPrefab;  // Settings 预制体（根节点上要有 PopupPanel 组件）

    [Header("弹窗挂载位置")]
    public Transform popupParent;           // 留空则挂到本脚本所在物体下

    [Header("对话联动")]
    [Tooltip("场景里的对话系统；弹窗打开时自动暂停对话，全部关闭后恢复。留空则不联动")]
    public DialogueSystem dialogueSystem;

    private PopupPanel puzzlePopup;         // 已创建出来的 Puzzle 弹窗实例
    private PopupPanel settingsPopup;       // 已创建出来的 Settings 弹窗实例

    /// <summary>本次进场景后玩家是否主动打开过拼图（章末要不要强制弹拼图靠它判断）</summary>
    public bool HasOpenedPuzzle { get; private set; }

    private void Awake()
    {
        // 没指定挂载点时，默认挂在自己身上（一般是 Canvas）
        if (popupParent == null)
        {
            popupParent = transform;
        }
    }

    // 点击 Puzzle 按钮：切换 Puzzle 弹窗的显示/隐藏
    public void OnPuzzleButtonClick()
    {
        TogglePopup(ref puzzlePopup, puzzlePopupPrefab);

        if (puzzlePopup != null && puzzlePopup.IsShowing)
        {
            HasOpenedPuzzle = true;
        }
    }

    // 点击 Settings 按钮：切换 Settings 弹窗的显示/隐藏
    public void OnSettingsButtonClick()
    {
        TogglePopup(ref settingsPopup, settingsPopupPrefab);
    }

    // 外部主动关闭 Puzzle 弹窗（可绑到弹窗里的关闭按钮）
    public void ClosePuzzlePopup()
    {
        if (puzzlePopup != null)
        {
            puzzlePopup.Hide();
            RefreshDialoguePause();
        }
    }

    // 外部主动关闭 Settings 弹窗（可绑到弹窗里的关闭按钮）
    public void CloseSettingsPopup()
    {
        if (settingsPopup != null)
        {
            settingsPopup.Hide();
            RefreshDialoguePause();
        }
    }

    /// <summary>
    /// 强制打开 Puzzle 弹窗（章末对话播完但玩家没点过拼图时，由 DialogueSceneFlow 调用）。
    /// 算一次"打开过"，避免重复强弹。
    /// </summary>
    public void OpenPuzzlePopup()
    {
        if (puzzlePopupPrefab == null)
        {
            Debug.LogWarning("未指定 Puzzle 弹窗预制体");
            return;
        }

        if (puzzlePopup == null)
        {
            puzzlePopup = Instantiate(puzzlePopupPrefab, popupParent);
            puzzlePopup.Hide();
        }

        CloseSettingsPopup();

        if (!puzzlePopup.IsShowing)
        {
            puzzlePopup.Show();
        }

        HasOpenedPuzzle = true;
        RefreshDialoguePause();
    }

    // 切换指定弹窗：没有实例就先创建，同时保证同一时间只显示一个弹窗
    private void TogglePopup(ref PopupPanel instance, PopupPanel prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("未指定弹窗预制体");
            return;
        }

        // 第一次点击时才实例化，之后一直复用
        if (instance == null)
        {
            instance = Instantiate(prefab, popupParent);
            instance.Hide();
        }

        // 已经显示着就收起，否则先关掉其他弹窗再弹出自己（互斥）
        if (instance.IsShowing)
        {
            instance.Hide();
        }
        else
        {
            ClosePuzzlePopup();
            CloseSettingsPopup();
            instance.Show();
        }

        RefreshDialoguePause();
    }

    // 有任意弹窗开着就暂停对话，全部关完再恢复
    private void RefreshDialoguePause()
    {
        if (dialogueSystem == null)
        {
            return;
        }

        bool anyShowing = (puzzlePopup != null && puzzlePopup.IsShowing)
                          || (settingsPopup != null && settingsPopup.IsShowing);
        dialogueSystem.SetPaused(anyShowing);
    }
}
