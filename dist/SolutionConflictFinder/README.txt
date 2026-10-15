Solution Conflict Finder  -  version 1.0.0
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
