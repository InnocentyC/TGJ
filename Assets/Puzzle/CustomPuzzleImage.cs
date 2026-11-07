using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 拼图碎片：实现 UGUI 三个拖拽接口，负责跟手拖动、吸附到正确槽位、放回待摆放区。
// 尺寸规则：在待摆放区里是缩小尺寸（traySize），拿起来/拖到板上变大（boardSize），
// 放回待摆放区（松手点在托盘区域内，或吸附失败）恢复缩小尺寸。
// 吸附成功后碎片锁定（IsLocked）不可再拖；管理器、槽位列表、托盘区域均由弹窗注入，不做场景搜索。
public class CustomPuzzleImage : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int pieceIndex;       // 本碎片的编号（要和槽位的 correctPieceIndex 相等）
    public int correctSlotIndex; // 本碎片应该放进哪个槽位（要和该槽位的 slotIndex 相等）

    private RectTransform rectTransform;  // 自身 RectTransform，用于移动位置
    private CanvasGroup canvasGroup;      // 用于拖拽时半透明、以及结束拼图时淡出
    private Vector2 originalPosition;     // 托盘中的位置，放回时回到这里（有 LayoutGroup 时会被重新排版覆盖）
    private Transform originalParent;     // 托盘容器，放回时要还原层级
    private int homeSiblingIndex = 0;     // 在托盘里的排序位置，放回时还原，保证顺序不乱
    private bool hasHomePosition;         // 托盘位置是否已经记录过
    private Transform rootParent;         // Canvas 根节点，拖拽时临时挂上去保证显示在最上层
    private PuzzleManager puzzleManager;  // 拼图管理器（由弹窗注入），用于读取吸附距离、槽位列表并上报进度

    // 双尺寸：板上完整尺寸 / 托盘缩小尺寸
    private Vector2 boardSize = Vector2.zero;  // 拖动中与吸附到槽位后的尺寸
    private Vector2 traySize = Vector2.zero;   // 待摆放区里的缩小尺寸（zero = 不缩小）
    private bool inTray = true;                // 当前是否处于待摆放区（缩小状态）
    private RectTransform trayZone;            // 待摆放区区域，松手时判断是否放回了托盘

    // 吸附成功后锁定：不可再拖动，弹窗补生成碎片时也据此跳过
    private bool isLocked = false;

    // 是否已锁定（吸附成功）
    public bool IsLocked
    {
        get { return isLocked; }
    }

    /// <summary>待摆放区区域（由弹窗注入）；松手位置落在区域内 = 放回托盘</summary>
    public RectTransform TrayZone
    {
        get { return trayZone; }
        set { trayZone = value; }
    }

    [Header("视觉效果")]
    public Image pieceImage;

    [Header("拖拽判定")]
    [Tooltip("透明区点击阈值：sprite alpha 低于该值的像素不响应拖拽（0 = 整个矩形都可点；0.1 = 只有接近实体的部分可点）。需要 sprite 导入设置勾选 Read/Write Enabled 才生效，否则退化为整个矩形可点")]
    [Range(0f, 1f)]
    public float alphaHitTestThreshold = 0.1f;

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

        // 不规则碎片：alpha 低于阈值的透明像素不响应射线，点透明区不会拿起碎片。
        // 依赖 sprite 导入设置勾选 Read/Write Enabled；没勾时 Unity 会打警告并退化为整个矩形可点
        if (pieceImage != null && alphaHitTestThreshold > 0f)
        {
            pieceImage.alphaHitTestMinimumThreshold = alphaHitTestThreshold;
        }
    }

    /// <summary>
    /// 由弹窗在生成碎片时注入依赖（替代以前的场景查找）。
    /// boardSize：板上完整尺寸（trayPieceScale 之前的解析结果）；traySize：待摆放区缩小尺寸。
    /// 碎片生成即进入待摆放区，所以立即应用 traySize；traySize 传 zero 表示不缩小。
    /// </summary>
    public void Setup(PuzzleManager manager, int index, int slotIndex, Sprite sprite,
                      Vector2 boardSize, Vector2 traySize)
    {
        puzzleManager = manager;
        pieceIndex = index;
        correctSlotIndex = slotIndex;

        this.boardSize = boardSize;
        this.traySize = traySize;
        inTray = true;

        if (pieceImage != null && sprite != null)
        {
            pieceImage.sprite = sprite;
        }

        Vector2 initialSize = HasSize(traySize) ? traySize : boardSize;
        if (HasSize(initialSize))
        {
            rectTransform.sizeDelta = initialSize;
        }
    }

    // 尺寸是否有效（宽高至少有一项大于 0）
    private static bool HasSize(Vector2 size)
    {
        return size.x > 0f || size.y > 0f;
    }

    // 记录托盘位置与排序，供放回托盘时使用
    // 不能在 Awake 里记：碎片位置若由 LayoutGroup 或代码布局决定，Awake 时布局尚未重建，取到的是占位坐标
    // 默认延迟到第一次被拖动时自动记录（见 OnBeginDrag）
    public void SaveHomePosition()
    {
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        homeSiblingIndex = transform.GetSiblingIndex();
        hasHomePosition = true;
    }

    // 开始拖拽：已锁定的碎片直接忽略；否则从托盘尺寸放大到板上尺寸、
    // 半透明表示"拿起来了"、关闭 blocksRaycasts 避免自身挡住射线、提到最上层并让 pivot 对准指针
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLocked)
        {
            return;
        }

        // 第一次被拖动时补记托盘位置：此时布局早已重建完成，且父物体尚未被临时改到 Canvas 根下
        if (!hasHomePosition)
        {
            SaveHomePosition();
        }

        canvasGroup.alpha = 0.7f;
        canvasGroup.blocksRaycasts = false;

        // 从托盘拿到手里：立刻放大到板上尺寸
        if (HasSize(boardSize))
        {
            rectTransform.sizeDelta = boardSize;
        }
        inTray = false;

        // 临时挂到 Canvas 根下保证显示在最上层（保持世界位置），然后把 pivot 对准指针，
        // 避免尺寸突变后碎片和手指错位
        transform.SetParent(rootParent);
        RectTransform parentRect = transform.parent as RectTransform;
        if (parentRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, GetCanvasCamera(), out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint;
        }
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

    // 结束拖拽：松手点在托盘区域内 = 放回托盘（恢复缩小尺寸）；
    // 否则尝试吸附到正确槽位（成功则锁定），失败也放回托盘
    public void OnEndDrag(PointerEventData eventData)
    {
        if (isLocked)
        {
            return;
        }

        // 恢复不透明与射线检测
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // 松手点落在待摆放区区域内：直接放回，不做吸附判定
        if (!IsPointerInTrayZone(eventData.position))
        {
            // 吸附成功的三个条件：找到最近槽位、槽位为空、槽位编号正好是本碎片的目标槽位
            CustomPuzzleSlot closestSlot = FindClosestSlot();
            if (closestSlot != null && closestSlot.IsEmpty() && closestSlot.slotIndex == correctSlotIndex)
            {
                // 挂到槽位下并居中对齐（保持板上尺寸），然后在槽位上登记本碎片
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
        }

        // 放回托盘 / 没吸附成功：还原托盘层级、排序与缩小尺寸
        ReturnToTray();
    }

    // 放回托盘：还原父物体与排序、恢复缩小尺寸、回到托盘原位（有 LayoutGroup 时位置由布局接管）
    private void ReturnToTray()
    {
        Transform target = originalParent != null ? originalParent : (Transform)trayZone;

        if (target != null)
        {
            transform.SetParent(target);
            transform.SetSiblingIndex(homeSiblingIndex);
        }

        if (HasSize(traySize))
        {
            rectTransform.sizeDelta = traySize;
        }
        inTray = true;

        // 有 LayoutGroup 时下面这行会被布局覆盖，没有时恢复到原锚点位置
        rectTransform.anchoredPosition = originalPosition;
    }

    // 指针是否落在待摆放区区域内
    private bool IsPointerInTrayZone(Vector2 screenPoint)
    {
        if (trayZone == null)
        {
            return false;  // 没配托盘区域时退化为旧行为：按吸附结果处理
        }

        return RectTransformUtility.RectangleContainsScreenPoint(trayZone, screenPoint, GetCanvasCamera());
    }

    // 当前所在 Canvas 的渲染相机（Overlay 模式为 null，其余为 worldCamera）
    private Camera GetCanvasCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
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
        // 吸附距离取自管理器（UI 像素），按 Canvas 缩放系数换算成世界距离；
        // 没找到管理器时用默认 100
        float maxDistance = (puzzleManager != null ? puzzleManager.snapDistance : 100f)
                            * GetCanvasScaleFactor();

        foreach (CustomPuzzleSlot slot in allSlots)
        {
            if (slot == null || !slot.IsEmpty()) continue;

            // 拖拽中碎片挂在 Canvas 根下、槽位在拼图板子节点下，父级不同坐标系也不同，
            // 所以改用世界坐标下的 pivot 点比较距离：碎片大小不一、父级层级不同都不影响判定
            RectTransform slotRect = slot.transform as RectTransform;
            if (slotRect == null) continue;

            float distance = Vector2.Distance(rectTransform.position, slotRect.position);
            if (distance < closestDistance && distance < maxDistance)
            {
                closestDistance = distance;
                closestSlot = slot;
            }
        }

        return closestSlot;
    }

    // 取所在 Canvas 的缩放系数（Overlay 模式下世界距离 = UI 像素 × scaleFactor），取不到按 1
    private float GetCanvasScaleFactor()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.scaleFactor : 1f;
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
        transform.SetSiblingIndex(homeSiblingIndex);
        rectTransform.anchoredPosition = originalPosition;

        if (HasSize(traySize))
        {
            rectTransform.sizeDelta = traySize;
        }
        inTray = true;
    }
}
