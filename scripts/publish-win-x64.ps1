param(
    [string]$DotNet = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\LiangTu.App\LiangTu.App.csproj'
$publishDirectory = Join-Path $root 'artifacts\publish\win-x64'
$nugetConfig = Join-Path $root 'NuGet.Config'

if ([string]::IsNullOrWhiteSpace($DotNet)) {
    $localDotNet = Join-Path (Split-Path $root -Parent) '.dotnet\dotnet.exe'
    $DotNet = if (Test-Path -LiteralPath $localDotNet) {
        $localDotNet
    }
    else {
        (Get-Command dotnet -ErrorAction Stop).Source
    }
}

if ([string]::IsNullOrWhiteSpace($env:DOTNET_CLI_HOME)) {
    $env:DOTNET_CLI_HOME = Join-Path (Split-Path $root -Parent) '.dotnet-home'
}

if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
    $env:NUGET_PACKAGES = Join-Path (Split-Path $root -Parent) '.nuget\packages'
}

& $DotNet restore $project `
    --runtime win-x64 `
    --configfile $nugetConfig
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

$publishRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts\publish'))
$resolvedPublishDirectory = [IO.Path]::GetFullPath($publishDirectory)
if (-not $resolvedPublishDirectory.StartsWith(
    $publishRoot + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unexpected publish directory: $resolvedPublishDirectory"
}

if (Test-Path -LiteralPath $resolvedPublishDirectory) {
    Remove-Item -LiteralPath $resolvedPublishDirectory -Recurse -Force
}

& $DotNet publish $project `
    --configuration Release `
    --no-restore `
    -p:PublishProfile=win-x64
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

[xml]$buildProperties = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'Directory.Build.props')
$version = ([string]$buildProperties.Project.PropertyGroup.VersionPrefix).Trim()
$archivePath = Join-Path $root "artifacts\publish\LiangTu-$version-win-x64.zip"

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

Compress-Archive `
    -Path (Join-Path $publishDirectory '*') `
    -DestinationPath $archivePath `
    -CompressionLevel Optimal

$executablePath = Join-Path $publishDirectory 'LiangTu.exe'
$executableHash = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
$checksumPath = Join-Path $root 'artifacts\publish\SHA256SUMS.txt'
Set-Content -LiteralPath $checksumPath -Encoding ASCII -Value @(
    "$executableHash  win-x64/LiangTu.exe",
    "$archiveHash  $(Split-Path $archivePath -Leaf)"
)

Write-Output "Published: $publishDirectory"
Write-Output "Archive:   $archivePath"
Write-Output "Checksums: $checksumPath"
