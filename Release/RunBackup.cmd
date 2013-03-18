@rem This example will run a backup of all files listed in the file BackupDirectories.txt
@elevate cmd.exe /c "bt.exe backup @BackupDirectories.txt %~d0\%td% & pause"
