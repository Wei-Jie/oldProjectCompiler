using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace EEPCompiler
{
    /// <summary>
    /// 核心編譯管線：整合規則 1~5，支援「MSBuild 引擎」與「免 SDK 之內建 Direct CSC + ResX 引擎」雙核心自動切換
    /// </summary>
    public class BuildEngine
    {
        private readonly ReferenceResolver _resolver;
        private readonly ProjectManager _projectManager;
        private readonly SyntaxAnalyzer _syntaxAnalyzer;
        private readonly ResxCompiler _resxCompiler;

        // 解析 C# / MSBuild 錯誤格式：
        // 1. File.cs(12,34): error CS0246: 訊息
        // 2. MSBUILD : error MSB3073: 訊息
        private static readonly Regex CscDiagRegex = new Regex(
            @"^(?<file>[^(]+)\((?<line>\d+),(?<col>\d+)\):\s*(?<sev>error|warning)\s+(?<code>[A-Za-z]+\d+):\s*(?<msg>.+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex GeneralDiagRegex = new Regex(
            @"^(?<origin>[^:]+):\s*(?<sev>error|warning)\s+(?<code>[A-Za-z]+\d+):\s*(?<msg>.+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public BuildEngine()
        {
            _resolver = new ReferenceResolver();
            _projectManager = new ProjectManager(_resolver);
            _syntaxAnalyzer = new SyntaxAnalyzer();
            _resxCompiler = new ResxCompiler();
        }

        public ReferenceResolver Resolver { get { return _resolver; } }
        public ProjectManager Projects { get { return _projectManager; } }
        public SyntaxAnalyzer Analyzer { get { return _syntaxAnalyzer; } }

        /// <summary>
        /// 針對單一專案執行「載入 -> 自動修復 (規則 2~4) -> 語法預檢 (規則 5) -> 雙核心編譯 (規則 1)」完整流程
        /// </summary>
        public ProjectBuildResult ExecuteBuild(string projectPath, CompilerOptions options)
        {
            Stopwatch sw = Stopwatch.StartNew();
            ProjectBuildResult result = new ProjectBuildResult();

            try
            {
                // 步驟 1：載入並解析專案與參考組件
                EepProjectInfo project = _projectManager.LoadProject(projectPath, options);
                result.Project = project;
                result.Logs.Add(string.Format("=== 開始處理專案: {0} ({1}) ===", project.ProjectName, project.EepRole));

                // 步驟 2：若啟用 AutoRepair，自動執行規則 2、3、4 修復
                if (options.AutoRepair)
                {
                    List<RepairRecord> repairs = _projectManager.RepairProject(project, options);
                    result.Repairs.AddRange(repairs);
                    foreach (RepairRecord r in repairs)
                    {
                        result.Logs.Add(string.Format("[自動修復 - {0}] {1}", r.RuleId, r.Description));
                    }
                    // 修復後重新解析一次確保狀態同步
                    if (repairs.Count > 0 && !string.IsNullOrEmpty(project.ProjectFilePath) && File.Exists(project.ProjectFilePath))
                    {
                        project = _projectManager.LoadProject(project.ProjectFilePath, options);
                        result.Project = project;
                    }
                }

                // 判斷本機是否僅有舊版 C# 5.0 / 2.0 編譯器
                List<string> msbuilds = ReferenceResolver.FindAvailableMSBuilds();
                bool hasModernRoslynMsBuild = false;
                foreach (string msb in msbuilds)
                {
                    if (msb.IndexOf("2022", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msb.IndexOf("2019", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msb.IndexOf("2017", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasModernRoslynMsBuild = true;
                        break;
                    }
                }

                // 步驟 3：執行規則 5 C# 語法與 EEP Designer 預檢
                bool strictCSharp5 = !hasModernRoslynMsBuild || options.Engine == EngineMode.Direct;
                result.SyntaxIssues = _syntaxAnalyzer.AnalyzeProject(project, strictCSharp5);
                foreach (SyntaxIssue si in result.SyntaxIssues)
                {
                    result.Logs.Add(string.Format("[語法預檢 {0}] {1}({2}): {3} -> 建議: {4}",
                        si.RuleCode, Path.GetFileName(si.FilePath), si.LineNumber, si.Message, si.Suggestion));
                }

                // 步驟 4：決定目標 CLR 版本 (v2.0 vs v4.0)
                TargetClrVersion effectiveClr = DetermineTargetClr(project, options);

                // 步驟 5：決定編譯引擎並執行建置
                bool hasCsprojFile = !string.IsNullOrEmpty(project.ProjectFilePath) && File.Exists(project.ProjectFilePath);

                // 若目標為 CLR v2.0，且本機只有 .NET 4.0 MSBuild (沒有 VS2019/2022 也沒有 .NET 3.5 MSBuild)，
                // 或使用者選擇 Direct / Auto，我們評估哪個引擎最穩定
                if (!hasCsprojFile || options.Engine == EngineMode.Direct)
                {
                    BuildWithDirectCsc(project, options, effectiveClr, result);
                }
                else if (options.Engine == EngineMode.MSBuild)
                {
                    BuildWithMSBuild(project, options, msbuilds, result);
                }
                else
                {
                    // Auto 模式：
                    // 若目標是 CLR v2.0 (VS2005)，而本機只有 v4.0.30319\MSBuild.exe 且無 Windows SDK，
                    // MSBuild 常會遇到 MSB3091 (缺 ResGen.exe) 或將組件編譯為 v4.0 runtime。
                    // 因此先嘗試 MSBuild；若失敗且錯誤包含環境/SDK 問題 (MSB3091, MSB3644, LC0000 等) 或需要強制 CLR v2.0，自動無縫切換至內建 Direct CSC 引擎！
                    if (msbuilds.Count > 0)
                    {
                        bool msbOk = BuildWithMSBuild(project, options, msbuilds, result);
                        if (!msbOk && ShouldFallbackToDirectEngine(result))
                        {
                            result.Logs.Add("[智慧切換] 偵測到本機 MSBuild 環境缺少舊版 SDK 工具 (如 ResGen.exe / LC.exe)，自動切換至【內建免 SDK Direct CSC + ResX 引擎】重新編譯...");
                            result.Diagnostics.Clear();
                            BuildWithDirectCsc(project, options, effectiveClr, result);
                        }
                        else if (msbOk && effectiveClr == TargetClrVersion.Net20 && !string.IsNullOrEmpty(result.OutputArtifactPath))
                        {
                            // 檢查 MSBuild 產出的 DLL 是否誤編成 v4.0.30319 (當目標是 v2.0 時會導致 EEP 發生 BadImageFormatException)
                            string actualClr = ReferenceResolver.DetectAssemblyClrVersion(result.OutputArtifactPath);
                            result.TargetRuntimeMetadata = actualClr;
                            if (actualClr.StartsWith("v4.0") && File.Exists(@"C:\Windows\Microsoft.NET\Framework\v2.0.50727\mscorlib.dll"))
                            {
                                result.Logs.Add("[CLR v2.0 保護] MSBuild 產出為 v4.0 Header，自動使用內建跨編譯器重新連結為 CLR v2.0.50727 以防 BadImageFormatException...");
                                result.Diagnostics.Clear();
                                BuildWithDirectCsc(project, options, TargetClrVersion.Net20, result);
                            }
                        }
                    }
                    else
                    {
                        BuildWithDirectCsc(project, options, effectiveClr, result);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Logs.Add("[嚴重例外錯誤] " + ex.ToString());
            }

            sw.Stop();
            result.DurationSeconds = sw.Elapsed.TotalSeconds;
            return result;
        }

        private TargetClrVersion DetermineTargetClr(EepProjectInfo project, CompilerOptions options)
        {
            if (options.TargetClr != TargetClrVersion.Auto)
                return options.TargetClr;

            // 檢查專案所引用的 Srvtools.dll / InfoRemoteModule.dll 的實際 CLR 版本
            foreach (ReferenceItem r in project.References)
            {
                if (r.IsEepAssembly && r.IsResolved && File.Exists(r.ResolvedPath))
                {
                    string ver = ReferenceResolver.DetectAssemblyClrVersion(r.ResolvedPath);
                    if (ver.StartsWith("v2.0")) return TargetClrVersion.Net20;
                    if (ver.StartsWith("v4.0")) return TargetClrVersion.Net40;
                }
            }

            if (project.TargetFrameworkVersion.StartsWith("v4", StringComparison.OrdinalIgnoreCase))
                return TargetClrVersion.Net40;

            // 若本機有 .NET 2.0 mscorlib.dll，預設針對 VS2005 產出 Net20；否則使用 Net40
            if (File.Exists(@"C:\Windows\Microsoft.NET\Framework\v2.0.50727\mscorlib.dll"))
                return TargetClrVersion.Net20;

            return TargetClrVersion.Net40;
        }

        private bool ShouldFallbackToDirectEngine(ProjectBuildResult result)
        {
            foreach (CompilerDiagnostic d in result.Diagnostics)
            {
                if (string.Equals(d.Code, "MSB3091", StringComparison.OrdinalIgnoreCase) || // 找不到 resgen.exe / lc.exe / al.exe
                    string.Equals(d.Code, "MSB3644", StringComparison.OrdinalIgnoreCase) || // 找不到 Reference Assemblies
                    string.Equals(d.Code, "MSB4019", StringComparison.OrdinalIgnoreCase) || // 找不到 Microsoft.CSharp.targets
                    string.Equals(d.Code, "LC0000", StringComparison.OrdinalIgnoreCase) ||  // LC.exe 錯誤
                    string.Equals(d.Code, "MSB6003", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(d.Code, "MSB6004", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            foreach (string log in result.Logs)
            {
                if (log.IndexOf("resgen.exe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    log.IndexOf("LC.exe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    log.IndexOf("MSB3091", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 規則 1：使用本機 MSBuild.exe 進行編譯
        /// </summary>
        private bool BuildWithMSBuild(EepProjectInfo project, CompilerOptions options, List<string> msbuilds, ProjectBuildResult result)
        {
            if (msbuilds == null || msbuilds.Count == 0)
            {
                result.Logs.Add("[MSBuild 引擎] 找不到可用的 MSBuild.exe。");
                return false;
            }

            string msbuildExe = msbuilds[0];
            result.EngineUsed = "MSBuild (" + msbuildExe + ")";
            result.Logs.Add("[引擎 1 - MSBuild] 使用編譯器: " + msbuildExe);

            // 檢查是否有未解決的外部參考，若有則透過 /p:ReferencePath 傳入額外搜尋目錄
            List<string> refDirs = new List<string>();
            foreach (ReferenceItem r in project.References)
            {
                if (r.IsResolved && !string.IsNullOrEmpty(r.ResolvedPath))
                {
                    string d = Path.GetDirectoryName(r.ResolvedPath);
                    if (!string.IsNullOrEmpty(d) && !refDirs.Contains(d))
                        refDirs.Add(d);
                }
            }

            StringBuilder args = new StringBuilder();
            args.AppendFormat("\"{0}\" /t:Rebuild /p:Configuration=\"{1}\" /v:minimal /nologo",
                project.ProjectFilePath, options.Configuration);

            if (refDirs.Count > 0)
            {
                args.AppendFormat(" /p:ReferencePath=\"{0}\"", string.Join(";", refDirs.ToArray()));
            }

            if (!string.IsNullOrEmpty(options.CustomOutputPath))
            {
                args.AppendFormat(" /p:OutputPath=\"{0}\"", options.CustomOutputPath);
            }

            int exitCode;
            string output = RunProcessCaptureOutput(msbuildExe, args.ToString(), project.ProjectDirectory, out exitCode);

            ParseCompilerOutput(output, project.ProjectDirectory, result);

            if (exitCode == 0)
            {
                string ext = string.Equals(project.OutputType, "WinExe", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(project.OutputType, "Exe", StringComparison.OrdinalIgnoreCase)
                             ? ".exe" : ".dll";
                string outDir = !string.IsNullOrEmpty(options.CustomOutputPath)
                    ? options.CustomOutputPath
                    : (Path.IsPathRooted(project.OutputPath)
                        ? project.OutputPath
                        : Path.GetFullPath(Path.Combine(project.ProjectDirectory, project.OutputPath)));

                result.OutputArtifactPath = Path.Combine(outDir, project.AssemblyName + ext);
                result.TargetRuntimeMetadata = ReferenceResolver.DetectAssemblyClrVersion(result.OutputArtifactPath);
                result.Success = true;
                result.Logs.Add(string.Format("[MSBuild 成功] 已產出組件: {0} (CLR: {1})", result.OutputArtifactPath, result.TargetRuntimeMetadata));
                return true;
            }

            result.Success = false;
            return false;
        }

        /// <summary>
        /// 內建免 SDK 引擎：使用內建 ResxCompiler 轉換 .resx，並呼叫 csc.exe (支援 /nostdlib CLR v2.0 跨編譯)
        /// </summary>
        private bool BuildWithDirectCsc(EepProjectInfo project, CompilerOptions options, TargetClrVersion targetClr, ProjectBuildResult result)
        {
            string cscExe = ReferenceResolver.FindBestCscCompiler(targetClr);
            if (string.IsNullOrEmpty(cscExe) || !File.Exists(cscExe))
            {
                result.Logs.Add("[Direct CSC 引擎錯誤] 找不到本機 csc.exe。");
                result.Success = false;
                return false;
            }

            string fwDir = ReferenceResolver.GetFrameworkDirectory(targetClr);
            bool useNoStdLibForNet20 = (targetClr == TargetClrVersion.Net20) &&
                                       cscExe.IndexOf("v4.0", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                       File.Exists(Path.Combine(fwDir, "mscorlib.dll"));

            result.EngineUsed = string.Format("Direct CSC ({0}{1})",
                cscExe,
                useNoStdLibForNet20 ? " -> 跨編譯目標 CLR v2.0" : "");

            result.Logs.Add("[引擎 2 - Direct CSC + 內建 ResX] 使用編譯器: " + result.EngineUsed);

            // 1. 先檢查是否有找不到的 EEP / 第三方參考組件 (規則 2 診斷)
            foreach (ReferenceItem r in project.References)
            {
                if (!r.IsResolved && !r.IsSystemAssembly)
                {
                    result.Diagnostics.Add(new CompilerDiagnostic
                    {
                        Severity = "Error",
                        Code = "CS0246-MISSING-REF",
                        FilePath = project.ProjectFilePath,
                        Line = 1,
                        Column = 1,
                        Message = string.Format("找不到參考的專屬組件 '{0}.dll' (原 HintPath: '{1}')。", r.ShortName, r.OriginalHintPath),
                        AiHint = string.Format("請確認 '{0}.dll' 是否存在於 EEPNetServer、EEPNetClient 或 Lib 目錄，或使用 --eep-root <路徑> 指定 EEP 安裝目錄。", r.ShortName)
                    });
                }
            }

            // 2. 使用內建免 SDK 的 ResxCompiler 將所有 .resx 編譯為 .resources
            string objDir = Path.Combine(project.ProjectDirectory, @"obj\EEPCompiler");
            List<string> resourceArgs = _resxCompiler.CompileProjectResources(project, objDir, result.Logs);

            // 3. 決定輸出目錄與檔案路徑
            string outDir = !string.IsNullOrEmpty(options.CustomOutputPath)
                ? options.CustomOutputPath
                : (Path.IsPathRooted(project.OutputPath)
                    ? project.OutputPath
                    : Path.GetFullPath(Path.Combine(project.ProjectDirectory, project.OutputPath)));

            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            string ext = ".dll";
            string targetType = "library";
            if (string.Equals(project.OutputType, "WinExe", StringComparison.OrdinalIgnoreCase))
            {
                ext = ".exe";
                targetType = "winexe";
            }
            else if (string.Equals(project.OutputType, "Exe", StringComparison.OrdinalIgnoreCase))
            {
                ext = ".exe";
                targetType = "exe";
            }

            string outputArtifact = Path.Combine(outDir, project.AssemblyName + ext);

            // 若啟用備份且目標已存在舊 DLL，先備份為 .bak
            if (options.BackupBeforeOverwrite && File.Exists(outputArtifact))
            {
                try
                {
                    string bakFile = outputArtifact + ".bak";
                    File.Copy(outputArtifact, bakFile, true);
                }
                catch { }
            }

            // 4. 建立 csc.exe 回應檔 (Response File .rsp) 以避免命令列過長與編碼問題
            string rspPath = Path.Combine(objDir, "eep_csc_args.rsp");
            StringBuilder rsp = new StringBuilder();
            rsp.AppendLine("/nologo");
            rsp.AppendLine("/utf8output");
            rsp.AppendFormat("/target:{0}\r\n", targetType);
            rsp.AppendFormat("/out:\"{0}\"\r\n", outputArtifact);

            if (string.Equals(options.Configuration, "Debug", StringComparison.OrdinalIgnoreCase))
            {
                rsp.AppendLine("/debug+");
                rsp.AppendLine("/define:DEBUG;TRACE");
            }
            else
            {
                rsp.AppendLine("/optimize+");
                rsp.AppendLine("/define:TRACE");
            }

            if (!string.IsNullOrEmpty(project.PlatformTarget) &&
                !string.Equals(project.PlatformTarget, "AnyCPU", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(project.PlatformTarget, "Any CPU", StringComparison.OrdinalIgnoreCase))
            {
                rsp.AppendFormat("/platform:{0}\r\n", project.PlatformTarget);
            }

            // 若使用 .NET 4.0 csc 跨編譯出 .NET 2.0 Runtime 組件
            HashSet<string> addedRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (useNoStdLibForNet20)
            {
                rsp.AppendLine("/nostdlib+");
                string mscorlib20 = Path.Combine(fwDir, "mscorlib.dll");
                rsp.AppendFormat("/reference:\"{0}\"\r\n", mscorlib20);
                addedRefs.Add("mscorlib");
            }

            // 加入專案參考組件
            foreach (ReferenceItem r in project.References)
            {
                if (addedRefs.Contains(r.ShortName)) continue;

                if (r.IsSystemAssembly && useNoStdLibForNet20)
                {
                    string sys20 = Path.Combine(fwDir, r.ShortName + ".dll");
                    if (File.Exists(sys20))
                    {
                        rsp.AppendFormat("/reference:\"{0}\"\r\n", sys20);
                        addedRefs.Add(r.ShortName);
                        continue;
                    }
                }

                if (r.IsResolved && !string.IsNullOrEmpty(r.ResolvedPath) && File.Exists(r.ResolvedPath))
                {
                    rsp.AppendFormat("/reference:\"{0}\"\r\n", r.ResolvedPath);
                    addedRefs.Add(r.ShortName);
                }
                else if (r.IsSystemAssembly)
                {
                    string sys40 = Path.Combine(@"C:\Windows\Microsoft.NET\Framework\v4.0.30319", r.ShortName + ".dll");
                    if (File.Exists(sys40))
                    {
                        rsp.AppendFormat("/reference:\"{0}\"\r\n", sys40);
                        addedRefs.Add(r.ShortName);
                    }
                }
            }

            // 確保基本 System 組件存在
            string[] coreSys = new string[] { "System", "System.Data", "System.Drawing", "System.Windows.Forms", "System.Xml" };
            foreach (string cs in coreSys)
            {
                if (!addedRefs.Contains(cs))
                {
                    string candidate = Path.Combine(fwDir, cs + ".dll");
                    if (File.Exists(candidate))
                    {
                        rsp.AppendFormat("/reference:\"{0}\"\r\n", candidate);
                        addedRefs.Add(cs);
                    }
                }
            }

            // 加入內嵌資源 (.resources)
            foreach (string resArg in resourceArgs)
            {
                rsp.AppendLine(resArg);
            }

            // 加入所有 .cs 原始碼檔案
            foreach (string csFile in project.CompileFiles)
            {
                if (File.Exists(csFile))
                {
                    rsp.AppendFormat("\"{0}\"\r\n", csFile);
                }
            }

            if (!Directory.Exists(objDir)) Directory.CreateDirectory(objDir);
            File.WriteAllText(rspPath, rsp.ToString(), Encoding.UTF8);

            int exitCode;
            string cscOutput = RunProcessCaptureOutput(cscExe, string.Format("@\"{0}\"", rspPath), project.ProjectDirectory, out exitCode);

            ParseCompilerOutput(cscOutput, project.ProjectDirectory, result);

            if (exitCode == 0 && File.Exists(outputArtifact))
            {
                result.OutputArtifactPath = outputArtifact;
                result.TargetRuntimeMetadata = ReferenceResolver.DetectAssemblyClrVersion(outputArtifact);
                result.Success = true;
                result.Logs.Add(string.Format("[Direct CSC 編譯成功] 已產出組件: {0} (CLR Metadata: {1})",
                    result.OutputArtifactPath, result.TargetRuntimeMetadata));
                return true;
            }

            result.Success = false;
            return false;
        }

        private string RunProcessCaptureOutput(string exePath, string arguments, string workingDir, out int exitCode)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exePath;
            psi.Arguments = arguments;
            psi.WorkingDirectory = Directory.Exists(workingDir) ? workingDir : Environment.CurrentDirectory;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            // 若參數包含 /utf8output 則使用 UTF8，否則使用系統預設編碼 (繁體中文 Windows 為 CP950 Big5)
            Encoding enc = arguments.IndexOf("/utf8output", StringComparison.OrdinalIgnoreCase) >= 0
                ? Encoding.UTF8
                : Encoding.Default;

            psi.StandardOutputEncoding = enc;
            psi.StandardErrorEncoding = enc;

            StringBuilder combined = new StringBuilder();
            using (Process proc = new Process())
            {
                proc.StartInfo = psi;
                proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        lock (combined) { combined.AppendLine(e.Data); }
                    }
                };
                proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        lock (combined) { combined.AppendLine(e.Data); }
                    }
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
                exitCode = proc.ExitCode;
            }

            return combined.ToString();
        }

        private void ParseCompilerOutput(string rawOutput, string projectDir, ProjectBuildResult result)
        {
            if (string.IsNullOrEmpty(rawOutput)) return;

            string[] lines = rawOutput.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                result.Logs.Add(trimmed);

                Match m1 = CscDiagRegex.Match(trimmed);
                if (m1.Success)
                {
                    string file = m1.Groups["file"].Value.Trim();
                    if (!Path.IsPathRooted(file) && !string.IsNullOrEmpty(projectDir))
                        file = Path.GetFullPath(Path.Combine(projectDir, file));

                    int lineNum = 0, colNum = 0;
                    int.TryParse(m1.Groups["line"].Value, out lineNum);
                    int.TryParse(m1.Groups["col"].Value, out colNum);

                    string sev = m1.Groups["sev"].Value;
                    string code = m1.Groups["code"].Value.ToUpperInvariant();
                    string msg = m1.Groups["msg"].Value.Trim();

                    // 移除 MSBuild 尾端附加的 [xxx.csproj]
                    msg = Regex.Replace(msg, @"\s*\[[^\]]+\.csproj\]\s*$", "");

                    CompilerDiagnostic diag = new CompilerDiagnostic
                    {
                        Severity = string.Equals(sev, "error", StringComparison.OrdinalIgnoreCase) ? "Error" : "Warning",
                        Code = code,
                        FilePath = file,
                        Line = lineNum,
                        Column = colNum,
                        Message = msg,
                        AiHint = GenerateAiHintForErrorCode(code, msg)
                    };
                    result.Diagnostics.Add(diag);
                    continue;
                }

                Match m2 = GeneralDiagRegex.Match(trimmed);
                if (m2.Success)
                {
                    string sev = m2.Groups["sev"].Value;
                    string code = m2.Groups["code"].Value.ToUpperInvariant();
                    string msg = m2.Groups["msg"].Value.Trim();
                    msg = Regex.Replace(msg, @"\s*\[[^\]]+\.csproj\]\s*$", "");

                    CompilerDiagnostic diag = new CompilerDiagnostic
                    {
                        Severity = string.Equals(sev, "error", StringComparison.OrdinalIgnoreCase) ? "Error" : "Warning",
                        Code = code,
                        FilePath = m2.Groups["origin"].Value.Trim(),
                        Line = 0,
                        Column = 0,
                        Message = msg,
                        AiHint = GenerateAiHintForErrorCode(code, msg)
                    };
                    result.Diagnostics.Add(diag);
                }
            }
        }

        /// <summary>
        /// 針對常見的 EEP / VS2005 編譯錯誤碼，自動產生給 Claude Code 的具體修復提示
        /// </summary>
        private string GenerateAiHintForErrorCode(string code, string message)
        {
            switch (code)
            {
                case "CS0246":
                case "CS0234":
                    return "[規則 2 / 規則 5] 找不到型別或命名空間：請檢查 (1) EEP 專屬 DLL (如 Srvtools.dll, InfoRemoteModule.dll, EFClientTools.dll) 的 <HintPath> 是否正確；或 (2) 是否誤用了 C# 9 Target-typed new() 或未引入的命名空間。";
                case "CS1056":
                    return "[規則 5 - C# 語法降級] 未預期的字元 '$'：舊版 C# 編譯器不支援 C# 6.0 字串插值 ($\"...\")，請改寫為 string.Format(\"...\", args)。";
                case "CS1525":
                    return "[規則 5 - C# 語法降級] 無效的運算式項：請檢查是否誤用了 C# 6.0 Null 條件運算子 (?.)、C# 8 switch 運算式或 C# 9 new()，請改寫為 C# 5.0 傳統語法。";
                case "CS0103":
                    if (message.IndexOf("nameof", StringComparison.OrdinalIgnoreCase) >= 0)
                        return "[規則 5 - C# 語法降級] 舊版編譯器不支援 C# 6.0 nameof()，請直接替換為字串常數 \"欄位或屬性名稱\"。";
                    return "名稱不存在於目前內容中：請檢查變數名稱拼寫，或檢查 .Designer.cs 中的控制項宣告是否被誤刪。";
                case "CS1519":
                case "CS1002":
                    return "[規則 5 - C# 語法降級] 宣告語法無效：請檢查是否使用了 File-scoped namespace (namespace X;)、自動屬性預設值 ({ get; set; } = val) 或 using var 宣告。";
                case "MSB3073":
                    return "[規則 4 - PostBuildEvent] 建置後事件 copy/xcopy 失敗：請執行 eepc.exe repair 自動註解無效的外部磁碟機路徑，或建立目標資料夾。";
                case "LC0000":
                case "MSB6006":
                    return "[規則 3 - 第三方授權檢查] LC.exe 失敗：請將 Properties\\licenses.licx 內容清空為 0 byte（切勿刪除檔案本身）。";
                case "MSB3091":
                    return "[無 SDK 環境] 本機缺少 Windows SDK (找不到 resgen.exe 或 al.exe)：請使用 eepc.exe --engine direct 切換至內建免 SDK 編譯引擎。";
                default:
                    return "";
            }
        }
    }
}
