using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

/// <summary>
/// 线性对话系统：逐字弹出 + 打字音效 + 点击/空格推进。没有分支选项。
///
/// 规则：
///   1. 一句话一个字一个字弹出来，弹字时播打字音效
///   2. 没弹完时点鼠标左键 / 按空格 -> 立刻显示整句
///   3. 已经显示完 -> 点鼠标左键 / 按空格 -> 下一句；最后一句之后再点就结束
///
/// 用法：
///   1. 挂到任意 GameObject 上
///   2. csvFile 拖进对话 CSV（不拖就按 resourcesPath 从 Resources 里加载）
///   3. contentText 拖进用来显示正文的 TextMeshPro，speakerText 可空
///   4. 打钩 playOnStart，或用代码调 StartDialogue(幕号)
///   5. 所有速度相关参数都能在 Inspector 里实时改，改完立即生效
/// </summary>
[AddComponentMenu("Dialogue/Dialogue System")]
public class DialogueSystem : MonoBehaviour
{
    [Header("数据源")]
    [Tooltip("对话 CSV 文件；为空时按下面的 Resources 路径加载")]
    public TextAsset csvFile;

    [Tooltip("csvFile 为空时的兜底路径（Resources 下，不带扩展名）")]
    public string resourcesPath = "Dialogue/dialogue";

    [Tooltip("CSV 第一列是表头（NPCName,SpeakerName,Dialogue,PuzzlePiece,SceneNumber）就打钩")]
    public bool hasHeaderRow = true;

    [Tooltip("CSV 分隔符，默认英文半角逗号。中文逗号「，」不是分隔符，内容里直接写就行，不用包引号")]
    public char delimiter = ',';

    [Header("CSV 列序号（从 0 开始）")]
    public int npcColumn = 0;
    public int speakerColumn = 1;
    public int contentColumn = 2;
    public int puzzleColumn = 3;
    public int sceneColumn = 4;

    [Header("播放")]
    [Tooltip("开局自动播放；关掉则由外部（如 DialogueSceneFlow）调 StartDialogue() 触发")]
    public bool playOnStart = true;

    [Tooltip("组件 Awake 时就解析 CSV（关掉则等第一次 StartDialogue 再解析）")]
    public bool loadOnAwake = true;

    [Header("显示（美术没给就先空着，不影响逻辑跑）")]
    [Tooltip("正文 TextMeshPro；不拖只会打警告，流程照跑")]
    public TMP_Text contentText;

    [Tooltip("说话人名 TextMeshPro；不拖就不显示说话人")]
    public TMP_Text speakerText;

    [Tooltip("对话 UI 根节点：开始时显示，结束时隐藏；不拖就不做显隐")]
    public GameObject dialogueRoot;

    [Header("速度（Inspector 里随时可调，立即生效）")]
    [Tooltip("每秒弹出多少个字，越大越快")]
    public float charsPerSecond = 25f;

    [Tooltip("遇到标点额外停顿的秒数，0 = 不停顿")]
    public float punctuationPause = 0.15f;

    [Tooltip("会额外停顿的标点；逐字比对，直接往里加字即可")]
    public string punctuationMarks = "，。！？、；：…,.!?;:";

    [Tooltip("用不受 timeScale 影响的时间（对话一般不受暂停/慢动作影响）")]
    public bool useUnscaledTime = true;

    [Header("打字音效")]
    [Tooltip("打字音效；为空就不出声")]
    public AudioClip typingSound;

    [Tooltip("播放用的 AudioSource；为空会自动取本物体上的，还没有就自动建一个")]
    public AudioSource audioSource;

    [Range(0f, 1f)]
    [Tooltip("打字音量")]
    public float typingVolume = 0.6f;

    [Tooltip("每弹出几个字播一次音效，1 = 每个字都播；字速很快时调大，避免糊成一片")]
    public int charsPerSound = 1;

    [Tooltip("音效音高随机范围，让每次敲击听起来不一样")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Tooltip("空格、标点这类空白/停顿处不播音效")]
    public bool silentOnPunctuation = true;

    [Header("输入")]
    [Tooltip("推进对话的按键，默认空格")]
    public KeyCode[] advanceKeys = new KeyCode[] { KeyCode.Space };

    [Tooltip("鼠标左键点击也能推进")]
    public bool acceptMouseClick = true;

    [Tooltip("点在 UI（按钮、面板等）上的点击不推进对话，防止点按钮时对话被翻页")]
    public bool ignoreClicksOverUI = true;

    [Tooltip("跳过/翻页后的输入冷却，防止一次点击连翻两句")]
    public float inputCooldown = 0.08f;

    [Header("文本")]
    [Tooltip("关掉后 <color> 之类的富文本标签会原样显示（逐字弹出时才不会被截断成乱码）")]
    public bool supportRichText = false;

