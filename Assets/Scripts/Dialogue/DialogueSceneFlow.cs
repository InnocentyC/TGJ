using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 对话场景流程控制器：一章一个，挂在场景里驱动整章的线性对话。
///
/// 流程：
///   1. 进场景自动铺 initialSceneNumber 的背景，然后让 DialogueSystem 从头开始播 CSV；
///   2. 每播"完"一句（玩家翻到下一句时，这句才算说完），按这句的字段结算：
///        PuzzlePiece  >= 0 -> 给玩家发这块碎片（写入 GameProgressManager 背包）
///        SceneNumber  >  0 -> 切换背景图到对应场景
///   3. 全部播完：
///        openPuzzleWhenFinished 且玩家本章没主动打开过拼图 -> 自动弹拼图
///        notifyPrologueFinishedWhenFinished -> 通知 GameProgressManager 序章结束（序章场景用）
///
/// 用法：
///   1. 挂到场景里任意 GameObject 上；
///   2. 拖入 dialogueSystem（它自己的 playOnStart 要关掉，由本脚本驱动）；
///   3. backgroundImage 拖全屏背景 Image，scenes 里按场景编号填背景图；
///   4. 章末要弹拼图就把 sceneUI 拖进来。
/// </summary>
[AddComponentMenu("Dialogue/Dialogue Scene Flow")]
public class DialogueSceneFlow : MonoBehaviour
{
    /// <summary>一个场景编号对应的表现：背景图 + 可选的附加物体（前景/道具层）</summary>
    [System.Serializable]
    public class SceneVisual
    {
        [Tooltip("场景编号，对应 CSV 的 SceneNumber 列")]
        public int sceneNumber = 1;

        [Tooltip("这个场景的背景图；留空则切到本场景时背景不变")]
        public Sprite background;

        [Tooltip("这个场景专属的前景/道具层（可选）；切走时自动隐藏")]
        public GameObject extraRoot;

        [Tooltip("叠加模式：触发时只显示本层、不隐藏其他层（终章角色逐个进场用）。关掉则是普通换背景模式")]
        public bool additive;
    }

    /// <summary>带 int 参数的 UnityEvent（碎片发放通知用，参数是 0 起的碎片下标）</summary>
    [System.Serializable]
    public class PieceEvent : UnityEvent<int>
    {
    }

    [Header("对话")]
    [Tooltip("场景里的对话系统；其 playOnStart 请关掉，由本脚本驱动")]
    public DialogueSystem dialogueSystem;

    [Header("场景表现")]
    [Tooltip("全屏背景 Image；留空则不做背景切换")]
    public Image backgroundImage;

    [Tooltip("开场先铺哪个场景的背景（对话里第一句带 SceneNumber 之前的部分都属于它）")]
    public int initialSceneNumber = 1;

    [Tooltip("场景编号 -> 背景/前景 的对照表")]
    public List<SceneVisual> scenes = new List<SceneVisual>();

    [Header("碎片")]
    [Tooltip("表里 PuzzlePiece 编号换算成 0 起下标的基数：表里从 1 开始编号就保持 1")]
    public int pieceNumberBase = 1;

    [Header("流程")]
    [Tooltip("进场景自动开始播对话；关掉则自己调 Begin()")]
    public bool playOnStart = true;

    [Tooltip("全部播完后，玩家没主动打开过拼图就自动弹拼图（章节场景用）")]
    public bool openPuzzleWhenFinished = true;

    [Tooltip("全部播完后通知 GameProgressManager 序章结束（序章场景打钩，章节场景别打）")]
    public bool notifyPrologueFinishedWhenFinished = false;

    [Header("联动")]
    [Tooltip("场景里的 WalkingSceneUI；章末强弹拼图用，没有拼图的场景可留空")]
    public WalkingSceneUI sceneUI;

    [Header("事件")]
    [Tooltip("整章对话全部播完时触发")]
    public UnityEvent onAllFinished;

    [Tooltip("发放碎片时触发，参数是 0 起的碎片下标（对应 PuzzleChapterConfig.pieces 的序号）")]
    public PieceEvent onPieceCollected;

    // 等待结算的上一句：翻页/结束时它才算"说完"
    private DialogueLine pendingLine;
    private bool begun;

