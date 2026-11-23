# Passkey Manager

**See and remove the passkeys stored in Windows Hello.**

Passkey Manager is a small portable Windows app for people whose sign-in prompt has grown into a long list of
identical-looking entries. It reads the passkeys that Windows Hello keeps on your PC, tells you which website each
one belongs to, and lets you delete the ones you no longer want.

- Portable: one `.exe`, no installer, no network access, no telemetry
- Everything it saves is created **next to the `.exe`**
- Light and dark mode

![Passkey Manager Dark Dashboard](Passkey%20Manager%20Dark%20Dashboard.png)

## Why this exists

Over time Windows Hello can collect many passkeys for the same site (for example every time you re-register one),
and the Windows Security prompt just lists them all as "Admin", "Admin", "Admin"... Windows 10 has no screen for
managing them, and the entries Windows stores don't contain a readable site name. Newer Windows 11 builds have a
Passkeys page under *Settings → Accounts*; this tool covers the machines that don't, and shows more detail.

## Features

- Lists every passkey stored in Windows Hello (the `NGC` key store), grouped by website
- **Names the sites.** Windows only stores a hash of each site's name; the app matches those hashes against a
  built-in list of services that support passkeys, sites you add, and (optionally) your browser bookmarks
- **Details window** for each passkey: site hash, user ID as text, hex and Base64URL, key name, how many other
  passkeys exist for the same site, when this app first saw it, Windows' own `certutil` output for the key, and a
  best-effort look at related files
- **Delete** the passkeys you check. A record of what was removed is saved first, and nothing is deleted if that
  record can't be written
- Search box, "check all for this site", CSV export of the list, light/dark theme

## Requirements

- Windows 10 or Windows 11, 64-bit
- Administrator rights (the app asks for them; Windows needs them to delete these keys)
- To **build**: the [.NET SDK](https://dotnet.microsoft.com/download) (10, or 8 with a one-line change, see below).
  The published `.exe` is self-contained and needs nothing installed

## Getting started

### Build

```bat
git clone <this repository>
cd <repository folder>
build.bat
```

`build.bat` runs `dotnet publish` and puts a single `PasskeyManager.exe` in the `dist` folder. You can also run
the command yourself:

```bat
dotnet publish src\PasskeyManager\PasskeyManager.csproj -c Release -o dist
```

- Using only the .NET 8 SDK? Change `net10.0-windows` to `net8.0-windows` in
  `src/PasskeyManager/PasskeyManager.csproj`.
- 32-bit Windows or ARM? Change `win-x64` in the same file to `win-x86` or `win-arm64`.

The build is unsigned, so Windows SmartScreen may warn the first time you run it.

### Releases

Pushing a version tag builds and publishes a release automatically (GitHub Actions, `.github/workflows/release.yml`):

```bat
git tag v1.0.0
git push origin v1.0.0
```

The workflow runs the tests, publishes the self-contained `PasskeyManager.exe`, and creates a GitHub release with
`PasskeyManager-v1.0.0.zip` (the exe plus `LICENSE` and `NOTICE.md`), the bare exe, and `SHA256SUMS.txt`. A tag
containing a hyphen, such as `v1.1.0-beta`, is marked as a pre-release. The tag also sets the version of the exe.
Every push and pull request is built and tested by `.github/workflows/ci.yml`, which keeps the exe as a downloadable
build artifact.

### Use

1. Start `PasskeyManager.exe` and accept the administrator prompt. It lists your passkeys.
2. Rows marked *Unknown site* have no name yet. Click **Import bookmarks** to add the sites from your Brave, Chrome
   and Edge bookmarks, and/or **Add sites…** to type or paste websites (full URLs are fine).
3. **Double-click a row** (or use **Details**) to see everything known about that passkey.
4. Tick the passkeys you want to remove. Right-click a row for *Check all for this site*.
5. Click **Delete checked…**, read the confirmation, and confirm.

After deleting, sign in to the website another way (for example with a password) and register a fresh passkey if
you still want one. The website keeps its own record, so removing a passkey here never deletes your account there.

> **Tip:** if a site shows five passkeys and you can't tell which one works, delete all five and create one new
> passkey. Windows doesn't say which is which.

## Where your data is stored

Everything lives in the folder that contains `PasskeyManager.exe`:

| Path (next to the exe) | What it is | Created when |
| --- | --- | --- |
| `backups\deleted-YYYYMMDD-HHMMSS.json` | A record of the passkeys removed in one delete action (site, IDs, key name) | Before every deletion |
| `seen.json` | When this app first saw each passkey | First scan |
| `sites.txt` | Websites you added (one per line) | When you add a site |

The only other file the app ever creates is the CSV you export, and only where you choose in the Save dialog.
The program works out its own location, so this is the same whether you start it from a shortcut, another
folder or a different working directory. To reset everything, delete those files. To uninstall, delete the folder.

Keep the program in a folder you can write to. If the `backups` folder can't be written, deletion is refused (by
design) and the app tells you where it tried to write.

## How it works

Windows keeps Windows Hello passkeys in the `NGC` key store. The app uses the built-in `certutil` tool:

| Action | Command |
| --- | --- |
| List | `certutil -csp NGC -key` |
| Details | `certutil -v -csp NGC -key <key name>` |
| Delete | `certutil -csp NGC -delkey <key name>` |

Each passkey shows up with a key name shaped like this (observed behavior, not documented by Microsoft):

```
FIDO_AUTHENTICATOR//<64 hex characters>_<user ID in hex>_<more>
```

