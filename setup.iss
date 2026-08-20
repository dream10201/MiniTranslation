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
; 按当前用户安装，无需管理员权限
PrivilegesRequired=lowest
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

[Run]
; 不加 skipifsilent：静默自动更新完成后也会重新拉起应用
Filename: "{app}\MiniTranslation.exe"; Description: "启动 MiniTranslation"; Flags: nowait postinstall
