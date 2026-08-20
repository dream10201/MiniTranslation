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
; 默认提权安装到 Program Files；命令行 /CURRENTUSER 可切换为当前用户安装
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
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

[Run]
; 开机自启动：登录触发的计划任务，与应用内开关同名同定义。
; 不用 schtasks 创建，其默认设置在电池供电时不运行、且有 72 小时时限
Filename: "powershell.exe"; Parameters: "-NoProfile -Command ""Register-ScheduledTask -TaskName 'MiniTranslation' -Force -Action (New-ScheduledTaskAction -Execute '{app}\MiniTranslation.exe') -Trigger (New-ScheduledTaskTrigger -AtLogOn) -Settings (New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan))"""; Flags: runhidden; Tasks: startup
Filename: "{app}\MiniTranslation.exe"; Description: "启动 MiniTranslation"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 自动更新的 A/B 版本目录（%LocalAppData%\MiniTranslation\app\<版本号>）
Type: filesandordirs; Name: "{localappdata}\MiniTranslation\app"

[UninstallRun]
; 卸载时移除自启动计划任务
Filename: "{sys}\schtasks.exe"; Parameters: "/delete /tn ""MiniTranslation"" /f"; Flags: runhidden; RunOnceId: "DelAutostartTask"
