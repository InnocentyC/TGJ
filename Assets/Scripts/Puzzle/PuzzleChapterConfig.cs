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

        [Tooltip("碎片显示尺寸（x=宽，y=高，UI 像素，填 sprite 整张图的包围盒尺寸，透明区也会占位）；宽高要么都填要么都留 0。留 0 时按 sprite 原始像素尺寸，再乘 pieceScale")]
        public Vector2 size;

        // pieceIndex 不存：直接用 pieces 列表序号。
        // correctSlotIndex 暂不加：默认槽位按序号一一对应；
        // 若某章需要乱序对应，再补此字段，由弹窗注入给碎片。
    }

    [Header("拼图碎片")]
    [Tooltip("碎片列表，顺序 = 编号 = 槽位序号；数量不定，加一块就加一项")]
    public PieceDefinition[] pieces;

    [Tooltip("全体碎片的等比缩放系数：1 = 原始尺寸（单独填了 Size 的块按 Size，没填的按 sprite 原图像素）；0.5 = 全部缩小一半")]
    public float pieceScale = 1f;

    [Tooltip("待摆放区里碎片的缩放（相对拼图板上的尺寸）：1 = 不缩小；0.6 = 托盘里是板上尺寸的 60%")]
    [Range(0.1f, 1f)]
    public float trayPieceScale = 0.6f;

    [Header("拼图结果")]
    [Tooltip("拼完后的结果图（可多张，拼完点着依次看）")]
    public Sprite[] resultImages;

    [Tooltip("碎片预制体模板（根节点挂 CustomPuzzleImage + CanvasGroup）")]
    public GameObject pieceTemplate;
}
