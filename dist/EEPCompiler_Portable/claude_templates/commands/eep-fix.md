請針對以下需求進行 EEP 程式碼修改、自動編譯驗證與知識庫更新：

修改需求：$ARGUMENTS

1. **定位目標專案**：先透過 `EEP_KNOWLEDGE_BASE.md` 與 `EEP_SYSTEM_MAP.md` 確認要修改的是哪一個 Client (`C...`) 或 Server (`S...`) 專案。
2. **檢查編碼**：若目標 `.cs` 檔案為 Big5 亂碼，請先執行 `.\EEPCompiler_Portable\eepc.exe to-utf8 "<檔案路徑>"`。
3. **安全修改程式碼**：
   - 嚴格遵守 C# 5.0 / VS2005 語法（禁止使用 `$"..."`、`?.`、`nameof`、`using var`、`new()`，字串格式化請一律用 `string.Format`）。
   - 嚴禁刪除 `.Designer.cs` 中的訊光元件宣告。
4. **執行自動修復與編譯驗證**：
   ```powershell
   .\EEPCompiler_Portable\eepc.exe build "<目標專案.csproj>" --auto-repair --claude
   ```
   若編譯失敗，請依據報告中的 AI 修復指引修正直到顯示 `PASS (0 Error(s))`。
5. **沉澱知識**：將本次修改的原因、影響欄位與商業邏輯追加紀錄至 `EEP_KNOWLEDGE_BASE.md`。
