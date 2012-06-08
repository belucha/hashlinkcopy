@echo off
@echo off
setlocal ENABLEEXTENSIONS
set hd=%~d0\Hash
set vb=Message
echo HashDir=%hd%
set td=%DATE:~6,4%-%DATE:~3,2%-%DATE:~0,2%
echo Target dir is '%td%'
for %%x in (Projekte Betriebsorganisation Indel Buchhaltung) do (
	echo ***************** %%x **********************
	de.intronik.hashcopy.exe \\Server\Intronik\%%x\ \Backup\%%x\ * \Hash\ 
	echo ***************** done **********************
)
