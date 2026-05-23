@echo off
setlocal EnableExtensions
cd /d "%~dp0"

REM Zip src\ProPlusBot for deployment (skips bin, obj, tools)
if "%~1"=="" (
  set "SRC=%~dp0..\src\ProPlusBot"
) else (
  set "SRC=%~f1"
)

if "%~2"=="" (
  set "ZIP=%~dp0..\archive.zip"
) else (
  set "ZIP=%~f2"
)

if not exist "%SRC%" (
  echo Source folder not found: %SRC%
  pause
  exit /b 1
)

if exist "%ZIP%" del /f /q "%ZIP%"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$root = (Resolve-Path -LiteralPath '%SRC%').Path; $zipDir = Split-Path -Parent '%ZIP%';" ^
  "if (-not $zipDir) { $zipDir = (Get-Location).Path } elseif (-not [System.IO.Path]::IsPathRooted('%ZIP%')) { $zipDir = Join-Path (Get-Location).Path $zipDir };" ^
  "$zip = Join-Path $zipDir (Split-Path -Leaf '%ZIP%'); $skip = @('bin','obj','tools');" ^
  "Add-Type -AssemblyName System.IO.Compression.FileSystem;" ^
  "$z = [System.IO.Compression.ZipFile]::Open($zip, 'Create');" ^
  "Get-ChildItem -LiteralPath $root -Recurse -File -Force | ForEach-Object {" ^
  "  $rel = $_.FullName.Substring($root.Length).TrimStart('\').Split('\');" ^
  "  if ($rel | Where-Object { $skip -contains $_ }) { return };" ^
  "  $entry = $rel -join '/';" ^
  "  [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, $_.FullName, $entry)" ^
  "}; $z.Dispose(); Write-Host ('Created: ' + $zip)"

if errorlevel 1 (
  echo Failed.
  pause
  exit /b 1
)

echo Done: %ZIP%
pause
