# EEPCompiler_Portable v1.1（修正版）

> 打包日期：2026-10-19　｜　以同仁的 eepc（oldProjectCompiler）為基礎，修正 7 個缺陷後重新打包，供新人編譯 EEP2006（VS2005／.NET 2.0）舊專案使用。
> 完整步驟見 `00_從下載到編譯完整步驟總覽.md`（已依 v1.1 改寫）；本檔說明 v1.1 的差異與公司環境的快速開始方式。

## 一、快速開始（約 5 分鐘）

1. **把整個 `EEPCompiler_Portable` 資料夾複製到本機**（任何位置皆可）。
2. **執行 `01_收集公司EEP與DevExpress組件.bat`**，依提示輸入：
   - EEP 根目錄：例如 `D:\EEP2006`
   - DevExpress 資料夾：例如 `D:\HPS\devexpressdll\Sources\DevExpress.DLL`

   執行完會列出 `lib\` 內每個 DLL 的版本，確認 `Srvtools.dll`、`InfoRemoteModule.dll` 是 2.2.0.0、大小約 2 MB 與 230 KB。
3. **複製要編譯的專案**到一個工作資料夾（不要直接在版控的原始碼上編譯），然後執行：

   ```powershell
   .\eepc.exe build "<專案.csproj>" --auto-repair --eep-root "D:\EEP2006" --output "<輸出資料夾>"
   ```

4. **閱讀輸出最後的「注意事項」**（見第三節），再用 `tools\` 的工具驗證（見第四節）。

## 二、v1.1 修正了什麼

| 編號 | 原版問題 | 影響 |
|---|---|---|
| D1 | 文化特定資源（如 `Component.vi-VN.resx`）被編成和主要資源同名，蓋掉主要資源 | 編譯失敗（CS1508）；手動排除後產出的 DLL 遺失 `InfoCommand` 的 SQL |
| D2 | ProjectReference 指向不存在的專案（如 `..\..\Srvtools\Srvtools.csproj`）時直接忽略 | 缺少 Srvtools 參照，編譯失敗 |
| D3 | 搜尋 DLL 時工具包 `lib\` 優先於 `--eep-root` | 使用到內附的 mock DLL |
| D4 | `System` 等 Framework 組件會從 EEP 目錄取到複本 | 參照到錯誤的 System.dll |
| D5 | 編譯錯誤沒有顯示；中文錯誤訊息亂碼；`--claude` 報告一律要求降級 C# 語法 | 無法判斷失敗原因，AI 可能誤改原始碼 |
| D6 | 依 mock DLL（.NET 4）判斷目標版本 | VS2005 專案被編成 .NET 4 |
| D7 | 表單中的 DataSet、圖片等物件資源被寫成 .NET 4 版本組件 | **部署到 EEP2006（CLR 2.0）後表單開不起來** |

其他調整：
- 移除工具包內附的 mock `Srvtools.dll`、`InfoRemoteModule.dll`，`lib\` 改由 01 批次檔收集真實組件。
- 01 批次檔只從 `EEPNetServer`（找不到才用 `EEPNetClient`）收集 EEP 組件，不再遞迴掃描整個根目錄，避免收集到 `.bak` 或舊版備份中的同名 DLL；新增 DevExpress 資料夾輸入與版本清單。
- 批次檔改存為 Big5（CP950）、CRLF 換行，並移除 `chcp 65001`：原版的 UTF-8 BOM 會讓第一行被 cmd 視為無效指令；在 UTF-8 模式下 `set /p` 讀不到重導輸入，中文路徑也容易讀錯。因此批次檔需在繁體中文 Windows 執行。
- 02 批次檔附加到專案 `CLAUDE.md` 的標題行改為英文，避免 Big5 字元寫入 UTF-8 檔案變成亂碼；並修正重複執行時會重複附加的問題。
- 01 批次檔輸入的路徑含括號（例如 `C:\Program Files (x86)\...`）也能正確處理。
- 從舊版 DLL 移植授權資源、或沒有授權資源可用時，都會列在「注意事項」。
- 文件同步更新：`00_從下載到編譯完整步驟總覽.md` 依 v1.1 改寫（取得方式、新版 01 收集流程、環境需求、部署前驗證、不可回存版控的修改）；`CLAUDE.md` 修正規則 3（licx 回存前須還原）、編譯指令加上 `--eep-root`／`--output`，新增規則 6（Big5／UTF-8 編碼）與規則 7（輸出位置、不可回存的修改、編譯後檢查）；`/eep-fix` 與 `eep-maintainer` Skill 改為在專案副本上編譯並輸出到 `eepc_out\`，並回報注意事項與部署前驗證。
- `eep_tool.py` 新增 `--output`，build／watch 未指定時預設輸出到專案資料夾下的 `eepc_out`；watch 模式會提醒自動修復會改寫被監控資料夾的檔案。

## 三、輸出中的「注意事項」怎麼看

| 訊息開頭 | 意思 | 要做什麼 |
|---|---|---|
| `[資源略過]` | 文化特定資源（vi-VN）沒有編入主組件，本工具不產生衛星組件 | 部署時沿用既有的 `vi-VN\<組件>.resources.dll`；若該 resx 有修改，需用其他方式產生 |
| `[授權資源]` | 專案有 `licenses.licx`，但產出的 DLL 不含 `*.licenses` | DevExpress 元件執行時可能出現試用提示。可先把舊版 DLL 放到 csproj 的 OutputPath 位置再編譯，讓工具移植 |
| `[規則 3 - 授權移植]` | 授權資源是從舊版 DLL 移植過來 | 是否符合 DevExpress 授權條款，依公司授權確認 |
| `[參考]` | 組件取自工具包 `lib\`，或 ProjectReference 改用 DLL | 確認版本正確 |

## 四、部署前驗證工具（`tools\`）

| 工具 | 用途 | 需求 |
|---|---|---|
| `tools\DllCompare\DllCompare.exe` | 把 DLL 轉成文字清單（參照、型別、IL、資源內容），用來和舊版比對，確認差異只有預期的修改。不載入也不執行 DLL | .NET 8 Runtime |
| `tools\ResCheck\ResCheck.exe` | 在 CLR 2.0 上讀取 DLL 的全部資源（會實際反序列化 DataSet、圖片），確認 EEP2006 能載入 | Windows 已啟用 .NET Framework 3.5（含 2.0） |

```powershell
# 與舊版比對
.\tools\DllCompare\DllCompare.exe "<舊版.dll>" old.txt
.\tools\DllCompare\DllCompare.exe "<新版.dll>" new.txt
git diff --no-index -U0 old.txt new.txt

