@echo off
setlocal
cd /d "%~dp0"

echo Building Quant Terminal...
dotnet build -c Release QuantHub.Desktop.csproj -v quiet
if errorlevel 1 (
    echo.
    echo Build failed - see errors above.
    pause
    exit /b 1
)

start "" "bin\Release\net8.0\QuantTerminal.exe"
