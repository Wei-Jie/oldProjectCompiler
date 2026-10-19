using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
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
        private bool _targetClr20;

        /// <summary>
        /// 將專案內所有 .resx 編譯為 .resources 檔案，回傳給 csc.exe 使用的 /resource: 參數清單
        /// </summary>
        public List<string> CompileProjectResources(EepProjectInfo project, string intermediateObjDir, List<string> logs, List<string> notices, bool targetClr20)
        {
            _targetClr20 = targetClr20;
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

                    // 文化特定資源 (如 Component.vi-VN.resx) 屬於衛星組件 (vi-VN\Assembly.resources.dll)，
                    // 不可編入主組件：其 LogicalResourceName 與中性資源相同，會覆蓋中性資源 (例如 InfoCommand 的 CommandText SQL)
                    string culture = GetResourceCulture(item.FullPath);
                    if (!string.IsNullOrEmpty(culture))
                    {
                        string msg = string.Format("[資源略過] '{0}' 為文化特定資源 ({1})，屬於衛星組件 {1}\\{2}.resources.dll，未編入主組件；本工具不產生衛星組件。",
                            item.RelativePath, culture, project.AssemblyName);
                        logs.Add(msg);
                        if (notices != null) notices.Add(msg);
                        continue;
                    }

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
            int logCountBefore = logs.Count;
            TryTransplantExistingLicenseResource(project, intermediateObjDir, resourceArgs, logs);
            if (notices != null)
            {
                // 授權資源是從舊組件移植而來，必須讓使用者知道來源
                for (int i = logCountBefore; i < logs.Count; i++) notices.Add(logs[i]);

                // 專案使用授權元件 (有 licenses.licx) 卻沒有可移植的授權資源時，產出的 DLL 不含 *.licenses
                bool hasLicenseResource = false;
                foreach (string arg in resourceArgs)
                {
                    if (arg.EndsWith(".licenses", StringComparison.OrdinalIgnoreCase)) hasLicenseResource = true;
                }
                if (project.LicenseFiles.Count > 0 && !hasLicenseResource)
                {
                    string outDir = Path.IsPathRooted(project.OutputPath)
                        ? project.OutputPath
                        : Path.GetFullPath(Path.Combine(project.ProjectDirectory, project.OutputPath));
                    notices.Add(string.Format("[授權資源] 專案有 licenses.licx，但 {0} 沒有舊版 {1} 可移植授權資源，產出的 DLL 不含 *.licenses；DevExpress 等元件執行時可能出現試用提示。如需授權資源，請先把舊版 DLL 放到該資料夾再編譯。",
                        outDir, project.AssemblyName + ".dll"));
                }
            }

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
                                logs.Add(string.Format("[規則 3 - 授權移植] 已從舊版組件無損提取合法授權資源 '{0}' ({1} bytes) 並嵌入新版組件！來源: {2}", rName, s.Length, sourceBinary));
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

        /// <summary>
        /// 若檔名為 Name.&lt;culture&gt;.ext (如 Component.vi-VN.resx) 且 culture 為有效的文化名稱，回傳該文化名稱；否則回傳空字串
        /// </summary>
        public static string GetResourceCulture(string resourcePath)
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(resourcePath);
            int dot = nameWithoutExt.LastIndexOf('.');
            if (dot <= 0 || dot == nameWithoutExt.Length - 1) return "";

            string candidate = nameWithoutExt.Substring(dot + 1);
            try
            {
                CultureInfo ci = CultureInfo.GetCultureInfo(candidate);
                return string.IsNullOrEmpty(ci.Name) ? "" : ci.Name;
            }
            catch (ArgumentException)
            {
                return "";
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

        /// <summary>
        /// 寫入單一資源。目標為 CLR v2.0 時，非基本型別的物件不可直接 AddResource：
        /// 本工具在 .NET 4 上執行，重新序列化會寫入 System.Data / System.Drawing 4.0.0.0，CLR v2.0 載入時找不到組件而無法開啟表單。
        /// 因此 resx 內原本就是 BinaryFormatter 序列化的資料直接沿用原始位元組；其他物件則序列化時把 Framework 組件版本對應回 2.0.0.0。
        /// </summary>
        private void AddResourceValue(ResourceWriter writer, string key, object value, Dictionary<string, byte[]> rawSerialized)
        {
            if (!_targetClr20 || value == null || IsPrimitiveResource(value))
            {
                writer.AddResource(key, value);
                return;
            }

            string typeName = Clr20NameBinder.MapToClr20(value.GetType().AssemblyQualifiedName);
            byte[] data;
            if (!rawSerialized.TryGetValue(key, out data))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Binder = new Clr20NameBinder();
                    bf.Serialize(ms, value);
                    data = ms.ToArray();
                }
            }
            writer.AddResourceData(key, typeName, data);
        }

        private static bool IsPrimitiveResource(object value)
        {
            return value is string || value is bool || value is char || value is byte || value is sbyte ||
                   value is short || value is ushort || value is int || value is uint || value is long || value is ulong ||
                   value is float || value is double || value is decimal || value is DateTime || value is TimeSpan ||
                   value is byte[] || value is Stream;
        }

        /// <summary>
        /// 讀取 resx 中 mimetype 為 binary.base64 (BinaryFormatter 序列化) 的原始位元組，以資源名稱為鍵
        /// </summary>
        private static Dictionary<string, byte[]> ReadRawSerializedNodes(string resxPath)
        {
            Dictionary<string, byte[]> result = new Dictionary<string, byte[]>();
            XmlDocument doc = new XmlDocument();
            doc.Load(resxPath);
            XmlNodeList dataNodes = doc.SelectNodes("//data");
            if (dataNodes == null) return result;

            foreach (XmlNode node in dataNodes)
            {
                if (node.Attributes == null || node.Attributes["name"] == null || node.Attributes["mimetype"] == null) continue;
                if (node.Attributes["mimetype"].Value != ResXResourceWriter.BinSerializedObjectMimeType) continue;
                XmlNode valNode = node.SelectSingleNode("value");
                if (valNode == null) continue;
                try
                {
                    result[node.Attributes["name"].Value] = Convert.FromBase64String(valNode.InnerText.Trim());
                }
                catch (FormatException) { }
            }
            return result;
        }

        private bool ConvertResxToResources(string resxPath, string outputResourcesPath, List<string> logs)
        {
            try
            {
                Dictionary<string, byte[]> rawSerialized = ReadRawSerializedNodes(resxPath);
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
                                AddResourceValue(writer, key, dict.Value, rawSerialized);
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
                                            Dictionary<string, byte[]> raw = new Dictionary<string, byte[]>();
                                            raw[name] = bytes;
                                            AddResourceValue(writer, name, obj, raw);
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

    /// <summary>
    /// 序列化時把 .NET Framework 4.0 組件名稱對應回 2.0.0.0，讓 CLR v2.0 (VS2005 / EEP2006) 能反序列化
    /// </summary>
    internal sealed class Clr20NameBinder : SerializationBinder
    {
        // mscorlib / System / System.Data 等 (b77a5c561934e089) 與 System.Drawing / System.Design 等 (b03f5f7f11d50a3a)
        private static readonly string[] FrameworkTokens = new string[] { "b77a5c561934e089", "b03f5f7f11d50a3a" };

        public static string MapToClr20(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            foreach (string token in FrameworkTokens)
            {
                name = name.Replace("Version=4.0.0.0, Culture=neutral, PublicKeyToken=" + token,
                                    "Version=2.0.0.0, Culture=neutral, PublicKeyToken=" + token);
            }
            return name;
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = MapToClr20(serializedType.Assembly.FullName);
            typeName = MapToClr20(serializedType.FullName);
        }

        public override Type BindToType(string assemblyName, string typeName)
        {
            return null;
        }
    }
}
