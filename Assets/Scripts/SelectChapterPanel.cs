using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 章节选择面板。挂到 SelectChapterPrefab 的根节点上，
/// 在 Inspector 里把 1 个返回按钮、3 个章节按钮拖进来，并填好各自要跳转的场景名。
/// 按钮的 OnClick 由脚本在 Awake 里自动绑定，不用再手动配事件。
/// </summary>
public class SelectChapterPanel : MonoBehaviour
{
    [Header("按钮")]
    [Tooltip("返回按钮：点击后隐藏本面板")]
    public Button backButton;

    [Tooltip("三个章节按钮，顺序对应下面的场景名")]
    public Button[] chapterButtons = new Button[3];

    [Header("章节场景")]
    [Tooltip("与 chapterButtons 一一对应的场景名，需要先加入 Build Settings")]
    public string[] chapterSceneNames = new string[3];

    private bool isLoading;   // 防止一次点击重复触发 LoadScene

    private void Awake()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(HidePanel);
        }

        if (chapterButtons == null)
        {
            return;
        }

        for (int i = 0; i < chapterButtons.Length; i++)
        {
            int index = i;   // 闭包必须用副本，否则三个按钮都会跳最后一章
            Button button = chapterButtons[i];

            if (button != null)
            {
                button.onClick.AddListener(() => SelectChapter(index));
            }
        }
    }

    /// <summary>返回按钮：隐藏面板（不销毁，下次还能再显示）</summary>
    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    /// <summary>章节按钮：跳转到第 index 章对应的场景（index 从 0 起）</summary>
    public void SelectChapter(int index)
    {
        if (isLoading)
        {
            return;
        }

        string sceneName = (chapterSceneNames != null && index >= 0 && index < chapterSceneNames.Length)
            ? chapterSceneNames[index]
            : null;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SelectChapterPanel] 第 " + (index + 1) + " 章没配场景名，请在 Inspector 里填 chapterSceneNames");
            return;
        }

        isLoading = true;
        SceneManager.LoadScene(sceneName);
    }
}
