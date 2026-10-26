param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '1.1.0',
    [string]$OutputDirectory,
    [string]$InnoSetupCompiler
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts\windows' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$stageBase = [IO.Path]::GetFullPath((Join-Path $root 'artifacts\staging'))
$stage = [IO.Path]::GetFullPath((Join-Path $stageBase ("desktop-" + [Guid]::NewGuid().ToString('N'))))
if ([IO.Path]::GetDirectoryName($stage) -ne $stageBase) { throw 'Diretorio de publicacao invalido.' }
New-Item -ItemType Directory -Path $stage, $OutputDirectory -Force | Out-Null
try {
    & dotnet publish (Join-Path $root 'FinanceiroApp\FinanceiroApp.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None "-p:Version=$Version" -o $stage
    if ($LASTEXITCODE -ne 0) { throw 'A publicacao Windows falhou.' }
    $zip = Join-Path $OutputDirectory "Financeiro-Windows-x64-$Version.zip"
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
    if (-not $InnoSetupCompiler) {
        $installed = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($installed) { $InnoSetupCompiler = $installed.Source }
        elseif (Test-Path -LiteralPath "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") { $InnoSetupCompiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
    }
    if ($InnoSetupCompiler) {
        & $InnoSetupCompiler "/DAppVersion=$Version" "/DSourceDirectory=$stage" "/DOutputDirectory=$OutputDirectory" (Join-Path $PSScriptRoot 'WindowsInstaller.iss')
        if ($LASTEXITCODE -ne 0) { throw 'O instalador Windows falhou.' }
    }
    Get-ChildItem -LiteralPath $OutputDirectory -File | Where-Object { $_.Extension -in '.zip', '.exe' } | ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText($_.FullName + '.sha256', "$hash  $($_.Name)`r`n")
        Write-Output $_.FullName
    }
    if (-not $InnoSetupCompiler) { Write-Output 'Pacote portatil criado. Instale Inno Setup 6 para gerar tambem o instalador.' }
}
finally {
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($stage)) -eq $stageBase -and (Test-Path -LiteralPath $stage)) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
