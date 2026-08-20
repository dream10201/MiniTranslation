; MiniTranslation 安装包脚本（Inno Setup 6）
; 编译：iscc /DMyAppVersion=x.y.z setup.iss

#ifndef MyAppVersion
#define MyAppVersion "0.0.0"
#endif

[Setup]
AppId={{8A785476-1AF1-47C8-95BB-7153DBAB8CB3}}
AppName=MiniTranslation
AppVersion={#MyAppVersion}
AppPublisher=dream10201
AppPublisherURL=https://github.com/dream10201/MiniTranslation
DefaultDirName={autopf}\MiniTranslation
DisableProgramGroupPage=yes
; 默认按当前用户安装（免提权）；启动时可选“为所有用户安装”自动申请提权
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
OutputDir=.
OutputBaseFilename=MiniTranslation-Setup
SetupIconFile=img.ico
UninstallDisplayIcon={app}\MiniTranslation.exe
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes

[Tasks]
Name: "startup"; Description: "开机自动启动"; Flags: unchecked

[Files]
Source: "publish\MiniTranslation.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\MiniTranslation"; Filename: "{app}\MiniTranslation.exe"

[Registry]
; 与应用内“开机自启动”开关使用同一个 Run 注册表键
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MiniTranslation"; ValueData: """{app}\MiniTranslation.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Dirs]
; 机器级安装：更新包下载目录，授普通用户写入，供计划任务读取执行
Name: "{commonappdata}\MiniTranslation\update"; Permissions: users-modify; Check: IsAdminInstallMode

[Run]
; 机器级安装：创建以最高权限运行的更新任务，之后普通用户触发即可静默更新，不再弹 UAC
Filename: "{sys}\schtasks.exe"; Parameters: "/create /tn ""MiniTranslation Update"" /tr ""{commonappdata}\MiniTranslation\update\MiniTranslation-Setup.exe /VERYSILENT /NORESTART /ALLUSERS"" /sc ONCE /st 00:00 /rl HIGHEST /f"; Flags: runhidden; Check: IsAdminInstallMode
; 不加 skipifsilent：静默自动更新完成后也会重新拉起应用
Filename: "{app}\MiniTranslation.exe"; Description: "启动 MiniTranslation"; Flags: nowait postinstall

[UninstallRun]
; 卸载时移除更新计划任务
Filename: "{sys}\schtasks.exe"; Parameters: "/delete /tn ""MiniTranslation Update"" /f"; Flags: runhidden; RunOnceId: "DelUpdateTask"; Check: IsAdminInstallMode
