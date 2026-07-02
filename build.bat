@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
title Build SimulateurPliage - TolTem

cd /d "%~dp0"

echo ============================================================
echo   Build SimulateurPliage  -  TolTem
echo   Dossier : %cd%
echo ============================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERREUR] dotnet introuvable. Installe le SDK .NET 8 : https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

rem --- fermer l'appli si elle tourne (sinon l'exe ne peut pas etre ecrase) ---
taskkill /IM SimulateurPliage.exe /F >nul 2>nul

rem --- nettoyage COMPLET (bin/obj/exe_final) : evite les exe vides/caches pourris ---
echo Nettoyage bin / obj / exe_final...
if exist "bin"       rmdir /s /q "bin"
if exist "obj"       rmdir /s /q "obj"
if exist "exe_final" rmdir /s /q "exe_final"

echo.
echo Publication win-x64 (single-file, self-contained, DLL natives embarquees)...
dotnet publish SimulateurPliage.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=false -p:DebugType=none -p:DebugSymbols=false -o exe_final
if errorlevel 1 (
    echo.
    echo *** BUILD ECHOUE ***
    echo.
    pause
    exit /b 1
)

if not exist "exe_final\SimulateurPliage.exe" (
    echo.
    echo *** BUILD ECHOUE : SimulateurPliage.exe introuvable ***
    echo.
    pause
    exit /b 1
)

rem --- controle de taille : un vrai exe self-contained fait des dizaines de Mo ---
for %%A in ("exe_final\SimulateurPliage.exe") do set SZ=%%~zA
echo.
echo Taille de l'exe : !SZ! octets
if !SZ! LSS 1000000 (
    echo [ATTENTION] L'exe fait moins de 1 Mo : probable stub vide.
    echo   Essaie :  dotnet run --project SimulateurPliage.csproj   pour tester le code sans packaging.
)

echo.
echo ============================================================
echo   OK -^> exe_final\SimulateurPliage.exe
echo ============================================================
echo.
choice /C ON /N /M "Ouvrir le dossier exe_final ? (O/N) "
if errorlevel 2 goto :fin
start "" "exe_final"
:fin
echo.
pause
