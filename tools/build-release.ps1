<#
.SYNOPSIS
  Builds the Potato Keys+ installer, and can publish it as a GitHub release.

.EXAMPLE
  .\tools\build-release.ps1                 # build dist\PotatoKeysPlus-Setup-<version>.exe
  .\tools\build-release.ps1 -Publish        # also tag v<version> and upload a GitHub release

  The version comes from <Version> in PotatoKeysPlus.csproj. Bump it before each release.
#>
param(
    [switch]$Publish,
    [string]$Notes = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "PotatoKeysPlus.csproj"
$publishDir = Join-Path $root "publish\win-x64"

[xml]$xml = Get-Content $project
$version = @($xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if (-not $version) { throw "No <Version> found in PotatoKeysPlus.csproj" }
Write-Host "Building Potato Keys+ $version" -ForegroundColor Yellow

# 1. Self-contained publish, so people don't need to install .NET themselves.
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained true -o $publishDir -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# 2. Installer wizard.
$iscc = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 isn't installed. Get it with: winget install JRSoftware.InnoSetup" }

& $iscc /Qp "/DAppVersion=$version" "/DPublishDir=$publishDir" (Join-Path $root "installer\PotatoKeysPlus.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

$setup = Join-Path $root "dist\PotatoKeysPlus-Setup-$version.exe"
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLower()
"$hash  PotatoKeysPlus-Setup-$version.exe" | Set-Content "$setup.sha256" -Encoding ascii
$size = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host "Installer: $setup ($size MB)" -ForegroundColor Green
Write-Host "SHA-256:   $hash"

# 3. Optional GitHub release. The in-app updater looks for the newest release with this Setup .exe.
if ($Publish) {
    $tag = "v$version"
    if (git -C $root tag --list $tag) { throw "Tag $tag already exists. Bump <Version> in the .csproj first." }
    git -C $root tag $tag
    git -C $root push origin $tag
    $ghArgs = @("release", "create", $tag, $setup, "$setup.sha256", "--repo", "Harel-k/PotatoKeysPlus", "--title", "Potato Keys+ $version")
    if ($Notes) { $ghArgs += @("--notes", $Notes) } else { $ghArgs += "--generate-notes" }
    gh @ghArgs
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
    Write-Host "Released $tag" -ForegroundColor Green
}
