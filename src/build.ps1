param([string] $OutputDirectory = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = $projectRoot }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler is required.' }
$framework = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
if (-not (Test-Path -LiteralPath $framework)) {
    throw '.NET Framework 4.7.2 Developer Pack is required to build. Precompiled EXE users do not need the Developer Pack.'
}

$sdkVersion = '1.0.3124.44'
$sdkSha256 = '31d61a59a5d5ae2ef5dcb9f175b626c1d64218217f879701ec73ed8fd74b65ae'
$cache = Join-Path $projectRoot '.packages'
$build = Join-Path $projectRoot '.build'
New-Item -ItemType Directory -Path $cache,$build -Force | Out-Null
$package = Join-Path $cache ('microsoft.web.webview2.' + $sdkVersion + '.nupkg')
if (-not (Test-Path -LiteralPath $package)) {
    $uri = 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/' + $sdkVersion + '/microsoft.web.webview2.' + $sdkVersion + '.nupkg'
    Invoke-WebRequest -Uri $uri -OutFile $package -UseBasicParsing
}
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant() -cne $sdkSha256) {
    throw 'WebView2 package checksum mismatch. The cached package will not be used.'
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$dependencies = @(
    @{ Entry='lib/net462/Microsoft.Web.WebView2.Core.dll'; File='Microsoft.Web.WebView2.Core.dll'; Resource='SoulWebLauncher.Microsoft.Web.WebView2.Core.gz' },
    @{ Entry='lib/net462/Microsoft.Web.WebView2.WinForms.dll'; File='Microsoft.Web.WebView2.WinForms.dll'; Resource='SoulWebLauncher.Microsoft.Web.WebView2.WinForms.gz' },
    @{ Entry='runtimes/win-x64/native/WebView2Loader.dll'; File='WebView2Loader.x64.dll'; Resource='SoulWebLauncher.WebView2Loader.x64.gz' },
    @{ Entry='runtimes/win-x86/native/WebView2Loader.dll'; File='WebView2Loader.x86.dll'; Resource='SoulWebLauncher.WebView2Loader.x86.gz' }
)
$resourceArguments = @()
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    foreach ($dependency in $dependencies) {
        $entry = $archive.GetEntry($dependency.Entry)
        if ($null -eq $entry) { throw ('Missing SDK file: ' + $dependency.Entry) }
        $dll = Join-Path $build $dependency.File
        $inputStream = $entry.Open()
        try {
            $outputStream = [IO.File]::Create($dll)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
        } finally { $inputStream.Dispose() }
        $gzipFile = $dll + '.gz'
        $gzipOutput = [IO.File]::Create($gzipFile)
        try {
            $gzip = New-Object IO.Compression.GZipStream($gzipOutput, [IO.Compression.CompressionLevel]::Optimal, $true)
            try {
                $dllInput = [IO.File]::OpenRead($dll)
                try { $dllInput.CopyTo($gzip) } finally { $dllInput.Dispose() }
            } finally { $gzip.Dispose() }
        } finally { $gzipOutput.Dispose() }
        $resourceArguments += ('/resource:' + $gzipFile + ',' + $dependency.Resource)
    }
} finally { $archive.Dispose() }

$version = (Get-Content -LiteralPath (Join-Path $projectRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION.' }
$executable = Join-Path $OutputDirectory 'SoulWebLauncher.exe'
$icon = Join-Path $projectRoot 'assets\app-icon.ico'
$png = Join-Path $projectRoot 'assets\app-icon.png'
$notices = Join-Path $projectRoot 'THIRD_PARTY_NOTICES.txt'
$arguments = @('/noconfig','/nologo','/target:winexe','/platform:anycpu','/optimize+','/warn:4','/warnaserror+','/codepage:65001','/nostdlib+','/main:SoulWebLauncher.Bootstrap')
foreach ($assembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Net.Http.dll')) {
    $arguments += ('/reference:' + (Join-Path $framework $assembly))
}
$arguments += @(
    ('/reference:' + (Join-Path $build 'Microsoft.Web.WebView2.Core.dll')),
    ('/reference:' + (Join-Path $build 'Microsoft.Web.WebView2.WinForms.dll')),
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),
    ('/win32icon:' + $icon),
    ('/resource:' + $icon + ',SoulWebLauncher.AppIcon.ico'),
    ('/resource:' + $png + ',SoulWebLauncher.AppIcon.png'),
    ('/resource:' + $notices + ',SoulWebLauncher.ThirdPartyNotices.txt'),
    ('/out:' + $executable)
)
$arguments += $resourceArguments
$arguments += @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Sort-Object Name | Select-Object -ExpandProperty FullName)
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
$metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
if ($metadata.FileVersion -ne ($version + '.0') -or $metadata.ProductVersion -ne $version) { throw 'EXE version mismatch.' }
$checksum = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'), ($checksum + "  SoulWebLauncher.exe`n"), [Text.Encoding]::ASCII)
Write-Output "Built: $executable"
Write-Output "Version: $version"
Write-Output "SHA256: $checksum"
