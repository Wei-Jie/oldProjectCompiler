# 訊光 EEP 舊系統業務邏輯與維運知識庫 (`EEP_KNOWLEDGE_BASE.md`)

> **用途說明**：
> 本檔案為團隊與 Claude Code 共同維護的「活體知識庫」。
> - 當使用 `/eep-ask`、`/eep-fix` 或 `/eep-learn` 時，Claude Code 會優先查閱本檔案，並自動將新查明的業務邏輯、欄位公式、狀態碼定義追加於下方。
> - 新進同仁只需將 `.claude/` 與本檔案納入專案，即可直接站在前人累積的知識基礎上進行維運！

---

## 一、全系統共用規則與常見資料表字典

- **選單與程式代號反查表 (`MENUTABLE`)**：
  - 當使用者詢問某個畫面名稱時，可查詢 `SELECT MENUID, CAPTION, PACKAGE, FORM FROM MENUTABLE WHERE CAPTION LIKE '%畫面名稱%'`，其中 `PACKAGE` 即為對應的 Client DLL 名稱。
- **前後端呼叫慣例**：
  - Client 端 (`Cxxx`) 透過 `InfoDataSet.RemoteName = "Sxxx.cmdYyy"` 綁定 Server 端 (`Sxxx`) 的 SQL 命令。
  - Client 端透過 `CLIUtils.CallMethod("Sxxx", "MethodName", new object[] { ... })` 呼叫 Server 端的商業邏輯方法。

---

## 二、各作業模組商業邏輯與除錯筆記（由 AI 與團隊持續累積）

<!-- Claude Code 會在每次追查或修改完成後，自動將新知識追加於下方 -->
