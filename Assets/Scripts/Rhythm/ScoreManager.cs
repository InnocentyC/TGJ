using UnityEngine;

namespace Rhythm
{
    /// <summary>
    /// 分数与结算统计。
    /// 计分：Perfect/Great 得基础分，并带连击加成（连击越高加成越多，封顶 20%）；
    /// 评级 S/A/B/C/D 按准确率划档，阈值可由外部配置（策划定稿后直接改数值）。
    /// </summary>
    public class ScoreManager
    {
        private readonly float[] rankThresholds; // 依次为 S/A/B/C 的最低准确率（0~1），低于 C 的全归 D

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int PerfectCount { get; private set; }
        public int GreatCount { get; private set; }
        public int MissCount { get; private set; }

        /// <summary>本局音符总数（含未判定的），准确率分母</summary>
        public int TotalNotes { get; set; }

        public ScoreManager(float[] thresholds)
        {
            rankThresholds = thresholds != null && thresholds.Length >= 4
                ? thresholds
                : new float[] { 0.95f, 0.9f, 0.8f, 0.7f };
        }

        /// <summary>Perfect=1、Great=0.5、Miss=0 的加权准确率（0~1）</summary>
        public float Accuracy
        {
            get
            {
                if (TotalNotes <= 0)
                {
                    return 0f;
                }

                return (PerfectCount + GreatCount * 0.5f) / TotalNotes;
            }
        }

        /// <summary>当前准确率对应的评级字符（S/A/B/C/D）</summary>
        public char Rank
        {
            get
            {
                float acc = Accuracy;

                if (acc >= rankThresholds[0]) return 'S';
                if (acc >= rankThresholds[1]) return 'A';
                if (acc >= rankThresholds[2]) return 'B';
                if (acc >= rankThresholds[3]) return 'C';
                return 'D';
            }
        }

        /// <summary>记一次判定结果，返回本次得分</summary>
        public int Add(JudgeResult result)
        {
            int gained = 0;

            switch (result)
            {
                case JudgeResult.Perfect:
                    PerfectCount++;
                    Combo++;
                    gained = BasePoint(JudgeResult.Perfect);
                    break;

                case JudgeResult.Great:
                    GreatCount++;
                    Combo++;
                    gained = BasePoint(JudgeResult.Great);
                    break;

                case JudgeResult.Miss:
                    MissCount++;
                    Combo = 0;
                    break;
            }

            if (Combo > MaxCombo)
            {
                MaxCombo = Combo;
            }

            Score += gained;
            return gained;
        }

        /// <summary>重开一局</summary>
        public void Reset()
        {
            Score = 0;
            Combo = 0;
            MaxCombo = 0;
            PerfectCount = 0;
            GreatCount = 0;
            MissCount = 0;
        }

        // 基础分 1000 / 500，连击加成：加成比例 = min(连击,100)/100 * 0.2，封顶 +20%
        private int BasePoint(JudgeResult result)
        {
            int baseScore = result == JudgeResult.Perfect ? 1000 : 500;
            float comboBonus = Mathf.Min(Combo, 100) / 100f * 0.2f;
            return Mathf.RoundToInt(baseScore * (1f + comboBonus));
        }
    }
}
