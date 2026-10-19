// DllCompare：把 .NET 組件轉成「正規化文字清單」，用來比對兩次編譯的結果是否一致。
// 只讀取中繼資料 (System.Reflection.Metadata)，不載入、不執行組件，因此不需要 DevExpress 等相依組件。
// 會排除每次編譯必然不同的項目：PE 時間戳記、MVID、metadata token 編號 (IL 內的 token 一律換成名稱)。
//
// 用法：DllCompare <輸入.dll> <輸出.txt>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Resources;
using System.Security.Cryptography;
using System.Text;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("用法: DllCompare <輸入.dll> <輸出.txt>");
            return 2;
        }

        using (FileStream fs = File.OpenRead(args[0]))
        using (PEReader pe = new PEReader(fs))
        {
            Dumper d = new Dumper(pe);
            File.WriteAllText(args[1], d.Dump(), new UTF8Encoding(true));
        }
        return 0;
    }
}

internal sealed class Dumper
{
    private readonly PEReader _pe;
    private readonly MetadataReader _md;
    private readonly SigProvider _sig;
    private readonly StringBuilder _sb = new StringBuilder();
    private static readonly Dictionary<short, OpCode> OpCodesByValue = BuildOpCodeTable();

    public Dumper(PEReader pe)
    {
        _pe = pe;
        _md = pe.GetMetadataReader();
        _sig = new SigProvider(_md);
    }

    public string Dump()
    {
        DumpAssembly();
        DumpTypes();
        DumpResources();
        return _sb.ToString();
    }

    // ---------- 組件 ----------

    private void DumpAssembly()
    {
        AssemblyDefinition asm = _md.GetAssemblyDefinition();
        _sb.AppendLine("# 組件");
        _sb.AppendLine("name: " + _md.GetString(asm.Name));
        _sb.AppendLine("version: " + asm.Version);
        _sb.AppendLine("culture: " + _md.GetString(asm.Culture));
        _sb.AppendLine("runtime: " + _md.MetadataVersion);
        _sb.AppendLine("pe: " + _pe.PEHeaders.CoffHeader.Machine + " / " + _pe.PEHeaders.CorHeader.Flags);

        _sb.AppendLine();
        _sb.AppendLine("# 參照組件");
        List<string> refs = new List<string>();
        foreach (AssemblyReferenceHandle h in _md.AssemblyReferences)
        {
            AssemblyReference r = _md.GetAssemblyReference(h);
            refs.Add(_md.GetString(r.Name) + " " + r.Version + " token=" + Hex(_md.GetBlobBytes(r.PublicKeyOrToken)));
        }
        refs.Sort(StringComparer.Ordinal);
        foreach (string r in refs) _sb.AppendLine(r);

        _sb.AppendLine();
        _sb.AppendLine("# 組件屬性");
        List<string> attrs = new List<string>();
        foreach (CustomAttributeHandle h in asm.GetCustomAttributes())
        {
            attrs.Add(FormatAttribute(_md.GetCustomAttribute(h)));
        }
        attrs.Sort(StringComparer.Ordinal);
        foreach (string a in attrs) _sb.AppendLine(a);
    }

    // ---------- 型別與成員 ----------

