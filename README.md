# 00. 從 GitHub 下載到完成編譯：Step-by-Step 完整步驟總覽

這份文件將所有步驟依序濃縮成 **5 個標準步驟（Step 1 ~ Step 5）**。您到了公司電腦後，**只需要照著這份文件從上往下做一次**，就能完成所有環境配置！

---

## 📁 檔案順序導覽（照數字順序看／點選即可）

### 1. 說明文件順序（專案根目錄）
- **`00_從下載到編譯完整步驟總覽.md`** 👈 **（本文件，看這份即可完成全流程！）**
- **`01_GitHub下載與更新教學.md`**：3 種從 GitHub 把專案抓到公司電腦的方法。
- **`02_公司環境部署與Claude整合指南.md`**：與既有 `CLAUDE.md` 合併、乾淨電腦編譯與 DevExpress 授權詳解。
- **`03_EEP五大編譯修復規則說明.md`**：AI 代理專用的底層技術規範。

### 2. 隨身工具包操作順序（`dist\EEPCompiler_Portable\` 資料夾內）
進到 `dist\EEPCompiler_Portable` 資料夾後，裡面的批次檔也已按執行順序編號：
1. **`01_收集公司EEP與DevExpress組件.bat`**（第 1 步：把公司的訊光與 DevExpress DLL 收集進 `lib\`）
2. **`02_安裝Claude規則到專案.bat`**（第 2 步：把編譯規則安全整合進公司專案的 `CLAUDE.md`）
3. **`03_啟動GUI桌面編譯器.bat`**（第 3 步：手動開啟視窗介面編譯，或交由 Claude Code 自動呼叫 `eepc.exe`）

---

## 🚀 全流程 5 步驟實戰（到公司電腦照著做）

```mermaid
flowchart LR
    S1["Step 1<br/>從 GitHub 下載"] --> S2["Step 2<br/>放入公司專案目錄"]
    S2 --> S3["Step 3<br/>雙擊 01_收集組件.bat"]
    S3 --> S4["Step 4<br/>雙擊 02_安裝Claude規則.bat"]
    S4 --> S5["Step 5<br/>Claude 自動編譯 或<br/>雙擊 03_啟動GUI.bat"]
```

---

### 【Step 1】在公司電腦從 GitHub 下載本工具
打開瀏覽器前往：**[https://github.com/Wei-Jie/oldProjectCompiler](https://github.com/Wei-Jie/oldProjectCompiler)**
- **最快下載法（免裝 Git）**：點擊右上角綠色的 **`<> Code`** -> **`Download ZIP`**，下載後解壓縮。
- **Git 指令下載法**：在終端機輸入 `git clone https://github.com/Wei-Jie/oldProjectCompiler.git`。

---

### 【Step 2】將 `EEPCompiler_Portable` 放進您的公司專案
1. 打開剛剛解壓縮出來的 `dist` 資料夾。
2. 把裡面的 **`EEPCompiler_Portable`** 整個資料夾，複製到您**公司舊系統專案的根目錄**下（例如 `D:\MyEEPProject\EEPCompiler_Portable\`）。

---

### 【Step 3】執行 `01_收集公司EEP與DevExpress組件.bat`（只需做一次）
1. 進入 `EEPCompiler_Portable` 資料夾，雙擊執行 **`01_收集公司EEP與DevExpress組件.bat`**。
2. 視窗會請您輸入（或用滑鼠拖曳）公司電腦上的 EEP 安裝目錄（例如 `C:\EEP.NET` 或 `C:\EEP2006`），也可以直接按 `Enter` 自動掃描 Windows GAC。
3. 它會自動把 `Srvtools.dll`、`InfoRemoteModule.dll`、`EFClientTools.dll` 以及 `DevExpress*.dll`、`FarPoint*.dll` 全部收集進 `EEPCompiler_Portable\lib\` 資料夾。
   > 💡 **好處**：收集完這一次之後，就算您把這個資料夾帶回**完全沒裝 EEP、也沒裝 VS2005 的乾淨電腦**，只要有 Source Code 都能直接獨立編譯！

---

### 【Step 4】執行 `02_安裝Claude規則到專案.bat`（整合 `CLAUDE.md`）
1. 雙擊執行 **`02_安裝Claude規則到專案.bat`**，直接按 `Enter`（預設會指向您專案的上層根目錄）。
2. 它會自動幫您完成：
   - 建立 `.claude\rules\eep-compiler.md` 模組化規則。
   - 若您原本已經有 `CLAUDE.md`，它會**100% 保留原本內容**，只在檔案最尾端安全補上 `eepc.exe` 的編譯規則！
   - （或者您也可以打開 Claude Code，請它：*「讀取 `EEPCompiler_Portable/CLAUDE.md` 並與我現有的 `CLAUDE.md` 整併」*）。

---

### 【Step 5】開始編譯！（兩種模式任選）

#### 模式 A：讓 Claude Code 改完程式自己「自動編譯＋自動除錯」（全自動閉環）
完成 Step 4 後，您在專案目錄啟動 Claude Code 請它改程式時，它每改完 `.cs` 就會自己呼叫：
```powershell
.\EEPCompiler_Portable\eepc.exe build "<您的專案.csproj 或 方案.sln>" --auto-repair --claude
```
- `eepc.exe` 會自動幫它修好失效的 `<HintPath>`、清空 `licenses.licx`（並從舊版 DLL 無損移植 `.licenses` 授權資源防止跳試用窗）、擋掉錯誤的 `PostBuildEvent`，並檢查是否有誤用 C# 6~12 新語法。
- 若有編譯錯誤，會輸出乾淨的 UTF-8 繁體中文錯誤行號與降級修改建議，讓 Claude Code 自動修到 `PASS (0 Error(s))` 為止！

#### 模式 B：手動開啟桌面視窗介面一鍵編譯（GUI 模式）
1. 雙擊執行 **`03_啟動GUI桌面編譯器.bat`**（或直接雙擊 **`eepc.exe`**）。
2. 把您的 `.sln`、`.csproj` 或資料夾直接**用滑鼠拖放進視窗**。
3. 點擊綠色的 **「3. 一鍵修復並開始編譯 (Build)」** 按鈕即可產出 `.dll`！
   - 如果看到紅字錯誤，直接點擊旁邊的 **「複製 Claude Code 除錯報告」** 按鈕，貼給 Claude Code 就能立刻幫您修好！
