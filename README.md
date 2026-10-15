# Solution Conflict Finder

## Install
1. Extract this zip INTO your `project_code` folder, next to `SolutionComponentsAnalyzer` (Core):

       project_code\
         SolutionComponentsAnalyzer.Core\            <- Core (already there)
         SolutionComponentsAnalyzer.UI\         <- new: the shareable WinForms DLL
         SolutionComponentsAnalyzer.Desktop\    <- new: the .exe

2. Visual Studio: right-click the solution > Add > Existing Project, add both new .csproj files.
3. Right-click `SolutionComponentsAnalyzer.Desktop` > Set as Startup Project. Press F5.

## Share it (one command)
Copy `publish.ps1` to the `project_code` folder (next to the Core folder), then in PowerShell:

    .\publish.ps1 -Version 1.0.0

It builds Release, copies the output to `dist\SolutionConflictFinder`, removes debug files,
checks that `SolutionConflictFinder.exe.config` is there, and creates `dist\SolutionConflictFinder-1.0.0.zip`.
Send that zip. Users extract it to a LOCAL folder and run `SolutionConflictFinder.exe`.

## XrmToolBox plugin (same design, embedded in XrmToolBox)
New project: `SolutionComponentsAnalyzer.XrmToolBox` (next to the other folders). It reuses the UI library, so the tables,
filter, export and colors are identical to the desktop app. Only the environment boxes differ, because XrmToolBox makes the connections.

1. Add `SolutionComponentsAnalyzer.XrmToolBox.csproj` to the solution (Add > Existing Project).
2. CLOSE XrmToolBox, then build the project. After each build it copies 3 DLLs
   (XrmToolBox, UI, Core) to `%AppData%\MscrmTools\XrmToolBox\Plugins`.
3. Start XrmToolBox, connect to the CURRENT environment, open the tool "Solution Conflict Finder".
4. Press "Connect other environment..." and pick the INCOMING environment in the XrmToolBox connection dialog.
   (Skip this to compare two solutions inside the same environment.)
5. Pick a solution on each side, press Compare.

If XrmToolBox does not show the tool: check the 3 DLLs are in the Plugins folder, and that XrmToolBox was closed while building.
To stop auto-copying: `dotnet build -p:DeployToXrmToolBox=false`.
Never copy Microsoft.Xrm.Sdk.dll into the Plugins folder, XrmToolBox already provides it.

## Use
1. Fill URL / user name / password for the CURRENT environment, press Connect, pick a solution.
2. Do the same for the INCOMING environment (the solution you will export and import).
3. Press Compare. Tabs: Conflicts, Review, Will be added, Only in current, All.
   Click a row to read the full details at the bottom. Type in Filter to search all tabs. Export CSV saves everything.

URLs and user names are remembered in %AppData%\SolutionConflictFinder\settings.txt. Passwords are never saved.
