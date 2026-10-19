using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace EEPCompiler
{
    /// <summary>
    /// 負責搜尋本機編譯工具 (MSBuild / csc.exe)、系統組件以及 EEP 專屬 DLL 引用路徑 (對應規則 1 與規則 2)
    /// </summary>
    public class ReferenceResolver
    {
        // 常見的訊光 EEP 私有核心組件清單
        private static readonly HashSet<string> KnownEepAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Srvtools",
            "InfoRemoteModule",
            "EFClientTools",
            "EFServerTools",
            "EFBase",
            "EFWCFModule",
            "FLCore",
            "FLTools",
            "FLRuntime",
            "FLDesigner",
            "FLDesignerCore",
            "clitools",
            "EEPNetClient",
            "EEPNetServer",
            "EEPServerTools",
            "WebTools",
            "AjaxTools",
            "JQClientTools",
            "ExportTools",
            "InitEEP"
        };

        // 標準 .NET Framework 系統組件清單
        private static readonly HashSet<string> KnownSystemAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mscorlib",
            "System",
            "System.Core",
            "System.Data",
            "System.Data.DataSetExtensions",
            "System.Data.OracleClient",
            "System.Deployment",
            "System.Design",
            "System.DirectoryServices",
            "System.Drawing",
            "System.Drawing.Design",
            "System.EnterpriseServices",
            "System.Management",
            "System.Messaging",
            "System.Runtime.Remoting",
            "System.Runtime.Serialization",
            "System.Runtime.Serialization.Formatters.Soap",
            "System.Security",
            "System.ServiceModel",
            "System.ServiceProcess",
            "System.Transactions",
            "System.Web",
            "System.Web.Extensions",
            "System.Web.Services",
            "System.Windows.Forms",
            "System.Xml",
            "System.Xml.Linq",
            "System.Configuration",
            "System.Configuration.Install",
            "Microsoft.CSharp",
            "Microsoft.VisualBasic"
        };

        // 快取已掃描到的 DLL 名稱 -> 完整路徑
        private readonly Dictionary<string, string> _dllCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsEepAssemblyName(string shortName)
        {
            if (string.IsNullOrEmpty(shortName)) return false;
            if (KnownEepAssemblies.Contains(shortName)) return true;
            return shortName.StartsWith("Info", StringComparison.OrdinalIgnoreCase) ||
                   shortName.StartsWith("Srv", StringComparison.OrdinalIgnoreCase) ||
                   shortName.StartsWith("EEP", StringComparison.OrdinalIgnoreCase) ||
                   shortName.StartsWith("EF", StringComparison.OrdinalIgnoreCase) ||
                   shortName.StartsWith("FL", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSystemAssemblyName(string shortName)
        {
            if (string.IsNullOrEmpty(shortName)) return false;
            return KnownSystemAssemblies.Contains(shortName) ||
                   shortName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                   shortName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 規則 1：尋找本機安裝的所有可用 MSBuild.exe 路徑（依優先順序排列）
        /// </summary>
        public static List<string> FindAvailableMSBuilds()
        {
            List<string> results = new List<string>();

            // 1. 嘗試透過 vswhere.exe 尋找 VS 2017 / 2019 / 2022
            string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            string[] vsRoots = new string[]
            {
                Path.Combine(progFiles, @"Microsoft Visual Studio\2022"),
                Path.Combine(progFilesX86, @"Microsoft Visual Studio\2022"),
                Path.Combine(progFilesX86, @"Microsoft Visual Studio\2019"),
                Path.Combine(progFiles, @"Microsoft Visual Studio\2019"),
                Path.Combine(progFilesX86, @"Microsoft Visual Studio\2017")
            };

            string[] editions = new string[] { "Enterprise", "Professional", "Community", "BuildTools" };

            foreach (string vsRoot in vsRoots)
            {
                if (string.IsNullOrEmpty(vsRoot) || !Directory.Exists(vsRoot)) continue;
                foreach (string ed in editions)
                {
                    string p1 = Path.Combine(vsRoot, ed, @"MSBuild\Current\Bin\MSBuild.exe");
                    if (File.Exists(p1) && !results.Contains(p1)) results.Add(p1);

                    string p2 = Path.Combine(vsRoot, ed, @"MSBuild\15.0\Bin\MSBuild.exe");
                    if (File.Exists(p2) && !results.Contains(p2)) results.Add(p2);
                }
            }

            // 2. 檢查獨立 MSBuild 14.0 / 12.0 (VS 2015 / 2013)
            string[] standaloneMsBuilds = new string[]
            {
                Path.Combine(progFilesX86, @"MSBuild\14.0\Bin\MSBuild.exe"),
                Path.Combine(progFilesX86, @"MSBuild\12.0\Bin\MSBuild.exe")
            };
            foreach (string p in standaloneMsBuilds)
            {
                if (File.Exists(p) && !results.Contains(p)) results.Add(p);
            }

            // 3. 檢查 Windows 內建 .NET Framework 4.0 MSBuild.exe (幾乎每台 Windows 都有)
            string net40MsBuild = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe";
            if (File.Exists(net40MsBuild) && !results.Contains(net40MsBuild)) results.Add(net40MsBuild);

            // 4. 檢查 .NET Framework 3.5 / 2.0 MSBuild.exe (VS2005 / VS2008 原生版本)
            string net35MsBuild = @"C:\Windows\Microsoft.NET\Framework\v3.5\MSBuild.exe";
            if (File.Exists(net35MsBuild) && !results.Contains(net35MsBuild)) results.Add(net35MsBuild);

            string net20MsBuild = @"C:\Windows\Microsoft.NET\Framework\v2.0.50727\MSBuild.exe";
            if (File.Exists(net20MsBuild) && !results.Contains(net20MsBuild)) results.Add(net20MsBuild);

            return results;
        }

        /// <summary>
        /// 尋找本機可用的 C# 編譯器 (csc.exe)
        /// </summary>
        public static string FindBestCscCompiler(TargetClrVersion targetClr)
        {
            string csc20 = @"C:\Windows\Microsoft.NET\Framework\v2.0.50727\csc.exe";
            string csc35 = @"C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe";
            string csc40 = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe";

            // 若本機隨附 portable csc 或使用 .NET 4.0 csc (搭配 /nostdlib 亦可跨編譯 v2.0)
            if (targetClr == TargetClrVersion.Net20)
            {
                if (File.Exists(csc35)) return csc35;
                if (File.Exists(csc20)) return csc20;
            }
            if (File.Exists(csc40)) return csc40;
            if (File.Exists(csc35)) return csc35;
            if (File.Exists(csc20)) return csc20;
            return "";
        }

        /// <summary>
        /// 取得指定 CLR 版本的系統框架目錄 (用於 /nostdlib 跨編譯或系統 DLL 解析)
        /// </summary>
        public static string GetFrameworkDirectory(TargetClrVersion targetClr)
        {
            string net20Dir = @"C:\Windows\Microsoft.NET\Framework\v2.0.50727";
            string net20RefDir = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\v3.5";
            string net40Dir = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319";

            if (targetClr == TargetClrVersion.Net20)
            {
                if (File.Exists(Path.Combine(net20Dir, "mscorlib.dll")))
                    return net20Dir;
                if (Directory.Exists(net20RefDir) && File.Exists(Path.Combine(net20RefDir, "mscorlib.dll")))
                    return net20RefDir;
            }
            return net40Dir;
        }

        /// <summary>
        /// 建立 DLL 搜尋索引：掃描專案目錄、上層目錄、EEPNetServer、EEPNetClient、Lib 等資料夾
        /// </summary>
        public void BuildSearchIndex(string projectDir, CompilerOptions options)
        {
            _dllCache.Clear();
            List<string> candidateDirs = new List<string>();

            // 1. 優先搜尋使用者指定的 EEP 根目錄與額外搜尋路徑 (先找到者優先，--eep-root 必須勝過工具包內附 DLL)
            if (!string.IsNullOrEmpty(options.EepRootPath) && Directory.Exists(options.EepRootPath))
            {
                AddRootWithSubfolders(options.EepRootPath, candidateDirs);
            }

            if (options.ExtraSearchPaths != null)
            {
                foreach (string extra in options.ExtraSearchPaths)
                {
                    if (!string.IsNullOrEmpty(extra) && Directory.Exists(extra))
                        candidateDirs.Add(extra);
                }
            }

            // 2. 專案目錄與往上追溯 4 層目錄（尋找 EEPNetServer, EEPNetClient, Lib, bin 等）
            if (!string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir))
            {
                string current = Path.GetFullPath(projectDir);
                for (int i = 0; i < 5 && !string.IsNullOrEmpty(current); i++)
                {
                    AddRootWithSubfolders(current, candidateDirs);
                    DirectoryInfo parent = Directory.GetParent(current);
                    current = parent != null ? parent.FullName : null;
                }
            }

            // 3. 常見本機訊光 EEP 安裝位置
            string[] commonInstallPaths = new string[]
            {
                @"C:\EEP.NET",
                @"C:\EEP2006",
                @"C:\EEP2008",
                @"C:\EEP2010",
                @"C:\EEP2012",
                @"C:\EEP2015",
                @"D:\EEP.NET",
                @"D:\EEP2006",
                @"D:\EEP2008",
                @"D:\EEP2010",
                @"D:\EEP2012",
                @"C:\Program Files (x86)\Infolight",
                @"C:\Program Files\Infolight",
                @"C:\Infolight"
            };

            foreach (string installRoot in commonInstallPaths)
            {
                if (Directory.Exists(installRoot))
                {
                    AddRootWithSubfolders(installRoot, candidateDirs);
                }
            }

            // 4. 最後才搜尋可攜式編譯器 (eepc.exe) 自身所在目錄與其下的 lib / EEPNetServer / EEPNetClient 資料夾
            //    (工具包內附的 Srvtools.dll / InfoRemoteModule.dll 可能是 mock，只能當最後備援)
            string toolBaseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(toolBaseDir) && Directory.Exists(toolBaseDir))
            {
                AddRootWithSubfolders(toolBaseDir, candidateDirs);
            }

            // 掃描所有候選目錄下的 .dll 與 .exe (如 EEPNetClient.exe / EEPNetServer.exe 有時也會被參考)
            HashSet<string> visitedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string dir in candidateDirs)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                string fullDir = Path.GetFullPath(dir);
                if (visitedDirs.Contains(fullDir)) continue;
                visitedDirs.Add(fullDir);

                IndexDirectoryFiles(fullDir, 2);
            }
        }

        /// <summary>
        /// 先加入 EEPNetServer / EEPNetClient / Lib 等子資料夾，再加入根目錄本身：
        /// 根目錄會往下遞迴掃描 2 層，若先掃根目錄，會先取到備份或其他工具資料夾內的同名 DLL (如 EEPManager\System.dll、EEPNetClient 舊版 Srvtools.dll)
        /// </summary>
        private void AddRootWithSubfolders(string rootDir, List<string> list)
        {
            AddCommonEepSubfolders(rootDir, list);
            list.Add(rootDir);
        }

        private void AddCommonEepSubfolders(string rootDir, List<string> list)
        {
            string[] subNames = new string[]
            {
                "EEPNetServer",
                "EEPNetClient",
                "EEPWebClient",
                "Lib",
                "Libs",
                "References",
                "bin",
                @"bin\Debug",
                @"bin\Release",
                "Dll",
                "Dlls"
            };
            foreach (string sub in subNames)
            {
                string combined = Path.Combine(rootDir, sub);
                if (Directory.Exists(combined))
                    list.Add(combined);
            }
        }

        private void IndexDirectoryFiles(string dir, int maxDepth)
        {
            try
            {
                string[] dlls = Directory.GetFiles(dir, "*.dll");
                foreach (string dll in dlls)
                {
                    string name = Path.GetFileNameWithoutExtension(dll);
                    if (!_dllCache.ContainsKey(name))
                    {
                        _dllCache[name] = dll;
                    }
                }

                string[] exes = Directory.GetFiles(dir, "*.exe");
                foreach (string exe in exes)
                {
                    string name = Path.GetFileNameWithoutExtension(exe);
                    if (IsEepAssemblyName(name) && !_dllCache.ContainsKey(name))
                    {
                        _dllCache[name] = exe;
                    }
                }

                if (maxDepth > 0)
                {
                    string[] subDirs = Directory.GetDirectories(dir);
                    foreach (string sub in subDirs)
                    {
                        string folderName = Path.GetFileName(sub);
                        if (folderName.StartsWith(".") ||
                            string.Equals(folderName, "obj", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(folderName, "node_modules", StringComparison.OrdinalIgnoreCase))
                            continue;

                        IndexDirectoryFiles(sub, maxDepth - 1);
                    }
                }
            }
            catch
            {
                // 忽略權限不足的資料夾
            }
        }

        /// <summary>
        /// 解析專案的所有 Reference 項目，設定 ResolvedPath 並標記是否找到
        /// </summary>
        public void ResolveProjectReferences(EepProjectInfo project, CompilerOptions options)
        {
            BuildSearchIndex(project.ProjectDirectory, options);

            string fw20Dir = @"C:\Windows\Microsoft.NET\Framework\v2.0.50727";
            string fw40Dir = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319";

            // 系統組件預設對應的 Framework：明確指定 v4.0 或專案為 v4.x 才用 v4.0，否則 VS2005 專案用 v2.0
            bool preferNet20 = options.TargetClr == TargetClrVersion.Net20 ||
                               (options.TargetClr == TargetClrVersion.Auto &&
                                !project.TargetFrameworkVersion.StartsWith("v4", StringComparison.OrdinalIgnoreCase));

            foreach (ReferenceItem item in project.References)
            {
                item.IsFromToolDirectory = false;

                // 1. 先檢查原本的 HintPath 是否存在
                if (!string.IsNullOrEmpty(item.OriginalHintPath))
                {
                    string candidate = item.OriginalHintPath;
                    if (!Path.IsPathRooted(candidate) && !string.IsNullOrEmpty(project.ProjectDirectory))
                    {
                        candidate = Path.GetFullPath(Path.Combine(project.ProjectDirectory, candidate));
                    }
                    if (File.Exists(candidate))
                    {
                        item.ResolvedPath = candidate;
                        item.IsResolved = true;
                        item.IsFromToolDirectory = IsInToolDirectory(candidate);
                        continue;
                    }
                }

                // 2. 系統組件 (.NET Framework) 一律從 Framework 目錄解析，不從 EEP 目錄搜尋
                //    (避免取到 EEP 安裝目錄內隨附的 System.dll 等複本)
                if (item.IsSystemAssembly)
                {
                    string sysCandidate20 = Path.Combine(fw20Dir, item.ShortName + ".dll");
                    string sysCandidate40 = Path.Combine(fw40Dir, item.ShortName + ".dll");

                    if (preferNet20 && File.Exists(sysCandidate20))
                    {
                        item.ResolvedPath = sysCandidate20;
                        item.IsResolved = true;
                        continue;
                    }
                    if (File.Exists(sysCandidate40))
                    {
                        item.ResolvedPath = sysCandidate40;
                        item.IsResolved = true;
                        continue;
                    }
                    if (File.Exists(sysCandidate20))
                    {
                        item.ResolvedPath = sysCandidate20;
                        item.IsResolved = true;
                        continue;
                    }
                }

                // 3. 檢查是否為搜尋索引中的組件 (包含 EEP 專屬 DLL 與第三方 DLL)
                string cachedPath;
                if (_dllCache.TryGetValue(item.ShortName, out cachedPath) && File.Exists(cachedPath))
                {
                    item.ResolvedPath = cachedPath;
                    item.IsResolved = true;
                    item.IsFromToolDirectory = IsInToolDirectory(cachedPath);
                    continue;
                }

                // 4. 檢查 GAC (C:\Windows\assembly\GAC_MSIL 等)
                string gacPath = SearchInGac(item.ShortName);
                if (!string.IsNullOrEmpty(gacPath))
                {
                    item.ResolvedPath = gacPath;
                    item.IsResolved = true;
                    continue;
                }

                item.IsResolved = false;
            }
        }

        /// <summary>
        /// 判斷檔案是否位於 eepc.exe 自身目錄之下 (工具包內附的 DLL 可能是 mock)
        /// </summary>
        public static bool IsInToolDirectory(string filePath)
        {
            try
            {
                string toolDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return Path.GetFullPath(filePath).StartsWith(toolDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private string SearchInGac(string shortName)
        {
            string[] gacRoots = new string[]
            {
                @"C:\Windows\Microsoft.NET\assembly\GAC_MSIL",
                @"C:\Windows\Microsoft.NET\assembly\GAC_32",
                @"C:\Windows\assembly\GAC_MSIL",
                @"C:\Windows\assembly\GAC_32",
                @"C:\Windows\assembly\GAC"
            };

            foreach (string root in gacRoots)
            {
                if (!Directory.Exists(root)) continue;
                string asmDir = Path.Combine(root, shortName);
                if (Directory.Exists(asmDir))
                {
                    try
                    {
                        string[] files = Directory.GetFiles(asmDir, shortName + ".dll", SearchOption.AllDirectories);
                        if (files.Length > 0) return files[0];
                    }
                    catch { }
                }
            }
            return "";
        }

        /// <summary>
        /// 計算從 fromDir 到 toFilePath 的相對路徑 (用於將修復後的 HintPath 寫回 .csproj)
        /// </summary>
        public static string GetRelativePath(string fromDir, string toFilePath)
        {
            if (string.IsNullOrEmpty(fromDir) || string.IsNullOrEmpty(toFilePath))
                return toFilePath;

            try
            {
                string normalizedDir = Path.GetFullPath(fromDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                       + Path.DirectorySeparatorChar;
                Uri fromUri = new Uri(normalizedDir);
                Uri toUri = new Uri(Path.GetFullPath(toFilePath));

                if (fromUri.Scheme != toUri.Scheme)
                    return toFilePath;

                Uri relativeUri = fromUri.MakeRelativeUri(toUri);
                string relativePath = Uri.UnescapeDataString(relativeUri.ToString());
                if (string.Equals(toUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                }
                return relativePath;
            }
            catch
            {
                return toFilePath;
            }
        }

        /// <summary>
        /// 偵測指定組件 (如 Srvtools.dll) 的目標 CLR Runtime 版本 (例如 v2.0.50727 或 v4.0.30319)
        /// </summary>
        public static string DetectAssemblyClrVersion(string dllPath)
        {
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
                return "";

            try
            {
                Assembly asm = Assembly.ReflectionOnlyLoadFrom(dllPath);
                return asm.ImageRuntimeVersion;
            }
            catch
            {
                // 若因為已載入同名組件而失敗，嘗試讀取 PE Header 中的 CLR 字串
                try
                {
                    byte[] bytes = File.ReadAllBytes(dllPath);
                    string ascii = System.Text.Encoding.ASCII.GetString(bytes);
                    if (ascii.Contains("v2.0.50727")) return "v2.0.50727";
                    if (ascii.Contains("v4.0.30319")) return "v4.0.30319";
                }
                catch { }
                return "";
            }
        }
    }
}
