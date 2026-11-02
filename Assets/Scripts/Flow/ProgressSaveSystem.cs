using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 进度存档的落盘读写。只负责"把 ProgressSaveData 存到磁盘 / 从磁盘读出来"，
/// 不关心流程逻辑。文件放在 Application.persistentDataPath 下，退出游戏不会丢。
/// </summary>
public static class ProgressSaveSystem
{
    private const string FileName = "progress.json";

    /// <summary>存档文件完整路径</summary>
    public static string SavePath
    {
        get { return Path.Combine(Application.persistentDataPath, FileName); }
    }

    /// <summary>是否存在存档文件</summary>
    public static bool Exists
    {
        get { return File.Exists(SavePath); }
    }

    /// <summary>写入存档</summary>
    public static void Save(ProgressSaveData data)
    {
        if (data == null)
        {
            return;
        }

        try
        {
            data.version = ProgressSaveData.CurrentVersion;
            data.savedAtBinary = DateTime.Now.ToBinary();
            data.savedAtText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ProgressSaveSystem] 保存进度失败：" + e.Message);
        }
    }

    /// <summary>
    /// 读取存档。没有文件、文件损坏、版本对不上都返回 null（由调用方当"无存档"处理）。
    /// </summary>
    public static ProgressSaveData Load()
    {
        try
        {
            if (!File.Exists(SavePath))
            {
                return null;
            }

            ProgressSaveData data = JsonUtility.FromJson<ProgressSaveData>(File.ReadAllText(SavePath));

            if (data == null || data.version != ProgressSaveData.CurrentVersion || !data.hasSave)
            {
                return null;
            }

            data.EnsureChapters();
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ProgressSaveSystem] 读取进度失败，按无存档处理：" + e.Message);
            return null;
        }
    }

    /// <summary>删档（主菜单的"清除存档"用）</summary>
    public static void Delete()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ProgressSaveSystem] 删除进度失败：" + e.Message);
        }
    }
}
