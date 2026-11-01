# Solution Conflict Finder

**Find out what a solution import will overwrite, before you import it.**

Solution Conflict Finder compares two Dynamics 365 / Dataverse solutions, either in the same environment or in two different ones, and tells you which components would be **overwritten**, **added**, or **left untouched** if you imported one into the other. It is read-only: it never changes either environment.

It ships in two forms that share the same comparison engine:

- **XrmToolBox plugin**: opens inside XrmToolBox and uses your saved connections.
- **Desktop app** (`SolutionConflictFinder.exe`): a stand-alone Windows app where you type the URL, user name and password.

<!-- [![NuGet](https://img.shields.io/nuget/v/HameedHassan.SolutionConflictFinder)](https://www.nuget.org/packages/HameedHassan.SolutionConflictFinder) -->
<!-- Uncomment the badge above after the package is published on nuget.org. -->

---

## Contents

- [The problem it solves](#the-problem-it-solves)
- [Screenshots](#screenshots)
- [What it compares](#what-it-compares)
- [How to read the results](#how-to-read-the-results)
- [Requirements](#requirements)
- [Install and use: XrmToolBox plugin](#install-and-use-xrmtoolbox-plugin)
- [Install and use: desktop app](#install-and-use-desktop-app)
- [How it works](#how-it-works)
- [Project structure](#project-structure)
- [Build from source](#build-from-source)
- [Publishing](#publishing)
- [Limitations](#limitations)

---

## The problem it solves

You have a solution in environment A that holds your customizations. Another team, or another environment B, has a different version of the same solution. When you export B and import it into A, every component that exists in **both** is replaced by B's version. Changes you made in A to a form, a view, a field, a web resource or a plugin are silently lost.

Solution Conflict Finder reads both solutions, compares every component, and shows you the conflicts **before** you import, so you can merge the two sets of customizations by hand and then import safely.

Throughout the tool:

| Term | Meaning |
|---|---|
| **Current** (A) | The environment and solution that will **receive** the import and hold your customizations |
| **Incoming** (B) | The solution you plan to import. It overwrites A where both have the same component |

---

## Screenshots

<!-- Add your own screenshots to docs/images and keep these lines. -->
<img width="1919" height="968" alt="Screenshot 2026-11-01 071149" src="https://github.com/user-attachments/assets/0efc3d6c-05cb-46c4-ac65-b96e988d4fcd" />

<img width="1919" height="1006" alt="Screenshot 2026-10-14 121736" src="https://github.com/user-attachments/assets/94f306d0-9543-4bf0-a295-fed0e19d49df" />

<img width="1919" height="1009" alt="Screenshot 2026-10-14 121750" src="https://github.com/user-attachments/assets/6c1cb77c-26e4-4ad4-aeab-7dd272e73544" />

<img width="1919" height="1007" alt="Screenshot 2026-10-14 121811" src="https://github.com/user-attachments/assets/8c9ab17b-4f85-4491-bb47-5e6b55a46971" />

---

## What it compares

| Component | What is checked |
|---|---|
| **Fields** | Display name, type, required level, schema name, and type-specific settings: min and max value, precision, max length, formats, option values and labels, lookup targets, audit and security flags |
| **Forms** | Controls added or removed, visible and disabled state, JavaScript libraries, event handlers, and any other change in the form XML |
| **Views** | Columns added or removed, FetchXML (filters, sorting, joins), LayoutXML (column order and width) |
| **Web resources** | Content, using a hash. Text files ignore line-ending and byte-order-mark differences |
| **Plugin assemblies** | Version, public key token, binary content |
| **Plugin steps** | Stage, mode, rank, filtering attributes, configuration, enabled state |
| **Workflows, business rules, actions, flows** | Name, category, state, definition |
| **Business process flows** | Stages and definition |
| **Global option sets** | Every option value and label |
| **Other components** | Reports, email templates, duplicate detection rules, charts, site maps, model-driven apps, canvas apps, plugin types, plugin step images, environment variables and their values. These are compared at a high level (content hash, or "exists in both, review manually") |

Sub-components that always belong to a parent (relationships, privileges, display strings and similar) are ignored by default so they do not flood the results.

XML is normalized before comparing (whitespace, comments and attribute order are ignored), so formatting noise does not appear as a conflict.

---

## How to read the results

The result window has one table per kind of difference, plus a details pane for the selected row:

| Tab | Meaning | What to do |
|---|---|---|
| **Conflicts** | The component exists in both and differs. Importing overwrites the current version | Review each one and merge your customizations |
| **Review** | Exists in both, but this component type cannot be compared in detail | Check it manually |
| **Will be added** | Exists only in the incoming solution. The import adds it | Usually safe |
| **Only in current** | Exists only in the current environment. The import does not touch it | Nothing to do |
| **All** | Everything above in one table | Sort and search |

You can sort by any column, filter across all tabs with the search box, and **export everything to CSV** (UTF-8, opens correctly in Excel, including non-English names).

---

## Requirements

- Windows 10 (version 1607 or later), Windows 11, or Windows Server 2016 or later
- **.NET Framework 4.8** (already included in Windows 10 version 1903 and later and in Windows 11)
- Network access to your Dynamics 365 / Dataverse environments
- A user that can read solutions and customizations in both environments (for example System Customizer or System Administrator)

For the XrmToolBox plugin you also need a current [XrmToolBox](https://www.xrmtoolbox.com). XrmToolBox itself requires the Windows feature *Windows Identity Foundation 3.5* to be enabled on some machines (Turn Windows features on or off).

---

## Install and use: XrmToolBox plugin

### Install

From the Tool Library, once published:

1. Open XrmToolBox, then **Configuration, Tool Library**.
2. Search for **Solution Conflict Finder** and install it.

Or manually, for testing:

1. Close XrmToolBox.
2. Copy the two files into your XrmToolBox plugin folder:

   ```
   %AppData%\MscrmTools\XrmToolBox\Plugins\
       SolutionComponentsAnalyzer.XrmToolBox.dll
       SolutionComponentsAnalyzer.XrmToolBox\
           SolutionComponentsAnalyzer.Core.dll
   ```

3. Start XrmToolBox and open **Solution Conflict Finder** from the tool list.

### Use

1. Connect to the **current** environment with the normal XrmToolBox Connect button. Its solutions load into the first box.
2. Press **Connect other environment...** and choose the **incoming** environment from your saved connections. Its solutions load into the second box. If you skip this step, both sides use the current environment, so you can compare two solutions inside one environment.
3. Choose a solution in each box. You can type to filter the list.
4. Press **Compare**. Large solutions can take a few minutes. Both environments are read at the same time.

---

## Install and use: desktop app

### Install

1. Download the latest zip from the [Releases](../../releases) page.
2. Right-click the zip, choose **Properties**, tick **Unblock**, and click OK.
3. Extract the **whole folder** to a local drive, for example `C:\Tools\SolutionConflictFinder`. Keep all files together.
4. Run `SolutionConflictFinder.exe`. No installer and no administrator rights are needed.

Windows may show a SmartScreen warning because the exe is not code-signed: click **More info**, then **Run anyway**.

### Use

1. In the **Current environment** box enter the URL (for example `https://crm.company.com/OrgName`), user name (`DOMAIN\user`) and password, press **Connect**, and choose a solution.
2. Do the same in the **Incoming environment** box.
3. Press **Compare**.

Your URLs and user names are remembered in `%AppData%\SolutionConflictFinder\settings.txt`. **Passwords are never saved.**

The desktop app signs in with user name and password (on-premises and IFD style). For environments that require multi-factor authentication, use the XrmToolBox plugin instead.

---

## How it works

```
 Dataverse A ──┐                                   ┌── Conflicts
               ├─► DataverseSolutionProvider ─►    │   Review
 Dataverse B ──┘     (reads solution              ComponentComparers ─► ComparisonResult ─► results tables / CSV
                      components and metadata)     (A vs B, per component type)             Will be added
                                                                                            Only in current
```

1. **Provider.** `DataverseSolutionProvider` queries the `solutioncomponent` table for the solution and loads each component with the Dataverse SDK: entity and attribute metadata, system forms, saved queries, web resources, plugin assemblies and steps, workflows, option sets, and a generic reader for everything else. The result is a plain `SolutionDefinition` object.
2. **Comparer.** `ComponentComparers` matches components of the two definitions by a stable key (ID, or name) and produces a list of differences. Each difference is a `Conflict`, `OnlyInSource`, `OnlyInTarget`, or `ExistsInBoth` (review).
3. **Service.** `SolutionComparisonService` runs both steps and returns a `ComparisonResult`.
4. **UI.** The desktop app and the XrmToolBox plugin each have their own results screen on top of the same Core library.

The tool only uses read operations (`Retrieve`, `RetrieveMultiple`, and metadata retrieval). It never creates, updates or deletes anything.

---

## Project structure

```
SolutionComponentsAnalyzer.Core/                  Core library (netstandard2.0): the comparison engine
    Models/                                       SolutionDefinition, Difference, ComparisonResult, ...
    Comparers/                                    ComponentComparers, XML normalizer
    Providers/                                    ISolutionProvider, DataverseSolutionProvider
    Services/                                     SolutionComparisonService
    Helpers/                                      Component type tables
SolutionComponentsAnalyzer.UI/                    WinForms UI library for the desktop app (net48)
SolutionComponentsAnalyzer.Desktop/               Desktop host: SolutionConflictFinder.exe (net48)
SolutionComponentsAnalyzer.XrmToolBox/            XrmToolBox plugin with its own UI (net48)
    Package/                                      nuspec, icon and pack.ps1 for the Tool Library
publish.ps1                                       Builds and zips the desktop app for sharing
```

The XrmToolBox plugin deliberately has its **own copy** of the results screen and does not reference the desktop UI project. Only the Core library is shared. A change to the results tables therefore has to be made in both UI projects, while a change to the comparison logic is made once, in Core.

---

## Build from source

**Prerequisites**

- Visual Studio 2019 or 2022 with the **.NET desktop development** workload
- .NET Framework 4.8 Developer Pack
- .NET SDK (for the `dotnet build` commands in the scripts)

**Steps**

1. Clone the repository and open the solution in Visual Studio.
2. Restore NuGet packages and build.

To test the plugin locally, close XrmToolBox and build `SolutionComponentsAnalyzer.XrmToolBox`. After every build it copies the plugin DLL and `SolutionComponentsAnalyzer.Core.dll` into your XrmToolBox Plugins folder (see the install layout above). To turn that off:

```powershell
dotnet build -p:DeployToXrmToolBox=false
```

**Package the desktop app for your team**

```powershell
.\publish.ps1 -Version 1.0.0
```

This builds in Release and creates `dist\SolutionConflictFinder-1.0.0.zip`, containing the exe, its config file, the DLLs and a short README for users.

---

## Publishing

The XrmToolBox plugin is distributed through NuGet and the XrmToolBox Tool Library.

```powershell
cd SolutionComponentsAnalyzer.XrmToolBox\Package
.\pack.ps1 -Version 1.2026.10.1
```

`pack.ps1` builds in Release, checks that the DLL version equals the package version, packs the `.nupkg`, and lists its contents. Upload the package on nuget.org, wait for it to be indexed, then register the package id on the XrmToolBox portal. Versions follow the XrmToolBox convention `1.YYYY.M.revision`.

---

## Limitations

- **Read-only analysis.** The tool tells you what will change; it does not merge or import anything.
- **Two-way comparison.** With only A and B it cannot tell "B has a new change" from "B is just outdated". A three-way comparison against a saved baseline would be needed for that.
- **Published metadata.** Field settings are read from published metadata, so publish customizations in both environments before comparing.
- **Field scope.** A field is included when it was added to the solution individually, or when its entity was added with all subcomponents and the field is custom.
- **High-level for some types.** Reports, site maps, apps, environment variables and similar components are compared by content hash or flagged for manual review, not in detail. Ribbon and command bar customizations are flagged for review only.
- **Environment variable values** can differ on purpose between environments. They are compared anyway, because importing a value overwrites the current one.
- **Desktop sign-in.** The desktop app supports user name and password only. Use the XrmToolBox plugin for MFA or OAuth environments.
- **Large solutions** take time, because every component is read individually.

---



