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

* You may need to install the [.NET 8.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime)
* Don't forget to make the file executable: `chmod +x PlexPU-x.x.x-<your-platform>`

### Platform testing notes

* Tested extensively: Windows 11
* Tested moderately: Linux (ARM)
* Not tested at all: Linux (x64), macOS

