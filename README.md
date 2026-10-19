# 00. 從取得工具包到完成編譯與知識庫建立：完整步驟總覽（v1.1）

> 本文件依 v1.1 修正版改寫（2026-10-19）。修正內容與驗證結果見 `README_先看我.md`。
> 原版的 `01_GitHub下載與更新教學.md`、`02_公司環境部署與Claude整合指南.md`、`03_EEP五大編譯修復規則說明.md` 未放入工具包；它們描述的是 v1.0，部分內容（GitHub 下載、組件收集方式）已不適用。

照這份文件從上往下做一次，就能完成編譯環境、全系統地圖索引，以及 Claude Code 規則與知識庫的安裝。

---

## 檔案導覽

| 檔案 | 用途 |
|---|---|
| `README_先看我.md` | v1.1 修正內容、快速開始、注意事項 |
| `00_從下載到編譯完整步驟總覽.md` | 本文件 |
| `01_收集公司EEP與DevExpress組件.bat` | 第 1 步：收集 EEP 與 DevExpress 組件到 `lib\` |
| `02_安裝Claude規則到專案.bat` | 第 2 步：安裝 Claude Code 規則、Skill、斜線指令與知識庫 |
| `03_啟動GUI桌面編譯器.bat` | 開啟 GUI（v1.1 未重新測試，建議用命令列） |
| `04_產生全系統業務導覽地圖(解決大海撈針).bat` | 掃描專案產生 `EEP_SYSTEM_MAP.md` |
| `eepc.exe` | 編譯器 v1.1 |
| `CLAUDE.md`、`claude_templates\` | Claude Code 規則與範本（由 02 安裝到專案） |
| `eep_tool.py` | Python 包裝工具（含 watch 自動編譯模式） |
| `lib\` | 編譯用組件（由 01 收集，初始為空） |
| `tools\` | 部署前驗證工具（DllCompare、ResCheck） |

---

## 全流程

```mermaid
flowchart LR
    S1["Step 1<br/>取得 v1.1 工具包"] --> S2["Step 2<br/>放入專案根目錄"]
    S2 --> S3["Step 3<br/>01 收集組件"]
    S3 --> S4["Step 4<br/>02 安裝 Claude 規則"]
    S4 --> S5["Step 5<br/>04 產生系統地圖<br/>/eep-import 承接舊報告"]
    S5 --> S6["Step 6<br/>/eep-ask、/eep-fix<br/>或命令列編譯"]
    S6 --> S7["Step 7<br/>部署前驗證與部署"]
```

---

### 【Step 1】取得 v1.1 工具包

- 向團隊取得 `EEPCompiler_Portable_v1.1_<日期>.zip`，解壓縮。
- **不要使用 GitHub 上 `dist\EEPCompiler_Portable` 的 v1.0**：它有 7 個已知缺陷（例如編出的表單在 EEP2006 開不起來），而且 `lib\` 內附的是測試用 mock DLL。
- 環境需求：
  - 繁體中文 Windows（批次檔為 Big5 編碼）。
  - Windows 功能已啟用 **.NET Framework 3.5（含 2.0）**：VS2005 專案需用 v2.0／v3.5 編譯器。
  - 不需要安裝 Visual Studio 2005。
  - 執行 `eepc.exe env` 可檢查本機的 MSBuild、csc 與 .NET Framework。

---

### 【Step 2】放置位置

建議把整個 `EEPCompiler_Portable` 資料夾放在**公司舊系統專案的根目錄**：

```text
D:\公司舊系統\                <-- 02、04 批次檔直接按 Enter 時的預設目標
  ├── COrder\                 <-- Client 專案
  ├── SOrder\                 <-- Server 專案
  └── EEPCompiler_Portable\   <-- 工具包（04 掃描時會跳過）
