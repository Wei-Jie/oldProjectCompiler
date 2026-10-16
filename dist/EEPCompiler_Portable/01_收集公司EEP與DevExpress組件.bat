@echo off
chcp 65001 >nul
echo ====================================================================
echo   訊光 EEP 與 DevExpress / 第三方組件 (.dll) 自動收集器
echo   自動將公司電腦上的 EEP DLL 與 DevExpress / FarPoint DLL 收集至本套件 lib\ 目錄
echo ====================================================================
set /p EEP_DIR="請輸入或拖曳公司電腦的 EEP 根目錄 (直接按 Enter 則自動掃描常見路徑與 Windows GAC): "
if not exist "%~dp0lib" mkdir "%~dp0lib"

if not "%EEP_DIR%"=="" (
    echo 正在從指定的 EEP 目錄收集組件...
    for %%F in (Srvtools.dll InfoRemoteModule.dll EFClientTools.dll EFServerTools.dll EFBase.dll FLCore.dll FLTools.dll FLRuntime.dll clitools.dll DevExpress*.dll FarPoint*.dll Infragistics*.dll) do (
        for /r "%EEP_DIR%" %%P in (%%F) do (
            if exist "%%P" (
                echo [收集成功] %%~nxP
                copy /y "%%P" "%~dp0lib\" >nul
            )
        )
    )
)

echo 正在從本機 Windows GAC (C:\Windows\assembly) 搜尋 DevExpress / FarPoint / 訊光組件...
for %%G in ("C:\Windows\assembly\GAC_MSIL" "C:\Windows\assembly\GAC_32" "C:\Windows\Microsoft.NET\assembly\GAC_MSIL") do (
    if exist %%G (
        for /r %%G %%P in (DevExpress*.dll FarPoint*.dll Srvtools.dll InfoRemoteModule.dll) do (
            if exist "%%P" (
                echo [GAC 收集成功] %%~nxP
                copy /y "%%P" "%~dp0lib\" >nul
            )
        )
    )
)

echo.
echo 收集完成！所有組件已存入 %~dp0lib\ 資料夾！
pause
