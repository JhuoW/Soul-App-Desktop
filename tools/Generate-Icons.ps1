$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'The .NET Framework C# compiler is required to regenerate icons.'
}
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceFile = Join-Path $PSScriptRoot 'GenerateIcons.cs'
$outputDirectory = Join-Path $projectRoot 'assets'
$tempDirectory = Join-Path $projectRoot 'obj\icon-generator'
New-Item -ItemType Directory -Path $tempDirectory -Force | Out-Null
$generator = Join-Path $tempDirectory 'GenerateIcons.exe'
& $compiler /nologo /target:exe /optimize+ /warnaserror+ /reference:System.dll /reference:System.Drawing.dll "/out:$generator" $sourceFile
if ($LASTEXITCODE -ne 0) { throw 'Icon generator compilation failed.' }
& $generator $outputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
