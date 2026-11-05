Unicode true
!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"
!include "Sections.nsh"
Name "MoVid"
OutFile "..\dist\MoVid-Setup-0.2.1.exe"
InstallDir "$LOCALAPPDATA\Programs\MoVid"
InstallDirRegKey HKCU "Software\MoVid" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
Icon "icon.ico"
UninstallIcon "icon.ico"
VIProductVersion "0.2.1.0"
VIAddVersionKey /LANG=1033 "ProductName" "MoVid"
VIAddVersionKey /LANG=1033 "FileDescription" "MoVid personal release reminders setup"
VIAddVersionKey /LANG=1033 "LegalCopyright" "MoVid contributors"
VIAddVersionKey /LANG=1033 "FileVersion" "0.2.1"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\MoVid.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Open MoVid"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
Var TestMode
Function .onInit
  ${GetParameters} $R0
  ${GetOptions} $R0 "/TESTMODE" $TestMode
  IfErrors +2
  StrCpy $TestMode "1"
  ReadRegDWORD $R1 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $R1 < 528040
    MessageBox MB_OK|MB_ICONSTOP "MoVid needs .NET Framework 4.8, included with current Windows 10/11 installations. Update Windows, then run setup again."
    Abort
  ${EndIf}
FunctionEnd
Section "MoVid application (required)" SEC_APP
  SectionIn RO
  SetShellVarContext current
  IfFileExists "$INSTDIR\MoVid.exe" 0 +2
    ExecWait '"$INSTDIR\MoVid.exe" --quit'
  Sleep 500
  SetOutPath "$INSTDIR"
  File "..\bin\MoVid.exe"
  File /oname=LICENSE.txt "..\LICENSE"
  File /oname=README.txt "..\README.md"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  ${If} $TestMode != "1"
    WriteRegStr HKCU "Software\MoVid" "InstallDir" "$INSTDIR"
    CreateDirectory "$SMPROGRAMS\MoVid"
    CreateShortcut "$SMPROGRAMS\MoVid\MoVid.lnk" "$INSTDIR\MoVid.exe"
    CreateShortcut "$SMPROGRAMS\MoVid\Uninstall MoVid.lnk" "$INSTDIR\Uninstall.exe"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MoVid" "DisplayName" "MoVid"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MoVid" "DisplayVersion" "0.2.1"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MoVid" "Publisher" "MoVid contributors"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MoVid" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MoVid" "DisplayIcon" "$INSTDIR\MoVid.exe"
    WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MoVid" "NoModify" 1
    WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MoVid" "NoRepair" 1
  ${Else}
    FileOpen $R1 "$INSTDIR\test-install.txt" w
    FileWrite $R1 "isolated-installer-test"
    FileClose $R1
  ${EndIf}
SectionEnd
Section "Start MoVid when I sign in to Windows" SEC_STARTUP
  ${If} $TestMode != "1"
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MoVid" '$\"$INSTDIR\MoVid.exe$\" --background'
  ${EndIf}
SectionEnd
Section /o "Desktop shortcut" SEC_DESKTOP
  ${If} $TestMode != "1"
    CreateShortcut "$DESKTOP\MoVid.lnk" "$INSTDIR\MoVid.exe"
  ${EndIf}
SectionEnd
Section -StartupPreference
  ${If} $TestMode != "1"
    ${IfNot} ${SectionIsSelected} ${SEC_STARTUP}
      DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MoVid"
    ${EndIf}
  ${EndIf}
SectionEnd
Section "Uninstall"
  SetShellVarContext current
  ExecWait '"$INSTDIR\MoVid.exe" --quit'
  Sleep 1000
  IfFileExists "$INSTDIR\test-install.txt" isolated normal
  normal:
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MoVid"
    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MoVid"
    DeleteRegKey HKCU "Software\MoVid"
    Delete "$SMPROGRAMS\MoVid\MoVid.lnk"
    Delete "$SMPROGRAMS\MoVid\Uninstall MoVid.lnk"
    RMDir "$SMPROGRAMS\MoVid"
    Delete "$DESKTOP\MoVid.lnk"
  isolated:
    Delete "$INSTDIR\test-install.txt"
    Delete "$INSTDIR\MoVid.exe"
    Delete "$INSTDIR\LICENSE.txt"
    Delete "$INSTDIR\README.txt"
    Delete "$INSTDIR\Uninstall.exe"
    RMDir "$INSTDIR"
    ; Preserve personal follows/backups. No background process or startup registration remains.
SectionEnd
