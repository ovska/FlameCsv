@echo off
setlocal

echo Looking for Visual Studio installation...

set VSWHERE_X86="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set VSWHERE_X64="%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"

if exist %VSWHERE_X86% (
    for /f "usebackq tokens=*" %%i in (`%VSWHERE_X86% -latest -products * -requires Microsoft.Component.MSBuild -property installationPath`) do (
        set VSDIR=%%i
    )
) else if exist %VSWHERE_X64% (
    for /f "usebackq tokens=*" %%i in (`%VSWHERE_X64% -latest -products * -requires Microsoft.Component.MSBuild -property installationPath`) do (
        set VSDIR=%%i
    )
) else (
    echo ERROR: Could not find vswhere.exe in either Program Files directories.
    exit /b 1
)

if not defined VSDIR (
    echo ERROR: Could not find Visual Studio installation.
    exit /b 1
)

set TEXTTRANSFORM=%VSDIR%\Common7\IDE\TextTransform.exe

if not exist "%TEXTTRANSFORM%" (
    echo ERROR: TextTransform.exe not found at %TEXTTRANSFORM%
    echo Make sure Visual Studio is installed with T4 templating support.
    exit /b 1
)

echo Found TextTransform at: %TEXTTRANSFORM%
echo.

if "%~1" == "" (
    echo No specific template specified. Processing all T4 Templates...
    
    for /r %%f in (*.tt) do (
        echo Transforming: %%f
        "%TEXTTRANSFORM%" -out "%%~dpnf.cs" "%%f"
    )
    
    echo.
    echo All T4 templates processed.
) else (
    if "%~x1" == ".tt" (
        echo Transforming single template: %~1
        "%TEXTTRANSFORM%" -out "%~dpn1.cs" "%~1"
        echo.
        echo Template processing complete.
    ) else (
        echo ERROR: The specified file is not a T4 template: %~1
        echo File extension must be .tt
        exit /b 1
    )
)

exit /b 0
