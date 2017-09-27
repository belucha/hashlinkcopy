@rem This example will run a backup of all files listed in the file BackupDirectories.txt

set td=F:
set log=%td%\Backup\%DATE:~6,4%-%DATE:~3,2%-%DATE:~0,2%.log
echo START %DATE% %TIME%
echo START %DATE% %TIME% > %log%

%~dp0/bt.exe backup @%~dp0/BackupDirectories.txt --ExcludeList:%~dp0/BackupExclude.txt %td%\Backup\Privat >> %log%

echo DONE %DATE% %TIME%
echo DONE %DATE% %TIME% >> %log%
