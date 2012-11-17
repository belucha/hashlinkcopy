@echo off
setlocal ENABLEEXTENSIONS
set td=%DATE:~6,4%-%DATE:~3,2%-%DATE:~0,2%
echo Target date is '%td%'
set tf=%td%\*\
echo Target folder is '%tf%'
for %%x in (D:\Projekte D:\Texte\) do (
rem for /F "tokens=*" %%x in (BackupDirectories.txt) do (
	echo hashcopy.exe %%x %tf%
	hashcopy.exe %%x %tf%
)
pause