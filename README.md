# BatchBaker

BatchBaker is a Windows desktop tool for creating Active Directory user accounts in bulk. Import a CSV file of new users, check and correct the rows, and BatchBaker creates each account in the OU for its department, gives it a generated temporary password, and produces a PDF with the login details to hand to the user.

It works in any domain with the expected OU layout: the domain is detected from the computer, and the department-to-OU mapping is set in `appsettings.json`.

## Features

- **CSV import with preview.** Every row is checked before anything is saved, and invalid rows are shown with the reason they were rejected.
- **Fix rows in place.** Double-click a rejected row to correct it, so you don't have to edit the CSV and import it again.
- **Add a single user** without a CSV file.
- **Department-based placement.** Each user is created in the OU that matches their department.
- **Generated passwords.** Every user gets a new temporary password. A change at first login is forced by default, and can be turned off per user in the editor.
- **Login PDFs.** One PDF per created user, with their username and temporary password.
- **Department overview.** Browse the current batch, everyone BatchBaker has created, or the live accounts in Active Directory, by sector and department.
- **Activity log** of imports, edits, saves and every account created or failed.

The user interface is in Danish.

## Requirements

- Windows 10 or 11, on a machine joined to the domain.
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (the installer checks for it).
- A Windows account with permission to create user accounts and reset passwords in the department OUs. BatchBaker connects to Active Directory as the user running it.

## User guide

[Brugervejledning.md](Brugervejledning.md) is the short user guide in Danish, including the CSV format and the Active Directory settings. It's installed next to the program, along with the Excel template.

## Installation

Run the BatchBaker installer (`BatchBaker.Wizard`). It installs the program and adds shortcuts to the Desktop and Start menu.

