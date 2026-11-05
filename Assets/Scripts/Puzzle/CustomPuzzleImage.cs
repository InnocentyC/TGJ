using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 拼图碎片：实现 UGUI 三个拖拽接口，负责跟手拖动、吸附到正确槽位、失败则回原位。
// 吸附成功后碎片锁定（IsLocked），不可再拖；管理器与槽位列表由弹窗注入，不做场景搜索。
public class CustomPuzzleImage : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int pieceIndex;       // 本碎片的编号（要和槽位的 correctPieceIndex 相等）
    public int correctSlotIndex; // 本碎片应该放进哪个槽位（要和该槽位的 slotIndex 相等）

    private RectTransform rectTransform;  // 自身 RectTransform，用于移动位置
    private CanvasGroup canvasGroup;      // 用于拖拽时半透明、以及结束拼图时淡出
    private Vector2 originalPosition;     // 开局位置，吸附失败时弹回这里
    private Transform originalParent;     // 开局的父物体，弹回时要还原层级
    private bool hasHomePosition;         // 开局位置是否已经记录过
    private Transform rootParent;         // Canvas 根节点，拖拽时临时挂上去保证显示在最上层
    private PuzzleManager puzzleManager;  // 拼图管理器（由弹窗注入），用于读取吸附距离、槽位列表并上报进度

    // 吸附成功后锁定：不可再拖动，弹窗补生成碎片时也据此跳过
    private bool isLocked = false;

    // 是否已锁定（吸附成功）
    public bool IsLocked
    {
        get { return isLocked; }
    }

    [Header("视觉效果")]
    public Image pieceImage;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        // 没有 CanvasGroup 就自动补一个（淡出动画依赖它）
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        rootParent = transform.root;

        // 没手动指定图案 Image 时退回自身组件，避免 Setup 注入 sprite 时落空
        if (pieceImage == null)
        {
            pieceImage = GetComponent<Image>();
        }
    }

    /// <summary>
    /// 由弹窗在生成碎片时注入依赖（替代以前的场景查找）。
    /// </summary>
    public void Setup(PuzzleManager manager, int index, int slotIndex, Sprite sprite)
    {
        puzzleManager = manager;
        pieceIndex = index;
        correctSlotIndex = slotIndex;

        if (pieceImage != null && sprite != null)
        {
            pieceImage.sprite = sprite;
        }
    }

    // 记录开局位置与开局父物体，供吸附失败 / ResetPuzzle 时回退使用
    // 不能在 Awake 里记：碎片位置若由 LayoutGroup 或代码布局决定，Awake 时布局尚未重建，取到的是占位坐标
    // 默认延迟到第一次被拖动时自动记录（见 OnBeginDrag）；布局确定后也可由外部主动调用本方法刷新
    public void SaveHomePosition()
    {
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        hasHomePosition = true;
    }

    // 开始拖拽：已锁定的碎片直接忽略；否则半透明表示"拿起来了"，
    // 关闭 blocksRaycasts 避免自身挡住射线，并提到最上层显示
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLocked)
        {
            return;
        }

        // 第一次被拖动时补记开局位置：此时布局早已重建完成，且父物体尚未被临时改到 Canvas 根下
        if (!hasHomePosition)
        {
            SaveHomePosition();
        }

        canvasGroup.alpha = 0.7f;
        canvasGroup.blocksRaycasts = false;
        transform.SetParent(rootParent);
    }

    // 拖拽中：把鼠标/手指的位移增量累加到 anchoredPosition 上，实现跟手移动
    public void OnDrag(PointerEventData eventData)
    {
        if (isLocked)
        {
            return;
        }

        rectTransform.anchoredPosition += eventData.delta;
    }

    // 结束拖拽：判断是否吸附到正确槽位，成功则锁定，否则回到原位
    public void OnEndDrag(PointerEventData eventData)
    {
        if (isLocked)
        {
            return;
        }

        // 恢复不透明与射线检测
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // 吸附成功的三个条件：找到最近槽位、槽位为空、槽位编号正好是本碎片的目标槽位
        CustomPuzzleSlot closestSlot = FindClosestSlot();
        if (closestSlot != null && closestSlot.IsEmpty() && closestSlot.slotIndex == correctSlotIndex)
        {
            // 挂到槽位下并居中对齐，然后在槽位上登记本碎片
            transform.SetParent(closestSlot.transform);
            rectTransform.anchoredPosition = Vector2.zero;
            closestSlot.PlacePiece(this);

            // 吸附成功：锁定，之后不可再拖动
            isLocked = true;

            // 通知管理器检查是否全部拼完
            if (puzzleManager != null)
            {
                puzzleManager.CheckPuzzleCompletion();
            }
            return;
        }

        // 没吸附成功：还原父物体并回到初始位置
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = originalPosition;
    }

    // 查找最近的拼图槽位（只考虑空槽位，且距离要在吸附范围内）
    private CustomPuzzleSlot FindClosestSlot()
    {
        // 槽位列表优先取管理器注册的；没有管理器时退回场景搜索（兼容旧用法）
        CustomPuzzleSlot[] allSlots;
        if (puzzleManager != null && puzzleManager.HasSlots)
        {
            allSlots = puzzleManager.Slots;
        }
        else
        {
            allSlots = FindObjectsOfType<CustomPuzzleSlot>();
        }

        CustomPuzzleSlot closestSlot = null;
        float closestDistance = float.MaxValue;
        // 吸附距离取自管理器，没找到管理器时用默认 100
        float maxDistance = puzzleManager != null ? puzzleManager.snapDistance : 100f;

        foreach (CustomPuzzleSlot slot in allSlots)
        {
            if (slot == null || !slot.IsEmpty()) continue;

            // 注意：这里直接比较 anchoredPosition，要求槽位与碎片处于同一坐标系，否则距离会算错
            Vector2 slotPosition = slot.transform.GetComponent<RectTransform>().anchoredPosition;
            float distance = Vector2.Distance(rectTransform.anchoredPosition, slotPosition);
            if (distance < closestDistance && distance < maxDistance)
            {
                closestDistance = distance;
                closestSlot = slot;
            }
        }

        return closestSlot;
    }

    // 重置碎片位置（ResetPuzzle / 调试用）：还原父物体并回到开局位置，同时解除锁定
    public void ResetPosition()
    {
        // 从未被拖动过 = 还没记录开局位置，此时碎片本来就在原位，跳过以免被挪到错误坐标
        if (!hasHomePosition)
        {
            return;
        }

        isLocked = false;
        transform.SetParent(originalParent);
        rectTransform.anchoredPosition = originalPosition;
    }
}
