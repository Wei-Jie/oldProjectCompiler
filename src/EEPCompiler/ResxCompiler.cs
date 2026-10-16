using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Xml;

namespace EEPCompiler
{
    /// <summary>
    /// 免安裝 Windows SDK (無須外部 ResGen.exe) 的內建 .resx 轉 .resources 資源編譯器
    /// 支援動態掛載 EEP 組件 (如 Srvtools.dll) 以反序列化 InfoCommand / InfoDataSet 等資源
    /// </summary>
    public class ResxCompiler
    {
        private readonly Dictionary<string, string> _resolvedAssemblies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 將專案內所有 .resx 編譯為 .resources 檔案，回傳給 csc.exe 使用的 /resource: 參數清單
        /// </summary>
        public List<string> CompileProjectResources(EepProjectInfo project, string intermediateObjDir, List<string> logs)
        {
            List<string> resourceArgs = new List<string>();
            if (project.EmbeddedResources == null || project.EmbeddedResources.Count == 0)
                return resourceArgs;

            if (!Directory.Exists(intermediateObjDir))
            {
                Directory.CreateDirectory(intermediateObjDir);
            }

            // 建立 AssemblyResolve 字典以供反序列化 EEP 元件時使用
            _resolvedAssemblies.Clear();
            foreach (ReferenceItem r in project.References)
            {
                if (r.IsResolved && !string.IsNullOrEmpty(r.ResolvedPath) && File.Exists(r.ResolvedPath))
                {
                    _resolvedAssemblies[r.ShortName] = r.ResolvedPath;
                }
            }

            ResolveEventHandler handler = new ResolveEventHandler(OnAssemblyResolve);
            AppDomain.CurrentDomain.AssemblyResolve += handler;

            try
            {
                foreach (ResourceItem item in project.EmbeddedResources)
                {
                    // 規則 3：略過 licenses.licx，不進行授權資源編譯
                    if (item.IsLicenseFile || item.RelativePath.EndsWith(".licx", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!File.Exists(item.FullPath))
                    {
                        logs.Add(string.Format("[資源警告] 找不到資源檔案: {0}", item.FullPath));
                        continue;
                    }

                    string ext = Path.GetExtension(item.FullPath).ToLowerInvariant();
                    if (ext == ".resx")
                    {
                        string outResFile = Path.Combine(intermediateObjDir, item.LogicalResourceName);
                        bool ok = ConvertResxToResources(item.FullPath, outResFile, logs);
                        if (ok && File.Exists(outResFile))
                        {
                            resourceArgs.Add(string.Format("/resource:\"{0}\",{1}", outResFile, item.LogicalResourceName));
                        }
                    }
                    else
                    {
                        // 非 .resx 的一般內嵌資源 (如 .bmp, .ico, .sql, .xml) 直接嵌入
                        resourceArgs.Add(string.Format("/resource:\"{0}\",{1}", item.FullPath, item.LogicalResourceName));
                    }
                }
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= handler;
            }

            // 規則 3 進階黑科技：若目標輸出路徑已有舊版編譯好的 DLL (或 .bak)，自動提取其中已簽章的 *.licenses 授權資源並移植至新 DLL，徹底避免執行期跳出 DevExpress 試用視窗！
            TryTransplantExistingLicenseResource(project, intermediateObjDir, resourceArgs, logs);

            return resourceArgs;
        }

        /// <summary>
        /// 從舊版 DLL (或 .dll.bak) 無損提取當年由正版 LC.exe 嵌入的 *.licenses 二進位資源，讓新編譯的 DLL 在無 LC.exe 環境下仍保有合法執行期授權
        /// </summary>
        public void TryTransplantExistingLicenseResource(EepProjectInfo project, string intermediateObjDir, List<string> resourceArgs, List<string> logs)
        {
            try
            {
                string outDir = Path.IsPathRooted(project.OutputPath)
                    ? project.OutputPath
                    : Path.GetFullPath(Path.Combine(project.ProjectDirectory, project.OutputPath));

                string ext = string.Equals(project.OutputType, "WinExe", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(project.OutputType, "Exe", StringComparison.OrdinalIgnoreCase)
                             ? ".exe" : ".dll";

                string targetFile = Path.Combine(outDir, project.AssemblyName + ext);
                string bakFile = targetFile + ".bak";

                string sourceBinary = File.Exists(bakFile) ? bakFile : (File.Exists(targetFile) ? targetFile : "");
                if (string.IsNullOrEmpty(sourceBinary)) return;

                byte[] rawBytes = File.ReadAllBytes(sourceBinary);
                Assembly oldAsm = Assembly.ReflectionOnlyLoad(rawBytes);
                string[] resNames = oldAsm.GetManifestResourceNames();
                if (resNames == null) return;

                foreach (string rName in resNames)
                {
                    if (rName.EndsWith(".licenses", StringComparison.OrdinalIgnoreCase))
                    {
                        using (Stream s = oldAsm.GetManifestResourceStream(rName))
                        {
                            if (s != null && s.Length > 0)
                            {
                                string extractedLicPath = Path.Combine(intermediateObjDir, rName);
                                using (FileStream fs = new FileStream(extractedLicPath, FileMode.Create, FileAccess.Write))
                                {
                                    byte[] buf = new byte[4096];
                                    int read;
                                    while ((read = s.Read(buf, 0, buf.Length)) > 0)
                                    {
                                        fs.Write(buf, 0, read);
                                    }
                                }
                                resourceArgs.Add(string.Format("/resource:\"{0}\",{1}", extractedLicPath, rName));
                                logs.Add(string.Format("[規則 3 - 授權移植] 已從舊版組件無損提取合法授權資源 '{0}' ({1} bytes) 並嵌入新版組件！", rName, s.Length));
                            }
                        }
                    }
                }
            }
            catch
            {
                // 若舊組件不含 .licenses 或無法讀取則安全略過
            }
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string shortName = new AssemblyName(args.Name).Name;
                string path;
                if (_resolvedAssemblies.TryGetValue(shortName, out path) && File.Exists(path))
                {
                    return Assembly.LoadFrom(path);
                }
            }
            catch { }
            return null;
        }

        private bool ConvertResxToResources(string resxPath, string outputResourcesPath, List<string> logs)
        {
            try
            {
                using (ResXResourceReader reader = new ResXResourceReader(resxPath))
                {
                    reader.BasePath = Path.GetDirectoryName(resxPath);
                    using (ResourceWriter writer = new ResourceWriter(outputResourcesPath))
                    {
                        IDictionaryEnumerator dict = reader.GetEnumerator();
                        while (dict.MoveNext())
                        {
                            string key = dict.Key as string;
                            if (key != null)
                            {
                                writer.AddResource(key, dict.Value);
                            }
                        }
                        writer.Generate();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                logs.Add(string.Format("[ResX 智慧容錯] 標準 ResXResourceReader 解析 '{0}' 時遇到外部型別限制 ({1})，啟動 XML 相容模式轉檔...",
                    Path.GetFileName(resxPath), ex.Message));
                return ConvertResxViaXmlFallback(resxPath, outputResourcesPath, logs);
            }
        }

        /// <summary>
        /// 當 .resx 內含無法在目前執行緒載入的設計期二進位物件時，使用 XML 容錯解析器轉換資源
        /// </summary>
        private bool ConvertResxViaXmlFallback(string resxPath, string outputResourcesPath, List<string> logs)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(resxPath);

                using (ResourceWriter writer = new ResourceWriter(outputResourcesPath))
                {
                    XmlNodeList dataNodes = doc.SelectNodes("//data");
                    if (dataNodes != null)
                    {
                        foreach (XmlNode node in dataNodes)
                        {
                            if (node.Attributes == null || node.Attributes["name"] == null) continue;
                            string name = node.Attributes["name"].Value;
                            string typeAttr = node.Attributes["type"] != null ? node.Attributes["type"].Value : "";
                            string mimeAttr = node.Attributes["mimetype"] != null ? node.Attributes["mimetype"].Value : "";

                            XmlNode valNode = node.SelectSingleNode("value");
                            if (valNode == null) continue;
                            string rawVal = valNode.InnerText;

                            if (string.IsNullOrEmpty(typeAttr) && string.IsNullOrEmpty(mimeAttr))
                            {
                                // 純字串資源 (包含 EEP InfoCommand 的 CommandText SQL 等)
                                writer.AddResource(name, rawVal);
                            }
                            else if (typeAttr.StartsWith("System.Boolean", StringComparison.OrdinalIgnoreCase))
                            {
                                bool b;
                                if (bool.TryParse(rawVal.Trim(), out b)) writer.AddResource(name, b);
                            }
                            else if (typeAttr.StartsWith("System.Int32", StringComparison.OrdinalIgnoreCase))
                            {
                                int n;
                                if (int.TryParse(rawVal.Trim(), out n)) writer.AddResource(name, n);
                            }
                            else if (!string.IsNullOrEmpty(mimeAttr) && mimeAttr.Contains("base64"))
                            {
                                // 若是二進位序列化資料，嘗試在目前 AppDomain 反序列化；若失敗則保留原始字串或略過
                                try
                                {
                                    byte[] bytes = Convert.FromBase64String(rawVal.Trim());
                                    if (mimeAttr.Contains("byte.base64"))
                                    {
                                        writer.AddResource(name, bytes);
                                    }
                                    else
                                    {
                                        using (MemoryStream ms = new MemoryStream(bytes))
                                        {
                                            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf =
                                                new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                                            object obj = bf.Deserialize(ms);
                                            writer.AddResource(name, obj);
                                        }
                                    }
                                }
                                catch
                                {
                                    // 略過無法於編譯期反序列化的第三方設計期屬性
                                }
                            }
                        }
                    }
                    writer.Generate();
                }
                return true;
            }
            catch (Exception ex)
            {
                logs.Add(string.Format("[資源錯誤] 無法轉換 .resx 檔案 '{0}': {1}", resxPath, ex.Message));
                return false;
            }
        }
    }
}
