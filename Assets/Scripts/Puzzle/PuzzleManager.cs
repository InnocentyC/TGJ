using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// 拼图管理器类：负责判定拼图是否完成、播放结果图演出、切换结果图片
public class PuzzleManager : MonoBehaviour
{
    [Header("拼图配置")]
    public int totalPieces = 12;           // 需要正确放置的碎片总数，必须等于场景里实际槽位的数量
    public float snapDistance = 100f;     // 碎片吸附到槽位的最大距离（UI 像素）

    [Header("图片切换")]
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

    private void Start()
    {
        // 如果没手动指定 resultImage，就用结果图数组的第一张
        if (resultImages != null && resultImages.Length > 0)
        {
            resultImage = resultImages[currentImageIndex];
        }

        // 开局先把结果图隐藏并设为全透明，避免提前露出答案
        if (resultImage != null)
        {
            resultImage.gameObject.SetActive(false);
            Color color = resultImage.color;
            color.a = 0f;
            resultImage.color = color;
        }

        // 给结果图挂上点击监听（用于拼完后点图看下一张）
        AddClickEventListenerToResultImage();
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

        // 每次都全量重新统计：扫描场景里所有槽位，数出"非空且放对"的数量
        placedPieces = 0;
        CustomPuzzleSlot[] allSlots = FindObjectsOfType<CustomPuzzleSlot>();

        foreach (CustomPuzzleSlot slot in allSlots)
        {
            if (!slot.IsEmpty() && slot.IsPieceCorrect())
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

        // 淡出所有拼图碎片（依赖每个碎片身上的 CanvasGroup，Awake 里会自动补）
        CustomPuzzleImage[] allPieces = FindObjectsOfType<CustomPuzzleImage>();
        float timer = 0f;

        while (timer < fadeOutDuration)
        {
            // 按时间进度把 alpha 从 1 插值到 0
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeOutDuration);

            foreach (CustomPuzzleImage piece in allPieces)
            {
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
        foreach (CustomPuzzleImage piece in allPieces)
        {
            piece.gameObject.SetActive(false);
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
    public void ResetPuzzle()
    {
        // 清空所有槽位里已放置的碎片
        CustomPuzzleSlot[] allSlots = FindObjectsOfType<CustomPuzzleSlot>();
        foreach (CustomPuzzleSlot slot in allSlots)
        {
            if (!slot.IsEmpty())
            {
                slot.RemovePiece();
            }
        }

        // 把所有碎片恢复显示、恢复不透明、回到初始位置
        CustomPuzzleImage[] allPieces = FindObjectsOfType<CustomPuzzleImage>();
        foreach (CustomPuzzleImage piece in allPieces)
        {
            CanvasGroup canvasGroup = piece.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            piece.gameObject.SetActive(true);
            piece.ResetPosition();
        }

        // 结果图重新隐藏并设为透明
        if (resultImage != null)
        {
            resultImage.gameObject.SetActive(false);
            Color color = resultImage.color;
            color.a = 0f;
            resultImage.color = color;
        }

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
