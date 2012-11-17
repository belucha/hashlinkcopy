@echo off
setlocal ENABLEEXTENSIONS
set td=%DATE:~6,4%-%DATE:~3,2%-%DATE:~0,2%
echo Target date is '%td%'
set tf=F:\%td%\
echo Target folder is '%tf%'
rem for %%x in (Projekte Betriebsorganisation Indel Buchhaltung) do (
rem for /F "tokens=*" %%x in (BackDirectories.txt) do (
for /F "tokens=*" %%x in (BackupDirectories.txt) do (
	echo %tf%%%x = D:\%%x
	hashcopy.exe D:\%%x\ %tf%%%x\
)
