@echo off
rem ===========================================================================
rem  HelperManager JSON Extractor - batch launcher
rem
rem  Does the same thing as the [Extract now] button on the Settings screen.
rem  Register it in Task Scheduler for unattended nightly refreshes.
rem
rem  Usage:
rem    extract.bat                         - extract everything
rem    extract.bat --only TableInfo.json   - extract one file only
rem    extract.bat --list                  - list targets only (no DB access)
rem
rem  This file is intentionally ASCII only: cmd.exe reads .bat files using the
rem  system code page (949 on Korean Windows), not UTF-8, so any non-ASCII text
rem  here corrupts the commands themselves. The extractor prints its own
rem  progress in Korean, which works fine because it sets the console to UTF-8.
rem ===========================================================================

setlocal

rem Prefer the exe next to this script, then an Extractor subfolder.
set "EXE=%~dp0HelperManager.Extractor.exe"
if not exist "%EXE%" set "EXE=%~dp0Extractor\HelperManager.Extractor.exe"

if not exist "%EXE%" (
    echo [ERROR] HelperManager.Extractor.exe not found.
    echo         Looked in: %~dp0
    echo                and: %~dp0Extractor\
    exit /b 2
)

"%EXE%" %*
set "RESULT=%ERRORLEVEL%"

echo.
if "%RESULT%"=="0" (
    echo Done.
) else (
    echo Failed. exit code: %RESULT%
)

exit /b %RESULT%
