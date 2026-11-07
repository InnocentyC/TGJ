using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 对话 CSV 解析器（纯静态工具，不挂在物体上）。
///
/// CSV 约定（与策划表格一致）：
///   NPCName, SpeakerName, Dialogue, PuzzlePiece, SceneNumber
///   PuzzlePiece：这句说完后获得的碎片编号，空 = 不获得
///   SceneNumber：这句说完后切换到的场景编号，空 = 不切换
///   第一行为表头（可关）；字段里带逗号或换行就用双引号包起来，引号内写两个引号表示一个引号
///   行首是 # 或 // 的行当注释跳过；空行跳过
/// </summary>
public static class DialogueCsvParser
{
    /// <summary>碎片编号解析不出来/留空时的兜底值（-1 = 不获得碎片）</summary>
    public const int DefaultPuzzlePiece = -1;

    /// <summary>场景编号留空时的兜底值（0 = 不切换场景）</summary>
    public const int DefaultSceneNumber = 0;

    /// <summary>
    /// 把 CSV 文本解析成对白列表。
    /// </summary>
    public static List<DialogueLine> Parse(
        string text,
        bool hasHeaderRow = true,
        char delimiter = ',',
        int npcColumn = 0,
        int speakerColumn = 1,
        int contentColumn = 2,
        int puzzleColumn = 3,
        int sceneColumn = 4)
    {
        List<DialogueLine> result = new List<DialogueLine>();
        List<string[]> rows = ParseRows(text, delimiter);

        for (int i = 0; i < rows.Count; i++)
        {
            string[] row = rows[i];

            // 跳过表头
            if (hasHeaderRow && i == 0)
            {
                continue;
            }

            if (IsBlankRow(row))
            {
                continue;
            }

            string firstCell = row.Length > 0 ? row[0].Trim() : "";

            // 注释行
            if (firstCell.StartsWith("#") || firstCell.StartsWith("//"))
            {
                continue;
            }

            string npcName = GetCell(row, npcColumn).Trim();
            string speaker = GetCell(row, speakerColumn).Trim();
            string content = NormalizeContent(GetCell(row, contentColumn));
            int puzzlePiece = ParseInt(GetCell(row, puzzleColumn), DefaultPuzzlePiece, "碎片编号", i + 1);
            int sceneNumber = ParseInt(GetCell(row, sceneColumn), DefaultSceneNumber, "场景编号", i + 1);

            result.Add(new DialogueLine(npcName, speaker, content, puzzlePiece, sceneNumber));
        }

        return result;
    }

    /// <summary>把 CSV 文本切成「行 -> 列」的二维数组，支持引号包裹与 "" 转义</summary>
    public static List<string[]> ParseRows(string text, char delimiter)
    {
        List<string[]> rows = new List<string[]>();

        if (string.IsNullOrEmpty(text))
        {
            return rows;
        }

        // 去掉 UTF-8 BOM，否则第一列的列名会带一个看不见的字符
        if (text[0] == '\uFEFF')
        {
            text = text.Substring(1);
        }

        List<string> fields = new List<string>();
        StringBuilder cell = new StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < text.Length)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // 两个连续引号 = 一个引号
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                cell.Append(c);
                i++;
                continue;
            }

            if (c == '"')
            {
                // 只有字段开头的引号才表示「这段被引号包起来了」；
                // 字段中间的引号按普通字符处理，避免说明文字里一个落单的引号把后面整段吞掉
                inQuotes = cell.Length == 0;

                if (!inQuotes)
                {
                    cell.Append(c);
                }

                i++;
                continue;
            }

            if (c == delimiter)
            {
                fields.Add(cell.ToString());
                cell.Length = 0;
                i++;
                continue;
            }

            if (c == '\r')
            {
                i++;
                continue;
            }

            if (c == '\n')
            {
                fields.Add(cell.ToString());
                cell.Length = 0;
                rows.Add(fields.ToArray());
                fields.Clear();
                i++;
                continue;
            }

            cell.Append(c);
            i++;
        }

        // 收尾：文件最后一行没有换行符时补一行
        if (cell.Length > 0 || fields.Count > 0)
        {
            fields.Add(cell.ToString());
            rows.Add(fields.ToArray());
        }

        if (inQuotes)
        {
            Debug.LogWarning("对话 CSV 里有没闭合的引号，后面的行可能被当成同一句，检查一下");
        }

        return rows;
    }

    /// <summary>取某一列，越界返回空串</summary>
    private static string GetCell(string[] row, int column)
    {
        if (column < 0 || row == null || column >= row.Length)
        {
            return "";
        }

        return row[column];
    }

    /// <summary>整行都是空的（比如文件末尾多打了个回车）</summary>
    private static bool IsBlankRow(string[] row)
    {
        if (row == null)
        {
            return true;
        }

        for (int i = 0; i < row.Length; i++)
        {
            if (!string.IsNullOrEmpty(row[i].Trim()))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>内容整理：去掉首尾空白，把字面量 \n 变成真换行</summary>
    private static string NormalizeContent(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return "";
        }

        string content = raw.Trim();
        content = content.Replace("\\n", "\n");

        return content;
    }

    /// <summary>数字列解析，失败就用 fallback 并打一条警告</summary>
    private static int ParseInt(string raw, int fallback, string columnName, int rowNumber)
    {
        if (string.IsNullOrEmpty(raw) || string.IsNullOrEmpty(raw.Trim()))
        {
            return fallback;
        }

        int value;

        if (int.TryParse(raw.Trim(), out value))
        {
            return value;
        }

        // 兼容 Excel 导出时整数被写成 1.0 的情况
        float f;
        if (float.TryParse(raw.Trim(), out f) && f == (int)f)
        {
            return (int)f;
        }

        Debug.LogWarning(string.Format("对话 CSV 第 {0} 行的{1}「{2}」不是数字，已按 {3} 处理", rowNumber, columnName, raw, fallback));
        return fallback;
    }
}
