@echo off
setlocal EnableExtensions
cd /d "%~dp0"

REM Converts Netscape cookies file to one-line Base64 for MediaDownload__YouTubeCookiesBase64
REM Default input:  youtube-cookies.txt (this folder)
REM Default output: youtube-cookies.b64.txt (this folder)
REM Usage:
REM   encode-youtube-cookies.bat
REM   encode-youtube-cookies.bat path\to\cookies.txt
REM   encode-youtube-cookies.bat path\to\cookies.txt path\to\output.b64.txt
REM See youtube-cookies-setup.html or .md for exporting cookies from Chrome or Firefox.

set "INPUT=%~1"
if "%INPUT%"=="" set "INPUT=%~dp0youtube-cookies.txt"

set "OUTPUT=%~2"
if "%OUTPUT%"=="" set "OUTPUT=%~dp0youtube-cookies.b64.txt"

if not exist "%INPUT%" (
    echo.
    echo ERROR: Cookies file not found:
    echo   %INPUT%
    echo.
    echo Copy youtube-cookies.sample.txt to youtube-cookies.txt, export cookies
    echo from your browser, then run this script again.
    echo See youtube-cookies-setup.html for Chrome and Firefox steps.
    echo.
    exit /b 1
)

for %%I in ("%INPUT%") do set "COOKIE_FILE=%%~fI"
for %%I in ("%OUTPUT%") do set "OUT_FILE=%%~fI"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$bytes = [IO.File]::ReadAllBytes($env:COOKIE_FILE); " ^
  "$b64 = [Convert]::ToBase64String($bytes); " ^
  "[IO.File]::WriteAllText($env:OUT_FILE, $b64, [Text.UTF8Encoding]::new($false)); " ^
  "Write-Host ''; " ^
  "Write-Host 'Input:  ' $env:COOKIE_FILE; " ^
  "Write-Host 'Output: ' $env:OUT_FILE; " ^
  "Write-Host 'Size:   ' $bytes.Length 'bytes -^> ' $b64.Length 'base64 chars'; " ^
  "Write-Host ''; " ^
  "Set-Clipboard -Value $b64; " ^
  "Write-Host 'Copied to clipboard.'; " ^
  "Write-Host ''; " ^
  "Write-Host 'Server (pick one):'; " ^
  "Write-Host '  A) Copy youtube-cookies.txt to app tools/ (best; no size limit)'; " ^
  "Write-Host '  B) Copy youtube-cookies.b64.txt to app tools/ (no long env var needed)'; " ^
  "Write-Host '  C) Inline env (often fails if over ~1 KB on host panels):'; " ^
  "Write-Host '     MediaDownload__YouTubeCookiesBase64=<one line>'; " ^
  "Write-Host '     Or: MediaDownload__YouTubeCookiesBase64File=youtube-cookies.b64.txt';"

if errorlevel 1 (
    echo.
    echo ERROR: PowerShell failed to encode the file.
    exit /b 1
)

echo.
echo Done.
endlocal
exit /b 0