    [Header("事件")]
    public UnityEvent onDialogueStart;
    public DialogueLineEvent onLineStart;
    public UnityEvent onDialogueEnd;

    // ---- 运行时状态 ----
    private List<DialogueLine> allLines = new List<DialogueLine>();
    private List<DialogueLine> playingLines = new List<DialogueLine>();
    private int currentIndex = -1;
    private bool isActive;
    private bool isTyping;
    private bool isPaused;
    private Coroutine typingCoroutine;
    private float cooldownTimer;
    private readonly StringBuilder typedBuilder = new StringBuilder();

    /// <summary>对话是否正在进行</summary>
    public bool IsActive
    {
        get { return isActive; }
    }

    /// <summary>当前这句话是否还在逐字弹出</summary>
    public bool IsTyping
    {
        get { return isTyping; }
    }

    /// <summary>对话是否被外部暂停（拼图/设置等面板打开时）。暂停时打字停住、输入忽略</summary>
    public bool IsPaused
    {
        get { return isPaused; }
    }

    /// <summary>当前播放到第几句（0 起，-1 表示还没开始）</summary>
    public int CurrentIndex
    {
        get { return currentIndex; }
    }

    /// <summary>本次播放总共有几句</summary>
    public int TotalCount
    {
        get { return playingLines == null ? 0 : playingLines.Count; }
    }

    /// <summary>当前这句的数据</summary>
    public DialogueLine CurrentLine
    {
        get
        {
            if (playingLines == null || currentIndex < 0 || currentIndex >= playingLines.Count)
            {
                return null;
            }

            return playingLines[currentIndex];
        }
    }

    /// <summary>C# 侧监听：每句开始时触发（Inspector 里挂回调用 onLineStart）</summary>
    public event Action<DialogueLine> LineStarted;

    /// <summary>C# 侧监听：整段对话结束</summary>
    public event Action DialogueEnded;

    // 每个字之间隔多少秒；速度可以在运行时随便改
    private float SecondsPerChar
    {
        get
        {
            if (charsPerSecond <= 0f)
            {
                return 0f;
            }

            return 1f / charsPerSecond;
        }
    }

