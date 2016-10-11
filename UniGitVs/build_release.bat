@echo Off
SETLOCAL ENABLEDELAYEDEXPANSION

for /f "tokens=2*" %%A in ('reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\devenv.exe"') do (
	if not [%%B] == [] (
		set devenvPath=%%B
		set devenvPath=!devenvPath:devenv.exe=devenv.com!
		"!devenvPath!" "UniGitVs.sln" /build Debug /project "UniGitVs.csproj" /projectconfig Release /out log.txt
	)
)

pause