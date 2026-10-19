@echo off
setlocal
echo ====================================================================
echo   訊光 EEP 與 DevExpress 組件 (.dll) 收集器
echo   將本機的 EEP 與 DevExpress DLL 收集到本工具包的 lib\ 目錄
echo ====================================================================
if not exist "%~dp0lib" mkdir "%~dp0lib"

echo.
set "EEP_DIR="
set /p EEP_DIR="1. 請輸入或拖曳 EEP 根目錄 (例如 D:\EEP2006；直接按 Enter 略過): "
if defined EEP_DIR set "EEP_DIR=%EEP_DIR:"=%"

set "DX_DIR="
set /p DX_DIR="2. 請輸入或拖曳 DevExpress DLL 資料夾 (例如 D:\HPS\devexpressdll\Sources\DevExpress.DLL；直接按 Enter 略過): "
if defined DX_DIR set "DX_DIR=%DX_DIR:"=%"

echo.
if not defined EEP_DIR goto :skip_eep
if exist "%EEP_DIR%\" goto :eep_ok
echo [錯誤] 找不到 EEP 根目錄: %EEP_DIR%
goto :skip_eep
:eep_ok
:: 只從 EEPNetServer 收集 (找不到才用 EEPNetClient)，不遞迴掃描整個根目錄：
:: 根目錄下常有 .bak、舊版備份資料夾，遞迴掃描會收集到版本不明的同名 DLL
echo 從 EEP 目錄收集 EEP 組件...
for %%F in (Srvtools.dll InfoRemoteModule.dll EFClientTools.dll EFServerTools.dll EFBase.dll FLCore.dll FLTools.dll FLRuntime.dll clitools.dll) do (
    if exist "%EEP_DIR%\EEPNetServer\%%F" (
        copy /y "%EEP_DIR%\EEPNetServer\%%F" "%~dp0lib\" >nul
        echo   [收集] %%F  ^(EEPNetServer^)
    ) else if exist "%EEP_DIR%\EEPNetClient\%%F" (
        copy /y "%EEP_DIR%\EEPNetClient\%%F" "%~dp0lib\" >nul
        echo   [收集] %%F  ^(EEPNetClient^)
    )
)
:: Client 端專案常參照 Interop.Excel.dll
if exist "%EEP_DIR%\EEPNetClient\Interop.Excel.dll" (
    copy /y "%EEP_DIR%\EEPNetClient\Interop.Excel.dll" "%~dp0lib\" >nul
    echo   [收集] Interop.Excel.dll  ^(EEPNetClient^)
)
:skip_eep

if not defined DX_DIR goto :skip_dx
if exist "%DX_DIR%\" goto :dx_ok
echo [錯誤] 找不到 DevExpress 資料夾: %DX_DIR%
goto :skip_dx
:dx_ok
echo 從 DevExpress 資料夾收集 DevExpress*.dll...
for %%P in ("%DX_DIR%\DevExpress*.dll") do (
    copy /y "%%~fP" "%~dp0lib\" >nul
    echo   [收集] %%~nxP
)
:skip_dx

:: Windows GAC 只補收前面沒收集到的 DevExpress / FarPoint 組件，不覆蓋已收集的檔案
echo 從 Windows GAC 補收 DevExpress / FarPoint 組件 (若有安裝)...
for %%G in ("C:\Windows\assembly\GAC_MSIL" "C:\Windows\assembly\GAC_32" "C:\Windows\Microsoft.NET\assembly\GAC_MSIL") do (
    if exist "%%~G\" (
        for /r "%%~G" %%P in (DevExpress*.dll FarPoint*.dll) do (
            if not exist "%~dp0lib\%%~nxP" (
                copy /y "%%P" "%~dp0lib\" >nul
                echo   [GAC 收集] %%~nxP
            )
        )
    )
)

echo.
echo ====================================================================
echo 收集完成，lib\ 目前的組件與版本:
powershell -NoProfile -Command "Get-ChildItem -LiteralPath '%~dp0lib' -Filter *.dll | ForEach-Object { try { $v=[Reflection.AssemblyName]::GetAssemblyName($_.FullName).Version } catch { $v='?' }; '  {0,-45} {1,-12} {2,10:N0} bytes' -f $_.Name, $v, $_.Length }"
echo ====================================================================
echo 請確認 Srvtools.dll / InfoRemoteModule.dll 為 2.2.0.0 且大小不是幾 KB
echo (舊版工具包內附的 mock 只有 3~7 KB，不能用來編譯要部署的 DLL)。
pause
