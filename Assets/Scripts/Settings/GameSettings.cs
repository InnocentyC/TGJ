using System.IO;
using UnityEngine;

/// <summary>
/// 游戏配置文档：所有可调设置的唯一数据源。
/// 音游、音量等系统后续统一从这里读取，不要在别处各存一份。
///
/// 使用方式：
///   1. 读取：GameSettings.Instance.masterVolume
///   2. 存盘：GameSettings.Instance.Save()
///   3. 在 Project 窗口右键 Create > Settings > GameSettings 建立 .asset，
///      并放进任意 Resources 文件夹（可选，便于在 Inspector 里配默认值）。
///      没有 .asset 也能正常跑，会自动创建一份实例并从本地 JSON 载入。
/// </summary>
[CreateAssetMenu(fileName = "GameSettings", menuName = "Settings/GameSettings", order = 1)]
public class GameSettings : ScriptableObject
{
    // 音游轨道数量：默认键位 asdfjkl，共 7 轨
    public const int LaneCount = 7;

    // 本地存档文件名（存在 Application.persistentDataPath 下）
    private const string SaveFileName = "gameSettings.json";

    // 默认键位：A S D F J K L
    public static readonly KeyCode[] DefaultLaneKeys =
    {
        KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F,
        KeyCode.J, KeyCode.K, KeyCode.L
    };

    [Header("音频")]
    [Range(0f, 1f)]
    [Tooltip("游戏主音量，0~1")]
    public float masterVolume = 0.8f;

    [Header("音游")]
    [Tooltip("音游判定延迟，单位毫秒，正数=判定往后推，负数=判定提前")]
    public float rhythmOffsetMs = 0f;

    [Tooltip("音游各轨道的按键，长度固定为 LaneCount(7)")]
    public KeyCode[] laneKeys = (KeyCode[])DefaultLaneKeys.Clone();

    // 运行时单例
    private static GameSettings instance;

    /// <summary>
    /// 全局访问入口。首次访问时：优先读 Resources 里的 .asset，没有就新建实例，
    /// 然后从本地 JSON 覆盖加载（读不到就用代码里的默认值）。
    /// </summary>
    public static GameSettings Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<GameSettings>("GameSettings");

                if (instance == null)
                {
                    instance = CreateInstance<GameSettings>();
                }

                instance.Load();
                instance.EnsureLaneKeysValid();
            }

            return instance;
        }
    }

    // 本地存档完整路径
    private static string SavePath
    {
        get { return Path.Combine(Application.persistentDataPath, SaveFileName); }
    }

    // 把当前配置写入本地 JSON
    public void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(this, true);
            File.WriteAllText(SavePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("保存设置失败：" + e.Message);
        }
    }

    // 从本地 JSON 覆盖加载；文件不存在时保持默认值
    public void Load()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(SavePath), this);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("读取设置失败，使用默认设置：" + e.Message);
            ResetToDefault();
        }
    }

    // 恢复默认设置（键位回到 asdfjkl）
    public void ResetToDefault()
    {
        masterVolume = 0.8f;
        rhythmOffsetMs = 0f;
        laneKeys = (KeyCode[])DefaultLaneKeys.Clone();
    }

    // 获取某个轨道的按键名（用于界面显示）
    public string GetLaneKeyName(int lane)
    {
        if (laneKeys == null || lane < 0 || lane >= laneKeys.Length)
        {
            return "-";
        }

        return laneKeys[lane].ToString();
    }

    // 防御：存档里键位数量不对时（比如改过 LaneCount）重建为默认值
    private void EnsureLaneKeysValid()
    {
        if (laneKeys == null || laneKeys.Length != LaneCount)
        {
            laneKeys = (KeyCode[])DefaultLaneKeys.Clone();
        }
    }
}
