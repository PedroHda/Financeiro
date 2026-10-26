param(
    [Parameter(Mandatory = $true)][string]$Apk,
    [string]$Adb = 'adb',
    [string]$OutputDirectory = 'artifacts/android-smoke'
)
$ErrorActionPreference = 'Stop'
$package = 'com.pedrohda.financeiro'
if ((& $Adb shell getprop ro.kernel.qemu).Trim() -ne '1') { throw 'Este teste limpa dados e so pode executar em um emulador.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $Adb install -r $Apk
if ($LASTEXITCODE -ne 0) { throw 'Falha ao instalar o APK de teste.' }
& $Adb shell pm clear $package | Out-Null
& $Adb logcat -c
& $Adb shell am start -n "$package/com.pedrohda.financeiro.MainActivity" | Out-Null

function Read-Hierarchy {
    & $Adb shell uiautomator dump /sdcard/financeiro-window.xml 2>$null | Out-Null
    $xml = & $Adb shell cat /sdcard/financeiro-window.xml
    return [xml]($xml -join "`n")
}
function Find-Node([string]$Text) {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $tree = Read-Hierarchy
        $node = $tree.SelectNodes('//node') | Where-Object { $_.GetAttribute('text') -eq $Text -or $_.GetAttribute('content-desc') -eq $Text } | Select-Object -First 1
        if ($node) { return $node }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Elemento nao encontrado: $Text"
}
function Tap([string]$Text) {
    $node = Find-Node $Text
    $bounds = [regex]::Match($node.GetAttribute('bounds'), '^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$')
    if (-not $bounds.Success) { throw "Limites invalidos: $Text" }
    $x = [int]( ([int]$bounds.Groups[1].Value + [int]$bounds.Groups[3].Value) / 2 )
    $y = [int]( ([int]$bounds.Groups[2].Value + [int]$bounds.Groups[4].Value) / 2 )
    & $Adb shell input tap $x $y
}
function Snapshot([string]$Name) {
    $tree = Read-Hierarchy
    $tree.Save((Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) "$Name.xml"))
    & $Adb shell screencap -p /sdcard/financeiro-screen.png
    & $Adb pull /sdcard/financeiro-screen.png (Join-Path $OutputDirectory "$Name.png") | Out-Null
}
Find-Node 'Financeiro' | Out-Null
Snapshot '01-home'
Tap 'Movimentos'
Find-Node 'Movimentos' | Out-Null
Snapshot '02-movimentos'
Tap 'Mais'
Tap 'Operações'
Find-Node 'Conciliação' | Out-Null
Snapshot '03-operacoes'
Tap 'Transferências'
Tap 'Adicionar'
Find-Node 'Nova transferência' | Out-Null
Find-Node 'Conta de origem' | Out-Null
Snapshot '04-transferencia'
& $Adb shell input keyevent 4
Tap 'Mais'
Tap 'Sincronização'
Find-Node 'Entrar ou criar conta' | Out-Null
Snapshot '05-sincronizacao'
$errors = & $Adb logcat -d -s AndroidRuntime:E
if (($errors -join "`n") -match 'FATAL EXCEPTION') { throw 'O aplicativo apresentou uma excecao fatal. Consulte os logs do emulador.' }
Write-Output 'Smoke Android aprovado: inicializacao, navegacao e formulario de transferencia.'
