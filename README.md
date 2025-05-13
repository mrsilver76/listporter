# Plex Playlist Uploader (PlexPU)
_A cross-platform command-line tool (Windows, Linux, macOS) for uploading standard or extended `.m3u` audio playlists to Plex Media Server. Supports path rewriting, selective updates and optional mirroring of playlists._

> [!NOTE]
> This program is a complete rewrite of [iTunes Playlist exporter](https://github.com/mrsilver76/itunes_playlist_exporter) and does not export playlists from iTunes. If you wish to export playlists from iTunes then please look at [TuneLift](https://github.com/mrsilver76/tunelift).

## Features
* 💻 Runs on Windows, Linux (x64 & ARM) and macOS.
* 📂 Uploads standard or extended M3U audio playlists to Plex.
* 🌐 Works with any Plex server platform (Windows, Linux, NAS, macOS) via the Plex API.
* ✅ Skips uploading playlists that haven’t changed.
* 🪞 Mirrors Plex playlists to match uploaded M3U files (optional)
* 🔁 Force playlist paths to use `/` or `\` to match your Plex server’s file path format (Linux, macOS, NAS, or Windows).
* 🛠️ Modify playlist file paths using find & replace rules, ensuring they align with how Plex sees your media library.
* 🧹 Deletes all Plex playlists before upload (optional)
* 📘 Logs activity to timestamped text files

## Download

Get the latest version from https://github.com/mrsilver76/plex-playlist-uploader/releases.

Each release includes the following files (`x.x.x` denotes the version number):

|Filename|Description|
|:--------|:-----------|
|`PlexPU-x.x.x-win-x64.exe`|✅ For Windows 10 and 11 ⬅️ **Most users should choose this**
|`PlexPU-x.x.x-linux-x64`|For Linux on Intel/AMD CPUs|
|`PlexPU-x.x.x-linux-arm64`|For Linux on ARM (e.g. Raspberry Pi)|
|`PlexPU-x.x.x-osx-arm64`|For macOS on Apple Silicon (eg. M1 and newer)|
|Source code (zip)|ZIP archive of the source code|
|Source code (tar.gz)|ZIP archive of the source code|

> [!TIP]
> There is no installer. Just download the appropriate file and run it from the command line.

### Linux/macOS users

* You may need to install the [.NET 8.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime).
* Don't forget to make the file executable: `chmod +x PlexPU-x.x.x-<your-platform>`

### Platform testing notes

* Tested extensively: Windows 11
* Tested moderately: Linux (ARM)
* Not tested at all: Linux (x64), macOS

## Quick start guide

Below are a couple of command scenarios for using PlexPU. They will work on all platforms.

```
PlexPU -s 127.0.0.1 -t ABCDEFG -l 8 -i "C:\Playlists\Running.m3u"

PlexPU --server 127.0.0.1 --token ABCDEFG --library 8 --import "C:\Playlists\Running.m3u"
```
* Connect to the Plex server running on the same machine
* Use Plex token `ABDEFGH`
* Use music library ID `8`
* Upload only `C:\Playlists\Running.m3u`

```
PlexPU -s 192.168.1.100 -t ABCDEFG -l 4 -I "/home/mrsilver/playlists/" -m -w

PlexPU --server 192.168.1.100 --token ABCDEFG --library 4 --import "/home/mrsilver/playlists/running.m3u" --mirror --windows
```
* Connect to Plex Server running at `192.168.1.100`
* Use Plex token `ABDEFGH`
* Use music library ID `4`
* Upload all playlists in `/home/mrsilver/playlists/`
* Remove any playlists from Plex that aren't uploaded (mirror)
* Replace Linux forward slashes (`/`) in the playlist path to backslashes (`\`)

```
PlexPU -s pimachine -t ABCDEFG -l 10 -i "C:\Playlists" -l -f "C:/Users/MrSilver/Music/iTunes/iTunes Media/Music" -r "/home/pi/music" -d

PlexPU --server pimachine --token ABCDEFG --library 10 --import "C:\Playlists" --linux --find "C:/Users/MrSilver/Music/iTunes/iTunes Media/Music" --replace "/home/pi/music" --delete
```
* Connect to Plex Server running at `pimachine`
* Use Plex token `ABCDEFG`
* Use music library `ID` 10
* Upload all playlists found in `C:\Playlists`
* Replace Windows backslashes (`\`) in the playlist path to forward slashes (`/`)
* Replace `C:/Users/MrSilver/Music/iTunes/iTunes Media/Music` in the playlist paths to `/home/pi/music`
* Delete all playlists on Plex first before importing

> [!IMPORTANT]
> Using `-l` will cause the any backslashes (`\`) in the filename and path to be replaced with with forward slashes (`/`) **before any search and replace is performed**. This is why the search string is written as `C:/Users/MrSilver/Music/iTunes/iTunes Media/Music`.

## Command line options

```
PlexPU -s <address>[:<port>] -t <token> -l <library> -i <path> [options]
```

### 🟩 Mandatory arguments

- **`-s <address>[:<port>]`, `--server <address>[:<port>]`**   
  Plex server address, optionally including the port (e.g. `localhost:32400`). If you do not supply a port then the default (`32400`) will be used.

- **`-t <token>`, `--token <token>`**   
  Plex authentication token. Required to interact with your Plex server. To find out your token, see the [Plex support article](https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/).

>[!CAUTION]
>You should never share your Plex token with anyone!

- **`-l <library>`, `--library <library>`**   
  Plex library ID that contains your music. This must be a _Music_ library.

>[!TIP]
>To find your library ID, go into the Plex web client, hover the mouse over the library you want and look at the URL. It will end with `source=xx` where `xx` is the library ID.

- **`-i <path>`, `--import <path>`**   
  Path to a single .m3u file or a directory containing multiple .m3u files.

### 🛠️ Optional arguments
  
#### 🔄 Playlist sync options

- **`-d`, `--delete`**   
  Deletes all existing playlists in the specified Plex music library before uploading any new ones.

- **`-m`, `--mirror`**   
  Mirrors Plex playlists to match the uploaded `.m3u` files. Any Plex playlists not represented in the imported list will be removed.

#### 🧭 Path rewriting options

If your `.m3u` playlists reference song locations that Plex can’t access (e.g. local drives or mismatched paths), those tracks won’t be playable. For instance, a playlist might use a local path like `D:\MyMusic`, while your Plex server expects a network path like `\\homepc\MyMusic` or `/mnt/music`.

These options let you rewrite the file paths in the playlist so they match the structure known to your Plex server.

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

### 📖 Help

- **`/?`, `-h`, `--help`**  
  Displays the full help text with all available options.

### Other useful information

#### Impacted playlists

PlexPU will only recognise, process and delete playlists which meet **all** of the following criteria:

1. They are an audio playlist.
2. They are not a smart playlist, that is a playlist that automatically updates based on defined criteria.
3. All items within the playlist come from the libary ID defined by `--library`

Playlists that do not meet all of these criteria will be ignored.

#### Logging

PlexPU outputs a more detailed set of logs than displayed on the screen. These can be helpful when trying to debug so, if you raise an issue, please be prepared to share them. 

You can find out where the logs are located by reading the output of `PlexPU -h`. There is a hidden command line argument `-v` or `--verbose` which will also log the API calls made to the Plex server.

All log files generated by PlexPU automatically censor the Plex token and Machine ID. Logs older than 14 days are deleted the next time PlexPU runs.

## Common questions

### ❓Can I just double-click on this program from Windows Explorer and it run?

The programs expects a number of command lines argument to run, so double-clicking on it in Explorer will not work.

However you can enable this with a couple of steps:

1. Place `PlexPU.exe` wherever you would like to store it.
2. Right-click on `PlexPU.exe`, select "Show more options" and then "Create shortcut".
3. Right-click on the newly created `PlexPU.exe - Shortcut` and select "Properties"
4. In the text box labelled "Target" add the arguments you want to use to the end of the string. Full details of all the arguments are documented [here](#command-line-options).
5. Click on "OK"
6. To run, double-click on `PlexPU.exe - Shortcut`. You can rename this to something more useful and move it elsewhere if you'd like.
7. Once Plex Playlist Updater has finished running, the pop-up window will close automatically.

## Questions/problems?

Please raise an issue at https://github.com/mrsilver76/plex-playlist-uploader/issues.

## Version history

### 0.9.0 (12 May 2025)
- Initial (and early) release, a C# port from "iTunes Playlist Exporter".
- Removed iTunes exporting functionality (moved to a separate program called [TuneLift](https://github.com/mrsilver76/tunelift))
