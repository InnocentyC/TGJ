namespace Rhythm
{
    /// <summary>音符在生命周期里的状态</summary>
    public enum NoteState
    {
        Waiting,    // 还没到出现时间，未进入对象池
        OnScreen,   // 已从池取出，正在下落
        Judged      // 已判定完毕（回池）
    }

    /// <summary>
    /// 单个音符的纯数据。由 ChartLoader 从 CSV 生成，判定的唯一依据是 hitTime。
    /// </summary>
    public class NoteData
    {
        /// <summary>应该被击中的时刻（秒，从音乐开始播放算起）</summary>
        public double hitTime;

        /// <summary>所在轨道，0 = 最左边</summary>
        public int lane;

        /// <summary>打击音编号（音高），对应 Inspector 打击音队列的下标；与轨道无绑定</summary>
        public int pitch;

        public NoteState state = NoteState.Waiting;

        /// <summary>在屏期间对应的池对象引用；不在屏时为 null</summary>
        public NoteController controller;

        public NoteData(double hitTime, int lane, int pitch)
        {
            this.hitTime = hitTime;
            this.lane = lane;
            this.pitch = pitch;
        }
    }

    /// <summary>判定结果档位</summary>
    public enum JudgeResult
    {
        None,       // 没有判定发生（空按、过早按）
        Perfect,
        Great,
        Miss
    }
}
