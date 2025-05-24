# ListPorter
_A cross-platform command-line tool (Windows, Linux, macOS) for importing standard or extended `.m3u` audio playlists to Plex Media Server. Supports path rewriting, selective updates and optional mirroring of playlists._

> [!TIP]
> Using iTunes on Windows? [TuneLift](https://github.com/mrsilver76/tunelift) makes it easy to export your iTunes playlists to `.m3u` files.


## 🧰 Features
* 💻 Runs on Windows, Linux (x64 & ARM) and macOS (Intel & Apple Silicon).
* 📂 Imports standard or extended M3U audio playlists to Plex.
* 🌐 Works with any Plex server platform (Windows, Linux, NAS, macOS) via the Plex API.
* ✅ Skips importing playlists that haven’t changed.
* 🪞 Mirrors Plex playlists to match imported M3U files (optional)
* 🔁 Force playlist paths to use `/` or `\` to match your Plex server’s file path format (Linux, macOS, NAS or Windows).
* 🛠️ Modify playlist file paths using find & replace rules, ensuring they align with how Plex sees your media library.
* 🧹 Deletes all Plex playlists before import (optional)
* 🔗 Preserves playlist IDs to maintain compatibility with external players (e.g. Sonos)
* 📘 Logs activity to timestamped text files

## 📦 Download

Get the latest version from https://github.com/mrsilver76/listporter/releases.

Each release includes the following files (`x.x.x` denotes the version number):

|Filename|Description|
|:--------|:-----------|
|`ListPorter-x.x.x-win-x64.exe`|✅ For Microsoft Windows 10 and 11 ⬅️ **Most users should choose this**
|`ListPorter-x.x.x-linux-x64`|For Linux on Intel/AMD CPUs|
|`ListPorter-x.x.x-linux-arm64`|For Linux on ARM (e.g. Raspberry Pi)|
|`ListPorter-x.x.x-osx-arm64`|For macOS on Apple Silicon (eg. M1 and newer)|
|`ListPorter-x.x.x-osx-x64`|For macOS on Intel-based Macs (pre-Apple Silicon)|
|Source code (zip)|ZIP archive of the source code|
|Source code (tar.gz)|TAR.GZ archive of the source code|

> [!TIP]
> There is no installer. Just download the appropriate file and run it from the command line.

### Linux/macOS users

* You will need to install the [.NET 8.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime).
* Don't forget to make the file executable: `chmod +x ListPorter-x.x.x-<your-platform>`

### Platform testing notes

* Tested extensively: Windows 11
* Tested moderately: Linux (ARM)
* Not tested at all: Windows 10, Linux (x64), macOS (x64 & Apple Silicon)

## 🚀 Quick start guide

Below are a couple of command scenarios for using ListPorter. They will work on all platforms.

```
ListPorter -s 127.0.0.1 -t ABCDEFG -l 8 -i "C:\Playlists" -m

ListPorter --server 127.0.0.1 --token ABCDEFG --library 8 --import "C:\Playlists" --mirror
```
* Connect to the Plex server running on the same machine
* Use Plex token `ABDEFGH`
* Use music library ID `8`
* Import all playlists in `C:\Playlists\`
* Remove any playlists from Plex that aren't imported (mirror)
  
```
ListPorter -s 192.168.1.100 -t ABCDEFG -l 4 -I "/home/mrsilver/playlists/Running.m3u" -w

ListPorter --server 192.168.1.100 --token ABCDEFG --library 4 --import "/home/mrsilver/playlists/running.m3u" --windows
```
* Connect to Plex Server running at `192.168.1.100`
* Use Plex token `ABCDEFG`
* Use music library ID `4`
* Import all playlists in `/home/mrsilver/playlists/`
* Replace Linux forward slashes (`/`) in the playlist path to Windows backslashes (`\`)

```
ListPorter -s pimachine -t ABCDEFG -l 10 -i "C:\Playlists" -l -f "C:/Users/MrSilver/Music/iTunes/iTunes Media/Music" -r "/home/pi/music" -d

ListPorter --server pimachine --token ABCDEFG --library 10 --import "C:\Playlists" --linux --find "C:/Users/MrSilver/Music/iTunes/iTunes Media/Music" --replace "/home/pi/music" --delete
```
* Connect to Plex Server running at `pimachine`
* Use Plex token `ABCDEFG`
* Use music library ID `10`
* Import all playlists found in `C:\Playlists`
* Replace Windows backslashes (`\`) in the playlist path to forward slashes (`/`)
* Replace `C:\Users\MrSilver\Music\iTunes\iTunes Media\Music` in the playlist paths to `/home/pi/music`
* Delete all playlists on Plex first before importing

> [!IMPORTANT]
> When using `--linux` or `--windows`, path slashes are converted before any `--find` and `--replace` operations. Make sure your `--find` string reflects the adjusted slash style. In the example above, backslashes are converted to `/`, so `--find` must also use forward slashes (`/`).

## 💻 Command line options

```
ListPorter -s <address>[:<port>] -t <token> -l <library> -i <path> [options]
```

### Mandatory arguments

- **`-s <address>[:<port>]`, `--server <address>[:<port>]`**   
  Plex server address, optionally including the port (e.g. `localhost:32400`). If you do not supply a port then the default (`32400`) will be used.

>[!WARNING]
>This tool does not currently support Plex servers set to require secure connections. To use it, change Plex’s Secure Connections setting to "Preferred" under **Settings > Network > Secure Connections**.


- **`-t <token>`, `--token <token>`**   
  Plex authentication token. Required to interact with your Plex server. To find out your token, see the [Plex support article](https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/).

>[!CAUTION]
>You should never share your Plex token with anyone!

- **`-l <library>`, `--library <library>`**   
  Plex library ID that contains your music. This must be a _Music_ library. To find your library ID, go into the Plex web client, hover the mouse over the library you want and look at the URL. It will end with `source=xx` where `xx` is the library ID.

- **`-i <path>`, `--import <path>`**   
  Path to a single .m3u file or a directory containing multiple .m3u files.

### Optional arguments
  
#### Playlist sync options

- **`-d`, `--delete`**   
  Deletes all existing playlists in the specified Plex music library before importing any new ones. Only audio playlists that are manual and entirely within the specified library are affected.

- **`-m`, `--mirror`**   
  Mirrors Plex playlists to match the imported `.m3u` files. Any Plex playlists not represented in the imported list will be removed. Only audio playlists that are manual and entirely within the specified library are affected.

#### Path rewriting options

If your `.m3u` playlists reference song locations that Plex can’t access (e.g. local drives or mismatched paths), those tracks won’t be playable. For instance, a playlist might use a local path like `D:\MyMusic`, while your Plex server expects a network path like `\\homepc\MyMusic` or `/mnt/music`.

These options let you adjust how file paths are interpreted during import, so they match the structure expected by your Plex server. The playlist files themselves are not changed.

- **`-u`, `--unix`**   
Force playlist paths to use forward slashes (`/`), often required for Plex servers running on Linux, macOS, or NAS.

- **`-w`, `--windows`**   
Force playlist paths to use backslashes (`\`), as used by Plex servers on Windows.

- **`-f <text>`, `--find <text>`**
Searches for a substring in each song's file path. Intended for use with `--replace` to rewrite paths. Matching is case-insensitive and only one `--find` string is supported per run.

> [!IMPORTANT]
> If you're also using `--unix` or `--windows`, the slash conversion happens before the search-and-replace step. Be sure your `--find` value uses the correct slash style for matching.

- **`-r <text>`, `--replace <text>`**   
Replaces matched text from `--find` with this new value. If `--find` is used and there is no `--replace` value, then it will be assumed to be blank and the matching string will be removed. 

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

### Can I run this on a headless Linux server or NAS?
Yes. The tool is a command-line application and can be run from a headless environment like a Linux server or NAS, provided the [.NET 8.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) is installed and the binary has execute permissions.

### What does the `--mirror` option do exactly?
When enabled, `--mirror` will remove any Plex playlists that are not represented in the M3U files you're importing. This allows you to keep your Plex playlists in sync with an external source, such as a local music manager or export directory. Mirroring is one-way only, you cannot use this tool to export changes you've made to your playlists in Plex.

> [!CAUTION]
> Be careful when using `--mirror` with a single file: this will cause all other playlists in the library to be removed, keeping only the one you provided.

### Does this overwrite existing playlists in Plex?
Only if their content has changed. The tool compares the track list in your M3U file with the existing Plex playlist. If they differ, it clears the Plex playlist and re-imports the correct tracks. If they are identical, it skips the update.

### How do I handle mismatched paths between the source and Plex server?
You can use the `--find` and `--replace` options to rewrite the file paths in your playlist to match what Plex expects. For example, if your M3U uses `D:\Music` and Plex expects `\\server\Music`, use `--find "D:\Music" --replace "\\server\Music"`

Also use `--unix` or `--windows` if Plex uses forward or backslashes differently than your M3U file.

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

### Why does the tool only clear the contents of existing playlists instead of deleting and recreating them?

Some external apps and hardware players (such as Sonos) reference Plex playlists by their unique internal ID. If the playlist is deleted and recreated, it gets a new ID, which can break external links or integrations. To maintain compatibility, the tool clears the playlist's contents and repopulates it instead of deleting the entire playlist. This ensures external systems retain their connection to the playlist.

## 🛟 Questions/problems?

Please raise an issue at https://github.com/mrsilver76/listporter/issues.

## 📝 Attribution

- Plex is a registered trademark of Plex, Inc. This tool is not affiliated with or endorsed by Plex, Inc.
- Music & multimedia icon by paonkz - Flaticon (https://www.flaticon.com/free-icons/music-and-multimedia)
- With thanks to https://www.plexopedia.com/plex-media-server/api/ for Plex API documentation.

## 🕰️ Version history

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

