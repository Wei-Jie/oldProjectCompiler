using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace EEPCompiler
{
    /// <summary>
    /// 訊光 EEP 全系統原始碼索引器 (解決剛接手舊系統大海撈針問題)
    /// 自動掃描所有 Client / Server 專案 (自動相容 Big5 與 UTF-8 讀取)，
    /// 擷取：畫面中文標題、Client->Server 呼叫關聯 (RemoteName / CallMethod)、資料表 (SQL Tables) 與後端商業方法，
    /// 產出 EEP_SYSTEM_MAP.md 供開發者與 Claude Code 秒速定位檔案！
    /// </summary>
    public class SystemIndexer
    {
        public class ProjectIndexEntry
        {
            public string ProjectName { get; set; }
            public string RelativePath { get; set; }
            public string Role { get; set; }
            public List<string> FormTitles { get; set; }
            public List<string> RemoteLinks { get; set; }
            public List<string> CalledServerMethods { get; set; }
            public List<string> SqlTables { get; set; }
            public List<string> ServerMethods { get; set; }
            public List<string> KeyButtonsAndLabels { get; set; }

            public ProjectIndexEntry()
            {
                ProjectName = "";
                RelativePath = "";
                Role = "Common";
                FormTitles = new List<string>();
                RemoteLinks = new List<string>();
                CalledServerMethods = new List<string>();
                SqlTables = new List<string>();
                ServerMethods = new List<string>();
                KeyButtonsAndLabels = new List<string>();
            }
        }

        // 擷取 this.Text = "中文標題";
        private static readonly Regex FormTextRegex = new Regex(
            @"this\.Text\s*=\s*""([^""]{2,60})""", RegexOptions.Compiled);

        // 擷取控制項 Text，例如 this.btnSubmit.Text = "確認送出";
        private static readonly Regex ControlTextRegex = new Regex(
            @"this\.(?:btn|button|lbl|label|tab|tp|gb|groupBox|chk|col)[A-Za-z0-9_]*\.Text\s*=\s*""([^""]{2,30})""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // 擷取 InfoDataSet 的 RemoteName = "SOrder.cmdOrders"
        private static readonly Regex RemoteNameRegex = new Regex(
            @"\.RemoteName\s*=\s*""([^""]+)""", RegexOptions.Compiled);

        // 擷取 CLIUtils.CallMethod("SOrder", "CheckAmount", ...)
        private static readonly Regex CallMethodRegex = new Regex(
            @"CallMethod\s*\(\s*""([^""]+)""\s*,\s*""([^""]+)""", RegexOptions.Compiled);

        // 擷取 Server 端公開方法 public object[] MethodName(object[] ...)
        private static readonly Regex ServerMethodDeclRegex = new Regex(
            @"public\s+object\s*\[\s*\]\s+([A-Za-z0-9_]+)\s*\(\s*object\s*\[\s*\]", RegexOptions.Compiled);

        // 擷取 SQL 中的資料表名稱 (FROM / JOIN / UPDATE / INSERT INTO)
        private static readonly Regex SqlTableRegex = new Regex(
            @"\b(?:FROM|JOIN|UPDATE|INTO)\s+(?:\[?[A-Za-z0-9_]+\]?\.)?\[?([A-Za-z_][A-Za-z0-9_]{2,40})\]?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> SqlIgnoredWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "WHERE", "SET", "VALUES", "AND", "OR", "NULL", "NOT", "IN", "AS", "ON", "LEFT", "RIGHT", "INNER", "OUTER", "TABLE", " Dual"
        };

        /// <summary>
        /// 掃描指定根目錄下的所有 EEP 專案，產出 EEP_SYSTEM_MAP.md 索引文件
        /// </summary>
        public static string GenerateSystemMap(string rootDir, out string outputMapFilePath)
        {
            string fullRoot = File.Exists(rootDir) ? Path.GetDirectoryName(Path.GetFullPath(rootDir)) : Path.GetFullPath(rootDir);
            outputMapFilePath = Path.Combine(fullRoot, "EEP_SYSTEM_MAP.md");

            string[] csprojs = Directory.GetFiles(fullRoot, "*.csproj", SearchOption.AllDirectories);
            List<ProjectIndexEntry> entries = new List<ProjectIndexEntry>();

            if (csprojs.Length > 0)
            {
                foreach (string projFile in csprojs)
                {
                    if (projFile.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    entries.Add(ScanSingleProject(fullRoot, Path.GetDirectoryName(projFile), Path.GetFileNameWithoutExtension(projFile)));
                }
            }
            else
            {
                entries.Add(ScanSingleProject(fullRoot, fullRoot, Path.GetFileName(fullRoot)));
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# 訊光 EEP 全系統業務與程式碼導覽地圖 (`EEP_SYSTEM_MAP.md`)");
            sb.AppendLine();
            sb.AppendLine("> **給 AI Agent (Claude Code) 與開發者的優先指令**：");
            sb.AppendLine("> 當使用者詢問某個畫面、功能、資料表或錯誤訊息時，**請務必先查閱本索引檔**，直接定位對應的 **Client 專案 (`C...`)** 與 **Server 專案 (`S...`)**，切勿盲目對全系統幾千個檔案進行大海撈針掃描！");
            sb.AppendLine();
            sb.AppendFormat("- **掃描根目錄**: `{0}`\r\n", fullRoot);
            sb.AppendFormat("- **收錄專案總數**: {0} 個專案\r\n", entries.Count);
            sb.AppendFormat("- **產生時間**: {0}\r\n\r\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            // 1. 總覽快速查找表
            sb.AppendLine("## 一、全系統作業快速對照總表 (Client <-> Server <-> 資料表)");
            sb.AppendLine();
            sb.AppendLine("| 專案代號 | 角色 | 畫面標題 / 功能名稱 | 關聯 Server 模組 (`RemoteName` / `CallMethod`) | 讀寫資料表 (SQL Tables) | 專案相對路徑 |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- |");

            foreach (ProjectIndexEntry e in entries)
            {
                string titles = e.FormTitles.Count > 0 ? string.Join(", ", e.FormTitles.ToArray()) : "-";
                List<string> allServerLinks = new List<string>();
                allServerLinks.AddRange(e.RemoteLinks);
                allServerLinks.AddRange(e.CalledServerMethods);
                string links = allServerLinks.Count > 0 ? string.Join("<br/>", allServerLinks.ToArray()) : "-";
                string tables = e.SqlTables.Count > 0 ? string.Join(", ", e.SqlTables.ToArray()) : "-";

                sb.AppendFormat("| **`{0}`** | {1} | {2} | {3} | `{4}` | `{5}` |\r\n",
                    e.ProjectName, e.Role, EscapeMd(titles), EscapeMd(links), tables, e.RelativePath);
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 二、各專案詳細商業邏輯與按鈕關鍵字索引");
            sb.AppendLine();

            foreach (ProjectIndexEntry e in entries)
            {
                sb.AppendFormat("### 📦 `{0}` ({1}) - 路徑: `{2}`\r\n", e.ProjectName, e.Role, e.RelativePath);
                if (e.FormTitles.Count > 0)
                    sb.AppendFormat("- **畫面中文標題**: {0}\r\n", string.Join(" / ", e.FormTitles.ToArray()));
                if (e.KeyButtonsAndLabels.Count > 0)
                    sb.AppendFormat("- **畫面按鈕與標籤關鍵字**: {0}\r\n", string.Join(", ", e.KeyButtonsAndLabels.ToArray()));
                if (e.RemoteLinks.Count > 0)
                    sb.AppendFormat("- **綁定資料來源 (`InfoDataSet.RemoteName`)**: `{0}`\r\n", string.Join("`, `", e.RemoteLinks.ToArray()));
                if (e.CalledServerMethods.Count > 0)
                    sb.AppendFormat("- **呼叫後端方法 (`CLIUtils.CallMethod`)**: `{0}`\r\n", string.Join("`, `", e.CalledServerMethods.ToArray()));
                if (e.ServerMethods.Count > 0)
                    sb.AppendFormat("- **提供之後端商業邏輯方法 (`Server Methods`)**: `{0}`\r\n", string.Join("`, `", e.ServerMethods.ToArray()));
                if (e.SqlTables.Count > 0)
                    sb.AppendFormat("- **關聯資料庫表格 (`SQL Tables`)**: `{0}`\r\n", string.Join("`, `", e.SqlTables.ToArray()));
                sb.AppendLine();
            }

            string report = sb.ToString();
            File.WriteAllText(outputMapFilePath, report, new UTF8Encoding(true));
            return report;
        }

        private static ProjectIndexEntry ScanSingleProject(string rootDir, string projDir, string projName)
        {
            ProjectIndexEntry entry = new ProjectIndexEntry();
            entry.ProjectName = projName;
            entry.RelativePath = ReferenceResolver.GetRelativePath(rootDir, projDir);

            string[] files = Directory.GetFiles(projDir, "*.*", SearchOption.AllDirectories);
            foreach (string f in files)
            {
                if (f.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".cs" && ext != ".resx") continue;

                string content = ReadFileSmartEncoding(f);
                if (string.IsNullOrEmpty(content)) continue;

                if (content.Contains(": InfoForm") || content.Contains(": Srvtools.InfoForm"))
                {
                    entry.Role = "Client (前端畫面)";
                }
                else if (content.Contains(": DataModule") || content.Contains(": Srvtools.DataModule") ||
                         content.Contains(": InfoDataModule") || content.Contains(": Srvtools.InfoDataModule") ||
                         content.Contains("Srvtools.UpdateComponent"))
                {
                    entry.Role = "Server (後端模組)";
                }

                // 1. 擷取畫面標題
                foreach (Match m in FormTextRegex.Matches(content))
                {
                    AddUnique(entry.FormTitles, m.Groups[1].Value.Trim(), 10);
                }

                // 2. 擷取按鈕或標籤關鍵字
                foreach (Match m in ControlTextRegex.Matches(content))
                {
                    string txt = m.Groups[1].Value.Trim();
                    if (!txt.StartsWith("label", StringComparison.OrdinalIgnoreCase) &&
                        !txt.StartsWith("button", StringComparison.OrdinalIgnoreCase))
                    {
                        AddUnique(entry.KeyButtonsAndLabels, txt, 20);
                    }
                }

                // 3. 擷取 RemoteName
                foreach (Match m in RemoteNameRegex.Matches(content))
                {
                    AddUnique(entry.RemoteLinks, m.Groups[1].Value.Trim(), 15);
                }

                // 4. 擷取 CallMethod
                foreach (Match m in CallMethodRegex.Matches(content))
                {
                    string callStr = m.Groups[1].Value.Trim() + "." + m.Groups[2].Value.Trim() + "()";
                    AddUnique(entry.CalledServerMethods, callStr, 20);
                }

                // 5. 擷取 Server 公開商業方法
                foreach (Match m in ServerMethodDeclRegex.Matches(content))
                {
                    AddUnique(entry.ServerMethods, m.Groups[1].Value.Trim() + "()", 30);
                }

                // 6. 擷取 SQL Table 名稱 (僅在包含 SELECT / UPDATE / INSERT INTO 的區塊分析)
                if (content.IndexOf("SELECT ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    content.IndexOf("UPDATE ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    content.IndexOf("INSERT INTO", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (Match m in SqlTableRegex.Matches(content))
                    {
                        string tbl = m.Groups[1].Value.Trim();
                        if (!SqlIgnoredWords.Contains(tbl) && tbl.Length >= 3)
                        {
                            AddUnique(entry.SqlTables, tbl, 25);
                        }
                    }
                }
            }

            return entry;
        }

        /// <summary>
        /// 智慧讀取檔案內容（自動相容 UTF-8 BOM、無 BOM UTF-8 與 Big5，不會改動原檔案）
        /// </summary>
        private static string ReadFileSmartEncoding(string filePath)
        {
            try
            {
                byte[] raw = File.ReadAllBytes(filePath);
                if (raw.Length == 0) return "";
                if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
                {
                    return Encoding.UTF8.GetString(raw, 3, raw.Length - 3);
                }
                try
                {
                    UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
                    return strictUtf8.GetString(raw);
                }
                catch
                {
                    return Encoding.GetEncoding(950).GetString(raw);
                }
            }
            catch
            {
                return "";
            }
        }

        private static void AddUnique(List<string> list, string val, int maxCount)
        {
            if (string.IsNullOrEmpty(val) || list.Count >= maxCount) return;
            if (!list.Contains(val)) list.Add(val);
        }

        private static string EscapeMd(string text)
        {
            if (string.IsNullOrEmpty(text)) return "-";
            return text.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
        }
    }
}