Before first use, check `appsettings.json` in the installation folder (see [Configuration](#configuration)).

## Using BatchBaker

### 1. Import a CSV file

Choose **Filer > Importér** and select the CSV file. A preview window opens with every row marked as:

- **Gyldig** (valid), ready to import, or
- **Ugyldig** (invalid), with the reason in the **Årsag** column.

A row is invalid if:

- its username already exists in the database,
- the same username appears more than once in the file, or
- its department doesn't match one of the known departments.

**Double-click an invalid row** to open it in the editor and fix it. The row is checked again when you save it. Then click **Importér gyldige rækker** to bring the valid rows into the main window. Invalid rows are left out. To drop rows instead, select one or several and click **Kassér valgte**.

### 2. Add or edit a single user

- **Filer > Tilføj bruger** opens an empty form for one new user.
- **Double-click a row** in the main window to edit it.

In the editor, the department is chosen from a list, so it can't be misspelled. Username, department and temporary password are required. **Generér** creates a new password. **Tving skift af adgangskode ved første login** is on by default; untick it to skip the forced change for that user, including users from a CSV.

Editing a user who has already been created in Active Directory only changes BatchBaker's own records, not the account in Active Directory.

### 3. Save and create the accounts

Choose **Filer > Gem**. BatchBaker:

1. checks every row again and saves the valid ones to its database,
2. asks whether to create the users in Active Directory, and
3. if you answer yes, creates each user that hasn't been created yet.

For each user, BatchBaker:

- creates the account in the OU for their department,
- sets their name, email, phone number, department and country,
- first checks that the username and logon name aren't already taken in the domain,
- generates a PDF with their username and temporary password.

Each user is handled separately, so one failure doesn't stop the rest. Failed users are marked **Ugyldig** with the reason in **Årsag**. When rejected users exist, **Gem** reopens the review window with just those users, both before saving and after the Active Directory run. Double-click a row to correct it, or select rows and click **Kassér valgte** for true duplicates. **Fortsæt** carries on, and after an Active Directory run only the corrected rows are tried again. **Annullér** keeps every row as it is. Discarding removes the row from BatchBaker only, never an account in Active Directory. Users that were already created are skipped. If something fails after an account is created (for example the PDF), the user stays marked as created and the problem is noted in **Årsag**. If the connection to Active Directory fails before the run starts, no users are attempted and you get one clear message.

When it's done, BatchBaker shows a summary and opens the folder with the PDFs.

### Browsing users by department

The panel on the left chooses what the main list shows:

- **Denne batch**: the users you're importing and editing. This is the only view you can edit, and **Gem** always works on it.
- **Oprettet med BatchBaker**: every user BatchBaker has saved, from all imports, and whether each was created in Active Directory.
- **Active Directory**: the user accounts actually in the department OUs right now, read live from the domain, including users not created by BatchBaker. **Opdatér** reads them again.

Below it, the tree shows **Alle afdelinger**, the sectors (Data, Business, Energi) and the 10 departments, each with the number of users in the current view. Click a node to show only those users. The sectors come from the OU structure: the OU directly above each department's OU.

### Viewing the log

**Log** in the menu bar shows everything BatchBaker has done, newest first: imports, edits, saves, and each account and PDF created or failed, with the reason.

### Administrér

- **Åbn PDF-mappe**: opens the folder the login PDFs are saved in.
- **Åbn datamappe**: opens the folder with BatchBaker's database and `crash.log`.
- **Kontrollér domæne og OU'er**: checks again that the computer is in a domain and that every department OU exists. The same check runs at startup.

## CSV format

The easiest way to make a file is the Excel template **`BatchBaker-skabelon.xlsx`**: fill in the **Brugere** sheet and save it as **CSV UTF-8**. The template has a dropdown for departments and checks usernames as you type. Its **Vejledning** sheet explains each column.

The first line is a header and is skipped. Columns are separated by commas or semicolons (Excel uses semicolons on Danish setups; BatchBaker detects which from the header), in this order:

| # | Column | Notes |
|---|---|---|
| 1 | First name | |
| 2 | Last name | |
| 3 | Username | Becomes the account's logon name. Must be unique. |
| 4 | Email | |
| 5 | Department | Must be one of the departments below. |
| 6 | Country | Defaults to `Denmark` if empty. |
| 7 | Title | Not used, but the column must be there. |
| 8 | Phone number | |
| 9 | Password | Optional and ignored. Every user gets a generated password. |

Example:

```csv
Fornavn,Efternavn,Brugernavn,Email,Afdeling,Land,Titel,Telefonnummer,Adgangskode
Anna,Jensen,ajensen,anna.jensen@example.dk,Økonomi,,Elev,12345678,
Mads,Nielsen,mnielsen,mads.nielsen@example.dk,IT Support,Denmark,Elev,87654321,
```

Save the file as **CSV UTF-8** so that Æ, Ø and Å are read correctly. A field containing the separator must be in double quotes (Excel does this automatically). Empty rows are skipped, and missing columns at the end of a row count as empty.

### Departments

Department names are matched case-insensitively, and `-` and `_` count as spaces (so `it-support` matches `IT Support`).

| Department | OU |
|---|---|
| Programmør | `OU=Data-Prog,OU=Data,…` |
| Infrastruktur | `OU=Data-Infra,OU=Data,…` |
| IT Support | `OU=IT-Support,OU=Data,…` |
| Cyber Security | `OU=Data-CYBER,OU=Data,…` |
| Administration | `OU=Administration,OU=Business,…` |
| Handel | `OU=Handel,OU=Business,…` |
| Økonomi | `OU=Økonomi,OU=Business,…` |
| Automatik | `OU=Automatik,OU=Energi,…` |
| Elektronik | `OU=Elektronik,OU=Energi,…` |
| Elektriker | `OU=Elektriker,OU=Energi,…` |

All OUs are under `OU=Afdelinger,OU=SKP` in the domain.

## Passwords

Every user gets a generated 12-character temporary password: 8 letters (at least one uppercase and one lowercase), 2 digits and 2 symbols, in random order. Characters that are easy to confuse on paper (`0`/`O`, `1`/`l`/`I`) are never used.

Users must change the password the first time they log in.

## Login PDFs

One PDF is created for each user that is created in Active Directory. It contains their username, temporary password and a reminder to change it.

PDFs are saved to a `PDF` folder on the Desktop of the person running BatchBaker, in a new subfolder for each run (for example `PDF\2026-10-27 14.30.05\`). The files are named `<username>_credentials.pdf`.

The PDFs contain passwords. Hand them out and delete them once the users have logged in.

## Configuration

Settings are in `appsettings.json` next to `BatchBaker.exe`:

```json
{
  "ActiveDirectory": {
    "Domain": "",
    "Departments": {
      "Data_ITSupport": "OU=IT-Support,OU=Data,OU=Afdelinger,OU=SKP",
      "...": "..."
    }
  },
  "PDF": {
    "FilePath": ""
  }
}
```

- **`ActiveDirectory:Domain`**: the domain users are created in. Leave it empty to use the domain the computer is joined to, or set it (for example `"SOC.Viborg"`) to override. Also used for each user's logon name (`username@SOC.Viborg`).
- **`ActiveDirectory:DomainControllers`**: the Domain Controllers to use, tried in order until one answers, for example `["dc01.firma.local", "dc02.firma.local"]`. Leave it empty to let Windows choose. It is re-checked at startup and before each run. On a computer that isn't domain-joined, also set `Domain`.
- **`ActiveDirectory:Departments`**: the OU for each department, without the domain part. The domain's `DC=...` is added automatically, so `OU=IT-Support,OU=Data,OU=Afdelinger,OU=SKP` becomes `OU=IT-Support,OU=Data,OU=Afdelinger,OU=SKP,DC=SOC,DC=Viborg`. A full path that already ends in `DC=...` is used as it is. Change these if the OU structure changes. BatchBaker warns at startup if any of them can't be found.
- **`PDF:FilePath`**: where to save PDFs. Leave empty for the `PDF` folder on the Desktop, or set a folder such as `"D:\\BatchBaker\\PDF"`. Environment variables like `%USERPROFILE%` can be used.

Restart BatchBaker after changing the file.

## Connecting to Active Directory

BatchBaker runs locally and connects as the signed-in Windows user, through `System.DirectoryServices` and `System.DirectoryServices.AccountManagement` (LDAP). It never asks for or stores an administrator password.

The account needs permission to create and read user objects and reset passwords in the department OUs. Delegating those rights on `OU=Afdelinger` is enough; it doesn't have to be a Domain Admin.

Connection, permission, password policy and duplicate-name errors are shown in Danish in the UI. The **Log** keeps AD's full technical error, including the extended code.

## Files and data

BatchBaker keeps its data in `%LocalAppData%\BatchBaker` (for example `C:\Users\<name>\AppData\Local\BatchBaker`). Each Windows user has their own copy.

- **`batchbaker.db`**: a SQLite database with imported users and the activity log. It's created on first start and updated automatically when a new version needs new columns.
- **`crash.log`**: details of any unexpected error. If it can't be written there, it goes to `%TEMP%\BatchBaker-crash.log` instead.

Older versions kept the database in a `data` folder next to `BatchBaker.exe`. If one is found there, it's copied to the new location the first time BatchBaker starts.

## For developers

### Building

The project targets .NET 10 (WPF). With the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed:

```
dotnet build
dotnet run
```

Or open `BatchBaker.slnx` in Visual Studio 2026.

**Debug builds skip the startup OU check**: `ADService.DoesOuExist` always returns `true`, so the app can be run without the domain (on a computer that isn't domain-joined, it shows one warning at startup). Creating users still needs a real domain. Use a Release build to test against Active Directory.

### Publishing and the installer

- **Folder publish:** the `FolderProfile` publish profile builds a Release version to `..\BBakerBuilds`.
- **Installer:** the `BatchBaker.Wizard` setup project (`..\BatchBaker.Wizard\BatchBaker.Wizard.vdproj`) builds an `.msi`. It needs the **Microsoft Visual Studio Installer Projects** extension.

### Project layout

| Path | Contents |
|---|---|
| `App.xaml.cs` | Startup, dependency injection, error handling and `crash.log` |
| `MainWindow.xaml(.cs)` | Main window: import, save, Active Directory creation |
| `ImportPreviewWindow.xaml(.cs)` | CSV preview before import |
| `Editor.xaml(.cs)` | Add or edit a single user |
| `LogViewerWindow.xaml(.cs)` | Activity log viewer |
| `MainWindowHelpers.cs` | CSV parsing, validation, password generation |
| `Services/ADService.cs` | Department-to-OU lookup, OU checks and reading users from AD |
| `Services/PDFService.cs` | PDF generation (QuestPDF) |
| `Configuration/` | Classes bound to `appsettings.json` |
| `Data/` | Database context and automatic schema updates |
| `Models/` | `People`, `ImportSet`, `ActivityLog`, and `UserOverview` (rows for the overview views) |

To add a department, add a property with a `[CSVValue(ColumnName = "...")]` attribute to `ADDepartments` in `Configuration/ActiveDirectory.cs`, and its OU to `appsettings.json` under the same name.

### Libraries

- [Entity Framework Core](https://learn.microsoft.com/ef/core/) with SQLite: local database
- [QuestPDF](https://www.questpdf.com/) (Community license): PDF generation
- `System.DirectoryServices.AccountManagement`: Active Directory
- `Microsoft.Extensions.Hosting`: configuration and dependency injection
