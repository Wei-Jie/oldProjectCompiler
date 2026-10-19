// ResCheck：在 CLR v2.0 上用 ResourceManager 讀取組件內每個 .resources 的所有資源 (會實際反序列化 DataSet、Bitmap 等物件)，
// 模擬 EEP2006 (CLR v2.0) 開啟表單時讀取資源的行為。只讀取資源，不執行組件內的程式碼。
// 用法：ResCheck <組件.dll>
using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;

internal static class ResCheck
{
    private static int Main(string[] args)
    {
        Console.WriteLine("CLR " + Environment.Version + "  " + args[0]);
        Assembly asm = Assembly.LoadFrom(args[0]);
        int failures = 0;
        foreach (string res in asm.GetManifestResourceNames())
        {
            if (!res.EndsWith(".resources")) continue;
            string baseName = res.Substring(0, res.Length - ".resources".Length);
            try
            {
                ResourceManager rm = new ResourceManager(baseName, asm);
                ResourceSet set = rm.GetResourceSet(CultureInfo.InvariantCulture, true, false);
                int count = 0;
                foreach (DictionaryEntry e in set)
                {
                    object v = e.Value;
                    count++;
                    if (!(v is string))
                        Console.WriteLine("  ok  " + res + " [" + e.Key + "] " + (v == null ? "null" : v.GetType().AssemblyQualifiedName));
                }
                Console.WriteLine("  ok  " + res + " (" + count + " 筆)");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine("  FAIL " + res + " : " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        return failures == 0 ? 0 : 1;
    }
}