    private float DeltaTime
    {
        get { return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime; }
    }

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (loadOnAwake)
        {
            LoadCsv();
        }
    }

    private void Start()
    {
        if (playOnStart)
        {
            StartDialogue();
        }
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= DeltaTime;
        }

        if (!isActive || isPaused || cooldownTimer > 0f)
        {
            return;
        }

        if (IsAdvanceInputDown())
        {
            AdvanceOrComplete();
        }
    }

    private void OnValidate()
    {
        charsPerSound = Mathf.Max(1, charsPerSound);

        if (pitchRange.x <= 0f)
        {
            pitchRange.x = 1f;
        }

        if (pitchRange.y <= 0f)
        {
            pitchRange.y = 1f;
        }
    }

    // ---------------- 对外接口 ----------------

    /// <summary>重新解析 CSV（CSV 改过之后调一下）</summary>
    public void LoadCsv()
    {
        TextAsset asset = csvFile;

        if (asset == null && !string.IsNullOrEmpty(resourcesPath))
        {
            asset = Resources.Load<TextAsset>(resourcesPath);

            if (asset == null)
            {
                Debug.LogWarning(string.Format("对话系统：找不到 CSV。检查 csvFile 或 Resources/{0}.csv", resourcesPath), this);
            }
        }

        allLines = asset != null
            ? DialogueCsvParser.Parse(asset.text, hasHeaderRow, delimiter, npcColumn, speakerColumn, contentColumn, puzzleColumn, sceneColumn)
            : new List<DialogueLine>();
    }

    /// <summary>
    /// 开始播放：按 CSV 顺序从头播到尾。场景切换/碎片发放由 DialogueSceneFlow 按每句的字段结算。
    /// </summary>
    public void StartDialogue()
    {
        if (allLines == null || allLines.Count == 0)
        {
            LoadCsv();
        }

        playingLines = new List<DialogueLine>(allLines);

        if (playingLines.Count == 0)
        {
            Debug.LogWarning("对话系统：CSV 里没有可播放的句子", this);
            return;
        }

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(true);
        }

        isActive = true;
        currentIndex = -1;

        if (onDialogueStart != null)
        {
            onDialogueStart.Invoke();
        }

        ShowLine(0);
    }

    /// <summary>跳到下一句；已经是最后一句就结束</summary>
    public void Next()
    {
        if (!isActive)
        {
            return;
        }

        int next = currentIndex + 1;

        if (next >= playingLines.Count)
        {
            EndDialogue();
            return;
        }

        ShowLine(next);
    }

    /// <summary>当前这句还没弹完时，直接整句显示出来</summary>
    public void CompleteCurrentLine()
    {
        if (!isTyping)
        {
            return;
        }

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;

        DialogueLine line = CurrentLine;

        if (contentText != null && line != null)
        {
            contentText.text = line.content;
        }

        cooldownTimer = inputCooldown;
    }

    /// <summary>结束整段对话</summary>
    public void Stop()
    {
        if (!isActive)
        {
            return;
        }

        EndDialogue();
    }

    /// <summary>暂停/恢复对话（面板开关时由外部调用）。暂停时打字和输入都停住，恢复后原样继续</summary>
    public void SetPaused(bool paused)
    {
        isPaused = paused;
    }

    /// <summary>运行时改速度：每秒多少字</summary>
    public void SetCharsPerSecond(float value)
    {
        charsPerSecond = value;
    }

    // ---------------- 内部逻辑 ----------------

    // 一次输入：没弹完就补全，弹完了就下一句
    private void AdvanceOrComplete()
    {
        if (isTyping)
        {
            CompleteCurrentLine();
            return;
        }

        cooldownTimer = inputCooldown;
        Next();
    }

    private bool IsAdvanceInputDown()
    {
        if (acceptMouseClick && Input.GetMouseButtonDown(0))
        {
            // 点在按钮/面板上：这次点击留给 UI，不推进对话
            bool overUI = ignoreClicksOverUI
                          && EventSystem.current != null
                          && EventSystem.current.IsPointerOverGameObject();

            if (!overUI)
            {
                return true;
            }
        }

        if (advanceKeys != null)
        {
            for (int i = 0; i < advanceKeys.Length; i++)
            {
                if (Input.GetKeyDown(advanceKeys[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ShowLine(int index)
    {
        currentIndex = index;

        DialogueLine line = playingLines[index];

        if (speakerText != null)
        {
            speakerText.text = line.speaker;
        }

        if (contentText != null)
        {
            contentText.richText = supportRichText;
            contentText.text = string.Empty;
        }

        if (onLineStart != null)
        {
            onLineStart.Invoke(line);
        }

        if (LineStarted != null)
        {
            LineStarted(line);
        }

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        typingCoroutine = StartCoroutine(TypeLineCoroutine(line));
    }

    // 逐字弹出：按时间累积，这样速度再快也不会被帧率限制
    private IEnumerator TypeLineCoroutine(DialogueLine line)
    {
        isTyping = true;

        string full = line != null ? line.content : string.Empty;

        typedBuilder.Length = 0;

        if (contentText != null)
        {
            contentText.text = string.Empty;
        }

        int index = 0;
        int sinceSound = 0;
        float wait = 0f;

        while (index < full.Length)
        {
            // 暂停时原地等，不消耗打字进度（用的是 unscaledTime，timeScale 管不到，必须自己停）
            if (isPaused)
            {
                yield return null;
                continue;
            }

            wait -= DeltaTime;

            bool soundPlayedThisFrame = false;

            while (wait <= 0f && index < full.Length)
            {
                char c = full[index];
                index++;

                typedBuilder.Append(c);
                wait += SecondsPerChar + GetPunctuationPause(c);

                // 音效：每 charsPerSound 个字一次，每帧最多一次
                if (silentOnPunctuation && IsSilentChar(c))
                {
                    continue;
                }

                sinceSound++;

                if (sinceSound >= charsPerSound && !soundPlayedThisFrame)
                {
                    sinceSound = 0;
                    soundPlayedThisFrame = true;
                    PlayTypingSound();
                }
            }

            if (contentText != null)
            {
                contentText.text = typedBuilder.ToString();
            }

            yield return null;
        }

        if (contentText != null)
        {
            contentText.text = full;
        }

        isTyping = false;
        typingCoroutine = null;
    }

    private float GetPunctuationPause(char c)
    {
        if (punctuationPause <= 0f || string.IsNullOrEmpty(punctuationMarks))
        {
            return 0f;
        }

        return punctuationMarks.IndexOf(c) >= 0 ? punctuationPause : 0f;
    }

    private bool IsSilentChar(char c)
    {
        return char.IsWhiteSpace(c) || (!string.IsNullOrEmpty(punctuationMarks) && punctuationMarks.IndexOf(c) >= 0);
    }

    private void PlayTypingSound()
    {
        if (typingSound == null)
        {
            return;
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        audioSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(typingSound, typingVolume);
    }

    private void EndDialogue()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isActive = false;
        isTyping = false;
        currentIndex = -1;

        if (dialogueRoot != null)
        {
            dialogueRoot.SetActive(false);
        }

        if (onDialogueEnd != null)
        {
            onDialogueEnd.Invoke();
        }

        if (DialogueEnded != null)
        {
            DialogueEnded();
        }
    }
}
