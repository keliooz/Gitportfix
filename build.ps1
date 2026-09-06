<#
    Publica o GitPortFix e gera o instalador.

    Uso:
      .\build.ps1              # publica o exe em bin\publish
      .\build.ps1 -Instalador  # publica e gera o setup em dist\
#>

param(
    [switch]$Instalador,
    [string]$Versao = "1.0.0"
)

$ErrorActionPreference = "Stop"
$raiz = $PSScriptRoot

Write-Host "Publicando o GitPortFix..." -ForegroundColor Cyan

# self-contained: o usuário final não precisa ter o .NET instalado.
# O exe fica maior, mas some o suporte a "não abre na minha máquina".
dotnet publish "$raiz\src\GitPortFix\GitPortFix.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$Versao `
    -o "$raiz\bin\publish"

if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar." }

$exe = Join-Path $raiz "bin\publish\GitPortFix.exe"
$tamanho = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Executável pronto: $exe ($tamanho MB)" -ForegroundColor Green

if (-not $Instalador) { return }

# Procura o compilador do Inno Setup nos caminhos habituais.
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning "Inno Setup 6 não encontrado. Baixe em https://jrsoftware.org/isdl.php"
    return
}

Write-Host "Gerando o instalador..." -ForegroundColor Cyan
& $iscc "/DMinhaVersao=$Versao" "$raiz\installer\GitPortFix.iss"

if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar o instalador." }

Write-Host "Instalador em: $raiz\dist\GitPortFix-Setup-$Versao.exe" -ForegroundColor Green
