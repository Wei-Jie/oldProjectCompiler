# Infolight EEP (.NET Framework / VS2005) 可攜式編譯與自動修復套件

這是一套專為 **訊光科技 (Infolight) EEP (.NET Framework 2.0 / 3.5 / 4.0、Visual Studio 2005 / 2008)** 舊版系統量身打造的**免安裝可攜式編譯與自動修復工具**。

當您在**沒有安裝 Visual Studio 2005 IDE 或 Windows SDK** 的公司電腦上，使用 **Claude Code** 修改了 EEP Client（WinForms `InfoForm`）或 EEP Server（`InfoDataModule` / `DataModule`）的 C# 程式碼卻無法重新編譯時，只需將 `dist\EEPCompiler_Portable` 資料夾拷貝過去即可立即解決！

---

## 核心功能與五大規則自動化

1. **規則 1（雙核心編譯引擎：MSBuild + 內建免 SDK Direct CSC/ResX）**
   - 自動搜尋本機的 `MSBuild.exe`（支援 VS2022 / VS2019 / VS2017 / .NET 4.0 / .NET 2.0）。
   - **免 SDK 自動救援**：若公司電腦未安裝 Windows SDK（缺少 `ResGen.exe`、`LC.exe`、`AL.exe` 導致 MSBuild 報 `MSB3091` 錯誤），或需強制透過 `/nostdlib` 跨編譯出 **CLR v2.0 (`v2.0.50727`)** 組件以防止 `BadImageFormatException`，工具會**自動無縫切換至內建的 Direct CSC + 原生 ResX 引擎**完成編譯！
2. **規則 2（自動修復 EEP 專屬 DLL `<HintPath>` 與解除版本強綁定）**
   - 自動在工作區、上層目錄與 `EEPNetServer`、`EEPNetClient`、`Lib` 搜尋 `Srvtools.dll`、`InfoRemoteModule.dll`、`EFClientTools.dll`、`EFServerTools.dll`、`EFBase.dll`、`FLCore.dll` 等組件。
   - 自動修正 `.csproj` 中失效的絕對路徑 `<HintPath>`，並自動補上 `<SpecificVersion>False</SpecificVersion>`（修改前自動備份 `.csproj.bak`）。
3. **規則 3（自動繞過 `licenses.licx` 第三方元件授權檢查）**
   - 自動將專案內非空白的 `Properties\licenses.licx` 備份並清空為 0 byte，同時將 `.csproj` 中的 `<EmbeddedResource Include="...licenses.licx">` 轉為 `<None>`，徹底根除 `LC.exe exited with code -1` 與 `MSB3091`。
4. **規則 4（自動處理 `<OutputPath>` 與無效 `<PostBuildEvent>`）**
   - 自動建立不存在的輸出目錄，並自動將指向不存在磁碟機（如舊伺服器 `Z:\`、`Y:\`）的 `<PostBuildEvent>` copy 指令加上 `rem` 註解，徹底解決 `MSB3073`。
5. **規則 5（C# 語法版本降級預檢 + CP950 轉 UTF-8 + `.Designer.cs` 保護）**
   - 自動將繁體中文 Windows 下編譯器的 Big5 (CP950) 輸出轉換為乾淨的 UTF-8，避免 Claude Code 讀到亂碼。
   - 內建語法掃描器：自動攔截 Claude Code 常誤用的 C# 6.0 ~ 12.0 新語法（如 `$"字串插值"`、`?.`、`nameof()`、`using var`、`new()`、File-scoped namespace），並附上明確的 **C# 5.0 / VS2005 降級改寫範例**。

---

## 如何帶到公司電腦使用？（三步驟快速上手）

### 步驟 1：拷貝可攜式資料夾
將 [`dist\EEPCompiler_Portable`](file:///c:/AI/vs2005compiler/dist/EEPCompiler_Portable) 整個資料夾（或壓縮檔 `dist\EEPCompiler_Portable.zip`）複製到公司電腦的 EEP 專案目錄下：
- `eepc.exe`：單一免安裝執行檔（**內含 GUI 桌面視窗介面與 CLI 命令列雙模式**，不需安裝 Python 或 SDK）。
- `CLAUDE.md`：給 Claude Code 讀取的規則與自動編譯指令設定檔。
- `eep_tool.py`：選用的 Python 腳本工具（含 `watch` 檔案變更自動監控編譯模式）。

### 步驟 2：選擇您的操作方式

#### 方式 A：使用圖形化桌面介面（GUI 手動點選）
直接**滑鼠雙擊 `eepc.exe`**（或執行 `eepc.exe --gui`）：
1. 將您的 `.sln`、`.csproj` 或專案資料夾直接**拖放（Drag & Drop）**進視窗，或點擊「選取專案檔...」。
2. （選填）若 EEP 元件放在特殊位置，可在「訊光 EEP 根目錄」選擇您的 `EEP.NET` 安裝資料夾。
3. 點擊綠色的 **「3. 一鍵修復並開始編譯 (Build)」** 按鈕。
4. 若有編譯錯誤，可點擊 **「複製 Claude Code 除錯報告」** 直接貼給 Claude Code 修復！

#### 方式 B：讓 Claude Code 自動化編譯與除錯（CLI 閉環模式）
1. 將 `CLAUDE.md` 複製到您公司 EEP 專案的根目錄。
2. Claude Code 讀取 `CLAUDE.md` 後，每次改完 `.cs` 就會自動執行：
   ```powershell
   & ".\eepc.exe" build "<您的專案.csproj 或 方案.sln>" --auto-repair --claude
   ```
   若您的 `EEPNetServer` / `EEPNetClient` 位於特定目錄（例如 `C:\EEP.NET`），可加上 `--eep-root`：
   ```powershell
   & ".\eepc.exe" build "<您的專案.csproj>" --eep-root "C:\EEP.NET" --auto-repair --claude
   ```

#### 方式 C：開啟 Python 自動監控編譯模式（Watch Mode）
若公司電腦有安裝 Python（建議搭配 `uv`）：
```powershell
uv run python eep_tool.py watch "<您的專案.csproj 或 資料夾>" --eep-root "C:\EEP.NET"
```
只要 Claude Code 或您儲存任何 `.cs` 檔案，工具就會在 0.8 秒內自動修復、重新編譯並產出最新診斷報告至 `EEP_BUILD_STATUS.md`！

---

## `eepc.exe` 命令列參數一覽

```text
eepc.exe build <專案.sln|.csproj|資料夾> [--auto-repair] [--claude] [--eep-root <EEP目錄>] [--engine auto|msbuild|direct] [--target auto|v2.0|v4.0]
eepc.exe repair <專案.sln|.csproj|資料夾> [--eep-root <EEP目錄>]
eepc.exe check <專案.sln|.csproj|資料夾>
eepc.exe env
eepc.exe --gui
```
