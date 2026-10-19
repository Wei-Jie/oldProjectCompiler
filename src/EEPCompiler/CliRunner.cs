using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EEPCompiler
{
    /// <summary>
    /// 負責處理 CLI 命令列參數解析、執行對應指令（build / repair / check / env）與輸出 Claude Code 友善報告
    /// </summary>
    public class CliRunner
    {
        public static int Run(string[] args)
        {
            // 強制設定 Console 輸出為 UTF-8，確保 Claude Code 與現代終端機不會讀到 Big5 亂碼
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch { }

            if (args == null || args.Length == 0 || IsHelpArg(args[0]))
            {
                PrintUsage();
                return 0;
            }

            string command = args[0].ToLowerInvariant();
            if (command != "build" && command != "repair" && command != "check" && command != "env" &&
                command != "to-utf8" && command != "to-big5" && command != "index")
            {
                // 若第一個參數直接是檔案或資料夾路徑，預設視為 build 指令
                if (File.Exists(args[0]) || Directory.Exists(args[0]))
                {
                    List<string> shifted = new List<string>();
                    shifted.Add("build");
                    shifted.AddRange(args);
                    args = shifted.ToArray();
                    command = "build";
                }
                else
                {
                    Console.WriteLine("[錯誤] 未知的指令或找不到的路徑: " + args[0]);
                    PrintUsage();
                    return 2;
                }
            }

            if (command == "env")
            {
                return RunEnvCheck();
            }

            CompilerOptions options = ParseOptions(args);
            if (string.IsNullOrEmpty(options.InputPath))
            {
                Console.WriteLine("[錯誤] 請指定專案或檔案路徑 (.cs / .sln / .csproj / 資料夾)。");
                PrintUsage();
                return 2;
            }

            if (!File.Exists(options.InputPath) && !Directory.Exists(options.InputPath))
            {
                Console.WriteLine("[錯誤] 找不到指定的目標路徑: " + options.InputPath);
                return 2;
            }

            if (command == "index")
            {
                string mapFile;
                SystemIndexer.GenerateSystemMap(options.InputPath, out mapFile);
                Console.WriteLine("======================================================================");
                Console.WriteLine("✔ 全系統業務與程式碼導覽地圖已產生完成！");
                Console.WriteLine("📄 索引檔位置: " + mapFile);
                Console.WriteLine("👉 請讓 Claude Code 先讀取此檔案，即可秒速定位畫面對應的 Client/Server 專案與資料表！");
                Console.WriteLine("======================================================================");
                return 0;
            }

            if (command == "to-utf8" || command == "to-big5")
            {
                return RunEncodingConvert(options.InputPath, command == "to-utf8");
            }

            BuildEngine engine = new BuildEngine();
            List<string> projectPaths = engine.Projects.DiscoverProjects(options.InputPath);
            if (projectPaths.Count == 0)
            {
                Console.WriteLine("[錯誤] 在指定路徑下找不到任何可編譯的 .csproj 或 C# 原始碼: " + options.InputPath);
                return 2;
            }

            if (command == "repair")
            {
                return RunRepairOnly(engine, projectPaths, options);
            }
            else if (command == "check")
            {
                return RunSyntaxCheckOnly(engine, projectPaths, options);
            }
            else
            {
                return RunBuild(engine, projectPaths, options);
            }
        }

        /// <summary>
        /// 雙向編碼轉換：支援單一 .cs 檔案或整個專案資料夾在 Big5 (CP950) 與 UTF-8 with BOM 之間無損互轉
        /// </summary>
        private static int RunEncodingConvert(string targetPath, bool toUtf8Bom)
        {
            List<string> csFiles = new List<string>();
            if (File.Exists(targetPath))
            {
                if (targetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    csFiles.Add(Path.GetFullPath(targetPath));
                }
                else
                {
                    string dir = Path.GetDirectoryName(Path.GetFullPath(targetPath));
                    csFiles.AddRange(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories));
                }
            }
            else if (Directory.Exists(targetPath))
            {
                csFiles.AddRange(Directory.GetFiles(Path.GetFullPath(targetPath), "*.cs", SearchOption.AllDirectories));
            }

            UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
            UTF8Encoding utf8WithBom = new UTF8Encoding(true);
            Encoding big5 = Encoding.GetEncoding(950);
            int convertedCount = 0;

            foreach (string file in csFiles)
            {
                if (file.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                try
                {
                    byte[] raw = File.ReadAllBytes(file);
                    if (raw.Length == 0) continue;

                    bool hasBom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
                    string content;
                    bool isCurrentlyUtf8 = false;

                    if (hasBom)
                    {
                        content = utf8WithBom.GetString(raw, 3, raw.Length - 3);
                        isCurrentlyUtf8 = true;
                    }
                    else
                    {
                        try
                        {
                            content = strictUtf8.GetString(raw);
                            isCurrentlyUtf8 = true;
                        }
                        catch
                        {
                            content = big5.GetString(raw);
                            isCurrentlyUtf8 = false;
                        }
                    }

                    if (toUtf8Bom)
                    {
                        // 目標：轉為 UTF-8 with BOM (供 Claude Code 正常讀寫與編譯)
                        if (!hasBom || !isCurrentlyUtf8)
                        {
                            File.WriteAllText(file, content, utf8WithBom);
                            convertedCount++;
                            Console.WriteLine(string.Format("[已轉為 UTF-8 with BOM] {0}", file));
                        }
                    }
                    else
                    {
                        // 目標：轉回 Big5 (CP950)
                        if (isCurrentlyUtf8 || hasBom)
                        {
                            File.WriteAllText(file, content, big5);
                            convertedCount++;
                            Console.WriteLine(string.Format("[已轉回 Big5 (CP950)] {0}", file));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("[轉碼失敗] {0}: {1}", file, ex.Message));
                }
            }

            Console.WriteLine(string.Format("編碼轉換完成，共轉換 {0} 個 .cs 檔案。", convertedCount));
            return 0;
        }

        private static int RunEnvCheck()
        {
            Console.WriteLine("=== Infolight EEP / VS2005 可攜式編譯環境檢測 ===");
            List<string> msbuilds = ReferenceResolver.FindAvailableMSBuilds();
            Console.WriteLine("1. 本機可用的 MSBuild.exe 數量: " + msbuilds.Count);
            foreach (string msb in msbuilds)
            {
                Console.WriteLine("   - " + msb);
            }

            string csc20 = ReferenceResolver.FindBestCscCompiler(TargetClrVersion.Net20);
            string csc40 = ReferenceResolver.FindBestCscCompiler(TargetClrVersion.Net40);
            Console.WriteLine("2. C# 編譯器 (csc.exe): " + (string.IsNullOrEmpty(csc40) ? "未找到" : csc40));
            Console.WriteLine("3. .NET 2.0 核心組件 (v2.0.50727\\mscorlib.dll): " +
                (File.Exists(@"C:\Windows\Microsoft.NET\Framework\v2.0.50727\mscorlib.dll") ? "已安裝 (支援 CLR v2.0 跨編譯)" : "未找到"));
            Console.WriteLine("4. .NET 4.0 核心組件 (v4.0.30319\\mscorlib.dll): " +
                (File.Exists(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319\mscorlib.dll") ? "已安裝" : "未找到"));
            Console.WriteLine("5. 內建免 SDK .resx 資源編譯器: 就緒 (無須外部 ResGen.exe / LC.exe)");
            return 0;
        }

        private static int RunRepairOnly(BuildEngine engine, List<string> projectPaths, CompilerOptions options)
        {
            int totalRepairs = 0;
            foreach (string projPath in projectPaths)
            {
                EepProjectInfo info = engine.Projects.LoadProject(projPath, options);
                List<RepairRecord> records = engine.Projects.RepairProject(info, options);
                totalRepairs += records.Count;

                Console.WriteLine(string.Format("=== 專案修復報告: {0} ({1}) ===", info.ProjectName, info.EepRole));
                if (records.Count == 0)
                {
                    Console.WriteLine("  [狀態] 所有設定正常，無須修復。");
                }
                else
                {
                    foreach (RepairRecord r in records)
                    {
                        Console.WriteLine(string.Format("  [{0}] {1}", r.RuleId, r.Description));
                    }
                }

                foreach (ReferenceItem ri in info.References)
                {
                    if (!ri.IsResolved && !ri.IsSystemAssembly)
                    {
                        Console.WriteLine(string.Format("  [警告 - 規則 2] 仍找不到組件 '{0}.dll'，請使用 --eep-root 指定 EEP 目錄。", ri.ShortName));
                    }
                }

                List<string> notices = new List<string>();
                BuildEngine.AddReferenceNotices(info, notices);
                foreach (string n in notices)
                {
                    Console.WriteLine("  " + n);
                }
            }
            Console.WriteLine(string.Format("修復完成，共執行 {0} 項自動調整。", totalRepairs));
            return 0;
        }

        private static int RunSyntaxCheckOnly(BuildEngine engine, List<string> projectPaths, CompilerOptions options)
        {
            int errorCount = 0;
            foreach (string projPath in projectPaths)
            {
                EepProjectInfo info = engine.Projects.LoadProject(projPath, options);
                List<SyntaxIssue> issues = engine.Analyzer.AnalyzeProject(info, true);
                Console.WriteLine(string.Format("=== 專案語法預檢 (規則 5): {0} ===", info.ProjectName));
                if (issues.Count == 0)
                {
                    Console.WriteLine("  [通過] 未發現違反 C# 5.0 / 7.3 或 EEP Designer 規範的語法。");
                }
                else
                {
                    foreach (SyntaxIssue si in issues)
                    {
                        if (si.IsError) errorCount++;
                        Console.WriteLine(string.Format("  [{0}] {1}({2}): {3}",
                            si.RuleCode, si.FilePath, si.LineNumber, si.Message));
                        Console.WriteLine("     程式碼: " + si.LinePreview);
                        Console.WriteLine("     修改建議: " + si.Suggestion);
                    }
                }
            }
            return errorCount > 0 ? 1 : 0;
        }

        private static int RunBuild(BuildEngine engine, List<string> projectPaths, CompilerOptions options)
        {
            List<ProjectBuildResult> allResults = new List<ProjectBuildResult>();
            bool allSuccess = true;

            foreach (string projPath in projectPaths)
            {
                ProjectBuildResult res = engine.ExecuteBuild(projPath, options);
                allResults.Add(res);
                if (!res.Success) allSuccess = false;
            }

            if (options.ClaudeMode)
            {
                Console.WriteLine(FormatClaudeReport(allResults));
            }
            else
            {
                foreach (ProjectBuildResult res in allResults)
                {
                    PrintStandardResult(res);
                }
            }

            return allSuccess ? 0 : 1;
        }

        public static void PrintStandardResult(ProjectBuildResult res)
        {
            string projTitle = res.Project != null ? res.Project.ProjectName : "Unknown";
            Console.WriteLine("============================================================");
            Console.WriteLine(string.Format("專案名稱: {0} ({1})", projTitle, res.Project != null ? res.Project.EepRole : ""));
            Console.WriteLine(string.Format("編譯狀態: {0} (耗時 {1:F2} 秒)", res.Success ? "SUCCESS (編譯成功)" : "FAILED (編譯失敗)", res.DurationSeconds));
            Console.WriteLine("使用引擎: " + res.EngineUsed);

            if (res.Success)
            {
                Console.WriteLine("輸出檔案: " + res.OutputArtifactPath);
                Console.WriteLine("目標 CLR: " + res.TargetRuntimeMetadata);
            }

            if (res.Repairs.Count > 0)
            {
                Console.WriteLine("--- 自動修復紀錄 (規則 2~4) ---");
                foreach (RepairRecord r in res.Repairs)
                {
                    Console.WriteLine(string.Format("  * [{0}] {1}", r.RuleId, r.Description));
                }
            }

            if (res.SyntaxIssues.Count > 0)
            {
                Console.WriteLine("--- 語法降級預檢提醒 (規則 5) ---");
                foreach (SyntaxIssue si in res.SyntaxIssues)
                {
                    Console.WriteLine(string.Format("  * [{0}] {1}({2}): {3}", si.RuleCode, si.FilePath, si.LineNumber, si.Message));
                    Console.WriteLine("    建議: " + si.Suggestion);
                }
            }

            if (res.Notices.Count > 0)
            {
                Console.WriteLine("--- 注意事項 ---");
                foreach (string n in res.Notices)
                {
                    Console.WriteLine("  * " + n);
                }
            }

            int errCount = 0;
            foreach (CompilerDiagnostic d in res.Diagnostics)
            {
                if (d.Severity == "Error") errCount++;
            }

            if (errCount > 0)
            {
                Console.WriteLine(string.Format("--- 編譯錯誤清單 (共 {0} 個錯誤) ---", errCount));
                foreach (CompilerDiagnostic d in res.Diagnostics)
                {
                    if (d.Severity != "Error") continue;
                    if (d.Line > 0)
                        Console.WriteLine(string.Format("  [ERROR {0}] {1}({2},{3}): {4}", d.Code, d.FilePath, d.Line, d.Column, d.Message));
                    else if (!string.IsNullOrEmpty(d.FilePath))
                        Console.WriteLine(string.Format("  [ERROR {0}] {1}: {2}", d.Code, d.FilePath, d.Message));
                    else
                        Console.WriteLine(string.Format("  [ERROR {0}] {1}", d.Code, d.Message));

                    if (!string.IsNullOrEmpty(d.AiHint))
                        Console.WriteLine("    -> 修復提示: " + d.AiHint);
                }
            }
            else if (!res.Success)
            {
                // 失敗但沒有解析出任何錯誤時，顯示原始輸出，避免錯誤原因被吞掉
                Console.WriteLine("--- 編譯失敗，但未解析出錯誤碼；以下為最後的原始輸出 ---");
                foreach (string line in GetLastLogLines(res, 30))
                {
                    Console.WriteLine("  " + line);
                }
            }
            Console.WriteLine("============================================================");
        }

        /// <summary>
        /// 產生專為 Claude Code / AI Agent 最佳化的 Markdown 結構化診斷報告
        /// </summary>
        public static string FormatClaudeReport(List<ProjectBuildResult> results)
        {
            StringBuilder sb = new StringBuilder();
            bool allOk = true;
            foreach (ProjectBuildResult r in results)
            {
                if (!r.Success) allOk = false;
            }

            sb.AppendLine("# EEP (.NET Framework / VS2005) 自動化編譯與診斷報告");
            sb.AppendFormat("- **整體狀態**: {0}\r\n", allOk ? "PASS (0 Error(s) - 編譯成功)" : "FAIL (編譯失敗，請依據下方 AI 修復指引修正程式碼)");
            sb.AppendFormat("- **處理專案數**: {0}\r\n\r\n", results.Count);

            foreach (ProjectBuildResult res in results)
            {
                string name = res.Project != null ? res.Project.ProjectName : "Project";
                sb.AppendFormat("## 專案：`{0}` ({1}) - {2}\r\n",
                    name,
                    res.Project != null ? res.Project.EepRole : "",
                    res.Success ? "SUCCESS" : "FAILED");

                sb.AppendFormat("- **編譯引擎**: `{0}`\r\n", res.EngineUsed);
                if (res.Success)
                {
                    sb.AppendFormat("- **輸出組件**: `{0}` (Runtime: `{1}`)\r\n", res.OutputArtifactPath, res.TargetRuntimeMetadata);
                }

                if (res.Repairs.Count > 0)
                {
                    sb.AppendLine("\r\n### 已自動執行的專案修復 (規則 2 ~ 規則 4)");
                    foreach (RepairRecord rep in res.Repairs)
                    {
                        sb.AppendFormat("- **[{0}]**: {1}\r\n", rep.RuleId, rep.Description);
                    }
                }

                if (res.Notices.Count > 0)
                {
                    sb.AppendLine("\r\n### 注意事項");
                    foreach (string n in res.Notices)
                    {
                        sb.AppendFormat("- {0}\r\n", n);
                    }
                }

                if (!res.Success || res.SyntaxIssues.Count > 0)
                {
                    if (res.SyntaxIssues.Count > 0)
                    {
                        sb.AppendLine("\r\n### 規則 5：C# 語法相容性與 Designer 檢查發現問題");
                        foreach (SyntaxIssue si in res.SyntaxIssues)
                        {
                            sb.AppendFormat("- **`{0}` (第 {1} 行)** [`{2}`]: {3}\r\n", si.FilePath, si.LineNumber, si.RuleCode, si.Message);
                            if (!string.IsNullOrEmpty(si.LinePreview))
                                sb.AppendFormat("  - 問題程式碼: `{0}`\r\n", si.LinePreview);
                            sb.AppendFormat("  - **必修建議**: {0}\r\n", si.Suggestion);
                        }
                    }

                    List<CompilerDiagnostic> errors = new List<CompilerDiagnostic>();
                    foreach (CompilerDiagnostic d in res.Diagnostics)
                    {
                        if (d.Severity == "Error") errors.Add(d);
                    }

                    if (errors.Count > 0)
                    {
                        sb.AppendFormat("\r\n### 編譯器錯誤明細 (共 {0} 項)\r\n", errors.Count);
                        foreach (CompilerDiagnostic err in errors)
                        {
                            if (err.Line > 0)
                                sb.AppendFormat("- **[`{0}`] `{1}` (第 {2} 行, 第 {3} 字元)**: {4}\r\n",
                                    err.Code, err.FilePath, err.Line, err.Column, err.Message);
                            else if (!string.IsNullOrEmpty(err.FilePath))
                                sb.AppendFormat("- **[`{0}`] `{1}`**: {2}\r\n",
                                    err.Code, err.FilePath, err.Message);
                            else
                                sb.AppendFormat("- **[`{0}`]**: {1}\r\n", err.Code, err.Message);

                            if (!string.IsNullOrEmpty(err.AiHint))
                                sb.AppendFormat("  - **AI 修復指引**: {0}\r\n", err.AiHint);
                        }
                    }
                    else if (!res.Success)
                    {
                        sb.AppendLine("\r\n### 編譯失敗，但未解析出錯誤碼（最後的原始輸出）");
                        sb.AppendLine("```");
                        foreach (string line in GetLastLogLines(res, 30))
                        {
                            sb.AppendLine(line);
                        }
                        sb.AppendLine("```");
                    }
                }
                sb.AppendLine();
            }

            if (!allOk)
            {
                sb.AppendLine("---");
                sb.AppendLine("### 給 AI Agent (Claude Code) 的下一步行動指令：");
                sb.AppendLine("1. 先依上方「編譯器錯誤明細」或原始輸出判斷原因：參考組件或資源錯誤 (如 CS0246 找不到 Srvtools、CS1508 資源重複) 請修正參考與 `--eep-root`，不要修改 `.cs`；只有確定是語法錯誤時，才將現代 C# 語法（如 `$\"...\"`, `?.`, `nameof`, `using var`, `new()`, File-scoped namespace）降級為 C# 5.0 / VS2005 相容語法。");
                sb.AppendLine("2. 嚴禁刪除 `.Designer.cs` 內的 `InfoDataSet`、`InfoBindingSource`、`InfoCommand`、`UpdateComponent` 宣告。");
                sb.AppendLine("3. 修改完成後，請再次執行 `eepc.exe build <專案路徑> --auto-repair --claude` 驗證直到顯示 `PASS (0 Error(s))`。");
            }

            return sb.ToString();
        }

        private static List<string> GetLastLogLines(ProjectBuildResult res, int maxLines)
        {
            int start = Math.Max(0, res.Logs.Count - maxLines);
            return res.Logs.GetRange(start, res.Logs.Count - start);
        }

        private static CompilerOptions ParseOptions(string[] args)
        {
            CompilerOptions opt = new CompilerOptions();
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "--claude", StringComparison.OrdinalIgnoreCase))
                {
                    opt.ClaudeMode = true;
                }
                else if (string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase))
                {
                    opt.JsonMode = true;
                }
                else if (string.Equals(a, "--auto-repair", StringComparison.OrdinalIgnoreCase))
                {
                    opt.AutoRepair = true;
                }
                else if (string.Equals(a, "--no-repair", StringComparison.OrdinalIgnoreCase))
                {
                    opt.AutoRepair = false;
                }
                else if (string.Equals(a, "--eep-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    opt.EepRootPath = args[++i];
                }
                else if (string.Equals(a, "--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    opt.CustomOutputPath = args[++i];
                }
                else if (string.Equals(a, "--config", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    opt.Configuration = args[++i];
                }
                else if (string.Equals(a, "--engine", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    string eng = args[++i].ToLowerInvariant();
                    if (eng == "msbuild") opt.Engine = EngineMode.MSBuild;
                    else if (eng == "direct") opt.Engine = EngineMode.Direct;
                    else opt.Engine = EngineMode.Auto;
                }
                else if (string.Equals(a, "--target", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    string tgt = args[++i].ToLowerInvariant();
                    if (tgt.Contains("2")) opt.TargetClr = TargetClrVersion.Net20;
                    else if (tgt.Contains("4")) opt.TargetClr = TargetClrVersion.Net40;
                    else opt.TargetClr = TargetClrVersion.Auto;
                }
                else if (!a.StartsWith("-") && string.IsNullOrEmpty(opt.InputPath))
                {
                    opt.InputPath = a;
                }
            }
            return opt;
        }

        private static bool IsHelpArg(string arg)
        {
            return arg == "-h" || arg == "--help" || arg == "/?" || arg == "help";
        }

        public static void PrintUsage()
        {
            Console.WriteLine("======================================================================");
            Console.WriteLine("  Infolight EEP / Visual Studio 2005 可攜式編譯與自動修復工具 (eepc.exe)");
            Console.WriteLine("======================================================================");
            Console.WriteLine("用法:");
            Console.WriteLine("  1. 啟動圖形化桌面介面 (GUI):");
            Console.WriteLine("     直接雙擊 eepc.exe 或執行: eepc.exe --gui");
            Console.WriteLine();
            Console.WriteLine("  2. 自動修復並編譯專案 (推薦 Claude Code 呼叫):");
            Console.WriteLine("     eepc.exe build <專案.sln|.csproj|資料夾> [--auto-repair] [--claude]");
            Console.WriteLine();
            Console.WriteLine("  3. 僅執行規則 2~4 自動修復 (.csproj HintPath / licenses.licx / PostBuildEvent):");
            Console.WriteLine("     eepc.exe repair <專案.sln|.csproj|資料夾> [--eep-root <EEP目錄>]");
            Console.WriteLine();
            Console.WriteLine("  4. 僅執行規則 5 C# 語法版本降級與 Designer 預檢:");
            Console.WriteLine("     eepc.exe check <專案.sln|.csproj|資料夾>");
            Console.WriteLine();
            Console.WriteLine("  5. 檢測本機 MSBuild / CSC / .NET Framework 環境:");
            Console.WriteLine("     eepc.exe env");
            Console.WriteLine();
            Console.WriteLine("常用選用參數:");
            Console.WriteLine("  --claude               輸出專為 Claude Code / AI 設計的 UTF-8 Markdown 診斷報告");
            Console.WriteLine("  --eep-root <路徑>      指定訊光 EEP 根目錄 (內含 EEPNetServer / EEPNetClient)");
            Console.WriteLine("  --engine <模式>        auto (預設智慧雙引擎) | msbuild | direct (免 SDK 內建引擎)");
            Console.WriteLine("  --target <版本>        auto (預設依 Srvtools 偵測) | v2.0 | v4.0");
            Console.WriteLine("  --output <目錄>        覆寫編譯輸出的目標資料夾");
            Console.WriteLine("  --no-repair            停用編譯前的 .csproj 與 licenses.licx 自動修復");
        }
    }
}
