using UnityEngine;

/// <summary>
/// 流程 -> 场景的映射表。所有场景名只在这里配一处，
/// GameProgressManager 靠它决定"当前该进哪个场景"。
///
/// 用法：Project 窗口右键 Create > Settings > GameFlowConfig，
/// 保存为 Assets/Resources/GameFlowConfig.asset（放在 Resources 下才会被自动加载；
/// 不放也行，会用代码里的默认值，或者在场景里把资产拖给 GameProgressManager 的 configOverride）。
/// </summary>
[CreateAssetMenu(fileName = "GameFlowConfig", menuName = "Settings/GameFlowConfig", order = 2)]
public class GameFlowConfig : ScriptableObject
{
    /// <summary>一幕对应的三个场景</summary>
    [System.Serializable]
    public class ActSceneSet
    {
        [Tooltip("章节选择界面上显示的名字，留空就用\"第N幕\"")]
        public string displayName = "";

        [Tooltip("地图探索场景")]
        public string exploreScene = "WalkingScene";

        [Tooltip("拼图场景；留空表示拼图就在探索场景里弹窗完成，不切场景")]
        public string puzzleScene = "";

        [Tooltip("音游场景；留空表示音游还没做，走到这步只打日志不切场景")]
        public string rhythmScene = "";
    }

    [Header("全局场景")]
    [Tooltip("开始界面")]
    public string startMenuScene = "StartScene";

    [Tooltip("开场剧情场景，格式没定，先留个坑")]
    public string prologueScene = "OpeningScene";

    [Tooltip("结尾场景，还没想好，留空即可；为空时走到结尾不会切场景")]
    public string endingScene = "";

    [Header("三幕")]
    public ActSceneSet[] acts = new ActSceneSet[]
    {
        new ActSceneSet { displayName = "第一幕" },
        new ActSceneSet { displayName = "第二幕" },
        new ActSceneSet { displayName = "第三幕" }
    };

    /// <summary>幕数</summary>
    public int ActCount
    {
        get { return acts == null ? 0 : acts.Length; }
    }

    /// <summary>保证 acts 长度正确、元素非空（加载后必须调一次）</summary>
    public void EnsureValid()
    {
        if (acts == null)
        {
            acts = new ActSceneSet[GameRules.ActCount];
        }

        if (acts.Length != GameRules.ActCount)
        {
            System.Array.Resize(ref acts, GameRules.ActCount);
        }

        for (int i = 0; i < acts.Length; i++)
        {
            if (acts[i] == null)
            {
                acts[i] = new ActSceneSet();
            }
        }
    }

    /// <summary>某一幕某个小阶段对应哪个场景；没配就返回空字符串</summary>
    public string GetStageScene(int actIndex, ActStage stage)
    {
        if (actIndex < 0 || acts == null || actIndex >= acts.Length)
        {
            return "";
        }

        ActSceneSet set = acts[actIndex];

        if (set == null)
        {
            return "";
        }

        switch (stage)
        {
            case ActStage.Puzzle:
                // 拼图没单独场景就复用探索场景（拼图是探索场景里的弹窗）
                return string.IsNullOrEmpty(set.puzzleScene) ? set.exploreScene : set.puzzleScene;

            case ActStage.Rhythm:
                return set.rhythmScene;

            default:
                return set.exploreScene;
        }
    }

    /// <summary>大阶段（开场/结尾/主菜单）对应哪个场景</summary>
    public string GetPhaseScene(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Prologue: return prologueScene;
            case GamePhase.Ending: return endingScene;
            default: return startMenuScene;
        }
    }

    /// <summary>章节显示名</summary>
    public string GetChapterName(int actIndex)
    {
        if (actIndex < 0 || acts == null || actIndex >= acts.Length || acts[actIndex] == null)
        {
            return "";
        }

        string name = acts[actIndex].displayName;
        return string.IsNullOrEmpty(name) ? string.Format("第{0}幕", actIndex + 1) : name;
    }

    /// <summary>场景名是否可用（配了且在 Build Settings 里）</summary>
    public static bool IsSceneAvailable(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }
}
