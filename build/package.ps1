<#
.SYNOPSIS
  Reproducible release packaging for win-fast-finder.

.DESCRIPTION
  Runs the exact pipeline used to produce the published installer:

    1. dotnet build Wff.sln --no-incremental   (must be 0 errors)
    2. dotnet test   --no-build                (must be fully green)
    3. publish Wff.App   framework-dependent, single-file  -> dist\win-fx
    4. publish Wff.Setup framework-dependent               -> dist\setup-fx
    5. assemble payload-slim.zip  (Wff.App.exe + LICENSE)
    6. stub + payload + 8-byte little-endian length footer
         -> dist\win-fast-finder-Setup-<version>.exe
    7. SHA-256 sidecar
    8. source zip excluding bin/obj/dist

  The installed product is framework-dependent on purpose: the .NET 8 Desktop
  Runtime is a documented prerequisite, which keeps the installer at a few
  hundred KB instead of hundreds of MB.

.EXAMPLE
  .\build\package.ps1

.EXAMPLE
  .\build\package.ps1 -SkipTests
#>
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root

function Assert-Step($label, $scriptBlock) {
    Write-Host "==> $label" -ForegroundColor Cyan
    & $scriptBlock
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
        throw "$label failed with exit code $LASTEXITCODE"
    }
}

# --- version ----------------------------------------------------------------
$props = Join-Path $root 'Directory.Build.props'
$version = ([regex]::Match((Get-Content $props -Raw), '<Version>([^<]+)</Version>')).Groups[1].Value
if (-not $version) { throw "Could not read <Version> from Directory.Build.props" }
Write-Host "version: $version" -ForegroundColor Green

# --- 1 & 2: build + test ----------------------------------------------------
if (-not $SkipBuild) {
    Assert-Step 'build' { dotnet build Wff.sln --no-incremental }
}
if (-not $SkipTests) {
    Assert-Step 'test' { dotnet test tests/Wff.Tests/Wff.Tests.csproj --no-build }
}

# --- 3 & 4: publish ---------------------------------------------------------
$distWin = Join-Path $root 'dist\win-fx'
$distSetup = Join-Path $root 'dist\setup-fx'

Assert-Step 'publish Wff.App' {
    dotnet publish src/Wff.App/Wff.App.csproj -c Release -o $distWin `
        -p:PublishSingleFile=true -p:SelfContained=false
}
Assert-Step 'publish Wff.Setup' {
    dotnet publish src/Wff.Setup/Wff.Setup.csproj -c Release -o $distSetup `
        -p:SelfContained=false
}

$stub = Join-Path $distSetup 'WffSetup.exe'
$app = Join-Path $distWin 'Wff.App.exe'
$license = Join-Path $root 'LICENSE'
foreach ($p in @($stub, $app, $license)) {
    if (-not (Test-Path -LiteralPath $p)) { throw "Missing required artifact: $p" }
}

# --- 5: payload zip ---------------------------------------------------------
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$payloadDir = Join-Path $env:TEMP 'wff-payload'
if (Test-Path $payloadDir) { Remove-Item $payloadDir -Recurse -Force }
New-Item -ItemType Directory -Path $payloadDir -Force | Out-Null
Copy-Item $app, $license $payloadDir -Force

$payloadZip = Join-Path $dist 'payload-slim.zip'
if (Test-Path $payloadZip) { Remove-Item $payloadZip -Force }
Compress-Archive -Path (Join-Path $payloadDir '*') -DestinationPath $payloadZip -Force
Write-Host "payload: $([math]::Round((Get-Item $payloadZip).Length/1KB,1)) KB"

# --- 6: assemble installer --------------------------------------------------
# Layout:  [WffSetup.exe][payload.zip][Int64 little-endian payload length]
$setupExe = Join-Path $dist "win-fast-finder-Setup-$version.exe"
if (Test-Path $setupExe) { Remove-Item $setupExe -Force }

$stubBytes = [IO.File]::ReadAllBytes($stub)
$payBytes = [IO.File]::ReadAllBytes($payloadZip)

$out = [IO.File]::Create($setupExe)
try {
    $out.Write($stubBytes, 0, $stubBytes.Length)
    $out.Write($payBytes, 0, $payBytes.Length)
    $out.Write([BitConverter]::GetBytes([Int64]$payBytes.Length), 0, 8)
} finally { $out.Close() }

# verify the footer is readable and points at a zip
$check = [IO.File]::ReadAllBytes($setupExe)
$payLen = [BitConverter]::ToInt64($check, $check.Length - 8)
if ($payLen -le 0 -or $payLen -ne $payBytes.Length) {
    throw "Installer footer mismatch: read $payLen, expected $($payBytes.Length)"
}
$zipStart = $check.Length - 8 - $payLen
if ($check[$zipStart] -ne 0x50 -or $check[$zipStart + 1] -ne 0x4B) {
    throw "Installer payload is not a zip archive (missing PK signature)"
}
Write-Host "installer: $setupExe ($([math]::Round((Get-Item $setupExe).Length/1KB,1)) KB, footer OK)"

# --- 7: checksum ------------------------------------------------------------
$hash = (Get-FileHash $setupExe -Algorithm SHA256).Hash
$hashFile = "$setupExe.sha256"
Set-Content -LiteralPath $hashFile -Value "$hash  $(Split-Path $setupExe -Leaf)" -Encoding ascii -NoNewline
Write-Host "sha256:   $hash"

# --- 8: source zip ----------------------------------------------------------
$sourceZip = Join-Path $dist "win-fast-finder-$version-source.zip"
if (Test-Path $sourceZip) { Remove-Item $sourceZip -Force }

$staging = Join-Path $env:TEMP 'wff-source'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null

$excludeDirs = @('bin', 'obj', 'dist', '.git', '.vs', '.agent', 'TestResults')
foreach ($item in Get-ChildItem -LiteralPath $root) {
    if ($item.Name -in $excludeDirs) { continue }
    if ($item.Name -eq 'Wff.sln' -or $item.Name -like '*.md' -or
        $item.Name -in @('.gitignore', '.editorconfig', 'global.json', 'Directory.Build.props', 'LICENSE', 'THIRD_PARTY_NOTICES')) {
        Copy-Item $item.FullName (Join-Path $staging $item.Name) -Recurse -Force
        continue
    }
    if ($item.PSIsContainer) {
        Copy-Item $item.FullName (Join-Path $staging $item.Name) -Recurse -Force
        Get-ChildItem (Join-Path $staging $item.Name) -Recurse -Directory |
            Where-Object { $excludeDirs -contains $_.Name } |
            ForEach-Object { Remove-Item $_.FullName -Recurse -Force }
    }
}
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $sourceZip -Force
$srcHash = (Get-FileHash $sourceZip -Algorithm SHA256).Hash
Set-Content -LiteralPath "$sourceZip.sha256" -Value "$srcHash  $(Split-Path $sourceZip -Leaf)" -Encoding ascii -NoNewline
$srcCount = (Get-ChildItem $staging -Recurse -File | Measure-Object).Count
Write-Host "source:   $sourceZip ($srcCount files, $([math]::Round((Get-Item $sourceZip).Length/1KB,1)) KB)"

Remove-Item $payloadDir, $staging -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host "Packaged win-fast-finder v$version" -ForegroundColor Green
Write-Host "  installer: $setupExe"
Write-Host "  source:    $sourceZip"
