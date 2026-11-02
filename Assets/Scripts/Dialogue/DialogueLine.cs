using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 一句对白的数据。CSV 里的一行 = 一个 DialogueLine。
/// 目前只有三个字段：说话人、内容、属于第几幕（线性流程，没有分支/跳转 id）。
/// </summary>
[System.Serializable]
public class DialogueLine
{
    [Tooltip("说话人；留空表示旁白")]
    public string speaker = "";

    [TextArea(2, 5)]
    [Tooltip("对白内容。CSV 里写 \\n 会被转成真正的换行")]
    public string content = "";

    [Tooltip("属于第几幕，从 1 开始；0 表示不属于任何幕（开场/通用台词）")]
    public int act = 1;

    public DialogueLine()
    {
    }

    public DialogueLine(string speaker, string content, int act)
    {
        this.speaker = speaker;
        this.content = content;
        this.act = act;
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
