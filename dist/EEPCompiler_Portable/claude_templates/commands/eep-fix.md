請針對以下需求進行 EEP 程式碼修改、自動編譯驗證與知識庫更新：

修改需求：$ARGUMENTS

1. **定位目標專案**：先透過 `EEP_KNOWLEDGE_BASE.md` 與 `EEP_SYSTEM_MAP.md` 確認要修改的是哪一個 Client (`C...`) 或 Server (`S...`) 專案。
2. **檢查編碼**：若目標 `.cs` 檔案為 Big5 亂碼，請先執行 `.\EEPCompiler_Portable\eepc.exe to-utf8 "<檔案路徑>"`，並記下哪些檔案轉過碼（回存前可能需要轉回 Big5）。
3. **安全修改程式碼**：
   - 嚴格遵守 C# 5.0 / VS2005 語法（禁止使用 `$"..."`、`?.`、`nameof`、`using var`、`new()`，字串格式化請一律用 `string.Format`）。
   - 嚴禁刪除 `.Designer.cs` 中的訊光元件宣告。
4. **在專案副本上編譯驗證**：`--auto-repair` 會改寫 `.csproj`、清空 `licenses.licx`，不可直接在原始碼上執行。
   - 先把目標專案資料夾複製到 `.\eepc_work\<專案名稱>`（每次重新複製，確保是最新修改）。
   - 若要保留 DevExpress 等授權資源：把目前部署中的舊版 DLL 複製到副本 `.csproj` 的 `<OutputPath>` 位置（相對於副本資料夾），工具才能移植 `*.licenses`。
   - 執行：
     ```powershell
     .\EEPCompiler_Portable\eepc.exe build ".\eepc_work\<專案名稱>\<專案名稱>.csproj" --auto-repair --eep-root "<EEP根目錄，例如 D:\EEP2006>" --output ".\eepc_out\<專案名稱>" --claude
     ```
   - 若編譯失敗，先看錯誤明細判斷原因：參考組件或資源錯誤請修正參考與 `--eep-root`，不要改 `.cs`；確定是語法錯誤才修改原始碼，修改後重新複製副本再編譯，直到顯示 `PASS (0 Error(s))`。
5. **回報結果**：向使用者說明
   - 輸出的 DLL 位置（`.\eepc_out\<專案名稱>\`）。
   - 報告中的每一則「注意事項」（`[資源略過]`、`[授權資源]`、`[規則 3 - 授權移植]`、`[參考]`）及其影響。
   - 建議部署前用 `EEPCompiler_Portable\tools\DllCompare` 與舊版比對、用 `tools\ResCheck` 做 CLR 2.0 資源載入檢查。
   - 原始碼中需要回存的檔案；若有 `to-utf8` 轉碼且版控為 Big5，提醒回存前用 `to-big5` 轉回。`eepc_work\`、`eepc_out\` 不要加入版控。
6. **沉澱知識**：將本次修改的原因、影響欄位與商業邏輯追加紀錄至 `EEP_KNOWLEDGE_BASE.md`。
