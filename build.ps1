param(
    [string]$OutputDirectory=(Join-Path $PSScriptRoot 'dist'),
    [switch]$SkipTests
)
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $compiler)) { $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if(-not (Test-Path -LiteralPath $compiler)) { throw 'No se encuentra el compilador C# de .NET Framework.' }
$output=New-Item -ItemType Directory -Force -Path $OutputDirectory
$app=Join-Path $output.FullName 'MisCofres.exe'
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$options=@('/nologo','/optimize+','/codepage:65001',
    '/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Core.dll',
    ('/win32icon:'+(Join-Path $PSScriptRoot 'assets\cofre.ico')),
    ('/resource:'+(Join-Path $PSScriptRoot 'assets\cofre.png')+',cofre.png'))
& $compiler $options /target:winexe ('/out:'+$app) $sources
if($LASTEXITCODE -ne 0) { throw 'No se pudo compilar MisCofres.' }
if(-not $SkipTests) {
    $testExe=Join-Path $output.FullName 'RegressionTests.exe'
    $testSources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object { $_.FullName })
    & $compiler $options /target:exe /main:RegressionTests ('/out:'+$testExe) $sources $testSources
    if($LASTEXITCODE -ne 0) { throw 'No se pudieron compilar las pruebas.' }
    & $testExe (Join-Path $PSScriptRoot 'tests\fixtures')
    if($LASTEXITCODE -ne 0) { throw 'Fallo en las pruebas.' }
    $selfTest=Start-Process -FilePath $app -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
    if($selfTest.ExitCode -ne 0) { throw 'Fallo en las pruebas internas de guardado.' }
}
$guide=Join-Path $output.FullName 'LEEME.txt'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LEEME.txt') -Destination $guide -Force
$zip=Join-Path $output.FullName 'MisCofres.zip'
Compress-Archive -LiteralPath $app,$guide -DestinationPath $zip -Force
$hashes=@($app,$zip) | ForEach-Object { $hash=Get-FileHash -LiteralPath $_ -Algorithm SHA256; $hash.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_) }
[IO.File]::WriteAllLines((Join-Path $output.FullName 'SHA256SUMS.txt'),$hashes,[Text.UTF8Encoding]::new($false))
Write-Output ('Listo: '+$app)
