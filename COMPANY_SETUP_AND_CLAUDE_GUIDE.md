# 公司環境部署、乾淨電腦編譯與 Claude Code 整合實戰指南

本文件彙整了將本專案從 GitHub 下載到**公司電腦**或**無安裝環境之乾淨電腦**時的完整操作建議、DevExpress 授權處理機制，以及與現有 `CLAUDE.md` 的整合方式。

---

## 一、從 GitHub 下載後的快速檢查清單

當您在公司電腦將本倉庫 `git clone`（或下載 ZIP 解壓縮）下來後，可以直接使用 [`dist\EEPCompiler_Portable\`](./dist/EEPCompiler_Portable) 資料夾內已預先打包好的工具：

| 檔案 / 資料夾 | 說明 |
| :--- | :--- |
| `eepc.exe` | **免安裝單一執行檔（CLI + GUI 雙模式）**，直接雙擊開啟桌面視窗，帶參數則供 Claude Code 呼叫。 |
| `CLAUDE.md` | 專為訊光 EEP (.NET / VS2005) 撰寫的 AI 編譯與 C# 語法降級規則檔。 |
| `安裝Claude規則到專案.bat` | **安全合併工具**：不覆蓋您原本的 `CLAUDE.md`，自動將 EEP 編譯規則附加並建立 `.claude\rules\eep-compiler.md`。 |
| `收集公司EEP組件.bat` | **組件收集工具**：一鍵從公司電腦的 EEP 目錄與 Windows GAC 收集 `Srvtools.dll`、`InfoRemoteModule.dll`、`DevExpress*.dll`、`FarPoint*.dll` 至 `lib\` 資料夾。 |
| `啟動GUI編譯器.bat` | 快速啟動繁體中文桌面 GUI 編譯器。 |
| `eep_tool.py` | Python 輔助腳本（支援 `watch` 檔案儲存自動觸發編譯模式）。 |
| `lib\` | 隨身 DLL 參考目錄（`eepc.exe` 編譯時會最優先從此資料夾載入 DLL）。 |

---

## 二、如何與公司專案現有的 `CLAUDE.md` 整合？（三種建議做法）

如果您的公司專案根目錄**原本就有一份 `CLAUDE.md`**（記載了商業邏輯或既有規範），請勿直接覆蓋，建議從以下三種方式擇一使用：

### 做法 1：執行 `安裝Claude規則到專案.bat`（最省事、自動防呆）
1. 將 `EEPCompiler_Portable` 資料夾複製到您公司專案根目錄下。
2. 雙擊執行 `EEPCompiler_Portable\安裝Claude規則到專案.bat`。
3. 腳本會自動完成兩件事（**100% 保留您原有的 `CLAUDE.md` 內容**）：
   - 將 EEP 規則複製到專案的 `.claude\rules\eep-compiler.md`（Claude Code 支援自動載入 `.claude/rules/*.md` 下的所有模組化規則）。
   - 在您原本的 `CLAUDE.md` 檔案最尾端安全附加（Append）引用區塊。

### 做法 2：在原本的 `CLAUDE.md` 中使用 `@路徑` 模組化引用（最乾淨）
Claude Code 原生支援在 `CLAUDE.md` 內使用 `@相對路徑` 來載入外部檔案。
您只需打開原本專案的 `CLAUDE.md`，在檔案最下方加上這兩行：

```markdown
## EEP 編譯與語法規範
@EEPCompiler_Portable/CLAUDE.md
```

這樣 Claude Code 啟動時就會自動把兩份檔案合併讀取，完全不會弄亂您原本的 `CLAUDE.md`。

### 做法 3：請公司電腦上的 Claude Code 幫您智慧整併（去蕪存菁）
若您原本的 `CLAUDE.md` 內含舊的編譯指令（例如以前寫的 `dotnet build` 或舊版 MSBuild 指令），擔心新舊指令衝突，可以在公司電腦啟動 Claude Code 後直接貼上以下提示詞：

> **給 Claude Code 的整併提示詞：**
> 「請讀取 `EEPCompiler_Portable/CLAUDE.md` 的內容，將它與我目前專案根目錄的 `CLAUDE.md` 進行整合：請完整保留我原本的專案架構與商業邏輯說明，但將『編譯指令、五大修復規則與 C# 5.0 語法限制』更新為使用 `.\EEPCompiler_Portable\eepc.exe build <專案路徑> --auto-repair --claude`。」

---

## 三、在「完全乾淨的電腦（只有 Source Code）」上編譯的秘訣

在一台**沒裝 Visual Studio 2005、沒裝 Windows SDK、也沒裝訊光 EEP 安裝包**的乾淨 Windows 10 / 11 電腦上：

1. **編譯器與 `.resx` 資源轉換已 100% 內建於系統與 `eepc.exe`**
   - Windows 10 / 11 均出廠內建 `.NET Framework 4.0` 的 `csc.exe` 與 `MSBuild.exe`。
   - 原本無 SDK 環境最容易報錯的 `.resx` 資源編譯（缺 `ResGen.exe`）與授權檢查（缺 `LC.exe`），已由 `eepc.exe` 內建引擎完全接手，無須安裝任何 SDK。
2. **唯一需要攜帶的是 `lib\` 資料夾內的外部參考 `.dll` 檔案**
   - 因為 C# 程式碼會 `using Srvtools;` 或引用第三方元件，編譯時需要讀取這些 `.dll` 的型別宣告（Metadata）。
   - **操作步驟**：
     1. 在公司電腦（有舊系統執行環境的電腦）上，雙擊執行一次 **`收集公司EEP組件.bat`**。
     2. 腳本會自動把公司電腦上的 `Srvtools.dll`、`InfoRemoteModule.dll`、`EFClientTools.dll`、`FLCore.dll` 以及 GAC 內的 `DevExpress*.dll`、`FarPoint*.dll` 全部複製進 `EEPCompiler_Portable\lib\` 資料夾。
     3. 收集完成後，這包 `EEPCompiler_Portable` 就成為**完全自給自足的隨身編譯器**——帶去任何一台只有 Source Code 的乾淨電腦，都能直接編譯出與公司正式環境 100% 相容的 `.dll`！

---

## 四、DevExpress 與第三方控制項問題與授權解決機制

舊版 EEP 專案常搭配 DevExpress 或 FarPoint Spread 等商業控制項，本工具已針對三個層面徹底解決：

1. **編譯期（Compile-Time）：徹底解決 `LC.exe exited with code -1` 與 `MSB3091`**
   - `eepc.exe --auto-repair` 會自動將專案內所有的 `Properties\licenses.licx` 備份並**清空為 0 byte**。
   - 同時自動將 `.csproj` 中的 `<EmbeddedResource Include="...licenses.licx">` **改為 `<None>`**。
   - 如此一來，無論走 `MSBuild` 或 `Direct CSC` 引擎，編譯過程**完全不會呼叫 `LC.exe`**，電腦沒裝 DevExpress 開發端安裝包也能 100% 編譯通過。
2. **執行期（Runtime）：舊版 `.licenses` 二進位授權資源無損移植（防止跳出 Trial 試用彈窗）**
   - 當使用內建 Direct 引擎重新編譯並覆蓋舊版 `.dll` 時，`eepc.exe` 的 `ResxCompiler` 會自動從原有的舊版 `.dll`（或 `.dll.bak`）中，將當年由正版開發機簽發嵌入的 `*.licenses` 二進位資源抽出來，並**原封不動移植嵌入到新編譯的 `.dll` 中**。
   - 這樣即使在無授權開發機上重新編譯，產出的新版 `.dll` 內部依然保有原本合法的執行期授權簽章。
3. **引用路徑（Reference）：自動搜尋 Windows GAC 與解除版本號強綁定**
   - 自動在 `lib\`、`EEPNetClient`、`EEPNetServer` 與 Windows GAC (`C:\Windows\assembly\GAC_MSIL`) 搜尋 `DevExpress.*.dll`。
   - 自動修正 `<HintPath>` 並強制設為 `<SpecificVersion>False</SpecificVersion>`，避免小版本號差異導致 `CS0246` 編譯失敗。

---

## 五、常用指令速查表

### 1. 開啟桌面視窗介面 (GUI 模式)
直接雙擊 `eepc.exe` 或在終端機執行：
```powershell
.\dist\EEPCompiler_Portable\eepc.exe --gui
```

### 2. 自動修復並編譯專案（Claude Code 自動化閉環指令）
```powershell
# 自動修復規則 2~4、檢查規則 5 語法，並輸出 UTF-8 AI 診斷報告
.\dist\EEPCompiler_Portable\eepc.exe build "<專案.csproj 或 方案.sln>" --auto-repair --claude

# 若需額外指定公司電腦的 EEP 安裝根目錄：
.\dist\EEPCompiler_Portable\eepc.exe build "<專案.csproj>" --eep-root "C:\EEP.NET" --auto-repair --claude
```

### 3. 強制使用免 SDK 內建引擎或指定目標 CLR 版本
```powershell
# 強制使用內建免 SDK Direct CSC + ResX 引擎，並指定目標為 CLR v2.0 (VS2005)
.\dist\EEPCompiler_Portable\eepc.exe build "<專案.csproj>" --engine direct --target v2.0 --auto-repair --claude
```

### 4. Python 檔案監控自動編譯模式 (Watch Mode)
```powershell
uv run python .\python_cli\eep_tool.py watch "<專案.csproj 或 資料夾>" --eep-root "C:\EEP.NET"
```
