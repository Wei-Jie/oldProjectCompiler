# 00. 從 GitHub 下載到完成編譯與知識庫建立：Step-by-Step 完整步驟總覽

這份文件將所有步驟依序濃縮成 **標準操作流程**。您到了公司電腦後，**只需要照著這份文件從上往下做一次**，就能完成編譯環境、全系統地圖索引，以及繼承既有 `.html` / `.md` 知識庫！

---

## 📁 檔案順序導覽（照數字順序看／點選即可）

### 1. 說明文件順序（專案根目錄）
- **`00_從下載到編譯完整步驟總覽.md`** 👈 **（本文件，看這份即可完成全流程！）**
- **`01_GitHub下載與更新教學.md`**：3 種從 GitHub 把專案抓到公司電腦與 `git pull` 更新的方法。
- **`02_公司環境部署與Claude整合指南.md`**：與既有 `CLAUDE.md` 合併、Big5/UTF-8 編碼轉換、團隊 Skill 斜線指令與匯入舊 `.html`/`.md` 詳解。
- **`03_EEP五大編譯修復規則說明.md`**：AI 代理專用的底層技術規範。

### 2. 隨身工具包操作順序（`dist\EEPCompiler_Portable\` 資料夾內）
進到 `dist\EEPCompiler_Portable` 資料夾後，裡面的批次檔已按執行順序編號：
1. **`01_收集公司EEP與DevExpress組件.bat`**（第 1 步：把公司的訊光與 DevExpress DLL 收集進 `lib\`）
2. **`02_安裝Claude規則到專案.bat`**（第 2 步：安全整合 `CLAUDE.md`，並自動安裝團隊 Skill、活體知識庫與 `/eep-ask` 等 4 個斜線指令）
3. **`03_啟動GUI桌面編譯器.bat`**（第 3 步：手動開啟視窗介面一鍵修復並編譯，或交由 Claude Code 自動呼叫 `eepc.exe`）
4. **`04_產生全系統業務導覽地圖(解決大海撈針).bat`**（第 4 步：3 秒掃描全系統產生 `EEP_SYSTEM_MAP.md`，讓您與 Claude 秒速定位畫面與資料表！）

---

## 🚀 全流程 6 步驟實戰（到公司電腦照著做）

```mermaid
flowchart LR
    S1["Step 1<br/>從 GitHub 下載"] --> S2["Step 2<br/>放入公司專案目錄"]
    S2 --> S3["Step 3<br/>雙擊 01_收集組件.bat"]
    S3 --> S4["Step 4<br/>雙擊 02_安裝Claude規則.bat"]
    S4 --> S5["Step 5<br/>雙擊 04_產生全系統地圖.bat<br/>並用 /eep-import 繼承舊報告"]
    S5 --> S6["Step 6<br/>使用 /eep-ask 查問題<br/>與 /eep-fix 自動改碼編譯"]
```

---

### 【Step 1】在公司電腦從 GitHub 下載本工具
打開瀏覽器前往：**[https://github.com/Wei-Jie/oldProjectCompiler](https://github.com/Wei-Jie/oldProjectCompiler)**
- **最快下載法（免裝 Git）**：點擊右上角綠色的 **`<> Code`** -> **`Download ZIP`**，下載後解壓縮。
- **Git 指令下載法**：在終端機輸入 `git clone https://github.com/Wei-Jie/oldProjectCompiler.git`（日後更新只需 `git pull`）。

---

