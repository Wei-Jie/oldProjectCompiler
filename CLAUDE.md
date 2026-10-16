# Infolight EEP (.NET Framework / VS2005) 專案編譯與修復規則 (Claude Code 專用設定檔)

## 專案背景與架構說明
本專案為使用 **訊光科技 (Infolight) EEP (Enterprise Execution Platform)** 框架所開發之 C# 系統（基於 `.NET Framework 2.0 / 3.5 / 4.0` 與 Visual Studio 2005 架構）。
由於開發環境未必安裝舊版 Visual Studio 2005 IDE 或 Windows SDK，我們已配置了專屬的可攜式編譯與自動修復工具 **`eepc.exe`**（與 `eep_tool.py`）。

當你在進行程式碼修改、除錯與自動化編譯時，**必須嚴格遵守以下規範與自動化閉環流程**。

---

## 核心自動化編譯指令（改完程式必執行）

每次修改完任何 `.cs` 或 `.csproj` 檔案後，**嚴禁執行 `dotnet build`**，請直接呼叫本專案隨附的 `eepc.exe`：

```powershell
# 自動執行規則 2~4 修復、規則 5 語法預檢，並使用雙核心引擎編譯（輸出 UTF-8 AI 診斷報告）
& ".\eepc.exe" build "<目標專案.csproj 或 方案.sln>" --auto-repair --claude
```

- 若你的工作目錄下 `eepc.exe` 放在子資料夾（例如 `.\EEPCompiler_Portable\eepc.exe`），請調整對應相對路徑，或指定訊光目錄：
  ```powershell
  & ".\EEPCompiler_Portable\eepc.exe" build "<目標專案.csproj>" --eep-root "C:\EEP.NET" --auto-repair --claude
  ```
- **Exit Code `0`**：代表編譯成功（`PASS (0 Error(s))`），DLL 已產出。
- **Exit Code `1`**：代表有語法或編譯錯誤，請根據終端機輸出的 **AI 修復指引** 修正 `.cs` 後重新執行上述指令，直到 `PASS` 為止。

---

## 五大環境與編譯規範（AI 代理必守鐵則）

### 規則 1：禁止使用 `dotnet build`，強制使用 `eepc.exe` 或 `MSBuild.exe`
- 舊版 EEP .NET 專案結構（包含 WinForms `InfoForm`、Server 端 `InfoDataModule` / `DataModule`、Remoting / WCF 架構）與現代 `.NET SDK CLI` 不相容。
- **禁止執行** `dotnet build` 或 `dotnet restore`。
- 請一律優先使用 `eepc.exe build <專案> --auto-repair --claude`（內部已整合本機 `MSBuild.exe` 自動搜尋，以及在缺少 `ResGen.exe` / `LC.exe` 時自動切換至免 SDK 的內建 Direct CSC + ResX 跨編譯引擎）。

### 規則 2：修復 EEP 專屬 DLL 引用路徑 (解決 `CS0246` 錯誤)
- EEP 系統高度依賴其私有底層組件（例如：`Srvtools.dll`、`InfoRemoteModule.dll`、`EFClientTools.dll`、`EFServerTools.dll`、`EFBase.dll`、`FLCore.dll`、`FLTools.dll` 等）。
- 當出現 `CS0246` 時，可執行 `eepc.exe repair <專案> --eep-root <EEP目錄>` 自動搜尋並更新 `.csproj` 的 `<HintPath>`，且確保 `<SpecificVersion>False</SpecificVersion>`。

### 規則 3：繞過第三方元件授權檢查 (解決 `LC.exe exited with code -1` 或 `MSB3091`)
- 若專案包含 `Properties\licenses.licx`，未安裝第三方控制項（如 DevExpress、FarPoint）本機開發授權或缺少 `LC.exe` 會導致編譯中斷。
- `eepc.exe --auto-repair` 會自動將 `licenses.licx` 內容清空為 0 byte（保留空白檔案以免 `.csproj` 找不到檔案），並將 `.csproj` 中的 `<EmbeddedResource Include="...licenses.licx">` 轉為 `<None>`。請勿手動將授權文字加回 `licenses.licx`。

### 規則 4：處理 EEP 特有的輸出路徑與建置後事件 (解決 `MSB3073` 錯誤)
- EEP 架構將模組分為 Server 端（放入 `EEPNetServer\<Solution>\`）與 Client 端（放入 `EEPNetClient\<Solution>\`）。
- 若 `.csproj` 的 `<PostBuildEvent>` 寫死不存在的磁碟機或目錄（如 `copy $(TargetPath) Z:\...`），`eepc.exe --auto-repair` 會自動註解無效路徑並確保本地 `<OutputPath>` 存在。

### 規則 5：C# 語法版本降級與保護 EEP Designer 結構 (極重要！)
舊版 `MSBuild.exe`（特別是 `.NET Framework 4.0` 內建的 `v4.0.30319\MSBuild.exe` 與 VS2005 `v2.0.50727\csc.exe`）最高僅支援 **C# 5.0（或 C# 2.0/3.0）**。撰寫或修改程式碼時，**嚴格禁止使用現代 C# 6.0 ~ 12.0 語法**：

1. **禁止使用字串插值 `$"..."` (C# 6.0)** -> 必須改用 `string.Format("文字 {0}", val)` 或傳統字串串接。
2. **禁止使用 Null 條件運算子 `?.` (C# 6.0)** -> 必須改用 `if (obj != null)` 或三元運算子 `obj != null ? obj.Prop : null`。
3. **禁止使用 `nameof(x)` (C# 6.0)** -> 必須直接使用字串常數 `"x"`。
4. **禁止使用自動屬性預設值 `{ get; set; } = val;` (C# 6.0)** -> 必須在建構子 (Constructor) 中賦值，或使用傳統私有欄位 (Backing Field)。
5. **禁止使用 File-scoped namespaces (`namespace MyNamespace;`) (C# 10.0)** -> 必須使用傳統大括號 `namespace MyNamespace { ... }`。
6. **禁止使用簡化版 `using var x = ...;` (C# 8.0)** -> 必須使用傳統 `using (var x = ...) { ... }`。
7. **禁止使用 Target-typed `new()` (C# 9.0)** -> 必須寫出完整型別 `new List<string>()`。
8. **禁止使用 Nullable Reference Types (`string?`, `!`) 與 `switch` 運算式 (C# 8.0)**。
9. **嚴禁破壞 EEP Designer 結構**：
   - 嚴禁刪除或重新命名繼承自 `InfoDataModule`、`DataModule`、`InfoForm` 的類別結構與建構子中的 `InitializeComponent()`。
   - 嚴禁移除 `.Designer.cs` 內的 `InfoDataSet`、`InfoBindingSource`、`InfoCommand`、`UpdateComponent` 等訊光專屬元件初始化宣告。
