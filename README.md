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
* Upload all playlists in `/home/mrsilver/playlists`
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
  Plex authentication token. Required to interact with your Plex server. To find out

- **`-l <library>`, `--library <library>`**   
  Plex library ID that contains your music. This must be a _Music_ library.

- **`-i <path>`, `--import <path>`**   
  Path to a single .m3u file or a directory containing multiple .m3u files.

### 🛠️ Optional arguments
  
#### 🔄 Playlist sync options

- **`-d`, `--delete`**
  Deletes all existing playlists in the specified Plex music library before uploading any new ones.

- **`-m`, `--mirror`**
  Mirrors Plex playlists to match the uploaded `.m3u` files. Any Plex playlists not represented in the imported list will be removed.

#### 🧭 Playlist song path rewriting options

- **`-f <text>`, `--find <text>`
Finds and replaces part of each song’s file path. Use with --replace to rewrite paths to match Plex's file structure.

- **`-r <text>`, `--replace <text>`
Replaces matched text from --find with the specified string.

- **`-u`, `--unix`
Force playlist paths to use forward slashes (/), often required for Plex servers running on Linux, macOS, or NAS.

- **`-w`, `--windows`
Force playlist paths to use backslashes (\), as used by Plex servers on Windows.

### 🎵 Playlist selection
