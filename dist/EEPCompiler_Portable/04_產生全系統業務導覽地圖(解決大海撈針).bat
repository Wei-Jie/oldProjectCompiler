@echo off
chcp 65001 >nul
for %%I in ("%~dp0..") do set "DEFAULT_DIR=%%~fI"
echo ====================================================================
echo   訊光 EEP 全系統業務與程式碼導覽地圖產生器 (解決大海撈針問題)
echo   自動遞迴掃描目標目錄下所有子資料夾的：畫面標題、C/S 呼叫關聯、SQL 資料表
echo ====================================================================
echo 目前預設掃描目錄 (本工具的上層目錄): %DEFAULT_DIR%
echo.
set /p SRC_DIR="請直接按 Enter 掃描上方預設目錄，或將其他專案資料夾拖曳至此視窗: "
if "%SRC_DIR%"=="" (
    set "SRC_DIR=%DEFAULT_DIR%"
)
:: 移除拖曳可能帶入的雙引號
set "SRC_DIR=%SRC_DIR:"=%"
echo.
echo 正在遞迴掃描目錄: "%SRC_DIR%" 下的所有子資料夾...
"%~dp0eepc.exe" index "%SRC_DIR%"
pause
