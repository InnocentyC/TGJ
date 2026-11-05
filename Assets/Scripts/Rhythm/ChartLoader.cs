using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Rhythm
{
    /// <summary>
    /// 谱面 CSV 加载器。
    /// 格式约定（见 Resources/Chart/Chart_Act1.csv）：
    ///   每行 "时间,轨道,音高"；# 开头是注释；空行跳过；时间单位秒，轨道/音高从 0 起。
    ///   音高 = 打击音编号（Inspector 打击音队列的下标），与轨道无绑定。
    /// 非法行打 warning 跳过，读完后按时间排序。
    /// </summary>
    public static class ChartLoader
    {
        /// <summary>
        /// 从 Resources 读取谱面。resourcePath 不含扩展名，例如 "Chart/Chart_Act1"。
        /// </summary>
        public static List<NoteData> Load(string resourcePath)
        {
            List<NoteData> notes = new List<NoteData>();

            if (string.IsNullOrEmpty(resourcePath))
            {
                Debug.LogError("[ChartLoader] 谱面路径为空");
                return notes;
            }

            TextAsset asset = Resources.Load<TextAsset>(resourcePath);

            if (asset == null)
            {
                Debug.LogError("[ChartLoader] 找不到谱面文件：Resources/" + resourcePath);
                return notes;
            }

            string[] lines = asset.text.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // 去掉可能的 UTF-8 BOM
                if (line.Length > 0 && line[0] == '\uFEFF')
                {
                    line = line.Substring(1).Trim();
                }

                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                {
                    continue;
                }

                string[] parts = line.Split(',');

                if (parts.Length < 3)
                {
                    Debug.LogWarning("[ChartLoader] 第 " + (i + 1) + " 行格式不对（应为 时间,轨道,音高），已跳过：" + line);
                    continue;
                }

                double hitTime;
                int lane;
                int pitch;

                // 必须用 InvariantCulture，否则某些系统区域设置下小数点解析会出问题
                if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out hitTime))
                {
                    Debug.LogWarning("[ChartLoader] 第 " + (i + 1) + " 行时间无法解析，已跳过：" + line);
                    continue;
                }

                if (!int.TryParse(parts[1].Trim(), out lane))
                {
                    Debug.LogWarning("[ChartLoader] 第 " + (i + 1) + " 行轨道无法解析，已跳过：" + line);
                    continue;
                }

                if (!int.TryParse(parts[2].Trim(), out pitch))
                {
                    Debug.LogWarning("[ChartLoader] 第 " + (i + 1) + " 行音高无法解析，已跳过：" + line);
                    continue;
                }

                if (lane < 0 || lane >= GameSettings.LaneCount)
                {
                    Debug.LogWarning("[ChartLoader] 第 " + (i + 1) + " 行轨道号越界（" + lane + "），已跳过：" + line);
                    continue;
                }

                if (pitch < 0)
                {
                    Debug.LogWarning("[ChartLoader] 第 " + (i + 1) + " 行音高为负，已跳过：" + line);
                    continue;
                }

                if (hitTime < 0)
                {
                    Debug.LogWarning("[ChartLoader] 第 " + (i + 1) + " 行时间为负，已跳过：" + line);
                    continue;
                }

                notes.Add(new NoteData(hitTime, lane, pitch));
            }

            // 谱面没排序也能用：这里统一按时间从小到大排
            notes.Sort((a, b) => a.hitTime.CompareTo(b.hitTime));

            Debug.Log("[ChartLoader] 载入谱面 " + resourcePath + "，共 " + notes.Count + " 个音符");
            return notes;
        }
    }
}
