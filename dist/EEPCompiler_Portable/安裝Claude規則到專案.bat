@echo off
chcp 65001 >nul
echo ====================================================================
echo   Claude Code 規則檔 (CLAUDE.md) 安全整合安裝工具
echo   (不會覆蓋您原本的 CLAUDE.md，會自動以附加區塊與 .claude\rules 方式整合)
echo ====================================================================
set "TOOL_DIR=%~dp0"
set /p PROJ_DIR="請輸入或拖曳您公司的專案根目錄 (直接按 Enter 則預設為本工具的上層目錄): "
if "%PROJ_DIR%"=="" (
    for %%I in ("%~dp0..") do set "PROJ_DIR=%%~fI"
)

echo.
echo 目標專案根目錄: %PROJ_DIR%

:: 1. 建立 .claude\rules\eep-compiler.md (Claude Code 原生支援自動載入 .claude\rules\*.md)
if not exist "%PROJ_DIR%\.claude\rules" mkdir "%PROJ_DIR%\.claude\rules"
copy /y "%TOOL_DIR%CLAUDE.md" "%PROJ_DIR%\.claude\rules\eep-compiler.md" >nul
echo [完成 1/2] 已安裝模組化規則至: "%PROJ_DIR%\.claude\rules\eep-compiler.md"

:: 2. 檢查專案根目錄是否已有 CLAUDE.md
if not exist "%PROJ_DIR%\CLAUDE.md" (
    copy /y "%TOOL_DIR%CLAUDE.md" "%PROJ_DIR%\CLAUDE.md" >nul
    echo [完成 2/2] 專案原本無 CLAUDE.md，已直接建立 "%PROJ_DIR%\CLAUDE.md"
) else (
    findstr /i /c:"eepc.exe" "%PROJ_DIR%\CLAUDE.md" >nul 2>&1
    if errorlevel 1 (
        echo. >> "%PROJ_DIR%\CLAUDE.md"
        echo. >> "%PROJ_DIR%\CLAUDE.md"
        echo # === Infolight EEP / VS2005 自動化編譯與修復規則 (由 EEPCompiler 自動附加) === >> "%PROJ_DIR%\CLAUDE.md"
        echo @.claude/rules/eep-compiler.md >> "%PROJ_DIR%\CLAUDE.md"
        type "%TOOL_DIR%CLAUDE.md" >> "%PROJ_DIR%\CLAUDE.md"
        echo [完成 2/2] 偵測到原有的 CLAUDE.md，已安全將 EEP 編譯規則附加於原檔案尾端（原內容 100%% 保留）！
    ) else (
        echo [略過 2/2] 原有的 CLAUDE.md 內已包含 eepc.exe 規則，無須重複附加。
    )
)

echo.
echo 整合完成！現在啟動 Claude Code 即可同時讀取您原本的專案設定與 EEP 編譯規則！
pause
