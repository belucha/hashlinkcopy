@echo off
setlocal ENABLEEXTENSIONS
set td=%DATE:~6,4%-%DATE:~3,2%-%DATE:~0,2%
echo Target date is '%td%'
set tf=%~d0\%td%\
echo Target folder is '%tf%'
pause
mkdir %tf% 
rem for %%x in (D:\Projekte D:\Texte\) do (
for /F "tokens=*" %%x in (BackupDirectories.txt) do (
	echo bt.exe %%x %tf%
	bt.exe %%x %tf%
)
pause