```

放在其他位置也可以：執行 02、04 時把專案資料夾拖曳進視窗再按 Enter。

---

### 【Step 3】執行 `01_收集公司EEP與DevExpress組件.bat`（每台電腦做一次）

1. 雙擊執行，依序輸入（可拖曳資料夾）：
   - **EEP 根目錄**：例如 `D:\EEP2006`。只會從其下的 `EEPNetServer`（找不到才用 `EEPNetClient`）收集 `Srvtools.dll`、`InfoRemoteModule.dll` 等，以及 `EEPNetClient\Interop.Excel.dll`，不會遞迴掃描 `.bak` 或舊版備份資料夾。
   - **DevExpress 資料夾**：例如 `D:\HPS\devexpressdll\Sources\DevExpress.DLL`，收集其中的 `DevExpress*.dll`。
   - 不需要的項目直接按 Enter 略過。
2. 最後會從 Windows GAC 補收尚未收集到的 DevExpress／FarPoint 組件（不覆蓋已收集的檔案）。
3. **確認畫面最後列出的版本**：`Srvtools.dll`、`InfoRemoteModule.dll` 應為 2.2.0.0，大小約 2 MB 與 230 KB；若只有幾 KB，表示是 mock，不能用來編譯要部署的 DLL。

收集後，即使電腦沒有安裝 EEP 開發環境也能編譯；但仍需 Step 1 的 .NET Framework 3.5。

---

### 【Step 4】執行 `02_安裝Claude規則到專案.bat`（安裝規則、Skill 與斜線指令）

雙擊執行並輸入（或直接按 Enter 使用上層目錄）專案根目錄，會在**不覆蓋既有內容**的前提下安裝：

1. **編譯規則**：複製為 `.claude\rules\eep-compiler.md`；專案根目錄的 `CLAUDE.md` 若已存在，只附加引用（重複執行不會重複附加）。
2. **團隊技能 (Skill)**：`.claude\skills\eep-maintainer\SKILL.md`。
3. **知識庫**：初始化 `EEP_KNOWLEDGE_BASE.md`（已存在則保留）。
4. **斜線指令**：`/eep-import`、`/eep-ask`、`/eep-fix`、`/eep-learn`。

工具包升級後，重新執行 02 即可更新規則與範本（知識庫不會被覆蓋）。

---

### 【Step 5】建立全系統地圖與承接既有報告

1. **產生 `EEP_SYSTEM_MAP.md`**：雙擊 `04_產生全系統業務導覽地圖(解決大海撈針).bat`，會掃描所有 Client 畫面標題、Server 方法與 SQL 資料表關聯。
2. **承接過去的分析報告**：在 Claude Code 輸入 `/eep-import`（或 `/eep-import D:\舊分析報告路徑`），把既有 `.html`／`.md` 報告的商業邏輯整理進 `EEP_KNOWLEDGE_BASE.md`。

---

### 【Step 6】日常維運與編譯

#### 方式 A：Claude Code 斜線指令（推薦）
- `/eep-ask <問題>`：先查知識庫與系統地圖定位檔案，回答後把新發現寫入知識庫。
- `/eep-fix <需求>`：修改單一作業的 C 或 S 專案，在**專案副本**上編譯驗證到 `PASS`，輸出到 `eepc_out\`，並回報「注意事項」。
- `/eep-learn <潛規則>`：把口述的規則存入知識庫。

#### 方式 B：命令列
```powershell
.\EEPCompiler_Portable\eepc.exe build "<專案副本>\<專案>.csproj" --auto-repair --eep-root "D:\EEP2006" --output "<輸出資料夾>"
```
- **一律加 `--output`**：專案的 OutputPath 常指向 `EEPNetServer\NewHPS\` 或 `EEPNetClient\NewHPS\`，不加可能覆蓋 EEP 環境中的 DLL。
- **在專案副本上編譯**：`--auto-repair` 會改寫 `.csproj`、清空 `licenses.licx`（見下方「不可回存版控的修改」）。
- 成功時 Exit Code 為 0；失敗時依輸出的錯誤明細處理。
- **閱讀輸出最後的「注意事項」**：`[資源略過]`、`[授權資源]`、`[規則 3 - 授權移植]`、`[參考]` 的意思見 `README_先看我.md` 第三節。

#### 方式 C：GUI
雙擊 `03_啟動GUI桌面編譯器.bat`（或直接雙擊 `eepc.exe`），把 `.csproj` 拖進視窗後按「一鍵修復並開始編譯」。v1.1 沒有重新測試 GUI，建議以方式 A、B 為主。

---

### 【Step 7】部署前驗證與部署

1. **與舊版比對**（`tools\DllCompare`，需 .NET 8 Runtime）：確認差異只有預期的修改。
   ```powershell
   .\EEPCompiler_Portable\tools\DllCompare\DllCompare.exe "<舊版.dll>" old.txt
   .\EEPCompiler_Portable\tools\DllCompare\DllCompare.exe "<新版.dll>" new.txt
   git diff --no-index -U0 old.txt new.txt
   ```
2. **CLR 2.0 資源載入檢查**（`tools\ResCheck`）：把新版 DLL 與它參照的 Srvtools、DevExpress 等 DLL 放在同一資料夾後執行，全部 `ok` 才部署。
   ```powershell
   .\EEPCompiler_Portable\tools\ResCheck\ResCheck.exe "<資料夾>\<新版.dll>"
   ```
3. **部署位置**：
   - Server 端（…S）：`EEPNetServer\<Solution>\`。替換後可能需要重啟 EEPNetServer 才會載入新版。
   - Client 端（…C）：伺服器的 `EEPNetClient\<Solution>\`。用戶端開啟表單時會自動下載，但只有伺服器那份的修改時間比用戶端新才會下載；替換後請用戶端完全關閉再重開。
   - `vi-VN\*.resources.dll` 衛星組件：本工具不產生，沿用既有檔案。
4. **授權資源**：專案有 `licenses.licx` 時，產出的 DLL 需含 `*.licenses`，否則 DevExpress 元件執行時可能出現試用提示。工具只能從 OutputPath 位置的舊版 DLL 移植；是否符合授權條款依公司授權確認。

---

## 不可回存版控的修改

以下是為了在本機編譯而產生的修改，回存 BitBucket 前必須還原或排除：

| 修改 | 來源 | 還原方式 |
|---|---|---|
| `.csproj` 的 HintPath、ProjectReference、licx 設定 | `--auto-repair` | 以 `.csproj.bak` 還原 |
| `licenses.licx` 被清空 | `--auto-repair` | 以 `.licx.bak` 還原 |
| `.cs` 被補上 UTF-8 BOM 或轉為 UTF-8 | `--auto-repair`、`to-utf8` | 若版控為 Big5，用 `to-big5` 轉回 |
| `obj\EEPCompiler\` 中間檔 | 編譯 | 不要加入版控 |

最簡單的做法是在專案副本上編譯，原始碼只做程式修改。

---

## Big5 與 UTF-8 亂碼處理速查

1. **Claude Code 開啟時中文正常**：檔案原本就是 UTF-8，可直接修改與編譯。`eepc.exe` 會為無 BOM 的 UTF-8 檔案補上 BOM（避免舊版編譯器誤判為 Big5），未修改的 Big5 檔案不會被改動。
2. **Claude Code 開啟時中文變亂碼**：檔案是 **Big5**。
   - 修改前轉成 UTF-8：
     ```powershell
     .\EEPCompiler_Portable\eepc.exe to-utf8 "FormOrder.cs"
     ```
   - 修改後，若版控為 Big5（例如 HPS 的 BitBucket），轉回 Big5 再回存：
     ```powershell
     .\EEPCompiler_Portable\eepc.exe to-big5 "FormOrder.cs"
     ```