    private void DumpTypes()
    {
        _sb.AppendLine();
        _sb.AppendLine("# 型別");

        List<TypeDefinitionHandle> types = _md.TypeDefinitions.ToList();
        types.Sort((a, b) => string.CompareOrdinal(TypeDefName(a), TypeDefName(b)));

        foreach (TypeDefinitionHandle th in types)
        {
            TypeDefinition td = _md.GetTypeDefinition(th);
            string baseName = td.BaseType.IsNil ? "" : " : " + _sig.TypeName(td.BaseType);
            _sb.AppendLine();
            _sb.AppendLine("type " + TypeDefName(th) + baseName + "  [" + td.Attributes + "]");

            List<string> fields = new List<string>();
            foreach (FieldDefinitionHandle fh in td.GetFields())
            {
                FieldDefinition f = _md.GetFieldDefinition(fh);
                fields.Add("  field " + _md.GetString(f.Name) + " : " + f.DecodeSignature(_sig, null) + "  [" + f.Attributes + "]");
            }
            fields.Sort(StringComparer.Ordinal);
            foreach (string f in fields) _sb.AppendLine(f);

            List<string> props = new List<string>();
            foreach (PropertyDefinitionHandle ph in td.GetProperties())
            {
                PropertyDefinition p = _md.GetPropertyDefinition(ph);
                MethodSignature<string> ps = p.DecodeSignature(_sig, null);
                props.Add("  property " + _md.GetString(p.Name) + " : " + ps.ReturnType);
            }
            props.Sort(StringComparer.Ordinal);
            foreach (string p in props) _sb.AppendLine(p);

            List<KeyValuePair<string, MethodDefinitionHandle>> methods = new List<KeyValuePair<string, MethodDefinitionHandle>>();
            foreach (MethodDefinitionHandle mh in td.GetMethods())
            {
                methods.Add(new KeyValuePair<string, MethodDefinitionHandle>(MethodHeader(mh), mh));
            }
            methods.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            foreach (KeyValuePair<string, MethodDefinitionHandle> m in methods)
            {
                _sb.AppendLine("  method " + m.Key);
                DumpMethodBody(m.Value);
            }
        }
    }

