@echo off
setlocal enabledelayedexpansion

REM BATCH FILE TO BUILD MULTIPLE RELEASE RUNTIMES
REM Configure the values below, place in same folder as
REM .csproj file and double-click to build all runtimes
REM and place into "Publish" folder

set BASENAME=ListPorter
set PROJECT=%BASENAME%.csproj
set RUNTIMES=win-x64 linux-x64 linux-arm64 osx-arm64 osx-x64
set CONFIG=Release
set OUTPUT=Publish
set EXENAME=ListPorter

REM --- NO CODE CHANGES NEEDED BEYOND HERE ---

REM --- GET VERSION LINE ---
findstr /i "<Version>" "%PROJECT%" > _ver.tmp
set /p fullline=<_ver.tmp
del _ver.tmp

REM --- PARSE VERSION ---
set "str1=%fullline:*<Version>=%"
set "version_raw=%str1:</Version>=%"

REM --- SPLIT VERSION INTO PARTS ---
for /f "tokens=1,2,3,4 delims=." %%a in ("%version_raw%") do (
    set "major=%%a"
    set "minor=%%b"
    set "build=%%c"
    set "revision=%%d"
)

REM --- FORMAT VERSION ---
set "version=%major%.%minor%.%revision%"
set /a buildCheck=%build% + 0

if !buildCheck! gtr 0 (
    set "version=%version%-pre%build%"
)

REM --- VERIFY VERSION EXTRACTION --
if not defined version (
	echo.
    echo Error: Could not extract version from %PROJECT%.
	echo.
	pause
    exit /b 1
)

echo.
echo Going to publish %version%

REM --- PUBLISH ---
for %%r in (%RUNTIMES%) do (
	echo.
    echo Publishing for runtime: %%r
	echo.
	dotnet publish "%PROJECT%" -c %CONFIG% -r %%r --self-contained false ^
		/p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=false ^
		-o Publish/%%r
    if errorlevel 1 (
        echo Error during publishing for %%r
        exit /b 1
    )
    
	REM --- RENAME OUTPUT FILE WITH VERSION AND RUNTIME ---
    move "Publish\%%r\%BASENAME%.exe" "Publish\%EXENAME%-%version%-%%r.exe" 2>nul
    move "Publish\%%r\%BASENAME%" "Publish\%EXENAME%-%version%-%%r" 2>nul
)

echo.
echo All runtimes built and renamed.
echo.
pause
