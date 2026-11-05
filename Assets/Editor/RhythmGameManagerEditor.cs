using UnityEditor;
using UnityEngine;

namespace Rhythm
{
    /// <summary>
    /// RhythmGameManager 的 Inspector 增强：顶部显示章节配置摘要。
    /// 只做显示，不改任何数据；不碰 GameProgressManager（避免编辑模式下误建单例）。
    /// </summary>
    [CustomEditor(typeof(RhythmGameManager))]
    public class RhythmGameManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RhythmGameManager mgr = (RhythmGameManager)target;

            int count = mgr.chapters != null ? mgr.chapters.Count : 0;

            EditorGUILayout.HelpBox(
                "共配置 " + count + " 章。\n" +
                "运行时：第 1 幕自动用第 1 项，第 2 幕用第 2 项……\n" +
                "直接打开本场景测试时用第 " + (mgr.sceneTestChapterIndex + 1) + " 项（sceneTestChapterIndex）。",
                MessageType.Info);

            // 配置项数量和章节数对不上时给个醒目提示
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    ChapterSetup chapter = mgr.chapters[i];

                    if (chapter == null)
                    {
                        EditorGUILayout.HelpBox("第 " + (i + 1) + " 章配置为空（列表里有空项）", MessageType.Error);
                        continue;
                    }

                    int clipCount = chapter.keySoundClips != null ? chapter.keySoundClips.Count : 0;

                    if (string.IsNullOrEmpty(chapter.chartResourcePath) && clipCount == 0 && chapter.musicClip == null)
                    {
                        EditorGUILayout.HelpBox("第 " + (i + 1) + " 章什么都没配（谱面留空会用 Chart/Chart_Act" + (i + 1) + "，音乐/打击音缺失会静音或用合成音）", MessageType.Warning);
                    }
                }
            }

            DrawDefaultInspector();
        }
    }
}
