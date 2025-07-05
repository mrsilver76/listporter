# ListPorter
_A cross-platform command-line tool (Windows, Linux, macOS) for importing standard or extended `.m3u` audio playlists to Plex Media Server. Supports path rewriting, selective updates and optional mirroring of playlists._

> [!TIP]
> Using iTunes on Windows? [TuneLift](https://github.com/mrsilver76/tunelift) makes it easy to export your iTunes playlists to `.m3u` files.


## 🧰 Features
* 💻 Runs on Windows 10 & 11, Linux (x64, ARM64, ARM32) and macOS (Intel & Apple Silicon).
* 📂 Imports standard or extended M3U audio playlists to Plex.
* 🌐 Works with any Plex server platform (Windows, Linux, NAS, macOS) via the Plex API.
* ✅ Skips importing playlists that haven’t changed.
* 🪞 Mirrors Plex playlists to match imported M3U files (optional)
* 🎯 Fuzzy matching logic (using last three path parts) to improve playlist-to-Plex matching.
* 🔁 Force playlist paths to use / or \ to match your Plex server’s file format (optional)
* 🛠️ Rewrite playlist paths using find & replace rules to match your Plex library (optional)
* 🧭 Prepend a base path to support relative paths in playlists (optional)
* 🧹 Deletes all Plex playlists before import (optional)
* 🔗 Preserves playlist IDs to maintain compatibility with external players (e.g. Sonos)
* 📘 Logs activity to timestamped text files

## 📦 Download

Get the latest version from https://github.com/mrsilver76/listporter/releases.

Each release includes the following files (`x.x.x` denotes the version number):

|Platform|Download|
|:--------|:-----------|
|Microsoft Windows 10 & 11|`ListPorter-x.x.x-win-x64.exe` ✅ **Most users should choose this**|
|Linux (64-bit Intel/AMD)|`ListPorter-x.x.x-linux-x64`|
|Linux (64-bit ARM), e.g. Pi 4 and newer|`ListPorter-x.x.x-linux-arm64`|
|Linux (32-bit ARM), e.g. Pi 3 and older|`ListPorter-x.x.x-linux-arm`|
|Synology DSM|`ListPorter-x.x.x-linux-x64` 📦 Run via Docker / Container Manager|
|macOS (Apple Silicon)|`ListPorter-x.x.x-osx-arm64`|
|macOS (Intel)|`ListPorter-x.x.x-osx-x64`|
|Other/Developers|Source code (zip / tar.gz)|

> [!TIP]
> There is no installer for native platforms. Just download the appropriate file and run it from the command line. If you're using Docker (e.g. on Synology), setup will differ - see notes below.

### Linux/macOS users

- Download the appropriate binary for your platform (see table above).
- Install the [.NET 8.0 runtime](https://learn.microsoft.com/en-gb/dotnet/core/install/linux?WT.mc_id=dotnet-35129-website).
- ⚠️ Do not install the SDK, ASP.NET Core Runtime, or Desktop Runtime.
- Make the downloaded file executable: `chmod +x ListPorter-x.x.x-<your-platform>`

### Synology DSM users

- Only Plus-series models (e.g. DS918+, DS920+) support Docker/Container Manager. Value and J-series models cannot run ListPorter this way.
- For DSM 7.2+, use Container Manager; older versions use the Docker package.
- Install the [.NET 8.0 runtime](https://learn.microsoft.com/en-gb/dotnet/core/install/linux?WT.mc_id=dotnet-35129-website) inside the container or use a [.NET container image](https://learn.microsoft.com/en-gb/dotnet/core/docker/introduction#net-images).
- ⚠️ Do not install the SDK, ASP.NET Core Runtime, or Desktop Runtime.
- Use the `ListPorter-x.x.x-linux-x64` binary inside the container.
- Mount your playlist folder into the container with read access and ensure network access to Plex.

### Platform testing notes

* Tested extensively: Windows 11  
* Tested moderately: Linux (64-bit ARM, Raspberry Pi 5 only)  
* Not tested: Windows 10, Linux (x64), Linux (32-bit ARM), Synology DSM (via Container Manager), macOS (x64 & Apple Silicon)

>[!NOTE]
>Docker, Synology DSM, and macOS environments have not been tested, and no platform-specific guidance is available as these setups are outside the developer’s experience. While ListPorter should work fine on them, support will be limited to questions directly related to the tool itself.

## 🚀 Quick start guide

**This is the simplest and most common way to use ListPorter.** It works across platforms and uses fuzzy matching to automatically align playlist paths with your Plex library.

>[!TIP]
>To ensure Plex contains only the playlists in your import folder (i.e. remove any that aren’t there), add the `--mirror` (`-m`) option.

```
ListPorter -s 127.0.0.1 -t ABCDEFG -l 8 -i "C:\Playlists"

ListPorter --server 127.0.0.1 --token ABCDEFG --library 8 --import "C:\Playlists"
```

The example below shows a more advanced scenario suitable when fuzzy matching isn’t enough. It demonstrates how to explicitly rewrite paths and convert formats when importing playlists created on one platform (e.g. Windows) into a Plex server running on another (e.g. Linux).
Note that `--find` (`-f`) uses forward slashes because `--linux` (`-l`) converts backslashes to forward slashes.

>[!CAUTION]
>This example deletes existing Plex playlists before import. Only use `--delete` (`-d`) if you're sure you want to replace everything..

```
ListPorter -s pimachine -t ABCDEFG -l 10 -i "C:\Playlists" -x -l -f "C:/Users/MrSilver/Music/iTunes/iTunes Media/Music" -r "/home/pi/music" -d

ListPorter --server pimachine --token ABCDEFG --library 10 --import "C:\Playlists" --exact-only --linux --find "C:/Users/MrSilver/Music/iTunes/iTunes Media/Music" --replace "/home/pi/music" --delete
```

## 💻 Command line options

ListPorter is a command-line tool. Run it from a terminal or command prompt, supplying all options and arguments directly on the command line. Logs with detailed information are also written and you can find the log file location using `--help` (`-h`).

```
ListPorter -s <address>[:<port>] -t <token> -l <library> -i <path> [options]
```

### Mandatory arguments

- **`-s <address>[:<port>]`, `--server <address>[:<port>]`**   
  Plex server address, optionally including the port (e.g. `localhost:32400`). If you do not supply a port then the default (`32400`) will be used.

  You can also prefix with `https://` or `http://` to specify the connection type (default is `http`).

>[!NOTE]
>If Plex is configured to require secure connections (under Settings > Remote Access) then plain `http://` connections will fail, so use `https://` instead.

- **`-t <token>`, `--token <token>`**   
  Plex authentication token. Required to interact with your Plex server. To find out your token, see [Plex's guide](https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/).

>[!CAUTION]
>You should never share your Plex token with anyone!

- **`-l <library>`, `--library <library>`**   
  Plex library ID that contains your music. This must be a _Music_ library. To find your library ID, go into the Plex web client, hover the mouse over the library you want and look at the URL. It will end with `source=xx` where `xx` is the library ID.

- **`-i <path>`, `--import <path>`**   
  Path to a single .m3u file or a directory containing multiple .m3u files.

### Optional arguments
  
#### Playlist sync options

These options will remove playlists from your Plex server under specific conditions. Only playlists that are manual (not smart/dynamic), contain audio tracks only and belong entirely to the music library specified by `--library` will ever be deleted. No other content (such as music files, metadata or non-matching playlists) is modified or removed..

- **`-d`, `--delete`**   
  Deletes all existing playlists in the specified Plex music library before importing any new ones.

- **`-m`, `--mirror`**   
  Mirrors Plex playlists to match the imported `.m3u` files. Any Plex playlists not represented in the imported list will be removed. 

> [!CAUTION]
> Be careful when using `--mirror` with a single file: this will cause all other playlists in the library to be removed, keeping only the one you provided.

#### Path rewriting options

ListPorter tries to match each file path in your playlist with the paths Plex has stored. It first attempts an exact match. If that fails, it automatically uses fuzzy matching, based on the assumption that music files are organised with a structure of `artist/album/track` or `artist\album\track`. It compares only the last three parts of each path, ignoring drive letters, shares, or deeper folder structures.

This approach works well when root paths differ or when file systems vary across devices, as long as the layout near the file itself is consistent. However, if files have been renamed or stored with a different folder hierarchy, exact or fuzzy matching may fail.

**If you use any of the options below to rewrite paths, fuzzy matching will be automatically disabled.** This is to avoid conflicts between automated and manual path handling.

These options don’t modify the playlist files themselves - they only affect how paths are interpreted during import.

- **`-u`, `--unix`, `--linux`**   
Force playlist paths to use forward slashes (`/`), often required for Plex servers running on Linux, macOS, or NAS. This does not affect any path set using `--base-path`.

- **`-w`, `--windows`**   
Force playlist paths to use backslashes (`\`), as used by Plex servers on Windows. This does not affect any path set using `--base-path`.

- **`-f <text>`, `--find <text>`**   
Searches for a substring in each song's file path. Intended for use with `--replace` to rewrite paths. Matching is case-insensitive and only one `--find` string is supported per run. Paths set using `--base-path` will not be searched.

> [!IMPORTANT]
> If you're also using `--unix` or `--windows`, the slash conversion happens before the search-and-replace step. Be sure your `--find` value uses the correct slash style for matching.

- **`-r <text>`, `--replace <text>`**   
Replaces matched text from `--find` with this new value. If `--find` is used and there is no `--replace` value, then it will be assumed to be blank and the matching string will be removed. Paths set using `--base-path` will not be affected.

- **`-b <path>`, `--base-path <path>`**   
Specifies a base path to prepend to all playlist entries. This is useful when your .m3u playlists contain relative paths (e.g. `./music/track.mp3`). If a track’s path starts with `./` or `.\` then the leading dot will be removed before applying the base path.
  
  The base path is applied after all other rewriting options (such as `--find`/`--replace` or `--unix`/`--windows`) and will affect all playlists in the current run.

- **`-x`, `--exact-only`**   
Disables fuzzy matching and any automatic path adjustments. Only exact, case-insensitive matches will be used to link playlist files to Plex tracks. Use this if you want full control and are relying entirely on exact paths or other file rewriting options listed above.

### Other options

- **`-v`, `--verbose`**  
  Outputs additional information to the log files to aid in debugging.
  
- **`/?`, `-h`, `--help`**  
  Displays the full help text with all available options, credits and the location of the log files.

## 🧩 How playlists are matched on Plex

The program will only recognise and process playlists on your Plex server that meet all of the following criteria:

1. All items in the playlist must be audio - playlists containing video or mixed content are ignored.
2. The playlist is manual, not a smart/dynamic playlist.
3. All audio tracks in the playlist belong to the library ID provided via `--library`.

These rules apply to all playlist-related operations, including `--mirror` and `--delete`.

>[!NOTE]
>The program will only report the number of playlists that meet these criteria, not the total number of playlists on your Plex server. If a playlist doesn’t appear or isn’t deleted/modified as expected, it likely does not meet one or more of these conditions.

## ❓ Common questions

### I'm struggling to understand how to run or configure ListPorter. What should I do?

ListPorter is a command-line tool, so some familiarity with using a terminal or command prompt is expected. If you’re new to this, you can ask tools like [ChatGPT](https://chat.openai.com/), [Gemini](https://gemini.google.com/), [Claude](https://claude.ai/) or [Copilot](https://copilot.microsoft.com/) to walk you through the process in simple terms. You’ll need your Plex token and the numeric ID of your music library. These can be found via your Plex Web interface 

Here’s a good starting prompt you can use (adjust as needed):

>_I'm using [Windows/macOS/Linux] and I want to use ListPorter to import my M3U playlists into Plex. Here's the link to the project: https://github.com/mrsilver76/listporter. Please guide me step-by-step as if I'm new to the command line. Don't ask for my Plex token._

Bear in mind that AI tools aren’t infallible - they can confidently give incorrect or misleading advice. Always think critically, double-check commands and be cautious when following their suggestions.

>[!CAUTION]
>If you're sharing content with AI tools, make sure you remove or redact your Plex token, IP addresses or any other sensitive information.

### Where are the logs stored? What do they show?

The program outputs a more detailed set of logs than displayed on the screen. These can be helpful when trying to debug so, if you raise an issue, please be prepared to share them. 

You can find out where the logs are located by reading the output of `ListPorter -h`. Logs older than 14 days are deleted every time the program runs.

>[!NOTE]
>All log files generated by the program automatically censor the Plex token and Machine ID.

### Can I just double-click on this program from Windows Explorer and it run?

The programs expects a number of command lines argument to run, so double-clicking on it in Explorer will not work.

However you can enable this with a couple of steps:

1. Place `ListPorter.exe` wherever you would like to store it.
2. Right-click on `ListPorter.exe`, select "Show more options" and then "Create shortcut".
3. Right-click on the newly created `ListPorter.exe - Shortcut` and select "Properties"
4. In the text box labelled "Target" add the arguments you want to use to the end of the string. Full details of all the arguments are documented [here](#-command-line-options).
5. Click on "OK"
6. To run, double-click on `ListPorter.exe - Shortcut`. You can rename this to something more useful and move it elsewhere if you'd like.
7. Once Plex Playlist Updater has finished running, the pop-up window will close automatically.

### Why do I see a warning that some items failed to match the Plex database?
This warning appears when ListPorter can’t link some playlist items to Plex tracks because their file paths don’t align closely enough. Although ListPorter uses automatic fuzzy matching (assuming the path ends `artist/album/track` or `artist\album\track`) it will fail if these components differ substantially or are absent.

In the situation where fuzzy matching is not working, you can use `--find`, `--replace`, `--unix`, `--windows` and `--base-path` to help rewrite your playlist tracks into a path that Plex can recognise.

To find out what path Plex is expecting:

1. Open [Plex Web](https://app.plex.tv/).
2. Navigate to one of the problematic tracks (make sure it’s also in the playlist you're importing).
3. Click the three dots (…) and choose “Get Info”.
4. Look under the Files section - this shows the full path Plex has stored for the track.

Adjust your playlist paths using the options above to match this format and re-run ListPorter. Once the paths align, the warnings should disappear.

### Can I run this on a headless Linux server or NAS?
Yes. The tool is a command-line application and can be run from a headless environment like a Linux server or NAS, provided the [.NET 8.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) is installed and the binary has execute permissions.

### What does the `--mirror` option do exactly?
When enabled, `--mirror` will remove any Plex playlists that are not represented in the M3U files you're importing. This allows you to keep your Plex playlists in sync with an external source, such as a local music manager or export directory. Mirroring is one-way only, you cannot use this tool to export changes you've made to your playlists in Plex.

> [!CAUTION]
> Be careful when using `--mirror` with a single file: this will cause all other playlists in the library to be removed, keeping only the one you provided.

### Does this overwrite existing playlists in Plex?
Only if their content has changed. The tool compares the track list in your M3U file with the existing Plex playlist. If they differ, it clears the Plex playlist and re-imports the correct tracks. If they are identical, it skips the update.

### I'm using `--windows` or `--unix`. Why isn't `--find` matching?
The `--windows` and `--unix` options change all slashes in the song paths before the `--find` and `--replace` logic runs. This means that if your `--find` string uses the original slash style (e.g., backslashes on Windows), it won’t match the transformed path.

As an example, lets assume your M3U contains the following:
```
D:\Music\Pop\track.mp3
```

If you run the tool with:
```
--unix --find "D:\Music" --replace "/mnt/media"
```
then after `--unix` is actioned, the path is transformed to:
```
D:/Music/Pop/track.mp3
```
So the `--find` string `"D:\Music"` doesn't match `"D:/Music"`.

✅ **Correct Usage**   
Use forward slashes in the `--find` string to match the slash transformation:
```
--unix --find "D:/Music" --replace "/mnt/media"
```

This will correctly transform the path to `/mnt/media/Pop/track.mp3`

### Why does the tool clear the contents of existing playlists instead of deleting and recreating them?

Some external apps and hardware players (such as Sonos) reference Plex playlists by their unique internal ID. If the playlist is deleted and recreated, it gets a new ID, which can break external links or integrations. To maintain compatibility, the tool clears the playlist's contents and repopulates it instead of deleting the entire playlist. This ensures external systems retain their connection to the playlist.

## 🛟 Questions/problems?

Please raise an issue at https://github.com/mrsilver76/listporter/issues.

## 💡 Future development: open but unplanned

ListPorter currently meets the needs it was designed for, and no major new features are planned at this time. However, the project remains open to community suggestions and improvements. If you have ideas or see ways to enhance the tool, please feel free to submit a [feature request](https://github.com/mrsilver76/listporter/issues).

## 📝 Attribution

- Plex is a registered trademark of Plex, Inc. This tool is not affiliated with or endorsed by Plex, Inc.
- Music & multimedia icon by paonkz - Flaticon (https://www.flaticon.com/free-icons/music-and-multimedia)
- With thanks to https://www.plexopedia.com/plex-media-server/api/ for Plex API documentation.

## 🕰️ Version history

### 1.0.0 (27 June 2025)
- 🏁 Declared as the first stable release.
- Added fuzzy matching logic to improve playlist-to-Plex track matching when exact paths don’t align.
- Added support for secure connections (HTTPS) when communicating with Plex servers.
- Added `--base-path` (`-b)` option to prepend a base path for playlists using relative paths.
- Added `--linux` as an alias for `--unix`.
- Improved `--help` formatting for better readability on 80-character terminals.
- Added contextual tips for import errors to assist troubleshooting without needing logs.
- Reduced API page size to 1000 to prevent Plex from generating warning entries.
- Path matching and rewriting issues are now surfaced to users (max 5 per playlist).
- Replaced `Publish.bat` with a streamlined `Publish.ps1` script for building executables.
- Added `linux-arm` builds for compatibility with Raspberry Pi 3 devices.
- Added additional logging to aid in debugging.
- Added statistics showing playlists skipped, created, updated and deleted.
- Fixed bug where a folder with no m3u or m3u8 files would not generate an appropriate error.
- Made output less spammy whilst loading and parsing m3u files. 
- Added GNU GPL v2 license notice to source files for clarity.

### 0.9.3 (24 May 2025)
- Fixed version checker incorrectly reporting updates available when already on the latest version.

### 0.9.2 (24 May 2025)
- Updated track discovery to use a more comprehensive Plex API endpoint, resolving issues where some valid items (like orphaned tracks) were previously omitted. Thanks to u/AnalogWalrus and u/spikeygg for spotting and helping to debug.
- Improved performance by over 30% through fewer API calls and reduced track lookups during playlist processing.
- Added hostname and port number of Plex server during connection test.
- Cleaned up version number handling, ensuring consistency and correct handling of pre-releases.
- Improved verbose mode to help in debugging issues.
- Added details about `--verbose` to `--help`.
- Cleaned up various pieces of code.

### 0.9.1 (19 May 2025)
- Renamed to "ListPorter" to avoid any potential issue with the Plex legal team.
- Fixed bug that meant that the Plex token and machine ID could end up in the logs.
- Log folders generated by 0.9.0 are removed, as they can contain token data.
- Tracks marked as deleted (but not removed) are now considered for playlists.
- Fixed logs to report more useful information.
- Fixed copyright and minor number formatting.
- Added error catching for some file operations.
- Changed incorrect scary messaging when there are empty playlists.
- Fixed terrible error messages when unable to connect to Plex server.
- Added links to Plex support articles for common connection issues.
- Added 10 second notification to avoid people thinking that the program had locked up.
- Updated `Publish.bat` to include missing osx-x64 (macOS on Intel) build.

### 0.9.0 (16 May 2025)
- Initial release, a C# port from [iTunes Playlist Exporter](https://github.com/mrsilver76/itunes_playlist_exporter).
- Now cross-platform, with support for Windows, Linux (x64 and ARM) and macOS.
- Removed iTunes exporting functionality, now handled by a separate tool called [TuneLift](https://github.com/mrsilver76/tunelift).
- Added automatic version checking with update notifications.
- Playlists are only updated if they have changed, eliminating the need to delete and re-import everything.
- Added `--mirror` option to remove playlists from Plex that no longer exist in the input directory.
- Modified playlists retain their original playlist ID, so external players like Sonos can continue to reference them.

