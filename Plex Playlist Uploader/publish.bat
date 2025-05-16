@echo off
setlocal

REM --- CONFIG ---
set BASENAME=Plex Playlist Uploader
set PROJECT=%BASENAME%.csproj
set RUNTIMES=win-x64 linux-x64 linux-arm64 osx-arm64
set CONFIG=Release
set OUTPUT=Publish
set EXENAME=PlexPU

REM --- GET VERSION ---
findstr /i "<Version>" "%PROJECT%" > _ver.tmp
set /p fullline=<_ver.tmp
set "str1=%fullline:*<Version>=%"
set "version=%str1:</Version>=%"
del _ver.tmp

:: Verify version extraction
if not defined version (
	echo.
    echo Error: Could not extract version from %PROJECT%.
	echo.
	pause
    exit /b 1
)

REM --- PUBLISH ---
for %%r in (%RUNTIMES%) do (
	echo.
    echo Publishing for runtime: %%r
	echo.
	dotnet publish "%PROJECT%" -c %CONFIG% -r %%r --self-contained false ^
		/p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=false ^
		-o publish/%%r
    if errorlevel 1 (
        echo Error during publishing for %%r
        exit /b 1
    )
    
    REM Rename the output file with version and runtime
    move "publish\%%r\%BASENAME%.exe" "publish\%EXENAME%-%version%-%%r.exe" 2>nul
    move "publish\%%r\%BASENAME%" "publish\%EXENAME%-%version%-%%r" 2>nul
)

echo.
echo All runtimes built and renamed.
echo.
pause