    private string TypeDefName(TypeDefinitionHandle h)
    {
        TypeDefinition td = _md.GetTypeDefinition(h);
        string name = _md.GetString(td.Name);
        string ns = _md.GetString(td.Namespace);
        TypeDefinitionHandle decl = td.GetDeclaringType();
        if (!decl.IsNil) return TypeDefName(decl) + "/" + name;
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    private string MethodHeader(MethodDefinitionHandle mh)
    {
        MethodDefinition m = _md.GetMethodDefinition(mh);
        MethodSignature<string> s = m.DecodeSignature(_sig, null);
        return s.ReturnType + " " + _md.GetString(m.Name) + "(" + string.Join(", ", s.ParameterTypes) + ")  [" + m.Attributes + "]";
    }

    private void DumpMethodBody(MethodDefinitionHandle mh)
    {
        MethodDefinition m = _md.GetMethodDefinition(mh);
        if (m.RelativeVirtualAddress == 0) return;

        MethodBodyBlock body = _pe.GetMethodBody(m.RelativeVirtualAddress);
        if (!body.LocalSignature.IsNil)
        {
            StandaloneSignature ls = _md.GetStandaloneSignature(body.LocalSignature);
            ImmutableArray<string> locals = ls.DecodeLocalSignature(_sig, null);
            _sb.AppendLine("    locals: " + string.Join(", ", locals));
        }
        // 先解碼全部指令，再把位移量換成「指令序號」：在方法中插入程式碼時，其後指令的位移量會整段平移，
        // 若直接輸出位移量會讓比對結果充滿無意義的差異
        List<int> offsets = new List<int>();
        List<string> texts = new List<string>();
        BlobReader il = body.GetILReader();
        while (il.RemainingBytes > 0)
        {
            offsets.Add(il.Offset);
            short code = il.ReadByte();
            if (code == 0xFE) code = (short)(0xFE00 | il.ReadByte());

            OpCode op;
            if (!OpCodesByValue.TryGetValue(code, out op))
            {
                texts.Add("??? 0x" + code.ToString("X"));
                break;
            }
            texts.Add(op.Name + Operand(op, ref il));
        }

        Dictionary<int, int> indexOf = new Dictionary<int, int>();
        for (int i = 0; i < offsets.Count; i++) indexOf[offsets[i]] = i;
        indexOf[body.Size] = offsets.Count;

        foreach (ExceptionRegion er in body.ExceptionRegions)
        {
            _sb.AppendLine("    try " + er.Kind + " " + Range(indexOf, er.TryOffset, er.TryLength) +
                           " handler " + Range(indexOf, er.HandlerOffset, er.HandlerLength) +
                           (er.CatchType.IsNil ? "" : " catch " + _sig.TypeName(er.CatchType)));
        }

        for (int i = 0; i < texts.Count; i++)
        {
            string t = BranchMarker.Replace(texts[i], m =>
            {
                int target;
                int targetIndex = indexOf.TryGetValue(int.Parse(m.Groups[1].Value), out target) ? target : -1;
                if (targetIndex < 0) return "IL_?";
                int delta = targetIndex - i;
                return delta >= 0 ? "->+" + delta : "->" + delta;
            });
            _sb.AppendLine("    " + t);
        }
    }

    private static readonly System.Text.RegularExpressions.Regex BranchMarker =
        new System.Text.RegularExpressions.Regex(@"@BR\((\d+)\)");

    private static string Range(Dictionary<int, int> indexOf, int offset, int length)
    {
        int start, end;
        string s = indexOf.TryGetValue(offset, out start) ? "#" + start : "?";
        string e = indexOf.TryGetValue(offset + length, out end) ? "#" + end : "?";
        return s + ".." + e;
    }

    private string Operand(OpCode op, ref BlobReader il)
    {
        switch (op.OperandType)
        {
            case OperandType.InlineNone: return "";
            case OperandType.ShortInlineI: return " " + il.ReadSByte();
            case OperandType.ShortInlineVar: return " " + il.ReadByte();
            case OperandType.ShortInlineBrTarget: { int d = il.ReadSByte(); return " @BR(" + (il.Offset + d) + ")"; }
            case OperandType.InlineBrTarget: { int d = il.ReadInt32(); return " @BR(" + (il.Offset + d) + ")"; }
            case OperandType.InlineI: return " " + il.ReadInt32();
            case OperandType.InlineI8: return " " + il.ReadInt64();
            case OperandType.ShortInlineR: return " " + il.ReadSingle().ToString("R");
            case OperandType.InlineR: return " " + il.ReadDouble().ToString("R");
            case OperandType.InlineVar: return " " + il.ReadUInt16();
            case OperandType.InlineString:
                {
                    int tok = il.ReadInt32();
                    return " \"" + Escape(_md.GetUserString(MetadataTokens.UserStringHandle(tok & 0xFFFFFF))) + "\"";
                }
            case OperandType.InlineSwitch:
                {
                    int n = il.ReadInt32();
                    int[] deltas = new int[n];
                    for (int i = 0; i < n; i++) deltas[i] = il.ReadInt32();
                    int baseOffset = il.Offset;
                    return " (" + string.Join(", ", deltas.Select(x => "@BR(" + (baseOffset + x) + ")")) + ")";
                }
            default:
                // InlineField / InlineMethod / InlineTok / InlineType / InlineSig：token 換成名稱
                return " " + TokenName(il.ReadInt32());
        }
    }

    private string TokenName(int token)
    {
        EntityHandle h = MetadataTokens.EntityHandle(token);
        switch (h.Kind)
        {
            case HandleKind.TypeDefinition:
            case HandleKind.TypeReference:
            case HandleKind.TypeSpecification:
                return _sig.TypeName(h);
            case HandleKind.FieldDefinition:
                {
                    FieldDefinition f = _md.GetFieldDefinition((FieldDefinitionHandle)h);
                    return f.DecodeSignature(_sig, null) + " " + TypeDefName(f.GetDeclaringType()) + "::" + _md.GetString(f.Name);
                }
            case HandleKind.MethodDefinition:
                {
                    MethodDefinition m = _md.GetMethodDefinition((MethodDefinitionHandle)h);
                    MethodSignature<string> s = m.DecodeSignature(_sig, null);
                    return s.ReturnType + " " + TypeDefName(m.GetDeclaringType()) + "::" + _md.GetString(m.Name) + "(" + string.Join(", ", s.ParameterTypes) + ")";
                }
            case HandleKind.MemberReference:
                {
                    MemberReference mr = _md.GetMemberReference((MemberReferenceHandle)h);
                    string parent = _sig.TypeName(mr.Parent);
                    string name = _md.GetString(mr.Name);
                    if (mr.GetKind() == MemberReferenceKind.Field)
                        return mr.DecodeFieldSignature(_sig, null) + " " + parent + "::" + name;
                    MethodSignature<string> s = mr.DecodeMethodSignature(_sig, null);
                    return s.ReturnType + " " + parent + "::" + name + "(" + string.Join(", ", s.ParameterTypes) + ")";
                }
            case HandleKind.MethodSpecification:
                {
                    MethodSpecification ms = _md.GetMethodSpecification((MethodSpecificationHandle)h);
                    return TokenName(MetadataTokens.GetToken(ms.Method)) + "<" + string.Join(", ", ms.DecodeSignature(_sig, null)) + ">";
                }
            case HandleKind.StandaloneSignature:
                return "sig";
            default:
                return h.Kind.ToString();
        }
    }

    // ---------- 資源 ----------

    private void DumpResources()
    {
        _sb.AppendLine();
        _sb.AppendLine("# 內嵌資源");

        PEMemoryBlock resSection = _pe.GetSectionData(_pe.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress);
        List<ManifestResource> resources = _md.ManifestResources.Select(h => _md.GetManifestResource(h)).ToList();
        resources.Sort((a, b) => string.CompareOrdinal(_md.GetString(a.Name), _md.GetString(b.Name)));

        foreach (ManifestResource r in resources)
        {
            string name = _md.GetString(r.Name);
            if (!r.Implementation.IsNil)
            {
                _sb.AppendLine("resource " + name + " (外部)");
                continue;
            }

            BlobReader br = resSection.GetReader((int)r.Offset, resSection.Length - (int)r.Offset);
            int len = br.ReadInt32();
            byte[] data = br.ReadBytes(len);
            _sb.AppendLine();
            _sb.AppendLine("resource " + name + "  length=" + len + "  sha256=" + Sha(data));

            if (name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
            {
                DumpResourceEntries(data);
            }
        }
    }

    private void DumpResourceEntries(byte[] data)
    {
        try
        {
            using (ResourceReader rr = new ResourceReader(new MemoryStream(data)))
            {
                List<string> keys = new List<string>();
                System.Collections.IDictionaryEnumerator e = rr.GetEnumerator();
                while (e.MoveNext()) keys.Add((string)e.Key);
                keys.Sort(StringComparer.Ordinal);

                foreach (string key in keys)
                {
                    string type;
                    byte[] raw;
                    rr.GetResourceData(key, out type, out raw);
                    if (type == "ResourceTypeCode.String")
                    {
                        string value = new BinaryReader(new MemoryStream(raw), Encoding.UTF8).ReadString();
                        _sb.AppendLine("  [" + key + "] string = \"" + Escape(value) + "\"");
                    }
                    else
                    {
                        _sb.AppendLine("  [" + key + "] " + type + " length=" + raw.Length + " sha256=" + Sha(raw));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _sb.AppendLine("  (無法解析 .resources: " + ex.Message + ")");
        }
    }

    // ---------- 共用 ----------

    private string FormatAttribute(CustomAttribute ca)
    {
        string ctor;
        if (ca.Constructor.Kind == HandleKind.MemberReference)
            ctor = _sig.TypeName(_md.GetMemberReference((MemberReferenceHandle)ca.Constructor).Parent);
        else
            ctor = TypeDefName(_md.GetMethodDefinition((MethodDefinitionHandle)ca.Constructor).GetDeclaringType());

        // 參數 blob：字串參數 (如 AssemblyTitle) 直接轉成可讀文字，其餘保留十六進位
        byte[] blob = _md.GetBlobBytes(ca.Value);
        string readable = Encoding.UTF8.GetString(blob.Skip(2).ToArray()).Replace("\0", "").Trim();
        return ctor + " = " + Escape(readable) + "  (" + Hex(blob) + ")";
    }

    private static Dictionary<short, OpCode> BuildOpCodeTable()
    {
        Dictionary<short, OpCode> map = new Dictionary<short, OpCode>();
        foreach (FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            OpCode op = (OpCode)f.GetValue(null);
            map[op.Value] = op;
        }
        return map;
    }

    private static string Sha(byte[] data)
    {
        return Hex(SHA256.HashData(data)).Substring(0, 16);
    }

    private static string Hex(byte[] data)
    {
        return Convert.ToHexString(data);
    }

    private static string Escape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\"", "\\\"");
    }
}

/// <summary>
/// 將簽章解碼為可讀的型別名稱；外部型別以 [組件]命名空間.名稱 表示，不需要載入該組件
/// </summary>
internal sealed class SigProvider : ISignatureTypeProvider<string, object>
{
    private readonly MetadataReader _md;

    public SigProvider(MetadataReader md)
    {
        _md = md;
    }

    public string TypeName(EntityHandle h)
    {
        switch (h.Kind)
        {
            case HandleKind.TypeDefinition: return GetTypeFromDefinition(_md, (TypeDefinitionHandle)h, 0);
            case HandleKind.TypeReference: return GetTypeFromReference(_md, (TypeReferenceHandle)h, 0);
            case HandleKind.TypeSpecification: return GetTypeFromSpecification(_md, null, (TypeSpecificationHandle)h, 0);
            case HandleKind.MethodDefinition: return GetTypeFromDefinition(_md, _md.GetMethodDefinition((MethodDefinitionHandle)h).GetDeclaringType(), 0);
            case HandleKind.ModuleReference: return "[module]";
            default: return h.Kind.ToString();
        }
    }

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) { return typeCode.ToString(); }

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        TypeDefinition td = reader.GetTypeDefinition(handle);
        string name = reader.GetString(td.Name);
        TypeDefinitionHandle decl = td.GetDeclaringType();
        if (!decl.IsNil) return GetTypeFromDefinition(reader, decl, 0) + "/" + name;
        string ns = reader.GetString(td.Namespace);
        return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        TypeReference tr = reader.GetTypeReference(handle);
        string name = reader.GetString(tr.Name);
        string ns = reader.GetString(tr.Namespace);
        string full = string.IsNullOrEmpty(ns) ? name : ns + "." + name;

        if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            return GetTypeFromReference(reader, (TypeReferenceHandle)tr.ResolutionScope, 0) + "/" + name;
        if (tr.ResolutionScope.Kind == HandleKind.AssemblyReference)
            return "[" + reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name) + "]" + full;
        return full;
    }

    public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }

    public string GetSZArrayType(string elementType) { return elementType + "[]"; }
    public string GetArrayType(string elementType, ArrayShape shape) { return elementType + "[" + new string(',', shape.Rank - 1) + "]"; }
    public string GetByReferenceType(string elementType) { return elementType + "&"; }
    public string GetPointerType(string elementType) { return elementType + "*"; }
    public string GetPinnedType(string elementType) { return elementType + " pinned"; }
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) { return genericType + "<" + string.Join(", ", typeArguments) + ">"; }
    public string GetGenericTypeParameter(object genericContext, int index) { return "!" + index; }
    public string GetGenericMethodParameter(object genericContext, int index) { return "!!" + index; }
    public string GetFunctionPointerType(MethodSignature<string> signature) { return "method " + signature.ReturnType + "(" + string.Join(", ", signature.ParameterTypes) + ")"; }
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) { return unmodifiedType + (isRequired ? " modreq(" : " modopt(") + modifier + ")"; }
}
