@echo off
echo ====================================================================
echo   Claude Code 規則檔、Skill 與團隊知識庫 一鍵安全安裝工具
echo   (不會覆蓋您原本的 CLAUDE.md 與已累積的 EEP_KNOWLEDGE_BASE.md)
echo ====================================================================
set "TOOL_DIR=%~dp0"
set /p PROJ_DIR="請輸入或拖曳您公司的專案根目錄 (直接按 Enter 則預設為本工具的上層目錄): "
if "%PROJ_DIR%"=="" (
    for %%I in ("%~dp0..") do set "PROJ_DIR=%%~fI"
)

echo.
echo 目標專案根目錄: %PROJ_DIR%

:: 1. 建立 .claude\rules\eep-compiler.md
if not exist "%PROJ_DIR%\.claude\rules" mkdir "%PROJ_DIR%\.claude\rules"
copy /y "%TOOL_DIR%CLAUDE.md" "%PROJ_DIR%\.claude\rules\eep-compiler.md" >nul
echo [完成 1/4] 已安裝編譯規則至: .claude\rules\eep-compiler.md

:: 2. 建立 .claude\skills\eep-maintainer\SKILL.md (團隊可傳承的 Claude Skill)
if not exist "%PROJ_DIR%\.claude\skills\eep-maintainer" mkdir "%PROJ_DIR%\.claude\skills\eep-maintainer"
copy /y "%TOOL_DIR%claude_templates\skills\eep-maintainer\SKILL.md" "%PROJ_DIR%\.claude\skills\eep-maintainer\SKILL.md" >nul
echo [完成 2/4] 已安裝 EEP 維運技能 (Skill) 至: .claude\skills\eep-maintainer\SKILL.md

:: 3. 建立 .claude\commands\ 斜線指令 (/eep-ask, /eep-fix, /eep-learn)
if not exist "%PROJ_DIR%\.claude\commands" mkdir "%PROJ_DIR%\.claude\commands"
copy /y "%TOOL_DIR%claude_templates\commands\*.md" "%PROJ_DIR%\.claude\commands\" >nul
echo [完成 3/4] 已安裝快捷指令: /eep-ask (查問題), /eep-fix (改程式並編譯), /eep-learn (存知識)

:: 4. 建立 EEP_KNOWLEDGE_BASE.md (若已存在則保留，絕不覆蓋團隊累積的心血)
if not exist "%PROJ_DIR%\EEP_KNOWLEDGE_BASE.md" (
    copy /y "%TOOL_DIR%claude_templates\EEP_KNOWLEDGE_BASE.md" "%PROJ_DIR%\EEP_KNOWLEDGE_BASE.md" >nul
    echo [完成 4/4] 已初始化團隊活體知識庫: EEP_KNOWLEDGE_BASE.md
) else (
    echo [保留 4/4] 偵測到既有的 EEP_KNOWLEDGE_BASE.md，已完整保留團隊累積的知識！
)

:: 5. 檢查專案根目錄是否已有 CLAUDE.md
if not exist "%PROJ_DIR%\CLAUDE.md" (
    copy /y "%TOOL_DIR%CLAUDE.md" "%PROJ_DIR%\CLAUDE.md" >nul
) else (
    findstr /i /c:"@.claude/rules/eep-compiler.md" "%PROJ_DIR%\CLAUDE.md" >nul 2>&1
    if errorlevel 1 (
        echo. >> "%PROJ_DIR%\CLAUDE.md"
        echo. >> "%PROJ_DIR%\CLAUDE.md"
        echo # === Infolight EEP / VS2005 build rules and team knowledge base - eepc === >> "%PROJ_DIR%\CLAUDE.md"
        echo @.claude/rules/eep-compiler.md >> "%PROJ_DIR%\CLAUDE.md"
        echo @EEP_KNOWLEDGE_BASE.md >> "%PROJ_DIR%\CLAUDE.md"
    )
)

echo.
echo ====================================================================
echo 安裝完成！現在您與團隊同仁打開 Claude Code 即可直接使用：
echo   * 輸入 /eep-ask ^<問題^>   - 自動查表秒速回答，並自動把新發現寫入知識庫
echo   * 輸入 /eep-fix ^<需求^>   - 自動修改並呼叫 eepc.exe 編譯驗證
echo   * 輸入 /eep-learn        - 將剛學會的潛規則永久存入團隊知識庫
echo ====================================================================
pause
