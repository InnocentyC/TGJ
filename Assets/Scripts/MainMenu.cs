using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 开始界面的按钮逻辑。挂到 Canvas（或任意物体）上，
/// StartButton 的 OnClick 绑定 StartGame()，QuitButton 绑定 QuitGame()，
/// SelectChapters 按钮绑定 ShowSelectChaptersPanel()（或把按钮拖给 selectChaptersButton 自动绑定），
/// GameSettings 按钮绑定 ShowGameSettingsPanel()（或把按钮拖给 gameSettingsButton 自动绑定）。
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("点击 Start 后要跳转的下一个场景名")]
    public string nextSceneName = "GameScene";

    [Header("章节选择")]
    [Tooltip("SelectChapters 按钮；留空则在按钮的 OnClick 里手动绑定 ShowSelectChaptersPanel()")]
    public Button selectChaptersButton;

    [Tooltip("点击后要显示的面板：可拖场景里的 Panel，也可直接拖 SelectChapterPrefab 预制体")]
    public GameObject selectChaptersPanel;

    [Header("游戏设置")]
    [Tooltip("GameSettings 按钮；留空则在按钮的 OnClick 里手动绑定 ShowGameSettingsPanel()")]
    public Button gameSettingsButton;

    [Tooltip("点击后要显示的设置面板：可拖场景里的 Panel，也可直接拖设置面板预制体")]
    public GameObject gameSettingsPanel;

    private void Awake()
    {
        if (selectChaptersPanel == null)
        {
            Debug.LogWarning("[MainMenu] 未指定章节选择面板 selectChaptersPanel");
        }
        else
        {
            // 拖的是 Project 里的预制体时，运行时才实例化到场景
            if (!selectChaptersPanel.scene.IsValid())
            {
                selectChaptersPanel = Instantiate(selectChaptersPanel);
            }

            // 面板默认先关掉，避免一进游戏就盖住主菜单
            selectChaptersPanel.SetActive(false);
        }

        if (gameSettingsPanel == null)
        {
            Debug.LogWarning("[MainMenu] 未指定设置面板 gameSettingsPanel");
        }
        else
        {
            // 拖的是 Project 里的预制体时，运行时才实例化到场景
            if (!gameSettingsPanel.scene.IsValid())
            {
                gameSettingsPanel = Instantiate(gameSettingsPanel);
            }

            // 面板默认先关掉，避免一进游戏就盖住主菜单
            gameSettingsPanel.SetActive(false);
        }

        // 拖了按钮就自动挂事件；OnClick 里手动绑过也不冲突（Show 是幂等的）
        if (selectChaptersButton != null)
        {
            selectChaptersButton.onClick.AddListener(ShowSelectChaptersPanel);
        }

        if (gameSettingsButton != null)
        {
            gameSettingsButton.onClick.AddListener(ShowGameSettingsPanel);
        }
    }

    private void OnDestroy()
    {
        if (selectChaptersButton != null)
        {
            selectChaptersButton.onClick.RemoveListener(ShowSelectChaptersPanel);
        }

        if (gameSettingsButton != null)
        {
            gameSettingsButton.onClick.RemoveListener(ShowGameSettingsPanel);
        }
    }

    /// <summary>点击 SelectChapters 按钮：显示对应的章节选择面板</summary>
    public void ShowSelectChaptersPanel()
    {
        if (selectChaptersPanel == null)
        {
            Debug.LogWarning("[MainMenu] 未指定章节选择面板 selectChaptersPanel");
            return;
        }

        selectChaptersPanel.SetActive(true);
    }

    /// <summary>关闭章节选择面板（可绑到面板里的返回按钮）</summary>
    public void HideSelectChaptersPanel()
    {
        if (selectChaptersPanel != null)
        {
            selectChaptersPanel.SetActive(false);
        }
    }

    /// <summary>点击 GameSettings 按钮：显示对应的设置面板</summary>
    public void ShowGameSettingsPanel()
    {
        if (gameSettingsPanel == null)
        {
            Debug.LogWarning("[MainMenu] 未指定设置面板 gameSettingsPanel");
            return;
        }

        gameSettingsPanel.SetActive(true);
    }

    /// <summary>关闭设置面板（可绑到面板里的返回按钮）</summary>
    public void HideGameSettingsPanel()
    {
        if (gameSettingsPanel != null)
        {
            gameSettingsPanel.SetActive(false);
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene(nextSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
