using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// 拼图管理器类：负责判定拼图是否完成、播放结果图演出、切换结果图片。
// 由拼图弹窗驱动：Setup() 注入章节配置（总块数、结果图），
// 碎片/槽位通过注册获得（不再全场景搜索）；ResetPuzzle 仅作调试用途。
public class PuzzleManager : MonoBehaviour
{
    [Header("拼图配置")]
    [Tooltip("需要正确放置的碎片总数；Setup 时会用章节配置覆盖")]
    public int totalPieces = 12;
    public float snapDistance = 100f;     // 碎片吸附到槽位的最大距离（UI 像素）

    [Header("图片切换")]
    [Tooltip("拼图完成后依次展示的结果图数组（按顺序一张张点着看）；sprite 可由章节配置覆盖")]
    public Image[] resultImages;          // 拼图完成后依次展示的结果图数组（按顺序一张张点着看）
    public Image resultImage;             // 当前正在展示的结果图（外部也可直接指定单张）
    public float fadeInDuration = 1f;     // 结果图淡入时长（秒）
    public float fadeOutDuration = 0.5f;  // 碎片淡出时长（秒，不能设为 0，否则协程里会除零）
    private int currentImageIndex = 0;    // 当前展示到第几张结果图

    [Header("事件")]
    public UnityEvent onPuzzleCompleted;          // 拼图完成的瞬间触发
    public UnityEvent onImageTransitionComplete;  // 结果图淡入动画播放完毕后触发
    public UnityEvent onLastImageClicked;         // 已经是最后一张结果图还被点击时触发（可用于跳关卡）

    private int placedPieces = 0;          // 已正确放置的碎片数量
    private bool isPuzzleCompleted = false; // 本轮拼图是否已完成
    private bool isTransitioning = false;   // 是否正在播放淡入淡出过渡动画（避免动画期间重复触发）

    // 本弹窗的槽位列表（Setup 时从子物体收集）
    private CustomPuzzleSlot[] slots;

    // 弹窗动态生成并注册的碎片列表
    private readonly System.Collections.Generic.List<CustomPuzzleImage> registeredPieces =
        new System.Collections.Generic.List<CustomPuzzleImage>();

    /// <summary>本弹窗的槽位列表（碎片吸附用）</summary>
    public CustomPuzzleSlot[] Slots
    {
        get { return slots; }
    }

    /// <summary>是否已经注册过槽位</summary>
    public bool HasSlots
    {
        get { return slots != null && slots.Length > 0; }
    }

    private void Start()
    {
        // 如果没手动指定 resultImage，就用结果图数组的第一张
        if (resultImages != null && resultImages.Length > 0)
        {
            resultImage = resultImages[currentImageIndex];
        }

        // 开局先把结果图隐藏并设为全透明，避免提前露出答案
        HideResultImages();

        // 给结果图挂上点击监听（用于拼完后点图看下一张）
        AddClickEventListenerToResultImage();
    }

    /// <summary>
    /// 由拼图弹窗注入章节配置：总块数取自配置、结果图 sprite 应用到本物体的结果图列表、
    /// 收集本物体下所有槽位、清空碎片注册表。在弹窗 Show 时调用。
    /// </summary>
    public void Setup(PuzzleChapterConfig config)
    {
        // 结束协程，复位过渡状态（复用弹窗时可能上局还在播动画）
        StopAllCoroutines();
        isTransitioning = false;

        if (config != null && config.pieces != null)
        {
            totalPieces = config.pieces.Length;
        }

        // 用配置里的结果图 sprite 覆盖到本弹窗预制体摆好的结果图上（两边都配了时以配置为准）
        if (config != null && config.resultImages != null &&
            resultImages != null && resultImages.Length > 0)
        {
            int count = Mathf.Min(config.resultImages.Length, resultImages.Length);
            for (int i = 0; i < count; i++)
            {
                if (config.resultImages[i] != null)
                {
                    resultImages[i].sprite = config.resultImages[i];
                }
            }
        }

        // 收集本弹窗下的所有槽位（含未激活的）
        slots = GetComponentsInChildren<CustomPuzzleSlot>(true);

        // 清空碎片注册表（碎片由弹窗重新生成后逐个注册）
        registeredPieces.Clear();

        // 复位内部状态与结果图显示
        placedPieces = 0;
        isPuzzleCompleted = false;
        currentImageIndex = 0;

        if (resultImages != null && resultImages.Length > 0)
        {
            resultImage = resultImages[0];
        }

        HideResultImages();
        AddClickEventListenerToResultImage();
    }

