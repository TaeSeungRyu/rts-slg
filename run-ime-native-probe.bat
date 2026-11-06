@echo off
dotnet run --project "%~dp0tools\ime-native-probe\ImeNativeProbe.csproj" --configuration Release
exit /b %errorlevel%
