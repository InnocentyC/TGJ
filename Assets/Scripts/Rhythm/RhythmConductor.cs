using UnityEngine;

namespace Rhythm
{
    /// <summary>
    /// 音游的唯一时间源。
    /// 有音乐时优先用 AudioSource.timeSamples / clip.frequency（采样级精度，和听到的声音同步）；
    /// 音乐未开始播放或没有配音乐时，退回 AudioSettings.dspTime 计时。
    /// 判定时间 = 原始时间 - (rhythmOffsetMs + audioLatencyMs) / 1000，
    /// 即正偏移让判定"往后推"（和 GameSettings 里 rhythmOffsetMs 的注释一致）。
    /// </summary>
    public class RhythmConductor : MonoBehaviour
    {
        private AudioSource musicSource;

        // PlayScheduled 的起播时刻（dspTime 域），音乐开始前的负时间靠它算
        private double scheduledStartTime = -1.0;

        // 保证原始时间不回退（timeSamples 在极端情况下可能短暂不单调）
        private double lastRawTime = double.NegativeInfinity;

        /// <summary>歌曲进度（秒），已叠加偏移补偿；音乐开始前为负数</summary>
        public double SongTime { get; private set; }

        /// <summary>未叠加偏移的原始时间（秒），即音乐本身播到了哪；用于"音乐是否播完"这类判断</summary>
        public double RawTime { get; private set; }

        public void Setup(AudioSource source)
        {
            musicSource = source;
        }

        /// <summary>
        /// 安排在 delay 秒后开始播放音乐（从本方法调用时刻算起）。
        /// 没有音乐时只是记录起始时刻，供 dspTime 退路计时用。
        /// </summary>
        public void Begin(float delay)
        {
            double now = AudioSettings.dspTime;
            scheduledStartTime = now + delay;
            lastRawTime = double.NegativeInfinity;

            if (musicSource != null && musicSource.clip != null)
            {
                musicSource.PlayScheduled(scheduledStartTime);
            }
        }

        /// <summary>停止播放（提前退出用）</summary>
        public void Stop()
        {
            if (musicSource != null)
            {
                musicSource.Stop();
            }

            scheduledStartTime = -1.0;
        }

        private void Update()
        {
            double raw;

            if (musicSource != null && musicSource.clip != null && musicSource.isPlaying)
            {
                raw = (double)musicSource.timeSamples / musicSource.clip.frequency;
            }
            else if (scheduledStartTime >= 0.0)
            {
                raw = AudioSettings.dspTime - scheduledStartTime;
            }
            else
            {
                raw = 0.0;
            }

            if (raw < lastRawTime)
            {
                raw = lastRawTime;
            }

            lastRawTime = raw;
            RawTime = raw;

            GameSettings settings = GameSettings.Instance;
            double offsetSeconds = (settings.rhythmOffsetMs + settings.audioLatencyMs) / 1000.0;

            SongTime = raw - offsetSeconds;
        }
    }
}
