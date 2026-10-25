param(
    [string]$AndroidSdkDirectory = 'C:\Program Files (x86)\Android\android-sdk',
    [string]$JavaSdkDirectory = 'C:\Program Files\Android\openjdk\jdk-21.0.8',
    [string]$OutputDirectory,
    [int]$VersionCode = 1,
    [string]$DisplayVersion = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'artifacts\apk' }
$buildRoot = $projectRoot
if ($projectRoot -match '[^\x00-\x7F]') {
    # AAPT2 on Windows requires an ASCII build path.
    $hashProvider = [Security.Cryptography.SHA256]::Create()
    $rootHash = [BitConverter]::ToString($hashProvider.ComputeHash([Text.Encoding]::UTF8.GetBytes($projectRoot))).Replace('-', '').Substring(0, 12)
    $hashProvider.Dispose()
    $buildBase = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'FinanceiroApp'))
    $buildRoot = [IO.Path]::GetFullPath((Join-Path $buildBase "AndroidBuild-$rootHash"))
    if ([IO.Path]::GetDirectoryName($buildRoot) -ne $buildBase -or [IO.Path]::GetFileName($buildRoot) -ne "AndroidBuild-$rootHash") {
        throw 'O diretorio auxiliar nao esta dentro da pasta de compilacao esperada.'
    }
    New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
    & robocopy $projectRoot $buildRoot /MIR /XD .git bin obj artifacts .vs /XF *.db *.db-* *.keystore *.jks signing-password.xml /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -gt 7) { throw 'Falha ao preparar a copia de compilacao Android.' }
}
$keytool = Join-Path $JavaSdkDirectory 'bin\keytool.exe'
if (-not (Test-Path -LiteralPath $keytool)) { throw 'Java SDK nao encontrado. Informe -JavaSdkDirectory.' }
if (-not (Test-Path -LiteralPath $AndroidSdkDirectory)) { throw 'Android SDK nao encontrado. Informe -AndroidSdkDirectory.' }

$signingDirectory = Join-Path $env:LOCALAPPDATA 'FinanceiroApp\Signing'
New-Item -ItemType Directory -Path $signingDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$keyPath = Join-Path $signingDirectory 'financeiro-mobile.keystore'
$secretPath = Join-Path $signingDirectory 'signing-password.xml'
if ((Test-Path -LiteralPath $keyPath) -and -not (Test-Path -LiteralPath $secretPath)) {
    throw 'A chave de assinatura existe, mas a senha protegida nao foi encontrada. Recupere signing-password.xml do mesmo usuario Windows.'
}
if (-not (Test-Path -LiteralPath $secretPath)) {
    $bytes = New-Object byte[] 32
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $generator.GetBytes($bytes)
    $generator.Dispose()
    ConvertTo-SecureString ([Convert]::ToBase64String($bytes)) -AsPlainText -Force | Export-Clixml -LiteralPath $secretPath
}
$secret = Import-Clixml -LiteralPath $secretPath
$credential = New-Object System.Management.Automation.PSCredential('financeiro', $secret)

try {
    $env:FINANCEIRO_SIGNING_PASSWORD = $credential.GetNetworkCredential().Password
    if (-not (Test-Path -LiteralPath $keyPath)) {
        & $keytool -genkeypair -keystore $keyPath -alias financeiro -keyalg RSA -keysize 2048 -validity 10000 -storetype PKCS12 -dname 'CN=Financeiro,OU=Personal,O=Financeiro,C=BR' -storepass:env FINANCEIRO_SIGNING_PASSWORD -keypass:env FINANCEIRO_SIGNING_PASSWORD
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar a chave de assinatura.' }
    }
    $arguments = @(
        'build', (Join-Path $buildRoot 'Financeiro.Mobile\Financeiro.Mobile.csproj'),
        '-c', 'Release', '-f', 'net10.0-android',
        "-p:AndroidSdkDirectory=$AndroidSdkDirectory", "-p:JavaSdkDirectory=$JavaSdkDirectory",
        '-p:AndroidBuildApplicationPackage=true', '-p:AndroidKeyStore=false',
        "-p:ApplicationVersion=$VersionCode", "-p:ApplicationDisplayVersion=$DisplayVersion"
    )
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilacao Android.' }
    $package = Join-Path $buildRoot 'Financeiro.Mobile\bin\Release\net10.0-android\com.pedrohda.financeiro.apk'
    if (-not (Test-Path -LiteralPath $package)) { throw 'O APK compilado nao foi encontrado.' }
    $buildTools = Get-ChildItem -LiteralPath (Join-Path $AndroidSdkDirectory 'build-tools') -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
    if (-not $buildTools) { throw 'Android build-tools nao encontrado.' }
    $aligned = Join-Path $OutputDirectory 'Financeiro-aligned.tmp.apk'
    $destination = Join-Path $OutputDirectory 'Financeiro-Android.apk'
    & (Join-Path $buildTools.FullName 'zipalign.exe') -f -p 4 $package $aligned
    if ($LASTEXITCODE -ne 0) { throw 'Falha no alinhamento do APK.' }
    $previousJavaHome = $env:JAVA_HOME
    $env:JAVA_HOME = $JavaSdkDirectory
    try {
        & (Join-Path $buildTools.FullName 'apksigner.bat') sign --ks $keyPath --ks-key-alias financeiro --ks-pass env:FINANCEIRO_SIGNING_PASSWORD --key-pass env:FINANCEIRO_SIGNING_PASSWORD --out $destination $aligned
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao assinar o APK.' }
        & (Join-Path $buildTools.FullName 'apksigner.bat') verify --verbose $destination
        if ($LASTEXITCODE -ne 0) { throw 'A verificacao da assinatura do APK falhou.' }
    } finally {
        $env:JAVA_HOME = $previousJavaHome
        if (Test-Path -LiteralPath $aligned) { Remove-Item -LiteralPath $aligned -Force }
    }
    $checksum = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'Financeiro-Android.apk.sha256'), "$checksum  Financeiro-Android.apk`r`n")
    Write-Output "APK: $destination"
    Write-Output "SHA256: $checksum"
    Write-Output "Chave de atualizacao preservada em: $signingDirectory"
}
finally {
    Remove-Item Env:FINANCEIRO_SIGNING_PASSWORD -ErrorAction SilentlyContinue
}
