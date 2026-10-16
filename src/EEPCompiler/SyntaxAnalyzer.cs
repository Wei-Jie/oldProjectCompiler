using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace EEPCompiler
{
    /// <summary>
    /// 對應指南規則 5：C# 語法版本降級預檢器與 EEP Designer 結構保護檢查
    /// </summary>
    public class SyntaxAnalyzer
    {
        // C# 10: File-scoped namespace (namespace Foo.Bar;)
        private static readonly Regex FileScopedNsRegex = new Regex(@"^\s*namespace\s+[A-Za-z0-9_.]+\s*;\s*$", RegexOptions.Compiled);

        // C# 8: using var x = ...;
        private static readonly Regex UsingVarRegex = new Regex(@"\busing\s+(?:var|[A-Za-z0-9_<>.]+)\s+[A-Za-z0-9_]+\s*=", RegexOptions.Compiled);

        // C# 9: Target-typed new() -> = new(); or = new(
        private static readonly Regex TargetTypedNewRegex = new Regex(@"=\s*new\s*\(", RegexOptions.Compiled);

        // C# 8: Switch expression -> ) switch { or identifier switch {
        private static readonly Regex SwitchExprRegex = new Regex(@"(?:\)|\b[A-Za-z0-9_]+)\s+switch\s*\{", RegexOptions.Compiled);

        // C# 6: 字串插值 $"..." or $@"..."
        private static readonly Regex StringInterpolationRegex = new Regex(@"(?:\$@|@\$|\$)""", RegexOptions.Compiled);

        // C# 6: Null 條件運算子 ?. (排除註解與數值後)
        private static readonly Regex NullConditionalRegex = new Regex(@"[A-Za-z0-9_\)\]]\?\.[A-Za-z_]", RegexOptions.Compiled);

        // C# 6: nameof(x)
        private static readonly Regex NameofRegex = new Regex(@"\bnameof\s*\(", RegexOptions.Compiled);

        // C# 6: 自動屬性預設值 { get; set; } = ...
        private static readonly Regex AutoPropInitRegex = new Regex(@"\{\s*get;\s*(?:private\s+)?set;\s*\}\s*=", RegexOptions.Compiled);

        /// <summary>
        /// 檢查專案中所有 .cs 檔案是否違反規則 5 (C# 相容語法與 EEP Designer 結構)
        /// </summary>
        public List<SyntaxIssue> AnalyzeProject(EepProjectInfo project, bool strictCSharp5Or2)
        {
            List<SyntaxIssue> issues = new List<SyntaxIssue>();

            foreach (string file in project.CompileFiles)
            {
                if (!File.Exists(file))
                {
                    issues.Add(new SyntaxIssue
                    {
                        RuleCode = "EEP-FILE01",
                        FilePath = file,
                        LineNumber = 1,
                        LinePreview = "",
                        Message = "找不到 .csproj 中所列的原始碼檔案。",
                        Suggestion = "請確認檔案是否被誤刪，或從 .csproj 移除該 <Compile> 項目。",
                        IsError = true
                    });
                    continue;
                }

                AnalyzeFile(file, strictCSharp5Or2, issues);
            }

            return issues;
        }

        private void AnalyzeFile(string filePath, bool strictCSharp5Or2, List<SyntaxIssue> issues)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch
            {
                return;
            }

            bool isDesignerFile = filePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
            bool inBlockComment = false;
            bool hasInitializeComponent = false;
            bool inheritsEepBase = false;

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNum = i + 1;
                string rawLine = lines[i];
                string codeLine = StripComments(rawLine, ref inBlockComment);

                if (string.IsNullOrEmpty(codeLine.Trim()))
                    continue;

                if (codeLine.Contains("InitializeComponent"))
                    hasInitializeComponent = true;

                if (codeLine.Contains(": InfoForm") ||
                    codeLine.Contains(": Srvtools.InfoForm") ||
                    codeLine.Contains(": InfoDataModule") ||
                    codeLine.Contains(": Srvtools.InfoDataModule") ||
                    codeLine.Contains(": DataModule") ||
                    codeLine.Contains(": Srvtools.DataModule"))
                {
                    inheritsEepBase = true;
                }

                // 1. 檢查 File-scoped namespace (C# 10)
                if (FileScopedNsRegex.IsMatch(codeLine))
                {
                    issues.Add(new SyntaxIssue
                    {
                        RuleCode = "RULE5-CS10-NS",
                        FilePath = filePath,
                        LineNumber = lineNum,
                        LinePreview = rawLine.Trim(),
                        Message = "偵測到 C# 10 File-scoped namespace 語法 (namespace Xxx;)，舊版 MSBuild 無法編譯。",
                        Suggestion = "請改用傳統大括號格式：namespace Xxx { ... }",
                        IsError = true
                    });
                }

                // 2. 檢查簡化版 using var (C# 8)
                if (UsingVarRegex.IsMatch(codeLine) && !codeLine.Contains("using ("))
                {
                    issues.Add(new SyntaxIssue
                    {
                        RuleCode = "RULE5-CS8-USING",
                        FilePath = filePath,
                        LineNumber = lineNum,
                        LinePreview = rawLine.Trim(),
                        Message = "偵測到 C# 8 簡化版 using 宣告 (using var ...;)。",
                        Suggestion = "請改用傳統區塊語法：using (var resource = ...) { ... }",
                        IsError = true
                    });
                }

                // 3. 檢查 Target-typed new() (C# 9)
                if (TargetTypedNewRegex.IsMatch(codeLine))
                {
                    issues.Add(new SyntaxIssue
                    {
                        RuleCode = "RULE5-CS9-NEW",
                        FilePath = filePath,
                        LineNumber = lineNum,
                        LinePreview = rawLine.Trim(),
                        Message = "偵測到 C# 9 Target-typed new() 運算式，舊版編譯器不支援省略型別。",
                        Suggestion = "請明確寫出完整型別名稱，例如：new List<string>() 或 new DataTable()",
                        IsError = true
                    });
                }

                // 4. 檢查 Switch expression (C# 8)
                if (SwitchExprRegex.IsMatch(codeLine))
                {
                    issues.Add(new SyntaxIssue
                    {
                        RuleCode = "RULE5-CS8-SWITCH",
                        FilePath = filePath,
                        LineNumber = lineNum,
                        LinePreview = rawLine.Trim(),
                        Message = "偵測到 C# 8 switch 運算式 (Switch expression)。",
                        Suggestion = "請改用傳統 switch (variable) { case ...: return ...; } 敘述。",
                        IsError = true
                    });
                }

                // 5. 檢查 #nullable 指令
                if (codeLine.TrimStart().StartsWith("#nullable"))
                {
                    issues.Add(new SyntaxIssue
                    {
                        RuleCode = "RULE5-CS8-NULLABLE",
                        FilePath = filePath,
                        LineNumber = lineNum,
                        LinePreview = rawLine.Trim(),
                        Message = "偵測到 C# 8 #nullable 指令。",
                        Suggestion = "請移除 #nullable 指令與參考型別後的 '?' 標記。",
                        IsError = true
                    });
                }

                // 6. 檢查 C# 6.0 語法（若本機僅有 .NET 4.0 MSBuild/csc 或 .NET 2.0 csc，使用 C# 6 語法必敗）
                if (StringInterpolationRegex.IsMatch(codeLine))
                {
                    issues.Add(new SyntaxIssue
                    {
                        RuleCode = "RULE5-CS6-INTERP",
                        FilePath = filePath,
                        LineNumber = lineNum,
                        LinePreview = rawLine.Trim(),
                        Message = "偵測到 C# 6.0 字串插值 ($\"...\")。在 .NET 4.0 內建 MSBuild 或 VS2005 編譯器下會報 CS1056 錯誤。",
                        Suggestion = "請改用 string.Format(\"文字 {0}\", 變數) 或傳統字串相加。",
                        IsError = strictCSharp5Or2
                    });
                }

                // 排除字串常數後再檢查 ?. 與 nameof 與自動屬性初始化
                string codeWithoutStrings = StripStringLiterals(codeLine);

                if (NullConditionalRegex.IsMatch(codeWithoutStrings))
                {
                    issues.Add(new SyntaxIssue
                    {
                        RuleCode = "RULE5-CS6-NULLCOND",
                        FilePath = filePath,
                        LineNumber = lineNum,
                        LinePreview = rawLine.Trim(),
                        Message = "偵測到 C# 6.0 Null 條件運算子 (?.)。在 .NET 4.0 內建 MSBuild 或 VS2005 編譯器下會報 CS1525 錯誤。",
                        Suggestion = "請改用傳統三元運算子或 null 判斷：if (obj != null) ...",
                        IsError = strictCSharp5Or2
                    });
                }

                if (NameofRegex.IsMatch(codeWithoutStrings))
                {
                    issues.Add(new SyntaxIssue
                    {
                        RuleCode = "RULE5-CS6-NAMEOF",
                        FilePath = filePath,
                        LineNumber = lineNum,
                        LinePreview = rawLine.Trim(),
                        Message = "偵測到 C# 6.0 nameof() 運算子。在 .NET 4.0 內建 MSBuild 或 VS2005 編譯器下會報 CS0103 錯誤。",
                        Suggestion = "請直接改用字串常數，例如將 nameof(CustomerID) 改為 \"CustomerID\"。",
                        IsError = strictCSharp5Or2
                    });
                }

                if (AutoPropInitRegex.IsMatch(codeWithoutStrings))
                {
                    issues.Add(new SyntaxIssue
                    {
                        RuleCode = "RULE5-CS6-AUTOPROP",
                        FilePath = filePath,
                        LineNumber = lineNum,
                        LinePreview = rawLine.Trim(),
                        Message = "偵測到 C# 6.0 自動屬性初始值 ({ get; set; } = ...)。",
                        Suggestion = "請移除屬性後的 '= 初始值'，改在類別建構子 (Constructor) 中賦值，或使用傳統 Backing Field。",
                        IsError = strictCSharp5Or2
                    });
                }
            }

            // 檢查 EEP 表單或資料模組是否遺失 InitializeComponent
            if (inheritsEepBase && !isDesignerFile && !hasInitializeComponent)
            {
                issues.Add(new SyntaxIssue
                {
                    RuleCode = "RULE5-EEP-DESIGNER",
                    FilePath = filePath,
                    LineNumber = 1,
                    LinePreview = Path.GetFileName(filePath),
                    Message = "此類別繼承自 EEP 基底類別 (InfoForm / InfoDataModule)，但未發現呼叫 InitializeComponent()。",
                    Suggestion = "請確保建構子中保留 InitializeComponent() 呼叫，且切勿刪除 .Designer.cs 中的 InfoDataSet / InfoCommand / UpdateComponent 綁定。",
                    IsError = false
                });
            }
        }

        private static string StripComments(string line, ref bool inBlockComment)
        {
            if (string.IsNullOrEmpty(line)) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (inBlockComment)
                {
                    if (i + 1 < line.Length && line[i] == '*' && line[i + 1] == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    continue;
                }

                if (!inString && i + 1 < line.Length && line[i] == '/' && line[i + 1] == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }

                if (!inString && i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/')
                {
                    break;
                }

                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
                {
                    inString = !inString;
                }

                sb.Append(line[i]);
            }
            return sb.ToString();
        }

        private static string StripStringLiterals(string line)
        {
            if (string.IsNullOrEmpty(line)) return "";
            return Regex.Replace(line, @"""(?:\\.|[^""\\])*""", "\"\"");
        }
    }
}
