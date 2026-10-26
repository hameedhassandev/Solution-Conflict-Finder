# Builds the plugin in Release, packs the NuGet package, and optionally pushes it to nuget.org.
#
#   .\pack.ps1                                   (build + pack only: safe to run any time)
#   .\pack.ps1 -Push -ApiKey YOUR_NUGET_KEY      (also publish to nuget.org)
#   .\pack.ps1 -Version 1.2026.11.1              (next release)
param(
    [string]$Version = "1.2026.10.1",
    [string]$PackageId = "HameedHassan.SolutionConflictFinder",   # must be UNIQUE on nuget.org: change it if taken
    [string]$XrmToolBoxVersion = "1.2025.10.74",                  # XrmToolBox version you built against (check nuget.org/packages/XrmToolBox)
    [switch]$Push,
    [string]$ApiKey = ""
)

$ErrorActionPreference = "Stop"

$here    = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = (Resolve-Path (Join-Path $here "..\SolutionComponentsAnalyzer.XrmToolBox.csproj")).Path
$nuspec  = Join-Path $here "SolutionConflictFinder.nuspec"
$out     = Join-Path $here "out"
$nuget   = Join-Path $here "nuget.exe"

Write-Host "1/5  Building Release $Version..." -ForegroundColor Cyan
dotnet build $project -c Release -p:Version=$Version -p:DeployToXrmToolBox=false
if ($LASTEXITCODE -ne 0) { throw "Build failed - fix the errors above." }

$bin = Join-Path (Split-Path -Parent $project) "bin\Release\net48"
foreach ($file in "SolutionComponentsAnalyzer.XrmToolBox.dll", "SolutionComponentsAnalyzer.Core.dll") {
    if (-not (Test-Path (Join-Path $bin $file))) { throw "Missing build output: $file" }
}

Write-Host "2/5  Checking that the DLL version equals the package version..." -ForegroundColor Cyan
$fileVersion = (Get-Item (Join-Path $bin "SolutionComponentsAnalyzer.XrmToolBox.dll")).VersionInfo.FileVersion
if (-not $fileVersion.StartsWith($Version)) { throw "DLL version $fileVersion does not match package version $Version (the Tool Library requires them to match)." }

Write-Host "3/5  Getting nuget.exe..." -ForegroundColor Cyan
if (-not (Test-Path $nuget)) {
    Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nuget
}

Write-Host "4/5  Packing..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $out | Out-Null
& $nuget pack $nuspec -OutputDirectory $out -NoPackageAnalysis -Properties "id=$PackageId;version=$Version;xtb=$XrmToolBoxVersion;bin=$bin;here=$here"
if ($LASTEXITCODE -ne 0) { throw "Pack failed." }

$package = Join-Path $out "$PackageId.$Version.nupkg"
Write-Host ""
Write-Host "Package content (check: tool DLL under lib\net48\Plugins, Core in the sub-folder, NO Microsoft.Xrm.Sdk):" -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($package)
$zip.Entries | ForEach-Object { "   " + $_.FullName }
$zip.Dispose()

if ($Push) {
    if (-not $ApiKey) { throw "Pass your nuget.org key:  -ApiKey YOUR_KEY" }
    Write-Host "5/5  Pushing to nuget.org..." -ForegroundColor Cyan
    & $nuget push $package -ApiKey $ApiKey -Source "https://api.nuget.org/v3/index.json"
    if ($LASTEXITCODE -ne 0) { throw "Push failed." }
    Write-Host "Pushed. nuget.org needs a few minutes to index it before you can register it on xrmtoolbox.com." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "5/5  Not pushed (no -Push). Package ready: $package" -ForegroundColor Green
}
