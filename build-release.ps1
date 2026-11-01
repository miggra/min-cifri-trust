$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$project = Join-Path $root "src\MinCifriTrust\MinCifriTrust.csproj"
$publishDir = Join-Path $root "artifacts\publish"
$installerDir = Join-Path $root "artifacts\installer"
$iss = Join-Path $root "installer\MinCifriTrust.iss"
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

Write-Host "==> Publish single-file exe"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $installerDir | Out-Null

dotnet publish $project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o $publishDir `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  /p:DebugType=none

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$exe = Join-Path $publishDir "MinCifriTrust.exe"
if (-not (Test-Path $exe)) { throw "Published exe not found: $exe" }

Write-Host "==> Build installer"
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed" }

Write-Host ""
Write-Host "Portable:  $exe"
Get-ChildItem $installerDir -Filter "*.exe" | ForEach-Object {
  Write-Host "Installer: $($_.FullName)"
}