    private void Start()
    {
        if (playOnStart)
        {
            Begin();
        }
    }

    private void OnDestroy()
    {
        Unhook();
    }

    /// <summary>开始整章流程：铺开场背景 -> 播对话</summary>
    public void Begin()
    {
        if (begun)
        {
            return;
        }

        if (dialogueSystem == null)
        {
            Debug.LogWarning("[DialogueSceneFlow] 没拖 dialogueSystem，流程不启动", this);
            return;
        }

        begun = true;

        ApplyVisual(initialSceneNumber);

        dialogueSystem.LineStarted += HandleLineStarted;
        dialogueSystem.DialogueEnded += HandleDialogueEnded;
        dialogueSystem.StartDialogue();
    }

    // 新的一句开始了：上一句才算说完，结算它的"对话后"效果
    private void HandleLineStarted(DialogueLine line)
    {
        Settle(pendingLine);
        pendingLine = line;
    }

    // 整段播完：结算最后一句，然后走章末逻辑
    private void HandleDialogueEnded()
    {
        Settle(pendingLine);
        pendingLine = null;

        Unhook();

        if (openPuzzleWhenFinished && sceneUI != null && !sceneUI.HasOpenedPuzzle)
        {
            sceneUI.OpenPuzzlePopup();
        }

        if (notifyPrologueFinishedWhenFinished && GameProgressManager.HasInstance)
        {
            GameProgressManager.Instance.NotifyPrologueFinished();
        }

        if (onAllFinished != null)
        {
            onAllFinished.Invoke();
        }
    }

    // 结算一句的"对话后"效果：发碎片、切场景
    private void Settle(DialogueLine line)
    {
        if (line == null)
        {
            return;
        }

        if (line.puzzlePiece >= 0)
        {
            GrantPiece(line.puzzlePiece);
        }

        if (line.sceneNumber > 0)
        {
            ApplyVisual(line.sceneNumber);
        }
    }

    private void GrantPiece(int rawNumber)
    {
        int pieceIndex = rawNumber - pieceNumberBase;

        // 只有正常走章节流程（GameProgressManager 在幕内）才落背包；
        // 直接起场景调试时不在幕内，打条日志跳过，避免刷警告
        if (GameProgressManager.HasInstance && GameProgressManager.Instance.Phase == GamePhase.Act)
        {
            bool ok = GameProgressManager.Instance.CollectPiece(GameProgressManager.Instance.ActIndex, pieceIndex);

            if (!ok)
            {
                Debug.LogWarning("[DialogueSceneFlow] 碎片 #" + rawNumber + "（下标 " + pieceIndex + "）入包失败，可能重复收集", this);
            }
        }
        else
        {
            Debug.Log("[DialogueSceneFlow] 发放碎片 #" + rawNumber + "（下标 " + pieceIndex + "）（当前不在幕内，未写入存档）", this);
        }

        if (onPieceCollected != null)
        {
            onPieceCollected.Invoke(pieceIndex);
        }
    }

    private void ApplyVisual(int sceneNumber)
    {
        SceneVisual target = null;

        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i] != null && scenes[i].sceneNumber == sceneNumber)
            {
                target = scenes[i];
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning("[DialogueSceneFlow] 场景编号 " + sceneNumber + " 没在 scenes 里配置，背景不切换", this);
            return;
        }

        if (target.additive)
        {
            // 叠加模式：只把本层亮出来，已有的层保持不动（角色逐个进场）
            if (target.extraRoot != null)
            {
                target.extraRoot.SetActive(true);
            }
        }
        else
        {
            if (backgroundImage != null && target.background != null)
            {
                backgroundImage.sprite = target.background;
            }

            // 普通模式互斥：隐藏所有其他层（包括叠加层），只显示本层
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i] != null && scenes[i].extraRoot != null)
                {
                    scenes[i].extraRoot.SetActive(scenes[i] == target);
                }
            }
        }
    }

    private void Unhook()
    {
        if (dialogueSystem != null)
        {
            dialogueSystem.LineStarted -= HandleLineStarted;
            dialogueSystem.DialogueEnded -= HandleDialogueEnded;
        }
    }
}