    /// <summary>弹窗生成一块碎片后注册进来，供完成判定和淡出演出使用</summary>
    public void RegisterPiece(CustomPuzzleImage piece)
    {
        if (piece != null && !registeredPieces.Contains(piece))
        {
            registeredPieces.Add(piece);
        }
    }

    /// <summary>弹窗销毁碎片后取消注册</summary>
    public void UnregisterPiece(CustomPuzzleImage piece)
    {
        if (piece != null)
        {
            registeredPieces.Remove(piece);
        }
    }

    // 把所有结果图隐藏并设为全透明
    private void HideResultImages()
    {
        if (resultImages == null)
        {
            return;
        }

        foreach (Image image in resultImages)
        {
            if (image == null) continue;

            image.gameObject.SetActive(false);
            Color color = image.color;
            color.a = 0f;
            image.color = color;
        }

        if (resultImage != null)
        {
            Color color = resultImage.color;
            color.a = 0f;
            resultImage.color = color;
        }
    }

    // 结果图片点击事件处理
    private void OnResultImageClick()
    {
        // 只有在拼图已完成、且没有正在播放过渡动画时才响应点击
        if (isPuzzleCompleted && !isTransitioning)
        {
            DirectSwitchToNextImage();
        }
    }

    // 直接切换到下一张图片
    private void DirectSwitchToNextImage()
    {
        // 只有一张（或没配）结果图时无法切换
        if (resultImages == null || resultImages.Length <= 1)
        {
            Debug.LogWarning("没有足够的图片进行切换");
            return;
        }

        // 已经是最后一张了：不再切换，改为抛出"最后一张被点击"事件
        if (currentImageIndex + 1 >= resultImages.Length)
        {
            onLastImageClicked?.Invoke();
            return;
        }

        // 隐藏当前结果图
        if (resultImage != null)
        {
            resultImage.gameObject.SetActive(false);
        }

        // 索引 +1，换成下一张结果图并直接显示（不透明）
        currentImageIndex++;

        resultImage = resultImages[currentImageIndex];

        if (resultImage != null)
        {
            resultImage.gameObject.SetActive(true);
            Color color = resultImage.color;
            color.a = 1f;
            resultImage.color = color;
        }

        // 新的结果图要重新绑定一次点击监听
        AddClickEventListenerToResultImage();
    }

    // 为结果图片添加点击事件监听器
    private void AddClickEventListenerToResultImage()
    {
        if (resultImage != null)
        {
            // 没有 Button 就自动补一个（但不会设置 targetGraphic，所以没有按钮过渡动效）
            Button button = resultImage.GetComponent<Button>();
            if (button == null)
            {
                button = resultImage.gameObject.AddComponent<Button>();
            }
            // 先移除再添加，防止切换图片时重复绑定导致一次点击触发多次
            button.onClick.RemoveListener(OnResultImageClick);
            button.onClick.AddListener(OnResultImageClick);
        }
    }

