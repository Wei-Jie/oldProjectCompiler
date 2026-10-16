# GitHub 專案下載與更新完整教學指南

- **專案名稱**：`oldProjectCompiler`
- **GitHub 倉庫網址**：[https://github.com/Wei-Jie/oldProjectCompiler](https://github.com/Wei-Jie/oldProjectCompiler)

到公司電腦後，您可以依照公司電腦的環境，從以下 **3 種下載方式** 選擇最方便的一種：

---

## 方式一：從瀏覽器直接下載整個專案壓縮包（最簡單，公司電腦沒裝 Git 也能用）

1. 在公司電腦打開瀏覽器，前往：
   👉 **[https://github.com/Wei-Jie/oldProjectCompiler](https://github.com/Wei-Jie/oldProjectCompiler)**
2. 如果是私人倉庫（Private），請先登入您的 GitHub 帳號（`Wei-Jie`）。
3. 點擊畫面右上角綠色的 **`<> Code`** 按鈕。
4. 在彈出的選單最下方，點擊 **`Download ZIP`**。
5. 下載完成後，將 `oldProjectCompiler-main.zip` 解壓縮。
6. 打開解壓縮後的資料夾，裡面的 **`dist\EEPCompiler_Portable`** 就是可以直接使用的免安裝編譯套件！

---

## 方式二：只單獨下載 `EEPCompiler_Portable.zip` 隨身包（最快、檔案最小）

如果您在公司電腦只需要執行檔與批次工具（不需要看編譯器本身的原始碼）：

1. 打開瀏覽器前往：
   👉 **[https://github.com/Wei-Jie/oldProjectCompiler/blob/main/dist/EEPCompiler_Portable.zip](https://github.com/Wei-Jie/oldProjectCompiler/blob/main/dist/EEPCompiler_Portable.zip)**
2. 點擊畫面右側的 **`Download raw file`**（下載圖示按鈕），即可直接取得 `EEPCompiler_Portable.zip`。
3. 解壓縮到您的公司舊系統專案目錄下即可直接使用！

---

## 方式三：使用 `git clone` 指令下載（適合有安裝 Git 的電腦，方便日後一鍵更新）

如果公司電腦有安裝 Git，打開 **PowerShell** 或 **CMD（命令提示字元）**：

### 1. 第一次下載（Clone）
切換到您想存放工具的資料夾（例如 `C:\AI` 或桌面），輸入以下指令：
```powershell
git clone https://github.com/Wei-Jie/oldProjectCompiler.git
```
執行後就會自動產生 `oldProjectCompiler` 資料夾。

### 2. 日後如果在私人電腦修改了工具，公司電腦如何同步最新進度？
只要在公司電腦的 `oldProjectCompiler` 資料夾內打開終端機，輸入：
```powershell
git pull
```
就會自動把最新版本拉下來！

---

## 下載完成後的 3 步驟啟用流程

1. **將 `EEPCompiler_Portable` 資料夾複製到公司舊系統專案目錄下**。
2. **整合 Claude Code 規則**：
   - 雙擊執行 `EEPCompiler_Portable\安裝Claude規則到專案.bat`（會自動保留您原本的 `CLAUDE.md`，並把 EEP 編譯規則安全附加進去）。
3. **開始編譯**：
   - **手動視窗操作**：直接雙擊 `EEPCompiler_Portable\eepc.exe`。
   - **交給 Claude Code 自動編譯除錯**：直接請 Claude Code 修改程式碼，它會依照 `CLAUDE.md` 自動呼叫 `eepc.exe build <專案> --auto-repair --claude` 完成修復與編譯！
