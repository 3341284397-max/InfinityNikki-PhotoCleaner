# Infinity Nikki · Local Photo Cleanup Assistant

Finds the photos you already **deleted inside the game but which are still sitting in your `ScreenShot` folder**, and moves them into a separate folder for you to confirm.

**It only identifies and moves. It never deletes a photo.**

English | [简体中文](README.zh-CN.md)

**[→ Try it online](https://3341284397-max.github.io/InfinityNikki-PhotoCleaner/)**　·　**[↓ Download the exe](https://github.com/3341284397-max/InfinityNikki-PhotoCleaner/releases/latest/download/NikkiPhotoCleaner.exe)**　·　the web version installs nothing — pick a folder and go.

**In China and GitHub is slow?** This repo is mirrored on Gitee: [gitee.com/lyrins/InfinityNikki-PhotoCleaner](https://gitee.com/lyrins/InfinityNikki-PhotoCleaner) — [download the exe there](https://gitee.com/lyrins/InfinityNikki-PhotoCleaner/releases/download/v1.9.0/NikkiPhotoCleaner.exe) (direct link, no login needed).

A third option: [Baidu Wangpan](https://pan.baidu.com/s/15jaYZvAnNP8VuFZwOQsp2w) — code `1111`. It holds the full bundle (exe + Chinese readme + screenshots + the dependency-free fallback), which is the one to use when neither code host is reachable.

No game folder handy? [Open the demo](https://3341284397-max.github.io/InfinityNikki-PhotoCleaner/?demo=1) — it runs on generated sample photos.

![Main window](docs/screenshots/main-en.png)

---

## The problem

Deleting a photo in-game usually only removes the album reference — the file itself stays in `ScreenShot`.
Over time that folder fills up with photos you can no longer see in game, eating gigabytes you don't dare reclaim
by hand because you can't tell which files are still in use.

This tool makes the question answerable: it tells you which local files are no longer in the album,
moves them aside, and lets you press Delete yourself.

---

## Four verdicts + two tags

A **verdict** is what the program concluded; a **tag** is something stuck onto the photo.
They are **two columns, not one thing**: the same photo can be judged "Deleted" and be tagged
"Ignored" by you, and the list shows `Deleted` `Ignored` side by side.

| Verdict | Meaning | Colour |
|---|---|---|
| **In album** | File name is still listed in the game's album cache; visible in game | green |
| **Deleted** | Was seen in the album before, and is gone from the cache now | clay |
| **Unsure** | Not in the album cache, and the tool has never seen it (old files, or first scan after install) | amber |
| **Too new** | Would be Deleted/Unsure, but modified less than 3 days ago — recent screenshots are never auto-selected | grey |

| Tag | Set by | Meaning |
|---|---|---|
| **Ignored** | you | Leave this one alone. Excluded from ticking, counts and moving. Removable at any time |
| **Imported** | the tool | The file was copied in, not taken by the game (creation time well after modification time) |

Evidence comes from two places:

- **Album cache list** — file names under `X6Game/Saved/GamePlayPhotos/*/NikkiPhotos_*`
- **Local ledger** — `album-ledger.tsv`, written by the tool, recording "this file was once in the album"

"Deleted" requires **both** conditions: was in the album, and is not any more.
**Nothing is ever pre-ticked** — ticking is always your action, never the tool's.

**A tag never rewrites the verdict.** Clear "Ignored" and the photo drops straight back to
what it was (in album stays in album, too new stays too new) — no rescan, and it does not
fall into "Deleted".

---

## How it works

### What a scan actually does

The tool reads two places and writes to neither of them: **the file names in the album cache**, and **the files in `ScreenShot`**. Then it asks three questions of every local file:

```
X6Game/ScreenShot/xxx.jpeg
  │
  ├─ ① Is the name in the album cache? ──── yes ──→ [In album] visible in game, leave it
  │
  └─ no ↓
       ├─ ② Does the ledger say it was once in the album? ── yes ──→ [Deleted]
       │                                        you deleted it in game, the file is still here
       └─ never recorded ↓
            └─→ [Unsure] the tool has no history for it — deleted, or just not synced yet
```

The third question ("too new") stacks on top of ① and ②: anything not in the album and modified less than three days ago is demoted to "Too new" and left alone.

### The ledger: why the first run flags nothing

The only way to tell "you deleted this in game" is **comparison**: it was in the album, and now it isn't. On the first run the tool has no "before" — it has never seen any photo.

So the first scan does one thing: **it builds a ledger**. Every file currently in the album gets written to `相册台账.tsv` / `album-ledger.tsv` (name, first seen, last seen, size). Everything not in the album can only be "Unsure" that round.

Delete a few photos in game, hit Rescan, and now "Deleted" appears.

**The ledger also stores file size.** File names get reused on disk: when a "Deleted" photo is replaced by a new screenshot with the same name, the size is almost certainly different. On a size mismatch the old record is discarded and the file is judged from scratch — otherwise the new photo would inherit "was once in the album" and be flagged Deleted, ready to be swept away by one click.

### Imported photos: backups copied back from a USB stick

Copy an old backup back into `ScreenShot` and the file names match the ledger exactly, so they get flagged "Deleted" — even though you never deleted them in game.

The tool spots them by **timestamp**:

> A game screenshot is finished the moment it is written — creation time and modified time are the same.
> A file copied in from a USB stick, a cloud drive or another PC keeps its **modified time**, but gets a **creation time of the moment it was copied**.

So `creation − modified > 1 hour` is flagged **Imported**. The test is one-sided: copying only ever pushes creation later than modification; editing in an image app does the opposite, so it cannot false-positive.

"Imported" is a **tag**, not a status — it never overwrites the verdict. The list shows both badges at once, because you need to know *what it would have been* as well as *why it is excluded*. Imported photos are never ticked, never counted as pending, and never picked up by Select all.

The test is not perfect: some copy tools, and moves on FAT32, preserve the creation time. That is exactly why manual tags exist.

### Manual tags: when the program cannot be sure

"Ignore" in the toolbar is the final say. Tagged photos get an extra **"Ignored" tag** — the verdict stays exactly as it was, and the list shows both side by side (`Deleted` `Ignored`). They leave ticking, counts and moving entirely; "Un-ignore" removes the tag and the photo returns to its original verdict at once, with no rescan.

Tags live in `user-marks.tsv`, deliberately separate from the ledger — the ledger is *what the program observed* (safe to delete and rebuild), tags are *what you said* (only you change them). The key is **file name + size**: same name with a different size means a different photo on disk, and the old tag does not apply. The direction is intentional — better to miss one ignore than to ignore a photo that should have been cleaned.

Before every change the previous version is kept as `user-marks.tsv.bak`.

### The review folder: things you moved out can come back

Two pills above the list — `Screenshots` and `Review` — switch which folder you are looking at.
The review view reuses **the same list and the same preview pane**, fed from
`NikkiCleaner_ToDelete`:

- Every moved-out photo is visible, can be enlarged with a double click, and can be ticked
- Once ticked, the primary button at the bottom turns into "Restore (N)" and moves them back **one by one**
- A file already present in the screenshot folder with the same name and size is skipped,
  not duplicated; same name but different content gets renamed so both survive
- Each restore is crossed off the move record, so the older batch-restore button never moves it twice
- The review folder is deleted automatically once it is empty — no empty shell left in `ScreenShot`

There is no verdict and no tag to set in this view (the photos are no longer in the screenshot
folder), so the tagging buttons are greyed out on purpose.

![Review-folder view](docs/screenshots/pending-en.png)


### What this program never does

- **It never deletes a file.** There is no delete call in the codebase; "move" is `File.Move`. The actual deletion is always you pressing Delete in Explorer.
- **It never goes online.** No network calls, no account, no telemetry.
- **It never guesses.** Anything uncertain becomes "Unsure", and "Unsure" is never auto-selected.

---


## Requirements

Three forms, pick one. None of them needs an extra runtime installed.

| Form | OS | Dependency |
|---|---|---|
| **Desktop exe** (recommended) | Windows 10 version 1903+ / Windows 11, 64-bit | .NET Framework 4.8 — **shipped with Windows since 10 1903**, nothing to install |
| **PowerShell** | Windows 10 / 11 | **Zero dependencies**, only the built-in PowerShell 5.1 |
| **Web** | Chrome / Edge 86+ on desktop | None. The browser reads the folder locally |

Display: the window defaults to 1180×780 and scales with 100% / 125% / 150% DPI settings.

### About the unsigned binary

The exe is not code-signed. Windows may block it in one of two ways:

| Mechanism | Where | What you see | What to do |
|---|---|---|---|
| **SmartScreen** | Win10 and Win11 | "Windows protected your PC" | *More info* → *Run anyway* |
| **Smart App Control (SAC)** | Win11 clean installs only, on by default | Blocked outright, **no override button** | Use the PowerShell or web version |

**Why there is no signature.** SAC trusts only certificates chaining to a CA in the
Microsoft Trusted Root Program, and it evaluates that in the cloud — it does not consult
the local certificate store. A **self-signed certificate is therefore useless against SAC**,
even if you manually install the root into Trusted Root Certification Authorities.
The options that actually work:

| Option | Cost | Availability for individuals |
|---|---|---|
| Azure Artifact Signing (formerly Trusted Signing) | $9.99/month | Individuals: USA and Canada only |
| OV / EV code signing certificate | $150–400/year | Identity validation plus a hardware token; max 460-day validity, renew and re-sign on expiry |
| Microsoft Store listing | $19 one-off | Microsoft re-signs the package, bypassing both mechanisms |

That cost is out of proportion for a free open-source utility, so this project ships unsigned
and offers the **zero-dependency PowerShell version** as the reliable alternative.

> Windows 10 has no SAC feature at all — Win10 users only ever hit the first row.

---

## Usage

### 1. Desktop exe

1. Download `NikkiPhotoCleaner.exe` and double-click it
2. *Change folder* → pick `X6Game\ScreenShot` (the game root also works, it will find the right subfolder)
3. *Rescan*
4. Review the pre-ticked **Deleted** items. The strip above the list shows `N of M selected`
   plus a *Select all* button — no need to click a hundred circles one by one. `Ctrl+A` works too
5. *Move to review folder* — files are **moved**, not deleted
6. Open that folder, check, then delete them yourself

Before anything moves, a confirmation window lists every single file (the list scrolls, so
hundreds of photos are all there):

![Confirm move](docs/screenshots/move-en.png)

### 2. PowerShell, zero dependency

Four double-click `.bat` launchers live in `package/`: scan, move, restore, and clean-up-tool-records.
`src/scripts/PhotoSync.ps1` can also be called directly; see the comment header for parameters.

### 3. Web

Open `web/index.html` in Chrome or Edge and pick the folder.
No game folder? Click *See a demo first*, or open with `?demo=1`.

![Web version](docs/screenshots/web-en.png)

Browsers are not allowed to move local files from a page, so the web version hands
that one step back to you. Tick the photos, press *Move photos*, and the dialog offers
three routes — pick whichever works on your machine:

**1. Download the script and double-click it** (easiest)
You get a `.bat` of a few KB. Drop it into `X6Game\ScreenShot` and run it. If you paste
the full screenshot-folder path into the dialog first, the script runs from anywhere.

**2. Copy the commands into a console window** (use this when the script gets blocked)
Nothing is written to disk, so there is no file to block. Open Command Prompt from the
Start menu, right-click to paste, press Enter. Each photo is one `if exist … move …`
line — name clashes become `_1`, nothing is overwritten — and a leading `chcp 936`
keeps Chinese paths readable.

**3. Copy the prompt and hand it to your AI assistant** (last resort)
Give the prompt from the dialog to an assistant that can touch files (Copilot, Claude,
ChatGPT…). It states the source folder, target folder, the file list, and the rules:
move only, never delete, do not overwrite.

All three can be previewed first via *Preview the script first*.

![Moving the photos](docs/screenshots/web-script-en.png)

A few details:

- **Why you paste a path.** Browsers never reveal a folder path to the page — only the
  folder name. Paste the full path once in the dialog (click the address bar in Explorer
  to copy it) and it is remembered; the script and the commands then locate the folder
  themselves instead of asking you to drag it in
- The script first looks for one of the listed photos next to itself, then in a
  `ScreenShot\` subfolder. Only if neither works does it ask you to drag the folder in
- Photos that are already gone count as *skipped*, not failed. If nothing moves, no
  empty folder is left behind
- The Chinese script is written as **GBK**. `cmd` reads a batch file byte by byte in the
  console code page, so a UTF-8 Chinese batch file misparses lines — in testing it ran
  the "delete yourself?" prompt as a command
- **If the script gets blocked.** A `.bat` downloaded from a browser carries the
  mark-of-the-web, and SmartScreen or a security suite may refuse to run it at all —
  even *More info → Run anyway* is sometimes not offered. It is not the contents
  (a few dozen `move` lines), it is the extension. The easy way around it is route 2
  above: no file on disk, nothing to block. Routes 3 and the exe are there as backup

> If you would rather move files with a single click, use the exe — it moves locally and
> can restore in one click. The web version's value is installing nothing at all, and
> never tripping Smart App Control on an unsigned binary.

---

## Safety guarantees

Designed so that a mis-click still cannot lose a photo:

- **No deletion.** There is no code path that deletes a photo; "move" is a plain `File.Move`
- **Undoable.** Every move writes `last-move.txt` with source and destination;
  *Restore last move* puts everything back
- **Record cleanup never touches photos.** It only clears the tool's own thumbnail cache, reports and ledger.
  Risky items (ledger, move record) are unticked by default and require a second confirmation
- **No network.** Everything is local; no account, no upload
- **Separate data folder.** All intermediate files stay inside the tool's own data directory

---

## Where files go

**On first launch the app asks once**, because the default (next to the exe) is unfriendly
to anyone who keeps the exe on the desktop — it would leave a permanent folder there. Three choices:

| Choice | Data lives in |
|---|---|
| Next to the game folder (recommended) | The **parent** of the segment named `InfinityNikki` in your screenshot path (`<…>\InfinityNikki\X6Game\ScreenShot`) — usually `InfinityNikki_APP`, side by side with the `InfinityNikki` folder |
| Same folder as the exe | The exe's own folder (the default up to 1.7) |
| Pick a folder myself | Anywhere |

The answer is stored in `%APPDATA%\NikkiPhotoCleaner\location.txt`.
It cannot live inside the data folder itself: the location is the thing being recorded, so that would be self-referential.

Upgrading users **never see this dialog** — if a data folder already sits next to the exe, it is adopted silently and remembered.

![Choosing where the data goes on first launch](docs/screenshots/data-en.png)


Inside the data folder:

| Item | Chinese UI | English UI |
|---|---|---|
| Data folder | `无限暖暖清理助手_使用数据` | `NikkiCleaner_Data` |
| Review folder | `无限暖暖清理助手_待清理照片` | `NikkiCleaner_ToDelete` |
| Album ledger | `相册台账.tsv` | `album-ledger.tsv` |
| Your tags | `用户标记.tsv` (auto `.bak` before each change) | `user-marks.tsv` |
| Reports | `网页报告/` | `reports/` |
| Settings | `设置.txt` | `settings.txt` |
| Move record | `最近一次移动记录.txt` | `last-move.txt` |

**The folder names are resolved once at startup**, by checking which one already exists.
Switching UI language therefore never creates a second set of folders, and the ledger and move record stay intact.
If both exist (an upgraded install), the Chinese one wins.

### Move, delete, uninstall

Three things live behind **Data location** in the top-right corner:

- **Move** — copies to the new location, **verifies file count and total bytes**, then deletes the old
  folder. If the check fails, nothing is deleted — better to fail than to leave you with half the data.
  Anything still held open by another program is queued and removed on the next launch.
  Existing files are never overwritten, and a location that already holds a data folder is refused,
  because merging two ledgers would scramble the "deleted in game" verdicts
- **Delete app data** — removes the folder **entirely** (the folder itself, not just its contents),
  clears the `location.txt` record, and **exits the app**. The next launch asks where to keep data
  again — that is your proof it worked. Photos are untouched; if the review folder still holds
  some, you are told

![Data location](docs/screenshots/data-manage-en.png)

Why deleting data has to exit the app: files held open by the running process never come off
cleanly. Older builds deleted the folder and then rescanned, and the rescan's very first act was
writing the ledger — so the folder reappeared holding nothing but an empty ledger, which looked
like "deleted, but not really".

There is no installer, so there is **no uninstaller**. Two ways to clean up completely:

1. Click **Delete app data** (removes the folder and the location record, then exits), then delete the exe
2. Delete three things by hand: the data folder, the review folder, and
   `%APPDATA%\NikkiPhotoCleaner\location.txt`
Forgot where your data went? Run the exe once and open **Data location** — it shows the full path.

---

## Bilingual UI

Switch language from the top-right button. English UI uses Segoe UI and English folder names;
Chinese UI uses Microsoft YaHei UI and Chinese folder names. The choice is persisted in the settings file.

---

## Build from source

Requires the .NET SDK.

```bash
cd src/app
dotnet build -c Release
# output: bin/Release/net48/NikkiPhotoCleaner.exe (single file, ~100 KB)
```

## Repository layout

```
src/app/        C# WinForms sources (Program.cs / DataLocation.cs / DataDialogs.cs / Theme.cs / L10n.cs / Config.cs / RecordCleaner.cs)
src/scripts/    Zero-dependency PowerShell version (lang/PhotoSync.en.ps1 holds the English strings)
src/test/       Regression host (net8); run-tests.sh covers 27 cases
web/            Static web version (index.html / styles.css / app.js)
package/        Four double-click .bat launchers, in Chinese and English
src/pack/       Builds the share package for cloud drives (exe + 使用说明.txt + screenshots)
docs/           Screenshots
```

## Tests

```bash
cd src/test && ./run-tests.sh
```

27 cases. Covers the verdict state machine, the "too new" threshold, ledger I/O, move and restore,
record cleanup, that switching language never splits the data folder (12, 13), imported-photo detection
and tag persistence (14–16), that a same-name/different-size file invalidates its ledger entry (17),
the stored data-location record and its fallback when stale (18–20), the game-sibling path derivation (21),
an end-to-end run proving the data does not land back next to the exe (22),
**a tag never rewriting the verdict** (23 — the 1.1 / 1.2 regression),
the review view listing and selectively restoring (24), no duplicate copies on restore (25),
the folder itself disappearing after "delete app data" (26), and the old folder being removed after a move (27).

## Known limitations

- Detection relies on the game's album cache layout (`Saved/GamePlayPhotos/*/NikkiPhotos_*`).
  A major game update that changes that layout would require adjusting the scanner
- Matching is by file name. If the game renames a file, it will show up as "Unsure"
- The web version itself cannot move files (browser sandbox). It only produces a script,
  commands or a prompt for you to run locally, and none of the three can undo a move —
  use the exe or the scripts under `src/scripts` if you want restore
- Only verified on Windows

## License

Source and issue tracker: <https://github.com/3341284397-max/InfinityNikki-PhotoCleaner> (mirror: [Gitee](https://gitee.com/lyrins/InfinityNikki-PhotoCleaner))

Code is released under [MIT](LICENSE). The application icon (`src/app/app.ico` — Damiao sweeping up
photos) was made by Lyrins and is released under the same MIT license.

The icon ships two framings: head only for 16–48 px, the full illustration at 64 px and above —
the full scene turns to mush at taskbar size, while the head silhouette still reads.

`docs/icon.png` is a 256 px preview; the full-resolution original (1254 px) is not shipped in the
repo. To change the icon, run `python src/app/make-icon.py <art.png>` — it regenerates `app.ico`,
`web/favicon.png` and `docs/icon.png` in one go.

Not affiliated with Papergames or *Infinity Nikki*. A small local utility made by a player.
