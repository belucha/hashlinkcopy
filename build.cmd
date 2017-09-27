@echo off
rem set config=Debug
set config=Release
set msbuild="c:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe"
if not exist %msbuild% (
	echo trying 1. alternate msbuild path
	set msbuild="c:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe"
)
if not exist %msbuild% (
	echo trying 2. alternate msbuild path
	set msbuild="c:\Program Files (x86)\MSBuild\14.0\Bin\MSBuild.exe"
)
if not exist %msbuild% (
	echo failed to find ms build
	exit 1
)
echo MSBuild path is %msbuild%
C:\Tools\nuget.exe restore de.intronik.Backup.sln
%msbuild% de.intronik.Backup.sln /p:Configuration=%config%
copy bin\release\bt.exe .\
copy bin\release\bt.exe.config .\
rem pause
