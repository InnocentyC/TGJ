using UnityEngine;
using UnityEngine.UI;

// 拼图槽位：拼图板上的"坑"，只负责记录放了哪块碎片、以及放得对不对
public class CustomPuzzleSlot : MonoBehaviour
{
    public int slotIndex;          // 本槽位的编号（碎片身上的 correctSlotIndex 要和它相等）
    public int correctPieceIndex;  // 本槽位应该放哪块碎片（要和该碎片的 pieceIndex 相等）

    private CustomPuzzleImage currentPiece; // 当前放在这个槽位里的碎片，null 表示空槽
    private Image slotImage;                // 槽位自身的底图（放上碎片后会被隐藏）
    private bool isHidden;                  // 槽位底图当前是否处于隐藏状态

    [Header("视觉效果")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.3f);      // 常态：半透明白色底框
    public Color highlightedColor = new Color(1f, 1f, 1f, 0.6f); // 预留的高亮色（当前代码未使用，需要在高亮逻辑里手动调用）

    private void Awake()
    {
        // 拿到自身 Image 并设为常态颜色
        slotImage = GetComponent<Image>();
        if (slotImage != null)
        {
            slotImage.color = normalColor;
        }
        isHidden = false;
    }

    // 放置碎片到槽位：记下引用，并把槽位底图藏起来（视觉上只看到碎片）
    public void PlacePiece(CustomPuzzleImage piece)
    {
        currentPiece = piece;
        HideSlot();
    }

    // 移除碎片：清空引用，重新显示底图
    public void RemovePiece()
    {
        currentPiece = null;
        ShowSlot();
    }

    // 检查槽位是否为空
    public bool IsEmpty()
    {
        return currentPiece == null;
    }

    // 检查放置的碎片是否正确
    public bool IsPieceCorrect()
    {
        if (currentPiece == null)
            return false;

        // 判定依据：碎片的 pieceIndex 是否等于本槽位要求的 correctPieceIndex
        return currentPiece.pieceIndex == correctPieceIndex;
    }

    // 隐藏槽位：关闭 Image 渲染，同时关掉 raycastTarget 避免底框拦截射线
    private void HideSlot()
    {
        if (slotImage != null)
        {
            slotImage.enabled = false;
            slotImage.raycastTarget = false;
        }
        isHidden = true;
    }

    // 显示槽位：恢复渲染、恢复颜色、恢复射线检测
    private void ShowSlot()
    {
        if (slotImage != null)
        {
            slotImage.enabled = true;
            slotImage.color = normalColor;
            slotImage.raycastTarget = true;
        }
        isHidden = false;
    }

    // 获取当前放置的碎片
    public CustomPuzzleImage CurrentPiece
    {
        get { return currentPiece; }
    }

    // 槽位是否已隐藏
    public bool IsHidden
    {
        get { return isHidden; }
    }
}
