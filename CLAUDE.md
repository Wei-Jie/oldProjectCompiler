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
# 在專案副本上編譯，並一律以 --output 指定輸出資料夾（見規則 7）
& ".\EEPCompiler_Portable\eepc.exe" build "<專案副本>\<目標專案.csproj>" --auto-repair --eep-root "<EEP根目錄，例如 D:\EEP2006>" --output ".\eepc_out\<專案名稱>" --claude
```

- 若 `eepc.exe` 不在 `.\EEPCompiler_Portable\` 下，請調整對應的相對路徑。
- **Exit Code `0`**：代表編譯成功（`PASS (0 Error(s))`），DLL 已產出到 `--output` 指定的資料夾。
- **Exit Code `1`**：代表編譯失敗。先看報告中的錯誤明細判斷原因：參考組件或資源錯誤（如 CS0246 找不到 Srvtools、CS1508 資源重複）請修正參考與 `--eep-root`，不要修改 `.cs`；確定是語法錯誤時才依規則 5 修正，直到 `PASS` 為止。

---

## 環境與編譯規範（AI 代理必守鐵則，共 7 條）

### 規則 1：禁止使用 `dotnet build`，強制使用 `eepc.exe` 或 `MSBuild.exe`
- 舊版 EEP .NET 專案結構（包含 WinForms `InfoForm`、Server 端 `InfoDataModule` / `DataModule`、Remoting / WCF 架構）與現代 `.NET SDK CLI` 不相容。
- **禁止執行** `dotnet build` 或 `dotnet restore`。
- 請一律優先使用 `eepc.exe build <專案> --auto-repair --eep-root <EEP根目錄> --output <輸出資料夾> --claude`（內部已整合本機 `MSBuild.exe` 自動搜尋，以及在缺少 `ResGen.exe` / `LC.exe` 時自動切換至免 SDK 的內建 Direct CSC + ResX 跨編譯引擎）。

### 規則 2：修復 EEP 專屬 DLL 引用路徑 (解決 `CS0246` 錯誤)
- EEP 系統高度依賴其私有底層組件（例如：`Srvtools.dll`、`InfoRemoteModule.dll`、`EFClientTools.dll`、`EFServerTools.dll`、`EFBase.dll`、`FLCore.dll`、`FLTools.dll` 等）。
- 當出現 `CS0246` 時，可執行 `eepc.exe repair <專案> --eep-root <EEP目錄>` 自動搜尋並更新 `.csproj` 的 `<HintPath>`，且確保 `<SpecificVersion>False</SpecificVersion>`。

### 規則 3：繞過第三方元件授權檢查 (解決 `LC.exe exited with code -1` 或 `MSB3091`)
- 若專案包含 `Properties\licenses.licx`，未安裝第三方控制項（如 DevExpress、FarPoint）本機開發授權或缺少 `LC.exe` 會導致編譯中斷。
- `eepc.exe --auto-repair` 會自動將 `licenses.licx` 內容清空為 0 byte（保留空白檔案以免 `.csproj` 找不到檔案），並將 `.csproj` 中的 `<EmbeddedResource Include="...licenses.licx">` 轉為 `<None>`（兩者皆有 `.bak` 備份）。
- 這些修改**只為了在本機編譯**：編譯期間不要把授權文字加回 `licenses.licx`；但回存版控前，必須以 `.licx.bak`、`.csproj.bak` 還原（見規則 7）。
- 清空 licx 後，產出的 DLL 只能從 OutputPath 位置的舊版 DLL 移植 `*.licenses` 授權資源；沒有舊版 DLL 時，輸出會出現 `[授權資源]` 提示，必須回報使用者。

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

### 規則 6：Big5 與 UTF-8 編碼
- `eepc.exe --auto-repair` 會為無 BOM 的 UTF-8 `.cs` 補上 BOM（避免舊版編譯器誤判為 Big5 而報 CS1010），未修改的 Big5 檔案不會被改動。
- 開啟 `.cs` 時中文為亂碼，代表原檔為 Big5：修改前執行 `eepc.exe to-utf8 "<檔案>"`；若版控為 Big5，回存前執行 `eepc.exe to-big5 "<檔案>"` 轉回。

### 規則 7：編譯輸出位置、不可回存的修改與編譯後檢查（v1.1 新增）
1. **一律用 `--output` 指定輸出資料夾**：EEP 專案的 `<OutputPath>` 常指向 `..\..\EEPNetServer\<Solution>\` 或 `..\..\EEPNetClient\<Solution>\`，直接編譯可能覆蓋 EEP 環境中正在使用的 DLL。
2. **`--auto-repair` 的修改不可回存版控**：它會改寫 `.csproj`（備份為 `.csproj.bak`）並清空 `licenses.licx`（備份為 `.licx.bak`），這些只是為了在本機編譯。回存 BitBucket 前必須還原；建議在專案副本上編譯。
3. **務必閱讀輸出中的「注意事項」**，並回報使用者：
   - `[資源略過]`：文化特定資源（如 `Component.vi-VN.resx`）沒有編入，本工具不產生衛星組件；部署時沿用既有的 `vi-VN\*.resources.dll`。
   - `[授權資源]`：產出的 DLL 不含 `*.licenses`，DevExpress 等元件執行時可能出現試用提示。
   - `[規則 3 - 授權移植]`：授權資源是從舊版 DLL 移植，需依公司授權確認。
   - `[參考]`：組件取自工具包 `lib\`，或 ProjectReference 已改用 DLL；確認版本正確。
4. **部署前驗證**（工具在 `EEPCompiler_Portable\tools\`，用法見 `EEPCompiler_Portable\README_先看我.md` 第四節）：
   - `DllCompare`：與舊版 DLL 比對，確認差異只有預期的修改。
   - `ResCheck`：在 CLR 2.0 上讀取全部資源，確認表單資源可在 EEP2006 載入。
