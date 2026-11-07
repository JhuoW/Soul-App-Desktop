$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = 'JhuoW/Soul-App-Desktop'
if ($env:GITHUB_REPOSITORY -ne $repository -or $env:GITHUB_SHA -notmatch '^[0-9a-f]{40}$') {
    throw 'This publisher must run in the intended GitHub repository at a specific commit.'
}
if ([string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    throw 'The GitHub Actions token is required.'
}
$version = (Get-Content -LiteralPath 'VERSION' -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION.' }
$tag = 'v' + $version
$exe = Get-Item -LiteralPath 'SoulWebLauncher.exe'
$metadata = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe.FullName)
if ($metadata.FileVersion -ne ($version + '.0') -or $metadata.ProductVersion -ne $version) {
    throw 'The executable version does not match VERSION.'
}
$exeHash = (Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$expectedLine = $exeHash + '  SoulWebLauncher.exe'
if ((Get-Content -LiteralPath 'SHA256SUMS.txt' -Raw).Trim() -cne $expectedLine) {
    throw 'The executable does not match SHA256SUMS.txt.'
}
$assets = @('SoulWebLauncher.exe', 'SHA256SUMS.txt')
$api = 'https://api.github.com/repos/' + $repository
$headers = @{
    Authorization = 'Bearer ' + $env:GITHUB_TOKEN
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}

function Read-OptionalResource([string] $uri) {
    try { return Invoke-RestMethod -Uri $uri -Headers $headers -Method Get }
    catch {
        if ($null -ne $_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 404) { return $null }
        throw
    }
}

$release = Read-OptionalResource ($api + '/releases/tags/' + $tag)
if ($null -eq $release) {
    # The by-tag endpoint only guarantees published releases. Find a resumable draft too.
    $pageNumber = 1
    do {
        $page = @((Invoke-RestMethod -Uri ($api + '/releases?per_page=100&page=' + $pageNumber) -Headers $headers -Method Get))
        $matchingDrafts = @($page | Where-Object { $_.tag_name -ceq $tag })
        if ($matchingDrafts.Count -gt 1) { throw 'Multiple releases use this tag; resolve the duplicate drafts before retrying.' }
        if ($matchingDrafts.Count -eq 1) { $release = $matchingDrafts[0]; break }
        $pageNumber++
    } while ($page.Count -eq 100)
}
if ($null -eq $release) {
    $existingTag = Read-OptionalResource ($api + '/git/ref/tags/' + $tag)
    if ($null -ne $existingTag) { throw 'The tag already exists without a matching release. Choose a new version.' }
    $payload = @{
        tag_name = $tag
        target_commitish = $env:GITHUB_SHA
        name = 'Soul Web Launcher ' + $tag
        body = Get-Content -LiteralPath 'RELEASE_NOTES.md' -Raw
        draft = $true
        prerelease = $false
        make_latest = 'false'
    } | ConvertTo-Json -Depth 4
    $release = Invoke-RestMethod -Uri ($api + '/releases') -Headers $headers -Method Post -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($payload))
} elseif ($release.target_commitish -ne $env:GITHUB_SHA) {
    throw 'This version belongs to a different commit. Increment VERSION rather than replacing it.'
}

$assetsEndpoint = $api + '/releases/' + $release.id + '/assets'
foreach ($assetName in $assets) {
    $file = Get-Item -LiteralPath $assetName
    $digest = 'sha256:' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $remoteAssets = @((Invoke-RestMethod -Uri $assetsEndpoint -Headers $headers -Method Get))
    $matches = @($remoteAssets | Where-Object { $_.name -ceq $assetName })
    if ($matches.Count -eq 0) {
        if (-not $release.draft) { throw 'A published release is missing an asset; create a new version instead of changing it.' }
        $uploadBase = ($release.upload_url -split '\{', 2)[0]
        $uploadUri = [Uri]($uploadBase + '?name=' + [Uri]::EscapeDataString($assetName))
        if ($uploadUri.Scheme -ne 'https' -or $uploadUri.Host -ne 'uploads.github.com') { throw 'Unexpected asset upload destination.' }
        $uploaded = Invoke-RestMethod -Uri $uploadUri -Headers $headers -Method Post -InFile $file.FullName -ContentType 'application/octet-stream'
        $matches = @($uploaded)
    }
    if ($matches.Count -ne 1 -or $matches[0].state -ne 'uploaded' -or $matches[0].size -ne $file.Length -or $matches[0].digest -cne $digest) {
        throw ('Asset verification failed: ' + $assetName + '. Existing assets are never overwritten.')
    }
}

# Re-read the server state before making the release public.
$verifiedAssets = @((Invoke-RestMethod -Uri $assetsEndpoint -Headers $headers -Method Get))
foreach ($assetName in $assets) {
    $file = Get-Item -LiteralPath $assetName
    $expectedDigest = 'sha256:' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $match = @($verifiedAssets | Where-Object { $_.name -ceq $assetName })
    if ($match.Count -ne 1 -or $match[0].state -ne 'uploaded' -or $match[0].size -ne $file.Length -or $match[0].digest -cne $expectedDigest) {
        throw ('Final asset verification failed: ' + $assetName)
    }
}
if ($release.draft) {
    $publish = @{ draft = $false; prerelease = $false; make_latest = 'true' } | ConvertTo-Json
    $release = Invoke-RestMethod -Uri ($api + '/releases/' + $release.id) -Headers $headers -Method Patch -ContentType 'application/json' -Body $publish
}
$final = Invoke-RestMethod -Uri ($api + '/releases/' + $release.id) -Headers $headers -Method Get
if ($final.draft -or $final.prerelease -or $final.tag_name -ne $tag) { throw 'The release is not public and stable.' }
Write-Output ('Published: ' + $final.html_url)
Write-Output ('SHA256: ' + $exeHash)
