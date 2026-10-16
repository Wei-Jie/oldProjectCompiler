using System;
using System.Collections.Generic;

namespace EEPCompiler
{
    /// <summary>
    /// 編譯引擎模式列舉
    /// </summary>
    public enum EngineMode
    {
        /// <summary>
        /// 自動模式：優先嘗試 MSBuild，若因缺少 SDK (如 MSB3091 找不到 resgen.exe/lc.exe) 則自動切換至內建 Direct CSC 引擎
        /// </summary>
        Auto,
        /// <summary>
        /// 強制使用 MSBuild.exe 建置 (規則 1)
        /// </summary>
        MSBuild,
        /// <summary>
        /// 強制使用內建免 SDK 之 Direct CSC + 內建 ResX 編譯器
        /// </summary>
        Direct
    }

    /// <summary>
    /// 目標 CLR 框架版本
    /// </summary>
    public enum TargetClrVersion
    {
        Auto,
        Net20,
        Net40
    }

    /// <summary>
    /// 編譯器全域設定 (儲存於 eepc.config.json 或命令列參數)
    /// </summary>
    public class CompilerOptions
    {
        public string InputPath { get; set; }
        public string EepRootPath { get; set; }
        public string CustomOutputPath { get; set; }
        public string Configuration { get; set; }
        public string Platform { get; set; }
        public EngineMode Engine { get; set; }
        public TargetClrVersion TargetClr { get; set; }
        public bool AutoRepair { get; set; }
        public bool BackupBeforeOverwrite { get; set; }
        public bool ClaudeMode { get; set; }
        public bool JsonMode { get; set; }
        public List<string> ExtraSearchPaths { get; set; }

        public CompilerOptions()
        {
            InputPath = "";
            EepRootPath = "";
            CustomOutputPath = "";
            Configuration = "Debug";
            Platform = "AnyCPU";
            Engine = EngineMode.Auto;
            TargetClr = TargetClrVersion.Auto;
            AutoRepair = true;
            BackupBeforeOverwrite = true;
            ClaudeMode = false;
            JsonMode = false;
            ExtraSearchPaths = new List<string>();
        }
    }

    /// <summary>
    /// 專案參考組件資訊 (對應規則 2)
    /// </summary>
    public class ReferenceItem
    {
        public string Include { get; set; }
        public string ShortName { get; set; }
        public string OriginalHintPath { get; set; }
        public string ResolvedPath { get; set; }
        public bool SpecificVersion { get; set; }
        public bool IsSystemAssembly { get; set; }
        public bool IsEepAssembly { get; set; }
        public bool IsResolved { get; set; }

        public ReferenceItem()
        {
            Include = "";
            ShortName = "";
            OriginalHintPath = "";
            ResolvedPath = "";
            SpecificVersion = false;
            IsSystemAssembly = false;
            IsEepAssembly = false;
            IsResolved = false;
        }
    }

    /// <summary>
    /// 內嵌資源檔項目 (.resx 等)
    /// </summary>
    public class ResourceItem
    {
        public string RelativePath { get; set; }
        public string FullPath { get; set; }
        public string DependentUpon { get; set; }
        public string LogicalResourceName { get; set; }
        public bool IsLicenseFile { get; set; }

        public ResourceItem()
        {
            RelativePath = "";
            FullPath = "";
            DependentUpon = "";
            LogicalResourceName = "";
            IsLicenseFile = false;
        }
    }

    /// <summary>
    /// 解析後的單一 EEP C# 專案資訊
    /// </summary>
    public class EepProjectInfo
    {
        public string ProjectFilePath { get; set; }
        public string ProjectDirectory { get; set; }
        public string ProjectName { get; set; }
        public string AssemblyName { get; set; }
        public string RootNamespace { get; set; }
        public string OutputType { get; set; }
        public string TargetFrameworkVersion { get; set; }
        public string OutputPath { get; set; }
        public string PostBuildEvent { get; set; }
        public string PlatformTarget { get; set; }
        /// <summary>
        /// 專案角色：Client (WinForms), Server (DataModule), Common (Library/Exe)
        /// </summary>
        public string EepRole { get; set; }
        public List<ReferenceItem> References { get; set; }
        public List<string> CompileFiles { get; set; }
        public List<ResourceItem> EmbeddedResources { get; set; }
        public List<string> LicenseFiles { get; set; }

        public EepProjectInfo()
        {
            ProjectFilePath = "";
            ProjectDirectory = "";
            ProjectName = "";
            AssemblyName = "";
            RootNamespace = "";
            OutputType = "Library";
            TargetFrameworkVersion = "v2.0";
            OutputPath = @"bin\Debug\";
            PostBuildEvent = "";
            PlatformTarget = "AnyCPU";
            EepRole = "Common";
            References = new List<ReferenceItem>();
            CompileFiles = new List<string>();
            EmbeddedResources = new List<ResourceItem>();
            LicenseFiles = new List<string>();
        }
    }

    /// <summary>
    /// 自動修復動作紀錄 (對應規則 2, 3, 4)
    /// </summary>
    public class RepairRecord
    {
        public string RuleId { get; set; }
        public string TargetFile { get; set; }
        public string Description { get; set; }
        public bool Applied { get; set; }

        public RepairRecord(string ruleId, string targetFile, string description, bool applied)
        {
            RuleId = ruleId;
            TargetFile = targetFile;
            Description = description;
            Applied = applied;
        }
    }

    /// <summary>
    /// 語法與結構相容性問題 (對應規則 5)
    /// </summary>
    public class SyntaxIssue
    {
        public string RuleCode { get; set; }
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public string LinePreview { get; set; }
        public string Message { get; set; }
        public string Suggestion { get; set; }
        public bool IsError { get; set; }

        public SyntaxIssue()
        {
            RuleCode = "";
            FilePath = "";
            LineNumber = 0;
            LinePreview = "";
            Message = "";
            Suggestion = "";
            IsError = true;
        }
    }

    /// <summary>
    /// 編譯器錯誤或警告訊息
    /// </summary>
    public class CompilerDiagnostic
    {
        public string Severity { get; set; }
        public string Code { get; set; }
        public string FilePath { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Message { get; set; }
        public string AiHint { get; set; }

        public CompilerDiagnostic()
        {
            Severity = "Error";
            Code = "";
            FilePath = "";
            Line = 0;
            Column = 0;
            Message = "";
            AiHint = "";
        }
    }

    /// <summary>
    /// 單一專案完整編譯與修復結果
    /// </summary>
    public class ProjectBuildResult
    {
        public EepProjectInfo Project { get; set; }
        public bool Success { get; set; }
        public string EngineUsed { get; set; }
        public string OutputArtifactPath { get; set; }
        public string TargetRuntimeMetadata { get; set; }
        public List<RepairRecord> Repairs { get; set; }
        public List<SyntaxIssue> SyntaxIssues { get; set; }
        public List<CompilerDiagnostic> Diagnostics { get; set; }
        public List<string> Logs { get; set; }
        public double DurationSeconds { get; set; }

        public ProjectBuildResult()
        {
            Success = false;
            EngineUsed = "";
            OutputArtifactPath = "";
            TargetRuntimeMetadata = "";
            Repairs = new List<RepairRecord>();
            SyntaxIssues = new List<SyntaxIssue>();
            Diagnostics = new List<CompilerDiagnostic>();
            Logs = new List<string>();
            DurationSeconds = 0;
        }
    }
}
