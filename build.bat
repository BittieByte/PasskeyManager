@echo off
REM Builds a single self-contained PasskeyManager.exe into the "dist" folder.
REM Requires the .NET SDK: https://dotnet.microsoft.com/download
dotnet publish src\PasskeyManager\PasskeyManager.csproj -c Release -o dist
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)
echo.
echo Done. Run dist\PasskeyManager.exe
pause
