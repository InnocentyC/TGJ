using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rhythm
{
    /// <summary>
    /// 一章的音游配置：谱面 + 音乐 + 打击音队列（数量每章随意）。
    /// 在 Inspector 的 chapters 列表里加几项就有几章。
    /// </summary>
    [System.Serializable]
    public class ChapterSetup
    {
        [Tooltip("本章谱面路径（Resources 下，不含扩展名）。留空则按章号自动取 Chart/Chart_Act{章号}")]
        public string chartResourcePath = "";

        [Tooltip("本章音乐。不填则无音乐，用系统时钟计时（测试用）")]
        public AudioClip musicClip;

        [Tooltip("本章打击音队列：按音高编号顺序排（谱面第三列数字 = 第几项），点 + 号加到需要的数量。不填用内置合成音色")]
        public List<AudioClip> keySoundClips = new List<AudioClip>();
    }

    /// <summary>
    /// 音游总控：状态机 Ready → Playing → Result，串起谱面/时间/池/判定/计分/UI。
    ///
    /// 使用方式：把本脚本挂到美术搭好的音游场景物体上，拖入 UI 引用（分数/连击文本、
    /// 判定弹出图）和音符预制体，填好判定线/出现点/轨道坐标，再配歌曲 AudioClip 即可。
    /// 所有数值可在 Inspector 调，玩家侧设置在 GameSettings。
    /// </summary>
    public class RhythmGameManager : MonoBehaviour
    {
        private enum GameState { Ready, Playing, Result }

        [Header("章节配置（每章各自的谱面/音乐/打击音，运行时按当前幕号自动选）")]
        [Tooltip("四章就加四项：第 1 幕用第 1 项，第 2 幕用第 2 项……打击音数量每章随意")]
        public List<ChapterSetup> chapters = new List<ChapterSetup>();

        [Tooltip("不在幕内（直接打开音游场景测试）时，使用第几个章节配置（0 起）")]
        public int sceneTestChapterIndex = 0;

        [Header("场景引用（美术搭好的场景里拖进来）")]
        [Tooltip("分数文本：旧版 Text 和 TMP 二选一拖即可，都拖了以 TMP 为准")]
        public Text scoreText;

        [Tooltip("连击文本（旧版 Text）")]
        public Text comboText;

        [Tooltip("分数文本（TextMeshPro 版）")]
        public TMPro.TMP_Text scoreTextTmp;

        [Tooltip("连击文本（TextMeshPro 版）")]
        public TMPro.TMP_Text comboTextTmp;

        // ===== 判定弹出图功能暂时关闭（策划要求去掉实时判定弹出）=====
        /*
        [Tooltip("判定弹出图：Perfect/Great/Miss 时换图并淡出")]
        public Image judgementImage;
        */

        // ===== 判定弹出图功能暂时关闭 =====
        /*
        [Tooltip("判定弹出图的位置标记：空物体摆在屏幕上要显示的位置，运行时把图移过去")]
        public Transform judgePopupPoint;

        [Tooltip("判定图保持显示时长（秒），之后才开始淡出")]
        public float judgePopupHoldTime = 0.35f;
        */

        [Tooltip("音符预制体（带 Image；可再挂 NoteController，没有会自动补）。不填用白色块")]
        public GameObject notePrefab;

        [Tooltip("音符的父节点（一般是美术 Canvas 下的容器）。不填挂在本物体下")]
        public Transform noteParent;

        [Header("演奏区标记点（场景里摆空物体拖进来，坐标自动换算）")]
        [Tooltip("放在判定线位置")]
        public Transform judgeLinePoint;

        [Tooltip("放在音符出现点位置")]
        public Transform spawnPoint;

        [Tooltip("7 个标记，分别放在各轨道中心线上，按轨道 0~6 顺序拖，7 个都要拖")]
        public Transform[] lanePoints;

        // ===== 判定弹出图功能暂时关闭 =====
        /*
        [Header("判定弹出图（三档，Inspector 里拖 sprite）")]
        public Sprite perfectSprite;
        public Sprite greatSprite;
        public Sprite missSprite;
        */

        [Header("判定窗口（毫秒，±）")]
        public float perfectWindowMs = 50f;
        public float greatWindowMs = 100f;
        public float missWindowMs = 150f;

        [Header("对象池")]
        [Tooltip("预热的音符数量")]
        public int poolPreloadCount = 20;

        [Tooltip("池耗尽时是否自动扩容")]
        public bool poolCanExpand = true;

        [Header("结算")]
        [Tooltip("音乐播完后多久进入结算（秒）。没配音乐时退回：最后一个音符判定完多久后结算")]
        public float endBufferSeconds = 2f;

        [Tooltip("结算后自动通知主线流程的延迟（秒；等结算 Panel 做好后改为按钮触发）")]
        public float resultAdvanceDelay = 3f;

        [Tooltip("评级阈值：S/A/B/C 的最低准确率，低于 C 的为 D。策划定稿后改这里")]
        [Range(0f, 1f)]
        public float[] rankThresholds = { 0.95f, 0.9f, 0.8f, 0.7f };

        [Header("开局")]
        [Tooltip("音乐起播前的准备时间（秒）")]
        public float startDelaySeconds = 2f;

        [Header("性能")]
        [Tooltip("目标帧率。0 = 用系统默认（一般 60）。调高配合高刷屏更丝滑")]
        public int targetFrameRate = 120;

        [Tooltip("音符是否用独立 Canvas 渲染（默认开）。独立后每帧移动音符只重建音符那一小块，不影响美术大 Canvas，能明显减少 UI 卡顿")]
        public bool notesUseOwnCanvas = true;

        private GameState state = GameState.Ready;

        private List<NoteData> notes = new List<NoteData>();
        private List<NoteData> activeNotes = new List<NoteData>();
        private int spawnIndex;

        private RhythmConductor conductor;
        private RhythmSceneUI ui;
        private NotePool pool;
        private JudgementManager judgement;
        private ScoreManager score;

        private AudioSource musicSource;
        private AudioSource keySoundSource;

        private float fallSpeed;          // 单位/秒（Canvas 局部坐标下）
        private double lastHitTime;       // 最后一个音符的时刻
        private ChapterSetup currentSetup; // 本局用的章节配置

        private void Start()
        {
            // 帧率：解开垂直同步限制，冲目标帧率（丝滑的关键之一）
            if (targetFrameRate > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = targetFrameRate;
            }

            LoadChapter();
            BuildComponents();

            score.TotalNotes = notes.Count;

            if (notes.Count > 0)
            {
                lastHitTime = notes[notes.Count - 1].hitTime;
            }
        }

        private void Update()
        {
            switch (state)
            {
                case GameState.Ready:
                    startDelaySeconds -= Time.deltaTime;

                    if (startDelaySeconds <= 0f)
                    {
                        conductor.Begin(0.3f);
                        state = GameState.Playing;
                    }

                    break;

                case GameState.Playing:
                    TickPlaying();
                    break;

                case GameState.Result:
                    resultAdvanceDelay -= Time.deltaTime;

                    if (resultAdvanceDelay <= 0f)
                    {
                        enabled = false; // 停止 Update，等主线切场景
                        GameProgressManager.Instance.NotifyRhythmCompleted();
                    }

                    break;
            }
        }

        // ---------------- 初始化 ----------------

        /// <summary>
        /// 按当前幕号选中章节配置并加载谱面。
        /// 第 1 幕用 chapters[0]，第 2 幕用 chapters[1]……不在幕内（直接开场景测试）时用 sceneTestChapterIndex。
        /// </summary>
        private void LoadChapter()
        {
            if (chapters == null || chapters.Count == 0)
            {
                Debug.LogError("[RhythmGameManager] 没有配置任何章节（Inspector 的 chapters 列表），无法开始音游");
                enabled = false;
                return;
            }

            int act = GameProgressManager.Instance.ActIndex;
            int index = act >= 0 ? act : sceneTestChapterIndex;
            index = Mathf.Clamp(index, 0, chapters.Count - 1);
            currentSetup = chapters[index];

            Debug.Log("[RhythmGameManager] 当前章节：第 " + (index + 1) + " 章" +
                (act >= 0 ? "（第 " + (act + 1) + " 幕）" : "（场景测试模式）"));

            // 谱面：本章没填路径就按章号自动取 Chart/Chart_Act{章号}
            string path = currentSetup.chartResourcePath;

            if (string.IsNullOrEmpty(path))
            {
                path = "Chart/Chart_Act" + (index + 1);
            }

            notes = ChartLoader.Load(path);
        }

        private void BuildComponents()
        {
            // 音频源（音乐用当前章节的）
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = currentSetup != null ? currentSetup.musicClip : null;
            musicSource.playOnAwake = false;

            keySoundSource = gameObject.AddComponent<AudioSource>();
            keySoundSource.playOnAwake = false;

            // 各模块
            conductor = gameObject.AddComponent<RhythmConductor>();
            conductor.Setup(musicSource);

            ui = gameObject.AddComponent<RhythmSceneUI>();
            ui.scoreText = scoreText;
            ui.comboText = comboText;
            ui.scoreTextTmp = scoreTextTmp;
            ui.comboTextTmp = comboTextTmp;
            // ===== 判定弹出图功能暂时关闭 =====
            // ui.judgementImage = judgementImage;

            // 布局全部来自标记点：换算成 noteParent 的局部坐标
            if (judgeLinePoint != null)
            {
                ui.SetJudgeLineY(ToNoteParentLocal(judgeLinePoint).y);
            }
            else
            {
                Debug.LogError("[RhythmGameManager] 没拖判定线标记点 judgeLinePoint，音符落点会不对");
            }

            if (spawnPoint != null)
            {
                ui.SetSpawnY(ToNoteParentLocal(spawnPoint).y);
            }
            else
            {
                Debug.LogError("[RhythmGameManager] 没拖出现点标记点 spawnPoint，音符出现位置会不对");
            }

            if (lanePoints != null && lanePoints.Length == GameSettings.LaneCount)
            {
                float[] laneX = new float[GameSettings.LaneCount];

                for (int i = 0; i < GameSettings.LaneCount; i++)
                {
                    if (lanePoints[i] != null)
                    {
                        laneX[i] = ToNoteParentLocal(lanePoints[i]).x;
                    }
                    else
                    {
                        Debug.LogError("[RhythmGameManager] 轨道标记点第 " + i + " 个没拖，该轨道 X 按 0 处理");
                    }
                }

                ui.SetLaneXPositions(laneX);
            }
            else
            {
                Debug.LogError("[RhythmGameManager] 轨道标记点 lanePoints 必须拖满 7 个，现在是 " +
                    (lanePoints == null ? 0 : lanePoints.Length) + " 个");
            }

            // ===== 判定弹出图功能暂时关闭（策划要求去掉实时判定弹出）=====
            /*
            ui.judgePopupHoldTime = judgePopupHoldTime;

            // 判定弹出图：没手动拖就自动生成一个显示槽。
            // 挂在 noteParent（Canvas 内）下，避免被 Screen Space Canvas 盖住；位置用标记点换算
            if (judgementImage == null)
            {
                if (judgePopupPoint != null && noteParent != null)
                {
                    GameObject popupObj = new GameObject("JudgePopup", typeof(Image));
                    popupObj.transform.SetParent(noteParent, false);
                    popupObj.transform.localPosition = ToNoteParentLocal(judgePopupPoint);

                    Image img = popupObj.GetComponent<Image>();
                    img.raycastTarget = false;
                    judgementImage = img;

                    // 自动生成时按 sprite 原始像素尺寸显示（每张图各按各的尺寸）
                    ui.SetJudgeImageAutoSize(true);
                }
                else
                {
                    Debug.LogError("[RhythmGameManager] 判定图显示不出来：需要同时拖 judgePopupPoint（显示位置标记）和 noteParent（Canvas 下的音符容器）");
                }
            }
            else if (judgePopupPoint != null)
            {
                // 手动拖了图就只对齐位置，不动层级和大小
                judgementImage.rectTransform.position = judgePopupPoint.position;
            }

            // 必须在判定图确定之后（自动生成也算）再把三张 sprite 和 CanvasGroup 绑上去
            ui.judgementImage = judgementImage;
            ui.Setup(perfectSprite, greatSprite, missSprite);
            */

            judgement = new JudgementManager(GameSettings.LaneCount);
            score = new ScoreManager(rankThresholds);

            fallSpeed = (ui.SpawnYPosition - ui.JudgeLineYPosition) / Mathf.Max(0.3f, GameSettings.Instance.approachTime);

            // 对象池。音符放独立 Canvas 下：每帧移动音符时只重建音符那一小块，
            // 不会拖着美术大 Canvas 整体重新合批（UI 卡顿的大头就在这）
            Transform notesRoot = noteParent != null ? noteParent : transform;

            if (notesUseOwnCanvas && noteParent != null)
            {
                GameObject notesCanvasObj = new GameObject("NotesCanvas", typeof(Canvas));
                notesCanvasObj.transform.SetParent(noteParent, false);
                notesRoot = notesCanvasObj.transform;
            }

            pool = new NotePool(notesRoot, poolCanExpand, CreateNoteInstance, poolPreloadCount);
        }

        private NoteController CreateNoteInstance()
        {
            GameObject obj;

            if (notePrefab != null)
            {
                obj = Instantiate(notePrefab);
            }
            else
            {
                // 没配预制体就用白色块兜底，保证能跑
                obj = new GameObject("Note");
                Image img = obj.AddComponent<Image>();
                Texture2D white = Texture2D.whiteTexture;
                img.sprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height),
                    new Vector2(0.5f, 0.5f), white.width);
                img.color = new Color(1f, 0.95f, 0.75f);
                img.rectTransform.sizeDelta = new Vector2(ui.LaneWidth - 40f, 56f);
            }

            NoteController controller = obj.GetComponent<NoteController>();

            if (controller == null)
            {
                controller = obj.AddComponent<NoteController>();
            }

            return controller;
        }

        // ---------------- 主循环 ----------------

        private void TickPlaying()
        {
            double songTime = conductor.SongTime;

            HandleInput(songTime);
            SpawnNotes(songTime);
            UpdateNotePositions(songTime);
            ProcessAutoMiss(songTime);
            CheckFinish(songTime);
        }

        private void HandleInput(double songTime)
        {
            KeyCode[] laneKeys = GameSettings.Instance.laneKeys;

            for (int lane = 0; lane < GameSettings.LaneCount && lane < laneKeys.Length; lane++)
            {
                if (Input.GetKeyDown(laneKeys[lane]))
                {
                    JudgeResult judged;
                    NoteData note;

                    if (judgement.TryJudge(lane, songTime, GetWindows(), out judged, out note))
                    {
                        ApplyJudgeResult(note, judged);
                        PlayKeySound(note.pitch); // 命中：按谱面指定的音高放音
                    }
                    else
                    {
                        // 空按/按太早：不消耗音符，用 0 号音色兜底响一声
                        PlayKeySound(0);
                    }
                }
            }
        }

        private void SpawnNotes(double songTime)
        {
            float approachTime = Mathf.Max(0.3f, GameSettings.Instance.approachTime);

            while (spawnIndex < notes.Count && notes[spawnIndex].hitTime - songTime <= approachTime)
            {
                NoteData note = notes[spawnIndex++];
                NoteController controller = pool.Get();

                if (controller == null)
                {
                    Debug.LogError("[RhythmGameManager] 取音符失败，跳过：" + note.hitTime);
                    continue;
                }

                controller.Bind(note);
                controller.gameObject.SetActive(true);
                judgement.RegisterSpawned(note);
                activeNotes.Add(note);
                PositionNote(note, songTime);
            }
        }

        private void UpdateNotePositions(double songTime)
        {
            for (int i = activeNotes.Count - 1; i >= 0; i--)
            {
                PositionNote(activeNotes[i], songTime);
            }
        }

        private void PositionNote(NoteData note, double songTime)
        {
            if (note.controller == null)
            {
                return;
            }

            // 位置由时间公式直接算，不做累积移动：对齐音乐零误差
            float y = ui.JudgeLineYPosition + (float)(note.hitTime - songTime) * fallSpeed;
            note.controller.transform.localPosition = new Vector3(ui.GetLaneX(note.lane), y, 0f);
        }

        private void ProcessAutoMiss(double songTime)
        {
            List<NoteData> missed = judgement.AutoMissCheck(songTime, GetWindows());

            for (int i = 0; i < missed.Count; i++)
            {
                ApplyJudgeResult(missed[i], JudgeResult.Miss);
            }
        }

        private void ApplyJudgeResult(NoteData note, JudgeResult result)
        {
            activeNotes.Remove(note);

            if (note.controller != null)
            {
                pool.Release(note.controller);
            }

            score.Add(result);
            ui.SetScore(score.Score);
            ui.SetCombo(score.Combo);
            // ===== 判定弹出图功能暂时关闭（策划要求去掉实时判定弹出）=====
            // ui.FlashJudgement(result);
        }

        private void CheckFinish(double songTime)
        {
            // 结算时机：音乐播完后 endBufferSeconds 秒；没配音乐时退回"最后一个音符 + endBufferSeconds"
            bool finished;

            if (musicSource != null && musicSource.clip != null)
            {
                finished = conductor.RawTime > musicSource.clip.length + endBufferSeconds;
            }
            else
            {
                finished = songTime > lastHitTime + endBufferSeconds;
            }

            if (spawnIndex >= notes.Count && activeNotes.Count == 0 &&
                finished && state == GameState.Playing)
            {
                state = GameState.Result;
                // 音乐已自然播完，切场景时自动停止
                LogResult();
            }
        }

        private void LogResult()
        {
            Debug.Log(string.Format(
                "[Rhythm] 结算 —— 评级: {0} | Score: {1} | MaxCombo: {2} | Perfect: {3} | Great: {4} | Miss: {5} | Accuracy: {6:P1}",
                score.Rank, score.Score, score.MaxCombo,
                score.PerfectCount, score.GreatCount, score.MissCount, score.Accuracy));
        }

        // ---------------- 音效 ----------------

        private void PlayKeySound(int pitch)
        {
            AudioClip clip = GetPitchClip(pitch);

            if (clip != null)
            {
                GameSettings settings = GameSettings.Instance;
                keySoundSource.PlayOneShot(clip, settings.keySoundVolume * settings.masterVolume);
            }
        }

        /// <summary>按音高编号取打击音：优先本章的 Inspector 队列，没配/编号越界用内置合成音</summary>
        private AudioClip GetPitchClip(int pitch)
        {
            List<AudioClip> clips = currentSetup != null ? currentSetup.keySoundClips : null;

            if (clips != null && clips.Count > 0)
            {
                int index = Mathf.Clamp(pitch, 0, clips.Count - 1);

                if (index != pitch)
                {
                    Debug.LogWarning("[RhythmGameManager] 音高 " + pitch + " 超出本章打击音队列长度（" +
                        clips.Count + "），用最后一项代替");
                }

                if (clips[index] != null)
                {
                    return clips[index];
                }
            }

            // 队列没配（或对应项为空）时用内置合成音，按音高编号给不同音高，保证能跑
            return GetSynthClip(pitch);
        }

        private readonly List<AudioClip> synthClips = new List<AudioClip>();

        private AudioClip GetSynthClip(int pitch)
        {
            int index = Mathf.Max(0, pitch);

            // 惰性生成：用到第几个音高就生成到第几个
            while (synthClips.Count <= index)
            {
                synthClips.Add(SynthClip(synthClips.Count));
            }

            return synthClips[index];
        }

        private static AudioClip SynthClip(int pitch)
        {
            // 内置合成音：从 C5 起的半音阶，编号每 +1 升一个半音
            int sampleRate = 44100;
            int samples = sampleRate / 10; // 0.1 秒
            float freq = 523.25f * Mathf.Pow(2f, pitch / 12f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (float)i / samples; // 线性衰减
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.8f;
            }

            AudioClip clip = AudioClip.Create("KeySynth" + pitch, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private JudgementWindows GetWindows()
        {
            JudgementWindows w;
            w.perfectMs = perfectWindowMs;
            w.greatMs = greatWindowMs;
            w.missMs = missWindowMs;
            return w;
        }

        // 把场景里标记点的世界坐标换算成 noteParent 的局部坐标（音符定位用的坐标系）
        private Vector3 ToNoteParentLocal(Transform point)
        {
            Transform parent = noteParent != null ? noteParent : transform;
            return parent.InverseTransformPoint(point.position);
        }
    }
}
