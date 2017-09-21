bt.exe - A high speed backup tool
=================================
The backup tool bt utilizes a combination of 
- SHA1 based hash tree,
- recursive symbolic links and
- NTFS alternated data streams to 
to efficently create full backup of very large file systems at an extremely fast speed,
and with as little disk usage as possible.

Features
========
- extremely fast, e.g. full backup of 750GB data in 2 million files and 60'000 folders in less than 45 minutes.
- minimal number of write operations on the target drive, if the source tree did not change since the last backup,
  only a single directory link is created.
- data base free design
- performance is independet from the number of files
- memory utilization only scales with tree depth, not with the number of files or folders
- files that have the same content are copied only once
- very fast clean up

Usage
=====
Basic syntax is:
	
	bt.exe command --option1=value1 --option2=value2 --optionWithOutValue parameter1 parameter2 ... parameterN

Options and commands are not case sensitive.

- the User needs rights to create symlinks
	(Gruppenrichtlinie Computerkonfiguration/Richtlinien/Windows-Einstellungen/Sicherheitseinstellungen/Lokale Richtlinien/Erstellen symbolischer Verknüpfungen)
- long path names should be activated
	(Gruppenrichtlinie Computerkonfiguration/Richtlinien/Administrative Vorlagen/System/Dateisystem/Lange Win32-Pfade aktivieren)
- NTFS 8.3 names disabled on target drive
	elevated cmd:  "fsutil 8dot3name set X: 1"
	"fsutil 8dot3name query X:" should return 8.3 names disabled

Command: backup|copy|cp:
------------------------

Syntax:
	
	bt.exe copy SourceFolder1[=alias1] [SourceFolder2=Alias2 [SourceFolderN]] TargetFolder
	
	or
	
	bt.exe backup @FolderList1.txt SourceFolder @AnotherFolderList.txt TargetFolder
	
The file "FolderList1.txt" is utf8 encoded and each line corresponds to a source folder, optionally followed by
an alias, sample FolderList1.txt: 

	C:\Users\Mark\Documents\=Mark
	C:\Users\Emmi\Documents\=Emmi
	D:\Projects
	C:\Projects=ProjC

With this file the two following commands would be identical:

	bt cp @FolderList1.txt F:\Backup\
	bt backup C:\Users\Mark\Documents\=Mark C:\Users\Emmi\Documents\=Emmi D:\Projects C:\Projects=ProjC F:\Backup\

If the target folder is already existing the current time stamp in the form "yyyy-MM-dd_HH-mm" 
is automatically appended. For each source folder specified a symbolic link with folder name is
created.

Exclude:

With the parameter --ExcludeList:'textfile.txt'
The textfile may contain
    F:\Quelle\Altium\Altium                         -excludes that specific file or folder
    .bak                                            -excludes all files or folders ending with that extension
    .example.ini                                    -excludes all files or folders *.example.ini but not example.ini
    Thumbs.db                                       -excludes all files or folders with that name
    ;anything                                       -commented out - backup is done for that line

Examples:

	bt.exe backup C:\Users\Emmy C:\Users\Mark D:\Movies D:\Data F:\Backup
	bt.exe backup @Folders.txt F:\Backup
	bt.exe backup @BackupDirectories.txt --ExcludeList:BackupExclude.txt F:\Backup\
	
If there are two source folders with the same name, e.g.

	bt.exe C:\Users\Emmy\Documents\ C:\Users\Mark\Documents\ F:\Backup
	
the backup tool would try to create two symbolic links with the "Documents" in the backup folder. 
This will fail, after the first folder. The alias syntax allows to create separate names for each
folder, e.g.
	
	bt.exe C:\Users\Emmy\Documents\=EmmiesDocs C:\Users\Mark\Documents\=MarksDocs F:\Backup

This would result in an folder structure like:

	F:\Backup\
		YYYY-MM-DD_HH-MM
			EmmiesDocs
				EmmiSub1
				EmmiSub2
			MarksDocs
				FileA
				MarkSub1

Clean
-----
   
Compress
--------
   
Options
-------   
   
Internals
=========
Motivation
----------
The need for a backup does not need to be explained. Also a full backup for each date is certainly 
desireable, if the cost in space and of course time are low.

There have been other programs namingly Luphino RsyncBackup that utilized advanced file system features
(hard links) to achive this goal.
However there where some major draw backs:

1) Due to a not fully understood problem of the NTFS file system the backup becomes slower with every backup iteration, 
   as the number of hard links on the target drive increases. Since every backup cycle needs to create
   the same number of hard links and folders as the source folders. In my case every daily backup was adding about
   2 million hard links to the backup drive. The initial 120min of a full backup, yielded in more than 8h time for
   creating the hard links. This was the case after running about 100 full back ups. Using a profiler I found that
   the Win32 function "CreateHardlink" was often stalled for about 60 seconds. The frequency of the stalling increased
   as the target drive had more and more hard links.

2) Even if nothing in the source tree changed, the number of file operations for a backup is equal to the number
   of files plus folders in the source tree. Usually only small portions of the source tree are changed.

3) The tool only creates hard links for files that are having the exact same name and path. If a large file
   is moved to another folder, it will be copied to the backup drive again.

4) If multiple files with the same content are found several times, they are copied to the backup drive multiple
   times, even if their content is exactly the same.

5) The efficent operation of the tool relies on a memory based dictionary that has been created during the previous
   backup. Thus the memory utilization scales with the number of files to be backuped.


Release notes
=============
- 3.10.0.0
	* MoveDirectory improved

- 3.9.0.0
	* check that target path contains no links at start of backup command

- 3.8.0.0
	* support of large path names (Windows 10, Windows Server 2016)
	* command line parameter --ExcludeList:"filename"
	* .net TargetFrameworkVersion v4.6

- 3.7.0.0
	* BUGFIX: fixed ArgumentException when adding "duplicate" entries to the Directory dictionary (reason was an unsuitable StringComparer for e.g. Paß and Pas)

- 3.6.0.0
	* BUGFIX: catch UnathorizedAccess exception
	* BUGFIX: fixed FormatException in SetTitle for filenames of the format {}

- 3.5.0.0
	* basic source of reduce added (not completed yet)

- 3.4.0.0
	* improved statistics
	* moved some methods
	* option parser fix, when using default values
	
- 3.3.0.0
	* basic replace/compress function works (statistics and error handling need a lot of improvement)

- 3.2.0.0
	* refactoring sources (much improved)
	
- 3.1.0.0
	* even sub folders are processed as hashs

- 3.0.0.0
	* new command line parser
	* command is first argument always
	
- 2.5.0.0
	* alias syntax is added
	* special handling of single source folder
	
- 2.4.0.0
	* multiple source files
	* improved display
	* copy progress
	* list file syntax implemented
