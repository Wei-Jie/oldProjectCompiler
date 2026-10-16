# Infolight EEP (.NET Framework) 專案編譯與修復指南 (AI Agent 專用規則檔)

## 專案背景與架構說明
本專案為使用 **訊光科技 (Infolight) EEP (Enterprise Development Platform)** 框架所開發之舊版 C# 系統（基於 `.NET Framework` 架構）。
由於本機環境未必安裝完整的 EEP 開發套件或舊版 Visual Studio IDE，AI 代理（如 Antigravity / Claude Code）在進行程式碼修改與自動化編譯時，**必須嚴格遵守以下五項環境與編譯規範**，以解決無法編譯的問題。

---

## 規則 1：禁止使用 `dotnet build`，強制使用 `MSBuild.exe`
舊版 EEP .NET 專案結構（包含 WinForms、WebForms、WCF 或 Remoting 架構）與現代 `.NET SDK CLI` 不相容。

### 執行動作：
1. **禁止執行** `dotnet build` 或 `dotnet restore`。
2. 請透過 PowerShell 腳本自動搜尋本機安裝的 `MSBuild.exe`（可利用 `vswhere.exe` 或搜尋常見路徑），並使用它來建置 `.sln` 或 `.csproj`：
   - 常見路徑 1 (VS 2022): `C:\Program Files\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\MSBuild.exe`
   - 常見路徑 2 (VS 2019): `C:\Program Files (x86)\Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\MSBuild.exe`
   - 常見路徑 3 (舊版 .NET 4.0): `C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe`
3. **標準編譯指令範例**：
   ```powershell
   & "<MSBuild.exe 完整路徑>" "<專案或方案檔.sln>" /t:Rebuild /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
   ```

---

## 規則 2：修復 EEP 專屬 DLL 引用路徑 (解決 `CS0246` 錯誤)
EEP 系統高度依賴其私有底層組件（例如：`Srvtools.dll`、`InfoRemoteModule.dll`、`EFClientTools.dll`、`EFServerTools.dll`、`EFBase.dll` 等）。當出現 `CS0246: 找不到類型或命名空間` 時，代表 `.csproj` 中的 `<HintPath>` 指向了不存在的絕對路徑（如 `C:\Program Files (x86)\Infolight\...`）。

### 執行動作：
1. **全域搜尋 DLL**：在整個專案工作區、上層目錄、或是本機的 `EEPNetServer`、`EEPNetClient`、`Infolight` 相關資料夾中搜尋遺失的 `.dll` 檔案。
2. **集中管理或修正路徑**：
   - 檢查所有 `.csproj` 檔案中的 `<Reference>` 標籤。
   - 若 `<HintPath>` 指向不存在的絕對路徑，請將其修正為指向專案內部現有 DLL 存放目錄（如 `..\Lib\`、`..\EEPNetServer\` 或 `..\bin\`）的相對路徑。
   - 確保標籤包含 `<SpecificVersion>False</SpecificVersion>` 以避免版本號綁定衝突：
     ```xml
     <Reference Include="Srvtools">
       <SpecificVersion>False</SpecificVersion>
       <HintPath>..\Lib\Srvtools.dll</HintPath>
     </Reference>
     ```

---

## 規則 3：繞過第三方元件授權檢查 (解決 `LC.exe exited with code -1` 錯誤)
若專案包含 `Properties\licenses.licx` 檔案，MSBuild 在編譯期間會呼叫 `LC.exe` 檢查第三方控制項（如 DevExpress、FarPoint 等）的本機開發授權，未安裝授權會直接導致編譯中斷。

### 執行動作：
1. 搜尋專案目錄下所有的 `licenses.licx` 檔案。
2. 若編譯發生 `LC.exe` 相關錯誤，請將 `licenses.licx` **檔案內容完全清空（保留大小為 0 byte 的空白檔案，切勿刪除檔案本身以免 `.csproj` 找不到檔案報錯）**。

---

## 規則 4：處理 EEP 特有的輸出路徑與建置後事件 (解決 `MSB3073` 錯誤)
EEP 架構將模組分為 Server 端（編譯後須放入 `EEPNetServer` 載入）與 Client 端（放入 `EEPNetClient` 載入）。`.csproj` 中常包含寫死的 `<PostBuildEvent>`（例如 `copy $(TargetPath) C:\EEP.NET\...`）或固定的 `<OutputPath>`。

### 執行動作：
1. 檢查 `.csproj` 中的 `<OutputPath>` 與 `<PostBuildEvent>`。
2. 若因為目標磁碟機或目錄不存在而發生 `MSB3073`（copy 命令失敗）錯誤：
   - 自動建立對應的目標輸出資料夾；或
   - 若為本機測試編譯，將 `<PostBuildEvent>` 中指向外部不存在路徑的 `copy` / `xcopy` 指令註解或改為本地輸出目錄。

---

## 規則 5：C# 語法版本降級與保護 EEP Designer 結構 (解決語法與執行期錯誤)
AI 代理在撰寫或重構程式碼時，常預設使用現代 C# 8.0 ~ 12.0 語法，這會導致舊版 MSBuild 編譯器或舊版 `.csproj` 報錯。

### 執行動作：
1. **嚴格遵守 C# 5.0 / 7.3 相容語法**：
   - **禁止使用** File-scoped namespaces（如 `namespace MyNamespace;`），必須使用傳統大括號 `namespace MyNamespace { ... }`。
   - **禁止使用** 簡化版 `using var file = ...;`，必須使用傳統 `using (var file = ...) { ... }`。
   - **禁止使用** Nullable Reference Types 語法（如 `string?`、`!` null-forgiving operator）。
   - **禁止使用** Target-typed `new()` 運算式（必須寫完整型別 `new List<string>()`）。
   - **禁止使用** `switch` 運算式 (Switch expressions) 與高階模式比對。
2. **禁止破壞 EEP 元件綁定與 `.Designer.cs`**：
   - 嚴禁隨意刪除或重新命名繼承自 `InfoDataModule`、`InfoForm` 的類別結構。
   - 嚴禁移除 `.Designer.cs` 內的 `InfoDataSet`、`InfoBindingSource`、`InfoCommand`、`UpdateComponent` 等訊光專屬元件的初始化宣告。

---

## AI 代理自動化診斷流程 (Step-by-Step Execution Plan)
當你收到此檔案時，請依序執行以下步驟：
1. **步驟一**：找出專案中所有的 `.sln` 與 `.csproj` 檔案，確認 TargetFrameworkVersion 與本機可用的 `MSBuild.exe` 路徑。
2. **步驟二**：嘗試使用 `MSBuild.exe` 執行第一次編譯，並擷取完整的錯誤代碼（如 `CS0246`、`CS1002`、`MSB3073`、`LC0000`）。
3. **步驟三**：對照上述 **規則 1 ~ 規則 5** 逐一修復 `.csproj` 設定、DLL `<HintPath>`、`licenses.licx` 以及不相容的現代 C# 語法。
4. **步驟四**：重新執行 `MSBuild.exe` 直到輸出 `0 個錯誤 (0 Error(s))`，並向使用者回報修改了哪些檔案與設定。