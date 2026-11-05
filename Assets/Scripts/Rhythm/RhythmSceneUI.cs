using System.Collections;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Rhythm
{
    /// <summary>
    /// 音游场景 UI 逻辑：计分/连击显示、判定弹出图。
    /// 场景本身由美术搭建，Inspector 里拖引用即可，代码不再自动生成场景。
    /// </summary>
    public class RhythmSceneUI : MonoBehaviour
    {
        // ---------------- 引用（美术场景里拖到 RhythmGameManager 上） ----------------
        // 分数/连击文本：旧版 Text 和 TextMeshPro 二选一拖即可，都拖了以 TMP 为准
        public Text scoreText;
        public Text comboText;
        public TMP_Text scoreTextTmp;
        public TMP_Text comboTextTmp;

        // ===== 判定弹出图功能暂时关闭（策划要求去掉实时判定弹出）=====
        /*
        [Tooltip("判定弹出图：判定发生时换成对应 sprite，显示后自动淡出")]
        public Image judgementImage;
        */

        [Header("演奏区布局（由 RhythmGameManager 从标记点换算后写入，不用手动填）")]
        [SerializeField] private float judgeLineY = -300f;
        [SerializeField] private float spawnY = 600f;
        [SerializeField] private float[] laneXPositions;

        // ===== 判定弹出图功能暂时关闭 =====
        /*
        [Tooltip("判定弹出图淡出时长（秒）")]
        public float judgePopupFadeTime = 0.4f;

        [Tooltip("判定弹出图保持不动显示的时长（秒），之后才开始淡出")]
        public float judgePopupHoldTime = 0.35f;

        private Sprite perfectSprite;
        private Sprite greatSprite;
        private Sprite missSprite;
        private CanvasGroup judgementGroup;
        private Coroutine fadeRoutine;
        private bool judgeImageAutoSize; // 自动生成的判定图按 sprite 原始尺寸显示
        */

        public float JudgeLineYPosition { get { return judgeLineY; } }
        public float SpawnYPosition { get { return spawnY; } }

        // 由 RhythmGameManager 在初始化时写入（来自场景标记点换算）
        public void SetJudgeLineY(float value) { judgeLineY = value; }
        public void SetSpawnY(float value) { spawnY = value; }
        public void SetLaneXPositions(float[] positions) { laneXPositions = positions; }
        // ===== 判定弹出图功能暂时关闭 =====
        // public void SetJudgeImageAutoSize(bool value) { judgeImageAutoSize = value; }

        public float LaneWidth
        {
            get
            {
                if (laneXPositions != null && laneXPositions.Length >= 2)
                {
                    return laneXPositions[1] - laneXPositions[0];
                }

                return LaneSpacingAuto;
            }
        }

        private static float LaneSpacingAuto { get { return (1920f - 240f) / GameSettings.LaneCount; } }

        /// <summary>第 lane 条轨道中心的 X 坐标</summary>
        public float GetLaneX(int lane)
        {
            if (laneXPositions != null && laneXPositions.Length == GameSettings.LaneCount)
            {
                return laneXPositions[lane];
            }

            // 没填就自动均分（1920 参考宽，左右各留 120）
            return 120f + LaneSpacingAuto * (lane + 0.5f);
        }

        // ===== 判定弹出图功能暂时关闭（策划要求去掉实时判定弹出）=====
        /*
        /// <summary>注入三档判定图，并给弹出图补上 CanvasGroup（用于淡出）</summary>
        public void Setup(Sprite perfect, Sprite great, Sprite miss)
        {
            perfectSprite = perfect;
            greatSprite = great;
            missSprite = miss;

            if (judgementImage != null && judgementImage.GetComponent<CanvasGroup>() == null)
            {
                judgementGroup = judgementImage.gameObject.AddComponent<CanvasGroup>();
            }
            else if (judgementImage != null)
            {
                judgementGroup = judgementImage.GetComponent<CanvasGroup>();
            }

            if (judgementImage != null)
            {
                judgementGroup.alpha = 0f;
            }
        }
        */

        // ---------------- 运行时刷新 ----------------

        public void SetScore(int value)
        {
            if (scoreTextTmp != null)
            {
                scoreTextTmp.text = value.ToString();
            }
            else if (scoreText != null)
            {
                scoreText.text = value.ToString();
            }
        }

        public void SetCombo(int combo)
        {
            string text = combo >= 2 ? combo.ToString() : "0";

            if (comboTextTmp != null)
            {
                comboTextTmp.text = text;
            }
            else if (comboText != null)
            {
                comboText.text = text;
            }
        }

        // ===== 判定弹出图功能暂时关闭（策划要求去掉实时判定弹出）=====
        /*
        /// <summary>判定发生：换成对应 sprite 并淡出</summary>
        public void FlashJudgement(JudgeResult result)
        {
            if (judgementImage == null || result == JudgeResult.None)
            {
                return;
            }

            switch (result)
            {
                case JudgeResult.Perfect:
                    judgementImage.sprite = perfectSprite;
                    break;

                case JudgeResult.Great:
                    judgementImage.sprite = greatSprite;
                    break;

                case JudgeResult.Miss:
                    judgementImage.sprite = missSprite;
                    break;
            }

            if (judgeImageAutoSize && judgementImage.sprite != null)
            {
                judgementImage.SetNativeSize(); // 三张图各按自己的像素尺寸显示
            }

            if (judgementImage.sprite == null)
            {
                Debug.LogWarning("[RhythmSceneUI] 未配置 " + result + " 的判定图（Inspector 里拖 sprite）");
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(FadeJudgePopup());
        }

        private IEnumerator FadeJudgePopup()
        {
            float fadeTime = Mathf.Max(0.05f, judgePopupFadeTime);
            float holdTime = Mathf.Max(0f, judgePopupHoldTime);
            judgementGroup.alpha = 1f;

            // 先完整显示一段时间，再淡出
            float t = 0f;

            while (t < holdTime)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            t = 0f;

            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                judgementGroup.alpha = 1f - t / fadeTime;
                yield return null;
            }

            judgementGroup.alpha = 0f;
            fadeRoutine = null;
        }
        */
    }
}