- The 64 hex characters are the **SHA-256 of the website's relying-party ID** (usually its domain). A hash can't be
  reversed, so the app hashes known domains and compares. A passkey may be bound to a parent domain, so each
  listed domain and its parent domains are checked.
- The user ID is chosen by the website, often random bytes. It is shown as text when readable and as hex otherwise.
  The friendly name Windows shows in its prompt isn't part of the key name, so the app can't display it.

"Import bookmarks" only reads the `Bookmarks` files of Brave, Chrome and Edge to collect website addresses. The
Details window also reads (never writes) Windows Hello's protected folder looking for related files. Nothing is
sent anywhere.

## Limitations

- **Passkeys can't be backed up or restored.** Their private keys are protected by Windows Hello (often the TPM)
  and can't be exported. The record in `backups\` only tells you what you removed.
- **No creation or last-used time.** Windows doesn't expose these for these keys. The app instead remembers when
  *it* first saw each passkey; keys that already existed on the first scan show "on/before" that date.
- Only passkeys in Windows Hello are listed. Passkeys held by a password manager, your phone or a hardware security
  key are not.
- The key-name format and `certutil` output are undocumented and may change in future Windows versions.

## The built-in site list

The list lives in [`src/PasskeyManager/Data/passkey-sites.csv`](src/PasskeyManager/Data/passkey-sites.csv) and is
embedded into the program at build time. Each line is one service:

```csv
domain,aliases,name,category,source
github.com,,GitHub,developer,https://docs.github.com/en/authentication/authenticating-with-a-passkey/about-passkeys
```

| Column | Meaning |
| --- | --- |
| `domain` | The domain the passkey is bound to |
| `aliases` | Other domains of the same service, separated by spaces (optional) |
| `name` | Display name |
| `category` | Free-form grouping label |
| `source` | Official documentation showing passkey support (optional, `https` only) |

**Inclusion rule:** only services that support passkeys. A service is listed if it supports passkey sign-in, or
passkeys as a second factor with documentation that is explicitly about passkeys. Services that only support
hardware security keys, U2F or generic two-factor authentication are left out on purpose, as are services that can
*store* passkeys but don't use them to sign you in to their own service.

To add a service, add a row (no duplicates; parent domains such as `google.com` for `mail.google.com` are matched
automatically, so list a subdomain only if the service really binds passkeys to it), include a documentation link,
and run the tests. You can also add sites just for yourself with **Add sites…**.

## Project layout

```
src/PasskeyManager/        the Windows Forms app
  Core/                    UI-free logic: certutil wrapper, site matching, storage paths, exports
  Ui/                      windows, theme
  Data/passkey-sites.csv   built-in site list
tests/PasskeyManager.Tests/ xUnit tests for Core (run on any OS)
build.bat                  publishes dist\PasskeyManager.exe
.github/workflows/         CI build and tests, release on version tags
```

## Running the tests

```bat
dotnet test tests\PasskeyManager.Tests
```

(With only the .NET 8 SDK, change `net10.0` to `net8.0` in `tests/PasskeyManager.Tests/PasskeyManager.Tests.csproj`.)

The tests use xUnit, so they also appear in Visual Studio's Test Explorer (*Test > Test Explorer*) and run with
`dotnet test`. They don't touch your passkeys or the real files next to the program: each test
works in a temporary folder. They compile the `Core` sources directly, so they run on any OS. They cover:

- `certutil` output parsing (valid, malformed, duplicate and mixed lines)
- user ID formatting (text, binary, hex, Base64URL) and the display values of a passkey
- the built-in site list: parsing, plus checks that every entry is well formed, has no duplicates, uses an `https`
  source and resolves to itself
- site matching: hashing, parent domains, country suffixes, URL normalization, added sites and `sites.txt`
- bookmark parsing, the deletion record and CSV export, the first-seen history, and the search of the Windows Hello folder
- that everything the app saves goes next to the executable and nowhere else

Each test is named as a sentence (for example "Subdomain also matches its parent domain"). Every check that passes
is also written to the test output with what was checked and the value seen, for example:

```
  ✓ e.RpIdHash = "a379a6f6eeafb9a55e378c118034e2751e682fab9f2d30ab13d2125586ce1947"
  ✓ hit.IsDirectory is true
  ✓ () => ParseText(...) threw FormatException: "Line 3: the domain is empty."
```

A failed check prints the expression that failed with the expected and actual values. Tests that loop over many
items show the first 25 checks and a count of the rest.

- Visual Studio: select a test in Test Explorer and open its **Output** link in the details pane.
- Console, with the output of passing tests included, and running a subset:

```bat
dotnet test tests\PasskeyManager.Tests --logger "console;verbosity=detailed"
dotnet test tests\PasskeyManager.Tests --filter SiteResolver
```

## Troubleshooting

- **"Access denied" when deleting**: run the program as administrator (it requests this automatically).
- **Everything shows as "Unknown site"**: use *Import bookmarks* or *Add sites…* and type the exact domain from the
  address bar. A passkey is bound to one name, so `example.com` and `www.example.com` can differ.
- **"Could not write the deletion record"**: the folder holding the program isn't writable. Move the program to a
  folder you can write to.

## Disclaimer

Deleting a passkey is permanent. Use this tool at your own risk, and make sure you can still sign in to an account
another way before removing its passkeys. This project is independent and not affiliated with or endorsed by
Microsoft or any listed service.

## License

[MIT](LICENSE). Third-party notices are in [NOTICE.md](NOTICE.md).