### 【Step 2】將 `EEPCompiler_Portable` 放進您的公司專案
1. 打開解壓縮出來的 `dist` 資料夾。
2. 把裡面的 **`EEPCompiler_Portable`** 整個資料夾，複製到您**公司舊系統專案的根目錄**下（例如 `D:\MyEEPProject\EEPCompiler_Portable\`）。

---

### 【Step 3】執行 `01_收集公司EEP與DevExpress組件.bat`（只需做一次）
1. 進入 `EEPCompiler_Portable` 資料夾，雙擊執行 **`01_收集公司EEP與DevExpress組件.bat`**。
2. 輸入或拖曳公司電腦的 EEP 安裝目錄（如 `C:\EEP.NET`），或直接按 `Enter` 自動掃描 Windows GAC。
3. 它會自動把 `Srvtools.dll`、`InfoRemoteModule.dll`、`EFClientTools.dll` 及 `DevExpress*.dll`、`FarPoint*.dll` 全部收集進 `EEPCompiler_Portable\lib\`。
   > 💡 收集完後，這包就是 100% 獨立的隨身編譯器，就算帶到完全沒裝 EEP 與 VS2005 的乾淨電腦也能直接編譯！

---

### 【Step 4】執行 `02_安裝Claude規則到專案.bat`（安裝規則、Skill 與斜線指令）
雙擊執行 **`02_安裝Claude規則到專案.bat`**，直接按 `Enter`，它會在**不覆蓋原有內容**的前提下自動安裝：
1. **編譯規則**：安全附加至您原有的 `CLAUDE.md`，並建立 `.claude\rules\eep-compiler.md`。
2. **團隊傳承技能 (Skill)**：安裝 `.claude\skills\eep-maintainer\SKILL.md`。
3. **活體知識庫**：初始化 `EEP_KNOWLEDGE_BASE.md`（若已存在則保留不覆蓋）。
4. **4 個專屬斜線指令**：安裝 `/eep-import`、`/eep-ask`、`/eep-fix`、`/eep-learn`。

---

### 【Step 5】建立全系統地圖 ＆ 承接您之前做好的 `.html` / `.md` 報告（終結大海撈針！）
1. **產生全系統程式碼地圖 (`EEP_SYSTEM_MAP.md`)**：
   - 雙擊執行 **`04_產生全系統業務導覽地圖(解決大海撈針).bat`**。
   - 只需 3~5 秒，就會自動掃描全系統所有 C 版（前端畫面標題）、S 版（後端方法）與 SQL 資料表關聯，產出 `EEP_SYSTEM_MAP.md`！
2. **原地承接您之前請 Claude 分析好的 `.html` 與 `.md` 報告**：
   - 在公司電腦打開 Claude Code，直接輸入：
     ```text
     /eep-import
     ```
     （或指定資料夾：`/eep-import D:\舊分析報告路徑`）
   - 公司電腦上的 Claude Code 就會自動讀取您之前做好的所有 `.html` 與 `.md` 報告，把裡面的商業邏輯與資料表整理進 `EEP_KNOWLEDGE_BASE.md`，完全不需將公司檔案帶出！

---

### 【Step 6】日常維運與自動編譯（CLI 斜線指令 ＋ GUI 視窗雙模式）

#### 方式 A：在 Claude Code 使用專屬斜線指令（推薦！）
- **追查使用者問題**：輸入 **`/eep-ask 為什麼採購單審核金額算錯？`**
  👉 AI 會先查 `EEP_KNOWLEDGE_BASE.md` 與 `EEP_SYSTEM_MAP.md` 秒速定位檔案，回答完還會**自動把新查到的業務邏輯存入知識庫**！
- **修改程式並自動編譯**：輸入 **`/eep-fix 請幫我修改 COrder 的金額檢核並重新編譯`**
  👉 AI 改完單一作業的 C 版或 S 版（不影響其他作業）後，會自動呼叫 `eepc.exe build <專案.csproj> --auto-repair --claude` 驗證到 `0 Error(s)`！
- **手動補充潛規則**：輸入 **`/eep-learn 訂單狀態碼 9 代表已作廢`**
  👉 隨時將前輩口述的潛規則存入團隊知識庫，日後新同仁可直接共用。

#### 方式 B：手動開啟桌面視窗介面一鍵編譯（GUI 模式）
- 雙擊 **`03_啟動GUI桌面編譯器.bat`**（或直接雙擊 **`eepc.exe`**）。
- 將要編譯的那支 `.csproj` 拖放進視窗，點擊綠色的 **「3. 一鍵修復並開始編譯 (Build)」** 按鈕即可！

---

## 🛠️ Big5 與 UTF-8 亂碼處理速查（規則 6）

1. **給 Claude Code 掃描時中文字正常顯示**：代表原本就是 UTF-8，直接改、直接編譯即可（`eepc.exe` 會在編譯時自動為改過的檔案補上 `UTF-8 BOM` 檔頭，而其他未修改的舊 Big5 檔案則保持不動，不會弄髒版控紀錄）。
2. **給 Claude Code 打開時中文字變亂碼**：代表該檔案原本是 **Big5**。
   - **修改前（轉為 UTF-8 讓 Claude 看得懂）**：
     ```powershell
     .\EEPCompiler_Portable\eepc.exe to-utf8 "FormOrder.cs"
     ```
   - **修改後（若公司版控要求轉回 Big5）**：
     ```powershell
     .\EEPCompiler_Portable\eepc.exe to-big5 "FormOrder.cs"
     ```
