using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings 弹窗面板。挂到 Settings 预制体的根节点上（它本身就是一个 PopupPanel）。
/// 只负责：把界面上的滑块/按钮改动写进 GameSettings，并把配置回显到界面。
/// 音量实际生效、音游判定偏移的使用逻辑留到后续实现。
/// </summary>
public class SettingsPanel : PopupPanel
{
    /// <summary>
    /// 一条键位设置行：一个按钮（点了就开始等按键）+ 一个文本（显示当前按键）。
    /// 若界面用 TextMeshPro，把 label 类型改成 TMP_Text 并相应改 RefreshKeyLabels 里的赋值。
    /// </summary>
    [System.Serializable]
    public class LaneKeyRow
    {
        public Button button;
        public Text label;
    }

    [Header("音量调节")]
    public Slider volumeSlider;              // 0~1，在 Unity 里设好 Min/Max

    [Header("音游延迟调节")]
    public Slider offsetSlider;              // 单位毫秒，范围在 Unity 里设（如 -200~200）
    public Text offsetValueText;             // 显示当前毫秒数

    [Header("键位调整")]
    public LaneKeyRow[] laneRows = new LaneKeyRow[GameSettings.LaneCount]; // 7 行，对应 asdfjkl

    [Header("按钮")]
    public Button resetButton;               // 恢复默认
    public Button closeButton;               // 关闭并保存

    private GameSettings settings;           // 配置文档引用
    private int listeningLane = -1;          // 当前正在等待按键的轨道，-1 表示没有

    private void Awake()
    {
        settings = GameSettings.Instance;

        // 滑块：值变化立即写入配置（真正的音量生效逻辑待实现）
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (offsetSlider != null)
        {
            offsetSlider.onValueChanged.AddListener(OnOffsetChanged);
        }

        // 键位按钮：每一行点击后进入"等待按键"状态
        for (int i = 0; i < laneRows.Length; i++)
        {
            int laneIndex = i; // 闭包需要副本，否则所有按钮捕获到同一个 i
            LaneKeyRow row = laneRows[i];

            if (row != null && row.button != null)
            {
                row.button.onClick.AddListener(() => StartListeningForKey(laneIndex));
            }
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
        }
    }

    // 打开面板时先把配置回显到界面
    public override void Show()
    {
        RefreshUI();
        base.Show();
    }

    // 每帧检测按键：只有在等待按键状态下才工作
    private void Update()
    {
        if (listeningLane < 0)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StopListeningForKey(); // 取消改键
            return;
        }

        if (Input.anyKeyDown)
        {
            // 遍历按键枚举，找出本次按下的是哪个键
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None)
                {
                    continue;
                }

                if (Input.GetKeyDown(key))
                {
                    AssignKey(listeningLane, key);
                    return;
                }
            }
        }
    }

    // 关闭面板时兜底保存一次，避免忘点保存丢失改动
    private void OnDisable()
    {
        if (settings != null)
        {
            settings.Save();
        }
    }

    // 音量滑块变化
    private void OnVolumeChanged(float value)
    {
        settings.masterVolume = value;
        // TODO：后续在这里把值应用到 AudioMixer / AudioListener.volume
    }

    // 延迟滑块变化
    private void OnOffsetChanged(float value)
    {
        settings.rhythmOffsetMs = value;

        if (offsetValueText != null)
        {
            offsetValueText.text = value.ToString("0") + " ms";
        }
        // TODO：音游开始时从 GameSettings.Instance.rhythmOffsetMs 读取并用于判定
    }

    // 开始等待按键：先把该行显示成提示文本
    private void StartListeningForKey(int lane)
    {
        listeningLane = lane;

        LaneKeyRow row = laneRows[lane];
        if (row != null && row.label != null)
        {
            row.label.text = "按任意键...";
        }
    }

    // 结束等待按键（取消或完成），恢复显示真实键位
    private void StopListeningForKey()
    {
        listeningLane = -1;
        RefreshKeyLabels();
    }

    // 写入新按键，并处理与别的轨道撞键的情况
    private void AssignKey(int lane, KeyCode key)
    {
        // 撞键则与原来的那一行互换，保证 7 个轨道按键互不重复
        for (int i = 0; i < settings.laneKeys.Length; i++)
        {
            if (i != lane && settings.laneKeys[i] == key)
            {
                settings.laneKeys[i] = settings.laneKeys[lane];
            }
        }

        settings.laneKeys[lane] = key;

        StopListeningForKey();
    }

    // 恢复默认设置并刷新界面
    private void OnResetClicked()
    {
        settings.ResetToDefault();
        RefreshUI();
    }

    // 关闭：保存后隐藏
    private void OnCloseClicked()
    {
        settings.Save();
        Hide();
    }

    // 把配置回显到所有控件
    private void RefreshUI()
    {
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(settings.masterVolume);
        }

        if (offsetSlider != null)
        {
            offsetSlider.SetValueWithoutNotify(settings.rhythmOffsetMs);
        }

        if (offsetValueText != null)
        {
            offsetValueText.text = settings.rhythmOffsetMs.ToString("0") + " ms";
        }

        RefreshKeyLabels();
    }

    // 刷新 7 行键位文本
    private void RefreshKeyLabels()
    {
        for (int i = 0; i < laneRows.Length; i++)
        {
            LaneKeyRow row = laneRows[i];

            if (row != null && row.label != null)
            {
                row.label.text = settings.GetLaneKeyName(i);
            }
        }
    }
}
