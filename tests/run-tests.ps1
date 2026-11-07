param([string] $AppPath = '', [switch] $UiLaunch)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($AppPath)) { $AppPath = Join-Path $projectRoot 'SoulWebLauncher.exe' }
$AppPath = [IO.Path]::GetFullPath($AppPath)
if (-not (Test-Path -LiteralPath $AppPath -PathType Leaf)) { throw 'Build the production EXE before running tests.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'The .NET Framework C# compiler is required.' }
$outputDirectory = Join-Path $projectRoot 'obj\test-output'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$runner = Join-Path $outputDirectory 'SmokeTests.exe'
$source = Join-Path $PSScriptRoot 'SmokeTests.cs'
$manifest = Join-Path $projectRoot 'src\app.manifest'
$arguments = @('/nologo','/target:exe','/platform:anycpu','/warn:4','/warnaserror+','/codepage:65001',
    '/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll',
    ('/win32manifest:' + $manifest),('/out:' + $runner),$source)
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Smoke-test runner compilation failed.' }
$testRoot = Join-Path $outputDirectory ('runtime-' + [Guid]::NewGuid().ToString('N'))
if ($UiLaunch) {
    Write-Output 'Opening the local UI test window. Close it through its own Exit choice to finish; no website or account is used.'
    & $runner $AppPath $testRoot '--ui-launch'
} else {
    Write-Output 'Running offline smoke tests. A local test window may appear briefly; no website or account is used.'
    & $runner $AppPath $testRoot
}
if ($LASTEXITCODE -ne 0) { throw ('Smoke tests failed with exit code ' + $LASTEXITCODE) }
