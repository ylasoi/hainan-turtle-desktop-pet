param([switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'Requires Windows with the .NET Framework C# compiler.' }
$spritePath = Join-Path $projectRoot 'assets\sprites.png'
if (-not (Test-Path -LiteralPath $spritePath)) { throw 'assets/sprites.png is missing. Restore the committed animation atlas first.' }
$binaryDirectory = Join-Path $projectRoot 'dist'
$null = New-Item -ItemType Directory -Path $binaryDirectory -Force
$exePath = Join-Path $binaryDirectory 'HainanTurtlePet.exe'
$compilerArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',
    '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll','/reference:System.Xml.dll',
    "/win32manifest:$(Join-Path $projectRoot 'src\app.manifest')",
    "/resource:$spritePath,HainanTurtlePet.sprites.png", "/out:$exePath")
$appIcon = Join-Path $projectRoot 'assets\pet.ico'
if (Test-Path -LiteralPath $appIcon) { $compilerArgs += "/win32icon:$appIcon" }
$compilerArgs += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compilerPath @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
if ($Test) {
    $testDirectory = Join-Path $projectRoot 'test-results'
    $selfTest = Start-Process -FilePath $exePath -ArgumentList @('--self-test', ('"' + $testDirectory + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($selfTest.ExitCode -ne 0) { throw "Self test failed. See $testDirectory" }
    # The GUI test presents a real layered window, exercises its own event paths,
    # and closes after five seconds. It never moves the user's mouse or types.
    $smokeTest = Start-Process -FilePath $exePath -ArgumentList @('--smoke-test', ('"' + $testDirectory + '"')) -PassThru -Wait -WindowStyle Hidden
    if ($smokeTest.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $testDirectory 'smoke-test.txt'))) {
        throw "GUI smoke test failed. See $testDirectory"
    }
}
Get-Item -LiteralPath $exePath | Select-Object FullName,Length
