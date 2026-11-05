using UnityEngine;

/// <summary>
/// 一章拼图的静态配置（每章一个 asset）。
/// pieces 的列表序号就是碎片编号（pieceIndex），槽位按编号一一对应：
/// 第 0 项碎片放进 slotIndex == 0 的槽位，以此类推。
/// 添加一块碎块 = 往 pieces 里加一项，总块数、完成判定自动跟上。
/// </summary>
[CreateAssetMenu(fileName = "PuzzleChapterConfig", menuName = "Puzzle/PuzzleChapterConfig", order = 0)]
public class PuzzleChapterConfig : ScriptableObject
{
    /// <summary>单块碎片的定义</summary>
    [System.Serializable]
    public class PieceDefinition
    {
        [Tooltip("碎片图案")]
        public Sprite image;

        // pieceIndex 不存：直接用 pieces 列表序号。
        // correctSlotIndex 暂不加：默认槽位按序号一一对应；
        // 若某章需要乱序对应，再补此字段，由弹窗注入给碎片。
    }

    [Header("拼图碎片")]
    [Tooltip("碎片列表，顺序 = 编号 = 槽位序号；数量不定，加一块就加一项")]
    public PieceDefinition[] pieces;

    [Header("拼图结果")]
    [Tooltip("拼完后的结果图（可多张，拼完点着依次看）")]
    public Sprite[] resultImages;

    [Tooltip("碎片预制体模板（根节点挂 CustomPuzzleImage + CanvasGroup）")]
    public GameObject pieceTemplate;
}
