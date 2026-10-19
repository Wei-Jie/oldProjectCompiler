using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace EEPCompiler
{
    /// <summary>
    /// 負責解析 .sln / .csproj / 資料夾，並執行指南規則 2、規則 3、規則 4 的自動修復
    /// </summary>
    public class ProjectManager
    {
        private readonly ReferenceResolver _resolver;

        public ProjectManager(ReferenceResolver resolver)
        {
            _resolver = resolver;
        }

        /// <summary>
        /// 從輸入路徑（可為 .sln、.csproj 或資料夾）探索所有待處理的專案路徑
        /// </summary>
        public List<string> DiscoverProjects(string inputPath)
        {
            List<string> projectPaths = new List<string>();
            if (string.IsNullOrEmpty(inputPath)) return projectPaths;

            if (File.Exists(inputPath))
            {
                string ext = Path.GetExtension(inputPath).ToLowerInvariant();
                if (ext == ".csproj")
                {
                    projectPaths.Add(Path.GetFullPath(inputPath));
                }
                else if (ext == ".sln")
                {
                    projectPaths.AddRange(ParseSolutionFile(Path.GetFullPath(inputPath)));
                }
                else if (ext == ".cs")
                {
                    // 若直接傳入某個 .cs 檔，往上尋找其所屬的 .csproj
                    string dir = Path.GetDirectoryName(Path.GetFullPath(inputPath));
                    string foundCsproj = FindNearestCsproj(dir);
                    if (!string.IsNullOrEmpty(foundCsproj))
                        projectPaths.Add(foundCsproj);
                    else
                        projectPaths.Add(dir);
                }
            }
            else if (Directory.Exists(inputPath))
            {
                string fullDir = Path.GetFullPath(inputPath);
                string[] csprojs = Directory.GetFiles(fullDir, "*.csproj", SearchOption.AllDirectories);
                if (csprojs.Length > 0)
                {
                    foreach (string p in csprojs)
                        projectPaths.Add(p);
                }
                else
                {
                    // 無 .csproj 的純原始碼資料夾模式
                    projectPaths.Add(fullDir);
                }
            }

            return projectPaths;
        }

        private string FindNearestCsproj(string startDir)
        {
            string current = startDir;
            for (int i = 0; i < 4 && !string.IsNullOrEmpty(current); i++)
            {
                string[] csprojs = Directory.GetFiles(current, "*.csproj");
                if (csprojs.Length > 0) return csprojs[0];
                DirectoryInfo parent = Directory.GetParent(current);
                current = parent != null ? parent.FullName : null;
            }
            return "";
        }

        private List<string> ParseSolutionFile(string slnPath)
        {
            List<string> results = new List<string>();
            string slnDir = Path.GetDirectoryName(slnPath);
            string[] lines = File.ReadAllLines(slnPath);

            // Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MyProj", "MyProj\MyProj.csproj", "{GUID}"
            Regex projRegex = new Regex(@"Project\(""\{[^}]+\}""\)\s*=\s*""[^""]+"",\s*""([^""]+\.csproj)""", RegexOptions.IgnoreCase);
            foreach (string line in lines)
            {
                Match m = projRegex.Match(line);
                if (m.Success)
                {
                    string relPath = m.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
                    string fullPath = Path.GetFullPath(Path.Combine(slnDir, relPath));
                    if (File.Exists(fullPath))
                    {
                        results.Add(fullPath);
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// 載入並解析單一 .csproj 或純原始碼資料夾
        /// </summary>
        public EepProjectInfo LoadProject(string path, CompilerOptions options)
        {
            if (Directory.Exists(path))
            {
                return LoadFolderAsProject(path, options);
            }

            EepProjectInfo info = new EepProjectInfo();
            info.ProjectFilePath = Path.GetFullPath(path);
            info.ProjectDirectory = Path.GetDirectoryName(info.ProjectFilePath);
            info.ProjectName = Path.GetFileNameWithoutExtension(info.ProjectFilePath);
            info.AssemblyName = info.ProjectName;
            info.RootNamespace = info.ProjectName;

            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.Load(info.ProjectFilePath);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            string defaultNs = doc.DocumentElement != null ? doc.DocumentElement.NamespaceURI : "";
            bool hasNs = !string.IsNullOrEmpty(defaultNs);
            if (hasNs)
            {
                nsmgr.AddNamespace("ms", defaultNs);
            }

            string prefix = hasNs ? "ms:" : "";

            // 讀取基本屬性
            XmlNode asmNode = doc.SelectSingleNode("//" + prefix + "AssemblyName", nsmgr);
            if (asmNode != null && !string.IsNullOrEmpty(asmNode.InnerText))
                info.AssemblyName = asmNode.InnerText.Trim();

            XmlNode nsNode = doc.SelectSingleNode("//" + prefix + "RootNamespace", nsmgr);
            if (nsNode != null && !string.IsNullOrEmpty(nsNode.InnerText))
                info.RootNamespace = nsNode.InnerText.Trim();

            XmlNode outTypeNode = doc.SelectSingleNode("//" + prefix + "OutputType", nsmgr);
            if (outTypeNode != null && !string.IsNullOrEmpty(outTypeNode.InnerText))
                info.OutputType = outTypeNode.InnerText.Trim();

            XmlNode tfvNode = doc.SelectSingleNode("//" + prefix + "TargetFrameworkVersion", nsmgr);
            if (tfvNode != null && !string.IsNullOrEmpty(tfvNode.InnerText))
                info.TargetFrameworkVersion = tfvNode.InnerText.Trim();
            else
                info.TargetFrameworkVersion = "v2.0"; // VS2005 .csproj 預設無此標籤，即為 v2.0

            // 讀取 OutputPath (優先對應指定 Configuration，否則取第一個非空 OutputPath)
            XmlNodeList propGroups = doc.SelectNodes("//" + prefix + "PropertyGroup", nsmgr);
            if (propGroups != null)
            {
                foreach (XmlNode pg in propGroups)
                {
                    string cond = pg.Attributes != null && pg.Attributes["Condition"] != null
                        ? pg.Attributes["Condition"].Value
                        : "";

                    XmlNode opNode = pg.SelectSingleNode(prefix + "OutputPath", nsmgr);
                    if (opNode != null && !string.IsNullOrEmpty(opNode.InnerText))
                    {
                        if (string.IsNullOrEmpty(info.OutputPath) ||
                            info.OutputPath == @"bin\Debug\" ||
                            cond.IndexOf(options.Configuration, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            info.OutputPath = opNode.InnerText.Trim();
                        }
                    }

                    XmlNode ptNode = pg.SelectSingleNode(prefix + "PlatformTarget", nsmgr);
                    if (ptNode != null && !string.IsNullOrEmpty(ptNode.InnerText))
                    {
                        info.PlatformTarget = ptNode.InnerText.Trim();
                    }

                    XmlNode pbeNode = pg.SelectSingleNode(prefix + "PostBuildEvent", nsmgr);
                    if (pbeNode != null && !string.IsNullOrEmpty(pbeNode.InnerText))
                    {
                        info.PostBuildEvent = pbeNode.InnerText.Trim();
                    }
                }
            }

            // 讀取 Reference
            XmlNodeList refNodes = doc.SelectNodes("//" + prefix + "Reference", nsmgr);
            if (refNodes != null)
            {
                foreach (XmlNode rNode in refNodes)
                {
                    if (rNode.Attributes == null || rNode.Attributes["Include"] == null) continue;
                    string inc = rNode.Attributes["Include"].Value.Trim();
                    string shortName = inc.Split(',')[0].Trim();

                    ReferenceItem rItem = new ReferenceItem();
                    rItem.Include = inc;
                    rItem.ShortName = shortName;
                    rItem.IsEepAssembly = ReferenceResolver.IsEepAssemblyName(shortName);
                    rItem.IsSystemAssembly = ReferenceResolver.IsSystemAssemblyName(shortName) && !rItem.IsEepAssembly;

                    XmlNode hpNode = rNode.SelectSingleNode(prefix + "HintPath", nsmgr);
                    if (hpNode != null)
                    {
                        rItem.OriginalHintPath = hpNode.InnerText.Trim();
                    }

                    XmlNode svNode = rNode.SelectSingleNode(prefix + "SpecificVersion", nsmgr);
                    if (svNode != null)
                    {
                        bool sv;
                        if (bool.TryParse(svNode.InnerText.Trim(), out sv))
                            rItem.SpecificVersion = sv;
                    }

                    info.References.Add(rItem);
                }
            }

            // 讀取 ProjectReference：本工具不連帶建置參考專案；若參考的 .csproj 不存在 (例如 ..\..\Srvtools\Srvtools.csproj)，
            // 改以同名 DLL 參考 (Srvtools.dll) 解析，避免參考整個遺失而報 CS0246
            XmlNodeList projRefNodes = doc.SelectNodes("//" + prefix + "ProjectReference", nsmgr);
            if (projRefNodes != null)
            {
                foreach (XmlNode prNode in projRefNodes)
                {
                    if (prNode.Attributes == null || prNode.Attributes["Include"] == null) continue;
                    string prInclude = prNode.Attributes["Include"].Value.Trim();
                    string prFull = Path.GetFullPath(Path.Combine(info.ProjectDirectory, prInclude));
                    if (File.Exists(prFull)) continue;

                    string asmName = GetProjectReferenceAssemblyName(prNode, prefix, nsmgr, prInclude);
                    bool alreadyReferenced = false;
                    foreach (ReferenceItem existing in info.References)
                    {
                        if (string.Equals(existing.ShortName, asmName, StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyReferenced = true;
                            break;
                        }
                    }
                    if (alreadyReferenced) continue;

                    ReferenceItem prItem = new ReferenceItem();
                    prItem.Include = asmName;
                    prItem.ShortName = asmName;
                    prItem.IsEepAssembly = ReferenceResolver.IsEepAssemblyName(asmName);
                    prItem.MissingProjectReference = prInclude;
                    info.References.Add(prItem);
                }
            }

            // 讀取 Compile (.cs)
            XmlNodeList compileNodes = doc.SelectNodes("//" + prefix + "Compile", nsmgr);
            if (compileNodes != null)
            {
                foreach (XmlNode cNode in compileNodes)
                {
                    if (cNode.Attributes == null || cNode.Attributes["Include"] == null) continue;
                    string rel = cNode.Attributes["Include"].Value.Trim();
                    string full = Path.GetFullPath(Path.Combine(info.ProjectDirectory, rel));
                    info.CompileFiles.Add(full);
                }
            }

            // 讀取 EmbeddedResource (.resx / licenses.licx)
            XmlNodeList resNodes = doc.SelectNodes("//" + prefix + "EmbeddedResource", nsmgr);
            if (resNodes != null)
            {
                foreach (XmlNode resNode in resNodes)
                {
                    if (resNode.Attributes == null || resNode.Attributes["Include"] == null) continue;
                    string rel = resNode.Attributes["Include"].Value.Trim();
                    string full = Path.GetFullPath(Path.Combine(info.ProjectDirectory, rel));

                    ResourceItem resItem = new ResourceItem();
                    resItem.RelativePath = rel;
                    resItem.FullPath = full;
                    resItem.IsLicenseFile = rel.EndsWith("licenses.licx", StringComparison.OrdinalIgnoreCase);

                    XmlNode depNode = resNode.SelectSingleNode(prefix + "DependentUpon", nsmgr);
                    if (depNode != null)
                        resItem.DependentUpon = depNode.InnerText.Trim();

                    resItem.LogicalResourceName = ComputeLogicalResourceName(info, rel, resItem.DependentUpon);
                    info.EmbeddedResources.Add(resItem);

                    if (resItem.IsLicenseFile && !info.LicenseFiles.Contains(full))
                    {
                        info.LicenseFiles.Add(full);
                    }
                }
            }

            // 額外掃描目錄下是否有未列入之 licenses.licx
            if (Directory.Exists(info.ProjectDirectory))
            {
                string[] licFiles = Directory.GetFiles(info.ProjectDirectory, "licenses.licx", SearchOption.AllDirectories);
                foreach (string lf in licFiles)
                {
                    string fullLf = Path.GetFullPath(lf);
                    if (!info.LicenseFiles.Contains(fullLf))
                        info.LicenseFiles.Add(fullLf);
                }
            }

            // 解析所有參考路徑
            _resolver.ResolveProjectReferences(info, options);

            // 判斷專案在 EEP 中的角色 (Client / Server / Common)
            DetectEepRole(info);

            return info;
        }

        /// <summary>
        /// 取得 ProjectReference 對應的組件名稱：優先使用 &lt;Name&gt;，否則使用 .csproj 檔名
        /// </summary>
        private static string GetProjectReferenceAssemblyName(XmlNode prNode, string prefix, XmlNamespaceManager nsmgr, string prInclude)
        {
            XmlNode nameNode = prNode.SelectSingleNode(prefix + "Name", nsmgr);
            if (nameNode != null && !string.IsNullOrEmpty(nameNode.InnerText.Trim()))
                return nameNode.InnerText.Trim();
            return Path.GetFileNameWithoutExtension(prInclude);
        }

        /// <summary>
        /// 當使用者只有散落的 .cs / .resx 資料夾而沒有 .csproj 時，自動組裝為虛擬專案
        /// </summary>
        private EepProjectInfo LoadFolderAsProject(string folderPath, CompilerOptions options)
        {
            EepProjectInfo info = new EepProjectInfo();
            info.ProjectDirectory = Path.GetFullPath(folderPath);
            info.ProjectName = Path.GetFileName(info.ProjectDirectory.TrimEnd(Path.DirectorySeparatorChar));
            info.AssemblyName = info.ProjectName;
            info.RootNamespace = info.ProjectName;
            info.OutputType = "Library";
            info.TargetFrameworkVersion = "v2.0";
            info.OutputPath = @"bin\Debug\";

            string[] csFiles = Directory.GetFiles(info.ProjectDirectory, "*.cs", SearchOption.AllDirectories);
            foreach (string cs in csFiles)
            {
                if (cs.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                info.CompileFiles.Add(cs);
            }

            string[] resxFiles = Directory.GetFiles(info.ProjectDirectory, "*.resx", SearchOption.AllDirectories);
            foreach (string rx in resxFiles)
            {
                if (rx.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string rel = ReferenceResolver.GetRelativePath(info.ProjectDirectory, rx);
                ResourceItem ri = new ResourceItem();
                ri.RelativePath = rel;
                ri.FullPath = rx;
                ri.LogicalResourceName = ComputeLogicalResourceName(info, rel, "");
                info.EmbeddedResources.Add(ri);
            }

            // 加入預設必備參考與 EEP 常用參考
            string[] defaultRefs = new string[]
            {
                "System", "System.Data", "System.Drawing", "System.Windows.Forms", "System.Xml",
                "System.Design", "System.Transactions", "System.Runtime.Remoting",
                "Srvtools", "InfoRemoteModule", "EFClientTools", "EFServerTools", "EFBase"
            };

            foreach (string r in defaultRefs)
            {
                ReferenceItem ri = new ReferenceItem();
                ri.Include = r;
                ri.ShortName = r;
                ri.IsEepAssembly = ReferenceResolver.IsEepAssemblyName(r);
                ri.IsSystemAssembly = ReferenceResolver.IsSystemAssemblyName(r) && !ri.IsEepAssembly;
                info.References.Add(ri);
            }

            _resolver.ResolveProjectReferences(info, options);
            DetectEepRole(info);
            return info;
        }

        /// <summary>
        /// 計算 VS2005 / MSBuild 嵌入 .resx 時的標準 LogicalResourceName (例如 RootNamespace.SubFolder.FormName.resources)
        /// </summary>
        private string ComputeLogicalResourceName(EepProjectInfo info, string relativeResxPath, string dependentUpon)
        {
            string relDir = Path.GetDirectoryName(relativeResxPath);
            string baseName = Path.GetFileNameWithoutExtension(relativeResxPath);

            // 若有對應的 .cs 檔案，嘗試從 .cs 檔案中讀取實際的 namespace 與 class 名稱，這與 MSBuild CreateCSharpManifestResourceName 行為一致
            string candidateCsPath = "";
            if (!string.IsNullOrEmpty(dependentUpon))
            {
                candidateCsPath = Path.Combine(info.ProjectDirectory, Path.Combine(relDir ?? "", dependentUpon));
            }
            else
            {
                candidateCsPath = Path.Combine(info.ProjectDirectory, Path.Combine(relDir ?? "", baseName + ".cs"));
            }

            if (File.Exists(candidateCsPath))
            {
                try
                {
                    string code = File.ReadAllText(candidateCsPath);
                    Match nsMatch = Regex.Match(code, @"\bnamespace\s+([A-Za-z0-9_.]+)");
                    Match clsMatch = Regex.Match(code, @"\b(?:partial\s+)?class\s+([A-Za-z0-9_]+)");
                    if (clsMatch.Success)
                    {
                        string clsName = clsMatch.Groups[1].Value;
                        if (nsMatch.Success)
                            return nsMatch.Groups[1].Value + "." + clsName + ".resources";
                        return clsName + ".resources";
                    }
                }
                catch { }
            }

            string nsPrefix = info.RootNamespace;
            if (!string.IsNullOrEmpty(relDir))
            {
                string subNs = relDir.Replace(Path.DirectorySeparatorChar, '.').Replace(Path.AltDirectorySeparatorChar, '.');
                if (!string.IsNullOrEmpty(nsPrefix))
                    nsPrefix = nsPrefix + "." + subNs;
                else
                    nsPrefix = subNs;
            }

            if (!string.IsNullOrEmpty(nsPrefix))
                return nsPrefix + "." + baseName + ".resources";
            return baseName + ".resources";
        }

        private void DetectEepRole(EepProjectInfo info)
        {
            if (info.OutputPath.IndexOf("EEPNetClient", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                info.EepRole = "Client (WinForms)";
                return;
            }
            if (info.OutputPath.IndexOf("EEPNetServer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                info.EepRole = "Server (DataModule)";
                return;
            }

            foreach (string cs in info.CompileFiles)
            {
                if (!File.Exists(cs)) continue;
                try
                {
                    string text = File.ReadAllText(cs);
                    if (text.Contains("InfoForm") || text.Contains("CLIUtils") || text.Contains("InfoDataGridView"))
                    {
                        info.EepRole = "Client (WinForms)";
                        return;
                    }
                    if (text.Contains("DataModule") || text.Contains("InfoDataModule") || text.Contains("UpdateComponent"))
                    {
                        info.EepRole = "Server (DataModule)";
                        return;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 執行指南規則 2、規則 3、規則 4 的自動修復
        /// </summary>
        public List<RepairRecord> RepairProject(EepProjectInfo project, CompilerOptions options)
        {
            List<RepairRecord> records = new List<RepairRecord>();

            // === 規則 6：自動為無 BOM 的 UTF-8 檔案補上 BOM (解決 Claude Code 存檔後舊版編譯器誤判為 Big5 報 CS1010 錯誤，同時不亂動其他未修改的 Big5 檔案以免弄髒版控) ===
            NormalizeSourceFileEncodings(project, records, false);

            // === 規則 3：繞過第三方元件授權檢查 (清空 licenses.licx 為 0 byte) ===
            foreach (string licPath in project.LicenseFiles)
            {
                if (File.Exists(licPath))
                {
                    FileInfo fi = new FileInfo(licPath);
                    if (fi.Length > 0)
                    {
                        try
                        {
                            string bakPath = licPath + ".bak";
                            if (!File.Exists(bakPath))
                            {
                                File.Copy(licPath, bakPath, false);
                            }
                            File.WriteAllText(licPath, string.Empty, Encoding.ASCII);
                            records.Add(new RepairRecord(
                                "規則 3 (LC.exe 授權繞過)",
                                licPath,
                                string.Format("已將非空白的 licenses.licx ({0} bytes) 備份並清空為 0 byte，防止 LC.exe exited with code -1。", fi.Length),
                                true));
                        }
                        catch (Exception ex)
                        {
                            records.Add(new RepairRecord(
                                "規則 3 (LC.exe 授權繞過)",
                                licPath,
                                "清空 licenses.licx 失敗: " + ex.Message,
                                false));
                        }
                    }
                }
            }

            // 若沒有 .csproj 實體檔案，只處理到規則 3 與輸出目錄建立
            if (string.IsNullOrEmpty(project.ProjectFilePath) || !File.Exists(project.ProjectFilePath))
            {
                EnsureOutputDirectory(project, records);
                return records;
            }

            // 載入 .csproj XML 準備修復規則 2 與規則 4
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.Load(project.ProjectFilePath);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            string defaultNs = doc.DocumentElement != null ? doc.DocumentElement.NamespaceURI : "";
            bool hasNs = !string.IsNullOrEmpty(defaultNs);
            if (hasNs)
            {
                nsmgr.AddNamespace("ms", defaultNs);
            }
            string prefix = hasNs ? "ms:" : "";
            bool csprojModified = false;

            // === 規則 2：修復 EEP 專屬與第三方 DLL 引用路徑 (解決 CS0246) ===
            // === 規則 3 強化：將 .csproj 中指向 licenses.licx 的 EmbeddedResource 改為 None，徹底阻止無 SDK 環境下 MSBuild 尋找 LC.exe ===
            XmlNodeList licResNodes = doc.SelectNodes("//" + prefix + "EmbeddedResource", nsmgr);
            if (licResNodes != null)
            {
                foreach (XmlNode lrNode in licResNodes)
                {
                    if (lrNode.Attributes == null || lrNode.Attributes["Include"] == null) continue;
                    string inc = lrNode.Attributes["Include"].Value.Trim();
                    if (inc.EndsWith("licenses.licx", StringComparison.OrdinalIgnoreCase))
                    {
                        XmlElement noneElem = doc.CreateElement("None", defaultNs);
                        noneElem.SetAttribute("Include", inc);
                        lrNode.ParentNode.ReplaceChild(noneElem, lrNode);
                        csprojModified = true;
                        records.Add(new RepairRecord(
                            "規則 3 (停用 .csproj LC Task)",
                            project.ProjectFilePath,
                            string.Format("已將 .csproj 中的 <EmbeddedResource Include=\"{0}\"> 轉為 <None>，防止無 SDK 環境下 MSBuild 呼叫 LC.exe 報錯。", inc),
                            true));
                    }
                }
            }

            XmlNodeList refNodes = doc.SelectNodes("//" + prefix + "Reference", nsmgr);
            if (refNodes != null)
            {
                foreach (XmlNode rNode in refNodes)
                {
                    if (rNode.Attributes == null || rNode.Attributes["Include"] == null) continue;
                    string inc = rNode.Attributes["Include"].Value.Trim();
                    string shortName = inc.Split(',')[0].Trim();

                    ReferenceItem matchedRef = null;
                    foreach (ReferenceItem ri in project.References)
                    {
                        if (string.Equals(ri.ShortName, shortName, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedRef = ri;
                            break;
                        }
                    }

                    if (matchedRef == null || matchedRef.IsSystemAssembly) continue;

                    XmlNode hpNode = rNode.SelectSingleNode(prefix + "HintPath", nsmgr);
                    XmlNode svNode = rNode.SelectSingleNode(prefix + "SpecificVersion", nsmgr);

                    // 檢查原本的 HintPath 是否損壞
                    bool originalExists = false;
                    if (hpNode != null && !string.IsNullOrEmpty(hpNode.InnerText))
                    {
                        string orig = hpNode.InnerText.Trim();
                        string fullOrig = Path.IsPathRooted(orig)
                            ? orig
                            : Path.GetFullPath(Path.Combine(project.ProjectDirectory, orig));
                        originalExists = File.Exists(fullOrig);
                    }

                    if (!originalExists && matchedRef.IsResolved && !string.IsNullOrEmpty(matchedRef.ResolvedPath))
                    {
                        string newRelPath = ReferenceResolver.GetRelativePath(project.ProjectDirectory, matchedRef.ResolvedPath);
                        if (hpNode == null)
                        {
                            hpNode = doc.CreateElement("HintPath", defaultNs);
                            rNode.AppendChild(hpNode);
                        }
                        string oldVal = hpNode.InnerText;
                        hpNode.InnerText = newRelPath;
                        csprojModified = true;

                        records.Add(new RepairRecord(
                            "規則 2 (修復 DLL HintPath)",
                            project.ProjectFilePath,
                            string.Format("組件 [{0}] 原路徑 '{1}' 不存在，已自動重定向至 '{2}'。", shortName, oldVal, newRelPath),
                            true));
                    }

                    // 針對非系統組件確保 <SpecificVersion>False</SpecificVersion>
                    if (matchedRef.IsEepAssembly || hpNode != null)
                    {
                        if (svNode == null)
                        {
                            svNode = doc.CreateElement("SpecificVersion", defaultNs);
                            svNode.InnerText = "False";
                            rNode.AppendChild(svNode);
                            csprojModified = true;
                            records.Add(new RepairRecord(
                                "規則 2 (解除版本號強綁定)",
                                project.ProjectFilePath,
                                string.Format("組件 [{0}] 已補上 <SpecificVersion>False</SpecificVersion>。", shortName),
                                true));
                        }
                        else if (!string.Equals(svNode.InnerText.Trim(), "False", StringComparison.OrdinalIgnoreCase))
                        {
                            svNode.InnerText = "False";
                            csprojModified = true;
                            records.Add(new RepairRecord(
                                "規則 2 (解除版本號強綁定)",
                                project.ProjectFilePath,
                                string.Format("組件 [{0}] 已將 SpecificVersion 改為 False。", shortName),
                                true));
                        }
                    }
                }
            }

            // === 規則 2 補充：指向不存在專案的 ProjectReference 改為 DLL 參考 (讓 MSBuild 與本工具都能解析) ===
            XmlNodeList projRefNodes = doc.SelectNodes("//" + prefix + "ProjectReference", nsmgr);
            if (projRefNodes != null)
            {
                List<XmlNode> projRefList = new List<XmlNode>();
                foreach (XmlNode n in projRefNodes) projRefList.Add(n);

                foreach (XmlNode prNode in projRefList)
                {
                    if (prNode.Attributes == null || prNode.Attributes["Include"] == null) continue;
                    string prInclude = prNode.Attributes["Include"].Value.Trim();

                    ReferenceItem matchedRef = null;
                    foreach (ReferenceItem ri in project.References)
                    {
                        if (string.Equals(ri.MissingProjectReference, prInclude, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedRef = ri;
                            break;
                        }
                    }
                    if (matchedRef == null || !matchedRef.IsResolved || string.IsNullOrEmpty(matchedRef.ResolvedPath)) continue;

                    string newRelPath = ReferenceResolver.GetRelativePath(project.ProjectDirectory, matchedRef.ResolvedPath);
                    XmlElement refElem = doc.CreateElement("Reference", defaultNs);
                    refElem.SetAttribute("Include", matchedRef.ShortName);

                    XmlElement svElem = doc.CreateElement("SpecificVersion", defaultNs);
                    svElem.InnerText = "False";
                    refElem.AppendChild(svElem);

                    XmlElement hpElem = doc.CreateElement("HintPath", defaultNs);
                    hpElem.InnerText = newRelPath;
                    refElem.AppendChild(hpElem);

                    XmlNode privateNode = prNode.SelectSingleNode(prefix + "Private", nsmgr);
                    if (privateNode != null)
                    {
                        XmlElement pvElem = doc.CreateElement("Private", defaultNs);
                        pvElem.InnerText = privateNode.InnerText.Trim();
                        refElem.AppendChild(pvElem);
                    }

                    prNode.ParentNode.ReplaceChild(refElem, prNode);
                    csprojModified = true;
                    records.Add(new RepairRecord(
                        "規則 2 (ProjectReference 改為 DLL 參考)",
                        project.ProjectFilePath,
                        string.Format("參考專案 '{0}' 不存在，已改為 DLL 參考 [{1}] -> '{2}'。", prInclude, matchedRef.ShortName, newRelPath),
                        true));
                }
            }

            // === 規則 4：處理 EEP 特有的輸出路徑與建置後事件 (解決 MSB3073) ===
            EnsureOutputDirectory(project, records);

            XmlNodeList pbeNodes = doc.SelectNodes("//" + prefix + "PostBuildEvent", nsmgr);
            if (pbeNodes != null)
            {
                foreach (XmlNode pbe in pbeNodes)
                {
                    string cmdText = pbe.InnerText;
                    if (string.IsNullOrEmpty(cmdText)) continue;

                    string[] lines = cmdText.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                    bool pbeChanged = false;
                    List<string> updatedLines = new List<string>();

                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("rem ", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(trimmed))
                        {
                            updatedLines.Add(line);
                            continue;
                        }

                        // 檢查是否包含指向不存在磁碟機 (例如 D:\, E:\, C:\EEP...) 的 copy / xcopy 命令
                        Match driveMatch = Regex.Match(trimmed, @"\b([A-Za-z]:\\[^\s""']+)", RegexOptions.IgnoreCase);
                        if (driveMatch.Success)
                        {
                            string targetPath = driveMatch.Groups[1].Value;
                            string targetDir = Path.GetDirectoryName(targetPath);
                            string rootDrive = Path.GetPathRoot(targetPath);

                            bool driveExists = !string.IsNullOrEmpty(rootDrive) && Directory.Exists(rootDrive);
                            if (!driveExists)
                            {
                                updatedLines.Add("rem [EEPCompiler Auto-Disabled Invalid Drive] " + line);
                                pbeChanged = true;
                                records.Add(new RepairRecord(
                                    "規則 4 (修復 PostBuildEvent MSB3073)",
                                    project.ProjectFilePath,
                                    string.Format("建置後事件包含不存在的磁碟機路徑 '{0}'，已自動加註解停用：{1}", rootDrive, trimmed),
                                    true));
                                continue;
                            }
                            else if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                            {
                                try
                                {
                                    Directory.CreateDirectory(targetDir);
                                    records.Add(new RepairRecord(
                                        "規則 4 (建立 PostBuildEvent 目標目錄)",
                                        targetDir,
                                        string.Format("已自動建立 PostBuildEvent 所需的目標目錄：{0}", targetDir),
                                        true));
                                }
                                catch
                                {
                                    updatedLines.Add("rem [EEPCompiler Auto-Disabled Uncreatable Path] " + line);
                                    pbeChanged = true;
                                    records.Add(new RepairRecord(
                                        "規則 4 (修復 PostBuildEvent MSB3073)",
                                        project.ProjectFilePath,
                                        string.Format("無法建立建置後事件目標目錄 '{0}'，已自動加註解停用。", targetDir),
                                        true));
                                    continue;
                                }
                            }
                        }
                        updatedLines.Add(line);
                    }

                    if (pbeChanged)
                    {
                        pbe.InnerText = string.Join("\r\n", updatedLines.ToArray());
                        project.PostBuildEvent = pbe.InnerText;
                        csprojModified = true;
                    }
                }
            }

            if (csprojModified)
            {
                try
                {
                    string bakCsproj = project.ProjectFilePath + ".bak";
                    if (!File.Exists(bakCsproj))
                    {
                        File.Copy(project.ProjectFilePath, bakCsproj, false);
                    }
                    doc.Save(project.ProjectFilePath);
                }
                catch (Exception ex)
                {
                    records.Add(new RepairRecord(
                        "儲存 .csproj",
                        project.ProjectFilePath,
                        "寫回修復後的 .csproj 失敗: " + ex.Message,
                        false));
                }
            }

            return records;
        }

        private void EnsureOutputDirectory(EepProjectInfo project, List<RepairRecord> records)
        {
            if (string.IsNullOrEmpty(project.OutputPath)) return;
            try
            {
                string outDir = Path.IsPathRooted(project.OutputPath)
                    ? project.OutputPath
                    : Path.GetFullPath(Path.Combine(project.ProjectDirectory, project.OutputPath));

                if (!Directory.Exists(outDir))
                {
                    string root = Path.GetPathRoot(outDir);
                    if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                    {
                        Directory.CreateDirectory(outDir);
                        records.Add(new RepairRecord(
                            "規則 4 (確保 OutputPath 存在)",
                            outDir,
                            "已自動建立專案輸出資料夾: " + outDir,
                            true));
                    }
                    else
                    {
                        // 若 OutputPath 寫死在不存在的磁碟機，自動降級為本地 bin\Debug\
                        string fallbackOut = Path.Combine(project.ProjectDirectory, @"bin\Debug\");
                        Directory.CreateDirectory(fallbackOut);
                        project.OutputPath = @"bin\Debug\";
                        records.Add(new RepairRecord(
                            "規則 4 (修正無效 OutputPath 磁碟機)",
                            fallbackOut,
                            string.Format("原 OutputPath '{0}' 磁碟機不存在，已切換至本地 '{1}'。", outDir, fallbackOut),
                            true));
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 規則 6：處理 .cs 原始碼檔案編碼
        /// - 預設 (convertBig5=false)：只幫「已被 Claude Code 改為無 BOM UTF-8」的檔案補上 UTF-8 BOM，不亂動其他未修改的 Big5 檔案（保護 Git/SVN/VSS 版控紀錄，且舊版編譯器本來就能直接編譯未動過的 Big5）
        /// - 顯式轉碼 (convertBig5=true)：當需要讓 Claude Code 編輯某個舊 Big5 檔案時，將 Big5 無損轉為 UTF-8 with BOM
        /// </summary>
        public void NormalizeSourceFileEncodings(EepProjectInfo project, List<RepairRecord> records, bool convertBig5)
        {
            if (project.CompileFiles == null) return;
            UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
            UTF8Encoding utf8WithBom = new UTF8Encoding(true);
            Encoding big5 = Encoding.GetEncoding(950);

            foreach (string csPath in project.CompileFiles)
            {
                if (!File.Exists(csPath)) continue;
                try
                {
                    byte[] raw = File.ReadAllBytes(csPath);
                    if (raw.Length == 0) continue;

                    // 1. 若已經帶有 UTF-8 BOM (EF BB BF)，直接略過
                    if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
                    {
                        continue;
                    }

                    // 檢查是否全為純 ASCII (0x00 ~ 0x7F)；若全為純英文數字則無須動它
                    bool hasNonAscii = false;
                    for (int i = 0; i < raw.Length; i++)
                    {
                        if (raw[i] > 0x7F)
                        {
                            hasNonAscii = true;
                            break;
                        }
                    }
                    if (!hasNonAscii) continue;

                    // 2. 含有中文字元但沒有 BOM：先測試是否為合法的無 BOM UTF-8 (例如剛被 Claude Code 編輯存檔)
                    string decodedText = null;
                    bool isUtf8NoBom = false;
                    try
                    {
                        decodedText = strictUtf8.GetString(raw);
                        isUtf8NoBom = true;
                    }
                    catch
                    {
                        isUtf8NoBom = false;
                    }

                    if (isUtf8NoBom)
                    {
                        // 補上 UTF-8 BOM，防止舊版 csc.exe / MSBuild 將其誤判為 Big5 而吃掉雙引號 (CS1010)
                        File.WriteAllText(csPath, decodedText, utf8WithBom);
                        records.Add(new RepairRecord(
                            "規則 6 (補上 UTF-8 BOM 防亂碼)",
                            csPath,
                            string.Format("檔案 '{0}' 原為無 BOM 之 UTF-8 (含中文字元)，已自動補上 UTF-8 BOM，防止舊版編譯器誤判為 Big5 報 CS1010 錯誤。", Path.GetFileName(csPath)),
                            true));
                    }
                    else if (convertBig5)
                    {
                        // 3. 顯式要求將舊版 Big5 (CP950) 轉為 UTF-8 with BOM 供 Claude Code 編輯
                        string bakPath = csPath + ".bak";
                        if (!File.Exists(bakPath))
                        {
                            File.Copy(csPath, bakPath, false);
                        }
                        decodedText = big5.GetString(raw);
                        File.WriteAllText(csPath, decodedText, utf8WithBom);
                        records.Add(new RepairRecord(
                            "規則 6 (Big5 轉 UTF-8 with BOM)",
                            csPath,
                            string.Format("檔案 '{0}' 原為 Big5 (CP950) 編碼，已備份並無損轉換為 UTF-8 with BOM 供 Claude Code 安全讀寫。", Path.GetFileName(csPath)),
                            true));
                    }
                }
                catch { }
            }
        }
    }
}

