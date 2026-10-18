# 02. 公司環境部署、乾淨電腦編譯、Big5 處理與 Claude Code 知識庫實戰指南

本文件完整收錄了在**公司電腦**或**乾淨電腦**使用本套件的所有進階細節，包含：與既有 `CLAUDE.md` 整合、DevExpress 授權處理、Big5/UTF-8 雙向轉碼、全系統地圖索引，以及如何將過去分析好的 `.html` / `.md` 報告承接為可傳承給新同仁的 Claude Code Skill。

---

## 一、隨身工具包 (`dist\EEPCompiler_Portable\`) 檔案清單

| 檔案 / 資料夾 | 用途說明 |
| :--- | :--- |
| **`00_從下載到編譯完整步驟總覽.md`** | 從下載到完成設定的 Step 1 ~ Step 6 總覽手冊。 |
| **`01_收集公司EEP與DevExpress組件.bat`** | 自動從公司 EEP 目錄與 Windows GAC 收集 `Srvtools.dll`、`InfoRemoteModule.dll`、`DevExpress*.dll`、`FarPoint*.dll` 至 `lib\` 資料夾。 |
| **`02_安裝Claude規則到專案.bat`** | **安全安裝工具**：不覆蓋您原本的 `CLAUDE.md` 與知識庫，自動安裝編譯規則、Skill (`eep-maintainer`) 與 4 個斜線指令（`/eep-import`、`/eep-ask`、`/eep-fix`、`/eep-learn`）。 |
| **`03_啟動GUI桌面編譯器.bat`** | 啟動繁體中文桌面視窗介面（亦可愛直接雙擊 `eepc.exe`）。 |
| **`04_產生全系統業務導覽地圖(解決大海撈針).bat`** | 3 秒掃描全系統所有 C 版（畫面標題）、S 版（後端方法）與 SQL 資料表關聯，產出 `EEP_SYSTEM_MAP.md`。 |
| **`eepc.exe`** | 免安裝單一核心執行檔（CLI + GUI 雙模式）。 |
| **`CLAUDE.md`** | 專為訊光 EEP (.NET / VS2005) 撰寫的 AI 編譯與 C# 5.0 語法降級規則檔。 |
| **`claude_templates\`** | 內含團隊傳承 Skill (`SKILL.md`)、4 個斜線指令與 `EEP_KNOWLEDGE_BASE.md` 初始樣板。 |
| **`lib\`** | 隨身 DLL 參考目錄（`eepc.exe` 編譯時會最優先從此資料夾載入 DLL）。 |

---

## 二、如何與公司專案現有的 `CLAUDE.md` 整合？（三種做法）

1. **做法 1（最推薦）：直接雙擊 `02_安裝Claude規則到專案.bat`**
   - 腳本會 **100% 保留您原有的 `CLAUDE.md` 內容**，自動將規則放入 `.claude\rules\eep-compiler.md`，並在原 `CLAUDE.md` 尾端安全加上 `@.claude/rules/eep-compiler.md` 與 `@EEP_KNOWLEDGE_BASE.md` 引用。
2. **做法 2：手動在原 `CLAUDE.md` 尾端加上 `@路徑` 引用**
   ```markdown
   ## EEP 編譯與語法規範
   @EEPCompiler_Portable/CLAUDE.md
   @EEP_KNOWLEDGE_BASE.md
   ```
3. **做法 3：請公司電腦上的 Claude Code 智慧整併**
   > **提示詞**：「請讀取 `EEPCompiler_Portable/CLAUDE.md`，將它與我現有的 `CLAUDE.md` 整合，保留原本的商業邏輯說明，並將編譯與驗證指令更新為使用 `.\EEPCompiler_Portable\eepc.exe build <專案路徑> --auto-repair --claude`。」

---

## 三、單一作業獨立編譯特性（C 版與 S 版互不干擾其他作業）

訊光 EEP 採用**「一支作業 ＝ 一顆 Client DLL (`Cxxx.dll`) ＋ 一顆 Server DLL (`Sxxx.dll`)」**的外掛架構：
- 當您只修改並重新編譯某支作業（例如訂單維護 `COrder.csproj` 與 `SOrder.csproj`）時，**完全不會影響系統裡其他幾百支作業的 DLL**。
- 若只改畫面防呆或按鈕事件，只需重編 **C 版**；若只改後端 SQL 或商業方法，只需重編 **S 版**。
- 編譯前建議先關閉本機開著的測試用 `EEPNetClient.exe` / `EEPNetServer.exe`，避免舊 DLL 被鎖定（`eepc.exe` 覆蓋前會自動備份 `.dll.bak`）。

---

## 四、DevExpress 與第三方控制項授權解決機制

1. **編譯期（解決 `LC.exe exited with code -1` 與 `MSB3091`）**：
   - `eepc.exe --auto-repair` 會自動將 `Properties\licenses.licx` 備份並**清空為 0 byte**，同時將 `.csproj` 中的 `<EmbeddedResource Include="...licenses.licx">` **改為 `<None>`**，使編譯過程完全不呼叫 `LC.exe`。
2. **執行期（舊版 `.licenses` 二進位授權資源無損移植，防止跳出試用彈窗）**：
   - 當使用 Direct 引擎重編並覆蓋舊版 `.dll` 時，`eepc.exe` 會自動從原有的舊版 `.dll`（或 `.dll.bak`）中抽出當年正版簽發的 `*.licenses` 內嵌資源，並**原封不動移植嵌入到新編譯的 `.dll` 中**。

---

## 五、Big5 與 UTF-8 混合編碼處理（規則 6 與雙向無損轉碼）

1. **為什麼我們預設不亂動未修改的 Big5 檔案？**
   - 舊版編譯器本來就能直接編譯 Big5 的 `.cs` 檔；若一次把專案裡幾十個沒改到的 Big5 檔案全轉成 UTF-8，會弄髒公司版控（Git / SVN / VSS）的變更紀錄。
   - 因此 `eepc.exe build` 預設**只會幫「已被 Claude Code 修改過、存成無 BOM 之 UTF-8」的檔案自動補上 `UTF-8 BOM` 檔頭**（防止舊編譯器誤用 Big5 讀取而吃掉雙引號報 `CS1010` 錯誤），未修改的 Big5 檔案則保持原狀不動。
2. **當 Claude Code 打開某支舊 `.cs` 檔發現中文是亂碼時（代表原檔為 Big5）**：
   - **步驟 1（修改前轉為 UTF-8 with BOM 讓 Claude 看得懂）**：
     ```powershell
     .\EEPCompiler_Portable\eepc.exe to-utf8 "FormOrder.cs"
     ```
   - **步驟 2（交由 Claude 修改並用 `eepc.exe build` 編譯）**。
   - **步驟 3（若公司版控要求改完後必須轉回 Big5）**：
     ```powershell
     .\EEPCompiler_Portable\eepc.exe to-big5 "FormOrder.cs"
     ```

---

## 六、終結大海撈針：全系統導覽地圖 (`EEP_SYSTEM_MAP.md`) 與團隊知識傳承 Skill

### 1. 一鍵產生 `EEP_SYSTEM_MAP.md`
雙擊 **`04_產生全系統業務導覽地圖(解決大海撈針).bat`**（或執行 `.\EEPCompiler_Portable\eepc.exe index "."`），工具會在幾秒內掃描所有專案（相容 Big5 與 UTF-8 且不改動原檔），整理出：
- 每個專案代號 (`Cxxx` / `Sxxx`) <-> 畫面中文標題 (`this.Text`) <-> 呼叫的後端模組 (`RemoteName` / `CallMethod`) <-> 讀寫的 SQL 資料表 (`Tables`)。
- 此外，您也可以在資料庫查詢 `SELECT MENUID, CAPTION, PACKAGE, FORM FROM MENUTABLE WHERE CAPTION LIKE '%畫面名稱%'`，直接從選單名稱反查 `PACKAGE`（DLL 名稱）。

### 2. 原地承接既有的 `.html` 與 `.md` 分析報告（零資安風險，不需將檔案帶出公司）
執行完 `02_安裝Claude規則到專案.bat` 後，在公司電腦的 Claude Code 輸入：
```text
/eep-import
```
公司電腦上的 Claude Code 就會自動掃描您之前分析做好的所有 `.html` 與 `.md` 報告，將裡面的模組邏輯、資料表定義與公式萃取整併進 **`EEP_KNOWLEDGE_BASE.md`**！

### 3. 日常維運與新同仁交接的 4 大斜線指令
| 斜線指令 | 功能說明 |
| :--- | :--- |
| **`/eep-import [路徑]`** | 自動吸收專案內既有的 `.html` 與 `.md` 分析報告，匯入 `EEP_KNOWLEDGE_BASE.md`。 |
| **`/eep-ask <問題>`** | 先查 `EEP_KNOWLEDGE_BASE.md` 與 `EEP_SYSTEM_MAP.md` 秒速回答問題，並**自動將新發現追加進知識庫**。 |
| **`/eep-fix <修改需求>`** | 嚴守 C# 5.0 語法修改目標 C/S 專案，自動執行 `eepc.exe build --auto-repair --claude` 驗證到 `0 Error(s)`，並記錄至知識庫。 |
| **`/eep-learn <筆記>`** | 隨時將前輩口述或新查明的系統潛規則歸檔至 `EEP_KNOWLEDGE_BASE.md`。 |

---

## 七、`eepc.exe` 完整命令列參數速查表

```powershell
# 1. 自動修復並編譯專案 (Claude Code 預設呼叫指令)
.\EEPCompiler_Portable\eepc.exe build "<專案.csproj|.sln|資料夾>" --auto-repair --claude [--eep-root "C:\EEP.NET"]

# 2. 產生全系統業務與程式碼導覽地圖 (輸出 EEP_SYSTEM_MAP.md)
.\EEPCompiler_Portable\eepc.exe index "<原始碼根目錄>"

# 3. Big5 與 UTF-8 with BOM 雙向無損互轉 (支援單一 .cs 檔或資料夾)
.\EEPCompiler_Portable\eepc.exe to-utf8 "<檔案.cs 或 資料夾>"
.\EEPCompiler_Portable\eepc.exe to-big5 "<檔案.cs 或 資料夾>"

# 4. 僅執行規則 2~4 與規則 6 自動修復
.\EEPCompiler_Portable\eepc.exe repair "<專案.csproj|.sln|資料夾>"

# 5. 僅執行規則 5 C# 語法降級與 Designer 預檢
.\EEPCompiler_Portable\eepc.exe check "<專案.csproj|.sln|資料夾>"

# 6. 開啟桌面視窗介面 (GUI)
.\EEPCompiler_Portable\eepc.exe --gui
```
