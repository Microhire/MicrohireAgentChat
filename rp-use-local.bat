@echo off
REM Switch RentalPoint back to LOCAL VM DB (.\SQLEXPRESS on this machine)
REM Place this .bat alongside RPIni.ini in C:\RP-AI-TEST\RentalPointV11\sys

setlocal
set "TARGET=C:\RP-AI-TEST\RentalPointV11\sys\RPIni.ini"

if exist "%TARGET%" if not exist "%TARGET%.bak" copy /Y "%TARGET%" "%TARGET%.bak" >nul

> "%TARGET%" echo [DIRECTORIES]
>>"%TARGET%" echo HPSYS_DIR=C:\RP-AI-TEST\RentalPointV11\sys
>>"%TARGET%" echo CONFIGFILEPATH=C:\RP-AI-TEST\RentalPointV11\sys
>>"%TARGET%" echo HPDATA_DIR=C:\RP-AI-TEST\AI-TEST-Data
>>"%TARGET%" echo HPDOCS_DIR=C:\RP-AI-TEST\AI-TEST-Docs
>>"%TARGET%" echo ARCHIVEDOCSPATH=C:\RP-AI-TEST\AI-TEST-Docs-Archive
>>"%TARGET%" echo HPROOT_DIR=C:\RP-AI-TEST\RentalPointV11
>>"%TARGET%" echo HPREPORTS_DIR=C:\RP-AI-TEST\AI-TEST-Reports
>>"%TARGET%" echo TEMPLATES_DIR=C:\RP-AI-TEST\AI-TEST-Docs\Templates
>>"%TARGET%" echo FR_TEMPLATES_DIR=C:\RP-AI-TEST\AI-TEST-Docs\FastReportTemplates
>>"%TARGET%" echo [DATABASE]
>>"%TARGET%" echo DATABASENAME=AITESTDB
>>"%TARGET%" echo SERVERNAME=.\SQLEXPRESS
>>"%TARGET%" echo SQLPROVIDER=
>>"%TARGET%" echo [LOGIN]
>>"%TARGET%" echo USESQLAUTH=0
>>"%TARGET%" echo LOGINNAME=
>>"%TARGET%" echo PASSWORD=
>>"%TARGET%" echo PasswordIsEncrypted=0

echo.
echo [OK] RentalPoint -^> LOCAL VM DB (.\SQLEXPRESS)
echo      %TARGET%
echo.
findstr /B "SERVERNAME DATABASENAME LOGINNAME" "%TARGET%"
endlocal
pause