# CLR 2.0 資源載入檢查：把 DLL 與它參照的 Srvtools、DevExpress 等 DLL 放在同一個資料夾
.\tools\ResCheck\ResCheck.exe "<資料夾>\<新版.dll>"
```

## 五、注意事項

1. **不要直接在版控原始碼上執行 `--auto-repair`**：它會改寫 `.csproj` 並清空 `licenses.licx`（皆有 `.bak` 備份），這些修改不可回存 BitBucket。
2. **一律加 `--output`**：EEP 專案的 OutputPath 常指向 `EEPNetServer\NewHPS\` 或 `EEPNetClient\NewHPS\`，不指定輸出可能覆蓋 EEP 環境中的 DLL。
3. **部署位置**：Server 端（…S）放 `EEPNetServer\<Solution>\`；Client 端（…C）放伺服器的 `EEPNetClient\<Solution>\`，用戶端會在開啟表單時自動下載（只有伺服器那份的修改時間較新才會下載）。替換 Server 端 DLL 後，可能需要重啟 EEPNetServer。
4. **驗證範圍**：v1.1 已用 HPSR039S、HPSR039C 驗證（與同仁以 VS2005 編譯的版本比對一致，CLR 2.0 資源載入通過）。其他專案（例如資源較多的 HPSI004C）尚未用本工具驗證，第一次使用時請務必做第四節的比對與檢查。01、02、04 批次檔與 `env`、`index` 指令已在 v1.1 測試；GUI 模式、`to-utf8`、`to-big5` 指令與 `eep_tool.py`（本機無 Python）未測試。
5. **授權**：`lib\` 內的 EEP 與 DevExpress 組件屬商業軟體，僅供公司內部編譯使用，不要放到公開的 GitHub。

## 六、檔案清單

| 檔案 | 說明 |
|---|---|
| `eepc.exe` | 編譯器 v1.1（console 程式；不帶參數執行會開啟 GUI）。SHA-256：`02BC4F8F6F97F96E911BAF2FDB8A7D77CA8CAA537E82C9EA8A357D304FCD0E76` |
| `01_收集公司EEP與DevExpress組件.bat` | 收集 EEP、DevExpress 組件到 `lib\` |
| `02_安裝Claude規則到專案.bat` | 將 Claude Code 規則、Skill、斜線指令安裝到專案 |
| `03_啟動GUI桌面編譯器.bat` | 開啟 GUI |
| `04_產生全系統業務導覽地圖(解決大海撈針).bat` | 掃描專案產生 `EEP_SYSTEM_MAP.md` |
| `CLAUDE.md`、`claude_templates\` | Claude Code 規則與範本 |
| `eep_tool.py` | Python 包裝工具（watch 模式等，需另外安裝 Python 3.9 以上；v1.1 的修改未實際執行測試） |
| `lib\` | 編譯用組件（由 01 批次檔收集，初始為空） |
| `tools\` | 部署前驗證工具（含原始碼） |
| `00_從下載到編譯完整步驟總覽.md` | 完整步驟（v1.1 改寫） |
