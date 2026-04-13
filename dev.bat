@echo off
echo [Dev] 启动开发模式（dotnet run）...
cd /d "%~dp0"
dotnet run --project ExhibitionClient.csproj
pause
