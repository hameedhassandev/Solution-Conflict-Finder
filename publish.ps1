param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"

# Put this script in the project_code folder (next to SolutionComponentsAnalyzer and SolutionComponentsAnalyzer.Desktop)
$root    = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "SolutionComponentsAnalyzer.Desktop\SolutionComponentsAnalyzer.Desktop.csproj"
$output  = Join-Path $root "SolutionComponentsAnalyzer.Desktop\bin\Release\net48"
$dist    = Join-Path $root "dist"
$folder  = Join-Path $dist "SolutionConflictFinder"
$zip     = Join-Path $dist "SolutionConflictFinder-$Version.zip"

Write-Host "1/4  Cleaning..." -ForegroundColor Cyan
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
if (Test-Path $output) { Remove-Item $output -Recurse -Force }
New-Item -ItemType Directory -Path $folder | Out-Null

Write-Host "2/4  Building Release $Version..." -ForegroundColor Cyan
# if 'dotnet' is not found, open "Developer PowerShell for VS" or replace this line with:
#   msbuild $project /restore /p:Configuration=Release /p:Version=$Version
dotnet build $project -c Release -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Build failed - fix the errors above and run again." }

Write-Host "3/4  Collecting files..." -ForegroundColor Cyan
Copy-Item "$output\*" $folder -Recurse
Get-ChildItem $folder -Recurse -Include *.pdb, *.xml | Remove-Item -Force

$required = @(
    "SolutionConflictFinder.exe",
    "SolutionConflictFinder.exe.config",   # binding redirects - the exe fails on some machines without it
    "SolutionComponentsAnalyzer.UI.dll",
    "SolutionComponentsAnalyzer.Core.dll",
    "Microsoft.Xrm.Sdk.dll"
)
foreach ($file in $required) {
    if (-not (Test-Path (Join-Path $folder $file))) { throw "Missing in output: $file" }
}

Write-Host "      Adding README for the team..." -ForegroundColor Cyan
$readme = @'
Solution Conflict Finder  -  version {{VERSION}}
================================================

WHAT IT DOES
  Compares two Dynamics 365 / Dataverse solutions and shows what importing one into the other
  will OVERWRITE (conflicts), ADD, or leave untouched.

REQUIREMENTS
  - Windows 10 (version 1607 or later) or Windows 11, or Windows Server 2016 or later
  - .NET Framework 4.8 (already included in Windows 10 version 1903+ and Windows 11)
      Older Windows 10 / Server 2016-2019: install ".NET Framework 4.8 Runtime" from microsoft.com (free, no admin tool needed beyond the installer).
      Check what you have (Command Prompt):
        reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release
      A number of 528040 or higher means 4.8 is installed.
  - Network access to your Dynamics 365 server (VPN if needed)
  - A Dynamics user that can read solutions and customizations (System Customizer or System Administrator)
  - No installation and no administrator rights needed

FIRST TIME ONLY  (important)
  1. If you received this as a .zip: right-click the zip > Properties > tick "Unblock" > OK.
  2. Extract the WHOLE folder to a LOCAL drive, for example C:\Tools\SolutionConflictFinder
     (do not run it from inside the zip, from a network share, or from an email attachment).
  3. Keep all files together. SolutionConflictFinder.exe.config and the .dll files must stay next to the .exe.

HOW TO USE
  1. Run SolutionConflictFinder.exe
  2. CURRENT environment (the one that will receive the import): enter URL, user name, password > Connect > choose a solution.
       URL example: https://crm.company.com/OrgName
       User example: DOMAIN\username
  3. INCOMING environment (the solution you want to import): the same.
  4. Press Compare. Large solutions can take several minutes.
  5. Read the tabs: Conflicts, Review, Will be added, Only in current, All.
     Click a row for full details. Use Filter to search. Export CSV saves everything.

PRIVACY
  Passwords are never saved. URLs and user names are remembered in
  %AppData%\SolutionConflictFinder\settings.txt

WINDOWS SAYS "WINDOWS PROTECTED YOUR PC" (SmartScreen)
  The app is not code-signed. Click "More info" > "Run anyway".

PROBLEMS
  "Could not load file or assembly ..."   -> the zip was not unblocked, or files were moved apart. Repeat FIRST TIME ONLY.
  "This app can't run on your PC" / nothing opens -> .NET Framework 4.8 is missing. Install it (see REQUIREMENTS).
  "Could not connect" -> check URL (include the organization name), user name format DOMAIN\user, VPN, and the password.
  Timeout while comparing -> run it again; very large solutions can need a few minutes.
'@
$readme = $readme.Replace("{{VERSION}}", $Version)
Set-Content -Path (Join-Path $folder "README.txt") -Value $readme -Encoding UTF8

Write-Host "4/4  Zipping..." -ForegroundColor Cyan
Compress-Archive -Path $folder -DestinationPath $zip

Write-Host ""
Write-Host "Done: $zip" -ForegroundColor Green
Get-ChildItem $folder | Select-Object Name, @{n="KB";e={[int]($_.Length/1KB)}} | Format-Table -AutoSize
