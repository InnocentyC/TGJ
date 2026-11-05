using System.Collections.Generic;
using UnityEngine;

namespace Rhythm
{
    /// <summary>判定窗口参数（毫秒，±）</summary>
    public struct JudgementWindows
    {
        public float perfectMs;
        public float greatMs;
        public float missMs;
    }

    /// <summary>
    /// 判定管理器。核心规则：
    /// 1. 每个 lane 一个 FIFO 队列（谱面按时间排序，入队顺序即落下顺序），
    ///    按键只判队头 —— 同一轨道多个可点音符时天然优先判先落下的那个。
    /// 2. 判定按时间差（songTime - hitTime），不按像素距离。
    /// 3. 提前按太多（超过 miss 窗口）→ 空按，不消耗音符；
    ///    队头超过 miss 窗口还没按 → 自动 Miss 出队（每帧由 AutoMissCheck 驱动）。
    /// </summary>
    public class JudgementManager
    {
        private readonly List<Queue<NoteData>> lanes = new List<Queue<NoteData>>();
        private readonly List<NoteData> missedBuffer = new List<NoteData>();

        public JudgementManager(int laneCount)
        {
            for (int i = 0; i < laneCount; i++)
            {
                lanes.Add(new Queue<NoteData>());
            }
        }

        /// <summary>音符从池取出、开始下落时入队</summary>
        public void RegisterSpawned(NoteData note)
        {
            lanes[note.lane].Enqueue(note);
        }

        /// <summary>
        /// 玩家按下某条轨道。
        /// 返回是否消耗了音符（Perfect/Great/Miss 都算消耗）；
        /// judged 输出判定档位，note 输出被判定（或自动 Miss）的音符，无则 null。
        /// </summary>
        public bool TryJudge(int lane, double songTime, JudgementWindows windows, out JudgeResult judged, out NoteData note)
        {
            judged = JudgeResult.None;
            note = null;

            if (lane < 0 || lane >= lanes.Count)
            {
                return false;
            }

            Queue<NoteData> queue = lanes[lane];

            if (queue.Count == 0)
            {
                return false;
            }

            NoteData head = queue.Peek();
            double delta = songTime - head.hitTime; // 正 = 按晚了

            // 提前按太多：空按，不消耗，等音符再落一点
            if (delta < -windows.missMs / 1000.0)
            {
                return false;
            }

            queue.Dequeue();
            note = head;

            double absDelta = System.Math.Abs(delta);

            if (absDelta <= windows.perfectMs / 1000.0)
            {
                judged = JudgeResult.Perfect;
            }
            else if (absDelta <= windows.greatMs / 1000.0)
            {
                judged = JudgeResult.Great;
            }
            else
            {
                // 在 miss 窗口内但超出 great 窗口（按得太晚/太早一点）
                judged = JudgeResult.Miss;
            }

            return true;
        }

        /// <summary>
        /// 每帧检查：把所有超过 miss 窗口还没按的队头自动判 Miss 并出队。
        /// 返回本次被自动 Miss 的音符列表（复用缓冲，调用方当帧用完即弃）。
        /// </summary>
        public List<NoteData> AutoMissCheck(double songTime, JudgementWindows windows)
        {
            missedBuffer.Clear();

            double limit = windows.missMs / 1000.0;

            for (int i = 0; i < lanes.Count; i++)
            {
                Queue<NoteData> queue = lanes[i];

                while (queue.Count > 0 && songTime - queue.Peek().hitTime > limit)
                {
                    missedBuffer.Add(queue.Dequeue());
                }
            }

            return missedBuffer;
        }

        /// <summary>清空所有 lane 队列（重开一局用）</summary>
        public void Clear()
        {
            for (int i = 0; i < lanes.Count; i++)
            {
                lanes[i].Clear();
            }
        }
    }
}
