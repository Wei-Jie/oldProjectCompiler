---
name: eep-maintainer
description: 訊光 Infolight EEP (.NET / VS2005) 舊系統維運、問題定位、自動化編譯與團隊知識沉澱專屬技能 (Skill)。當使用者詢問舊系統業務邏輯、追查畫面/資料庫問題、或需要修改並重新編譯 EEP Client/Server 專案時自動啟用。
---

# 訊光 EEP 舊系統維運與知識傳承技能 (EEP Maintainer Skill)

本技能專為團隊維運訊光科技 (Infolight) EEP 舊系統設計，結合「快速索引定位」、「安全修改與自動編譯」以及「團隊業務知識自動累積」三大工作流。

---

## 工作流一：快速追查使用者問題（禁止大海撈針）

當同仁詢問某個功能、畫面報錯、金額計算邏輯或資料表流向時，請嚴格依照以下順序定位：

1. **優先查閱團隊已累積的知識庫 (`EEP_KNOWLEDGE_BASE.md`)**
   - 若專案根目錄存在 `EEP_KNOWLEDGE_BASE.md`，請最優先讀取，確認過去是否已追查並記錄過相同模組的邏輯。
2. **查閱全系統業務導覽地圖 (`EEP_SYSTEM_MAP.md`)**
   - 若尚無 `EEP_SYSTEM_MAP.md`，請先執行：
     ```powershell
     .\EEPCompiler_Portable\eepc.exe index "."
     ```
   - 從 `EEP_SYSTEM_MAP.md` 中比對「畫面中文標題」、「按鈕關鍵字」或「SQL 資料表」，秒速鎖定對應的 **Client 專案 (`Cxxx`)** 與 **Server 專案 (`Sxxx`)**。
   - 若資料庫可查詢，亦可提示同仁查詢 `SELECT MENUID, CAPTION, PACKAGE, FORM FROM MENUTABLE WHERE CAPTION LIKE '%關鍵字%'`。
3. **只讀取鎖定的 Client 與 Server 專案檔案**
   - 若打開 `.cs` 發現中文為亂碼（代表原檔為 Big5），請先執行 `.\EEPCompiler_Portable\eepc.exe to-utf8 "<檔案路徑>"` 轉碼後再讀取。
   - 順著 Client 的 `InfoDataSet.RemoteName` 與 `CLIUtils.CallMethod("Sxxx", "MethodName", ...)` 追蹤至 Server 端的 `InfoCommand` 與商業邏輯方法。

---

## 工作流二：修改程式碼與自動編譯驗證

1. **只修改目標作業的 C 版或 S 版專案**（EEP 為獨立 DLL 外掛架構，單獨編譯該作業的 `.csproj` 不會影響其他作業）。
2. **嚴守舊版 C# 5.0 / VS2005 語法限制**：
   - 禁止使用 `$"..."`（請用 `string.Format`）、禁止 `?.`、禁止 `nameof()`、禁止 `using var`、禁止 `new()`。
   - 嚴禁破壞 `.Designer.cs` 中的 `InfoDataSet`、`InfoBindingSource`、`InfoCommand`、`UpdateComponent` 宣告。
3. **在專案副本上執行自動修復與編譯閉環**（`--auto-repair` 會改寫 `.csproj`、清空 `licenses.licx`，不可直接在原始碼上執行）：
   - 把目標專案資料夾複製到 `.\eepc_work\<專案名稱>`；需要保留 DevExpress 授權資源時，把部署中的舊版 DLL 放到副本 `.csproj` 的 `<OutputPath>` 位置。
   - 執行（一律指定 `--output`，避免 OutputPath 覆蓋 EEP 環境中的 DLL）：
     ```powershell
     .\EEPCompiler_Portable\eepc.exe build ".\eepc_work\<專案名稱>\<專案名稱>.csproj" --auto-repair --eep-root "<EEP根目錄，例如 D:\EEP2006>" --output ".\eepc_out\<專案名稱>" --claude
     ```
   - 確保輸出為 `PASS (0 Error(s))`。失敗時先看錯誤明細：參考組件或資源錯誤請修正參考與 `--eep-root`，確定是語法錯誤才修改 `.cs`。
4. **回報注意事項與部署前驗證**：
   - 逐一說明報告中的「注意事項」：`[資源略過]`（衛星組件未產生，沿用既有 `vi-VN\*.resources.dll`）、`[授權資源]`（DLL 不含 `*.licenses`，執行時可能出現試用提示）、`[規則 3 - 授權移植]`（授權資源從舊版 DLL 移植）、`[參考]`（組件來源）。
   - 建議部署前用 `EEPCompiler_Portable\tools\DllCompare` 與舊版比對，並用 `tools\ResCheck` 做 CLR 2.0 資源載入檢查。
   - 部署位置：Server 端（`Sxxx`）放 `EEPNetServer\<Solution>\`，替換後可能需重啟 EEPNetServer；Client 端（`Cxxx`）放伺服器的 `EEPNetClient\<Solution>\`，用戶端完全重開後才會下載新版。
5. **回存版控前**：只回存原始碼的程式修改；`.csproj.bak`、`.licx.bak`、`eepc_work\`、`eepc_out\`、`obj\EEPCompiler\` 不可加入版控；`to-utf8` 轉過的檔案若版控為 Big5，需用 `to-big5` 轉回。

---

## 工作流三：自動累積團隊知識庫 (`EEP_KNOWLEDGE_BASE.md`)

**每當你成功替同仁查明一個業務邏輯、或是修好一個 Bug 後，請主動將本次發現濃縮為結構化筆記，追加（Append）至專案根目錄的 `EEP_KNOWLEDGE_BASE.md` 中**，格式如下：

```markdown
### [模組代號 Cxxx / Sxxx] 畫面中文名稱 - 主題簡述 (YYYY-MM-DD)
- **對應專案與檔案**: Client: `Cxxx/Form1.cs`, Server: `Sxxx/Component.cs`
- **關聯資料表**: `TABLE_A`, `TABLE_B`
- **核心商業邏輯與潛規則**:
  1. 說明欄位計算方式、狀態碼意義（例如 Status='Y' 代表已核准）或檢核條件。
  2. 說明常踩的坑或特殊例外處理。
```