    // 检查拼图是否完成（由碎片成功放入槽位后调用）
    public void CheckPuzzleCompletion()
    {
        // 已完成或正在播动画时不再判定
        if (isPuzzleCompleted || isTransitioning)
            return;

        if (!HasSlots)
        {
            return;
        }

        // 全量重新统计：数出"非空且放对"的槽位数量
        placedPieces = 0;
        foreach (CustomPuzzleSlot slot in slots)
        {
            if (slot != null && !slot.IsEmpty() && slot.IsPieceCorrect())
            {
                placedPieces++;
            }
        }

        // 达到目标数量就算完成
        if (placedPieces >= totalPieces)
        {
            CompletePuzzle();
        }
    }

   // 完成拼图
    private void CompletePuzzle()
    {
        isPuzzleCompleted = true;

        // 通知外部：拼图完成
        onPuzzleCompleted?.Invoke();

        // 开始播放"碎片淡出 -> 结果图淡入"的过渡动画
        StartCoroutine(TransitionToResultImage());
    }

    // 切换到结果图片的协程
    private System.Collections.IEnumerator TransitionToResultImage()
    {
        isTransitioning = true;

        // 淡出本弹窗注册的所有拼图碎片（依赖每个碎片身上的 CanvasGroup，Awake 里会自动补）
        float timer = 0f;

        while (timer < fadeOutDuration)
        {
            // 按时间进度把 alpha 从 1 插值到 0
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeOutDuration);

            foreach (CustomPuzzleImage piece in registeredPieces)
            {
                if (piece == null) continue;

                CanvasGroup canvasGroup = piece.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = alpha;
                }
            }

            timer += Time.deltaTime;
            yield return null;  // 等一帧
        }

        // 淡出结束后彻底隐藏所有碎片
        foreach (CustomPuzzleImage piece in registeredPieces)
        {
            if (piece != null)
            {
                piece.gameObject.SetActive(false);
            }
        }

        // 显示结果图并从透明淡入到不透明
        if (resultImage != null)
        {
            resultImage.gameObject.SetActive(true);
            timer = 0f;

            while (timer < fadeInDuration)
            {
                float alpha = Mathf.Lerp(0f, 1f, timer / fadeInDuration);
                Color color = resultImage.color;
                color.a = alpha;
                resultImage.color = color;

                timer += Time.deltaTime;
                yield return null;
            }

            // 收尾时强制设为完全不透明，避免插值误差留下半透明
            Color finalColor = resultImage.color;
            finalColor.a = 1f;
            resultImage.color = finalColor;
        }

        isTransitioning = false;

        // 通知外部：过渡动画播完了
        onImageTransitionComplete?.Invoke();
    }

    // 重置拼图：清空所有槽位、还原碎片位置与透明度、隐藏结果图，恢复到开局状态
    // 注意：正常流程不再调用（关闭弹窗要保留摆放进度），仅供调试
    public void ResetPuzzle()
    {
        // 清空所有槽位里已放置的碎片
        if (HasSlots)
        {
            foreach (CustomPuzzleSlot slot in slots)
            {
                if (slot != null && !slot.IsEmpty())
                {
                    slot.RemovePiece();
                }
            }
        }

        // 把所有碎片恢复显示、恢复不透明、回到初始位置（ResetPosition 会同时解除锁定）
        foreach (CustomPuzzleImage piece in registeredPieces)
        {
            if (piece == null) continue;

            CanvasGroup canvasGroup = piece.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            piece.gameObject.SetActive(true);
            piece.ResetPosition();
        }

        // 结果图重新隐藏并设为透明
        HideResultImages();

        // 状态位全部复位
        placedPieces = 0;
        isPuzzleCompleted = false;
        isTransitioning = false;
    }

    // 检查是否可以开始下一轮图片切换（已完成且没在播动画）
    public bool CanStartNextTransition()
    {
        return isPuzzleCompleted && !isTransitioning;
    }

    // 拼图是否已完成
    public bool IsPuzzleCompleted
    {
        get { return isPuzzleCompleted; }
    }

    // 当前是否正在切换图片
    public bool IsTransitioning
    {
        get { return isTransitioning; }
    }
}
