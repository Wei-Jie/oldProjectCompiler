請針對使用者提出的問題，按照以下標準步驟進行快速定位與解答（**嚴禁盲目掃描全系統所有檔案**）：

使用者問題：$ARGUMENTS

1. **步驟 1（查閱既有知識庫）**：先讀取 `EEP_KNOWLEDGE_BASE.md`（若存在），查看團隊過去是否已整理過相關業務規則。
2. **步驟 2（查閱全系統導覽地圖）**：讀取 `EEP_SYSTEM_MAP.md`（若不存在，請先執行 `.\EEPCompiler_Portable\eepc.exe index "."` 產生）。從表中找出與問題相關的「畫面標題」、「Client 專案代號 (`C...`)」、「Server 專案代號 (`S...`)」與「SQL 資料表」。
3. **步驟 3（精準讀取程式碼）**：只打開鎖定的 Client 與 Server `.cs` 檔案（若發現 Big5 亂碼，先執行 `.\EEPCompiler_Portable\eepc.exe to-utf8 "<檔案路徑>"`）。順著 `InfoDataSet.RemoteName` 或 `CLIUtils.CallMethod` 追查完整前後端邏輯。
4. **步驟 4（清楚回覆並自動沉澱知識）**：
   - 用淺顯易懂的繁體中文向使用者說明商業邏輯、判斷條件與所在檔案行號。
   - **自動將本次查明的業務規則與檔案關聯，追加寫入 `EEP_KNOWLEDGE_BASE.md` 中**，供未來團隊與新同仁直接查閱！
