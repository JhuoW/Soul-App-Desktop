$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'The .NET Framework C# compiler was not found. Windows .NET Framework 4.x is required.'
}

$sourceFile = Join-Path $PSScriptRoot 'SoulWebLauncher.cs'
$manifestFile = Join-Path $PSScriptRoot 'app.manifest'
$releaseDirectory = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $releaseDirectory 'SoulWebLauncher.exe'
$versionFile = Join-Path $releaseDirectory 'VERSION'

foreach ($requiredFile in @($sourceFile, $manifestFile, $versionFile)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required file is missing: $requiredFile"
    }
}
$releaseVersion = [System.IO.File]::ReadAllText($versionFile).Trim()
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw 'VERSION must contain a version such as 1.0.0.'
}

$compilerArguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/warn:4',
    '/warnaserror+',
    '/codepage:65001',
    '/reference:System.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    "/win32manifest:$manifestFile",
    "/out:$executable",
    $sourceFile
)
& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw 'Compilation did not produce the expected executable.'
}
$fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
if ($fileVersion.FileVersion -ne ($releaseVersion + '.0') -or $fileVersion.ProductVersion -ne $releaseVersion) {
    throw 'The executable version does not match VERSION. Update the assembly version attributes before publishing.'
}
$checksum = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText((Join-Path $releaseDirectory 'SHA256SUMS.txt'),
    ($checksum + "  SoulWebLauncher.exe`n"), [System.Text.Encoding]::ASCII)
Write-Output "Built: $executable"
Write-Output "Version: $releaseVersion"
Write-Output "SHA256: $checksum"

