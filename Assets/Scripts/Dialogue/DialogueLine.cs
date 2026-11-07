using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 一句对白的数据。CSV 里的一行 = 一个 DialogueLine。
/// 字段对应表格：NPCName / SpeakerName / Dialogue / PuzzlePiece / SceneNumber。
/// 线性流程，没有分支/跳转 id。
/// </summary>
[System.Serializable]
public class DialogueLine
{
    [Tooltip("与之交互的 NPC；留空表示这句是玩家自白/旁白")]
    public string npcName = "";

    [Tooltip("此时正在说话的人名；留空表示旁白")]
    public string speaker = "";

    [TextArea(2, 5)]
    [Tooltip("对白内容。CSV 里写 \\n 会被转成真正的换行")]
    public string content = "";

    [Tooltip("这句说完后获得的拼图碎片编号（按美术文件名，从 1 开始）；-1 表示不获得")]
    public int puzzlePiece = -1;

    [Tooltip("这句说完后切换到的场景编号；0 表示不切换（保持在当前场景）")]
    public int sceneNumber = 0;

    public DialogueLine()
    {
    }

    public DialogueLine(string npcName, string speaker, string content, int puzzlePiece, int sceneNumber)
    {
        this.npcName = npcName;
        this.speaker = speaker;
        this.content = content;
        this.puzzlePiece = puzzlePiece;
        this.sceneNumber = sceneNumber;
    }

    /// <summary>是否有说话人（false 就是旁白）</summary>
    public bool HasSpeaker
    {
        get { return !string.IsNullOrEmpty(speaker); }
    }

    public override string ToString()
    {
        return string.Format("[{0}] {1}", HasSpeaker ? speaker : "旁白", content);
    }
}

/// <summary>
/// 带 DialogueLine 参数的 UnityEvent。
/// Unity 不能直接序列化泛型 UnityEvent，所以要包一层具名子类，才能在 Inspector 里挂回调。
/// </summary>
[System.Serializable]
public class DialogueLineEvent : UnityEvent<DialogueLine>
{
}
