# 訊光 EEP 全系統業務與程式碼導覽地圖 (`EEP_SYSTEM_MAP.md`)

> **給 AI Agent (Claude Code) 與開發者的優先指令**：
> 當使用者詢問某個畫面、功能、資料表或錯誤訊息時，**請務必先查閱本索引檔**，直接定位對應的 **Client 專案 (`C...`)** 與 **Server 專案 (`S...`)**，切勿盲目對全系統幾千個檔案進行大海撈針掃描！

- **掃描根目錄**: `c:\AI\vs2005compiler\samples`
- **收錄專案總數**: 2 個專案
- **產生時間**: 2026-10-18 22:56:10

## 一、全系統作業快速對照總表 (Client <-> Server <-> 資料表)

| 專案代號 | 角色 | 畫面標題 / 功能名稱 | 關聯 Server 模組 (`RemoteName` / `CallMethod`) | 讀寫資料表 (SQL Tables) | 專案相對路徑 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`CBankClient`** | Client (前端畫面) | 訊光 EEP 銀行訂單維護表單 (VS2005) | SOrderServer.cmdOrders<br/>SOrderServer.CheckOrderAmount() | `-` | `CBankClient` |
| **`SOrderServer`** | Client (前端畫面) | - | - | `Orders` | `SOrderServer` |

---

## 二、各專案詳細商業邏輯與按鈕關鍵字索引

### 📦 `CBankClient` (Client (前端畫面)) - 路徑: `CBankClient`
- **畫面中文標題**: 訊光 EEP 銀行訂單維護表單 (VS2005)
- **綁定資料來源 (`InfoDataSet.RemoteName`)**: `SOrderServer.cmdOrders`
- **呼叫後端方法 (`CLIUtils.CallMethod`)**: `SOrderServer.CheckOrderAmount()`

### 📦 `SOrderServer` (Client (前端畫面)) - 路徑: `SOrderServer`
- **提供之後端商業邏輯方法 (`Server Methods`)**: `CheckOrderAmount()`
- **關聯資料庫表格 (`SQL Tables`)**: `Orders`

