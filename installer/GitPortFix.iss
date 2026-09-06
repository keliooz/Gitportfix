; Instalador do GitPortFix — Inno Setup 6
; Compile com: iscc installer\GitPortFix.iss
; Antes disso rode build.ps1, que publica o executável.

#define MeuNome     "GitPortFix"
#define MinhaVersao "1.0.0"
#define MeuAutor    "Time de Desenvolvimento"
#define MeuExe      "GitPortFix.exe"

[Setup]
AppId={{A7F3C2E1-9D4B-4A6E-8C13-5F2B7E9A0D48}
AppName={#MeuNome}
AppVersion={#MinhaVersao}
AppPublisher={#MeuAutor}
DefaultDirName={autopf}\{#MeuNome}
DefaultGroupName={#MeuNome}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=GitPortFix-Setup-{#MinhaVersao}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Instala só para o usuário atual: dispensa senha de administrador,
; o que costuma ser decisivo em máquina corporativa.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MeuExe}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"
Name: "contextmenu"; Description: "Adicionar ""Abrir com GitPortFix"" ao menu do botão direito em pastas"; GroupDescription: "Integração com o Windows:"

[Files]
Source: "..\bin\publish\{#MeuExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md";             DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{autoprograms}\{#MeuNome}"; Filename: "{app}\{#MeuExe}"
Name: "{autodesktop}\{#MeuNome}";  Filename: "{app}\{#MeuExe}"; Tasks: desktopicon

[Registry]
; Botão direito sobre a pasta do projeto.
Root: HKA; Subkey: "Software\Classes\Directory\shell\GitPortFix"; \
    ValueType: string; ValueName: ""; ValueData: "Abrir com GitPortFix"; \
    Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\GitPortFix"; \
    ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MeuExe}"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\shell\GitPortFix\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#MeuExe}"" ""%1"""; \
    Flags: uninsdeletekey; Tasks: contextmenu

; Botão direito no fundo da pasta já aberta. %V é a pasta atual.
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\GitPortFix"; \
    ValueType: string; ValueName: ""; ValueData: "Abrir com GitPortFix"; \
    Flags: uninsdeletekey; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\GitPortFix"; \
    ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MeuExe}"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\GitPortFix\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#MeuExe}"" ""%V"""; \
    Flags: uninsdeletekey; Tasks: contextmenu

[Run]
Filename: "{app}\{#MeuExe}"; Description: "Abrir o {#MeuNome} agora"; Flags: nowait postinstall skipifsilent

[Code]
// O app depende do git no PATH. Melhor avisar na instalação do que dar erro no primeiro uso.
function GitEstaInstalado(): Boolean;
var
  Codigo: Integer;
begin
  Result := Exec('cmd.exe', '/c git --version', '', SW_HIDE, ewWaitUntilTerminated, Codigo)
            and (Codigo = 0);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;

  if not GitEstaInstalado() then
  begin
    if MsgBox('O Git não foi encontrado no PATH desta máquina.' + #13#10#13#10 +
              'O GitPortFix precisa dele para funcionar. Você pode instalar depois, ' +
              'em https://git-scm.com/download/win' + #13#10#13#10 +
              'Continuar a instalação mesmo assim?',
              mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
end;
