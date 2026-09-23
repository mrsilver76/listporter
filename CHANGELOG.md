# Version history

## 1.2.0 (23 September 2026)
- Added automatic detection of the Plex music library. Use of `-l` (`--library`) is no longer required when the server has only one music library. If you have more than one library, ListPorter will list them so you can add `-l` to your command line.
- Improved the formatting of the help output, which now word-wraps based on the terminal width.
- Updated logging to report the full command line rather than just arguments.
- Plex usernames written to the log file are now partially masked if they look like an email address (console output is unaffected).
- Simplified the startup banner, by removing coloured text and reduced the amount of information shown before playlists start processing.
- Fixed an issue where `--mirror` could incorrectly delete a playlist if the contents of the `m3u` file couldn't be properly loaded.
- Fixed an issue where, if a request to clear a playlist on Plex failed, you'd end up with duplicated tracks.
- Fixed `--delete` not working. The code only recognised `--delete-all` which hadn't been documented, so now both options can be used.
- Fixed an issue where ListPorter would terminate with the wrong error code (`-1` instead of `1`). This made it difficult for scripts to identify when ListPorter fails to complete.
- Fixed a bug where single-letter arguments requiring a value (e.g. `-s` or `-t`) could be incorrectly parsed.
- Fixed an issue where a "still waiting" message was not being displayed when waiting for a slow Plex library update.
- FAQs updated with a new entry explaining why you might be asked to pick a music library.
- Version history moved from `README.md` into a new `CHANGELOG.md` file.
- Cleaned up various pieces of code flagged by Visual Studio's code analysis (simplifications and style).
- Publish script was updated to allow building for a specific architecture.

## 1.1.2 (16 March 2026)
- Fixed a bug where paths in playlists with accented or special characters could fail to match to content already in Plex due to UTF8 normalisation differences.
- Fixed a bug where using `--unix` or `--windows` without `--find` or `--base-path` would incorrectly disable fuzzy matching.
- Fixed a bug where some command line options weren't being correctly noted in the console/terminal output.
- Updated the publish script to use `--no-self-contained` as identifed by dotnet/sdk#51888.
- Updated copyright.

## 1.1.1 (06 October 2025)
- Output now confirms which Plex account or managed user the playlists will be uploaded to, based on the token provided.
- Fixed bug where logs were not being saved in the correct location.
- Fixed bug where Plex token was being incorrectly saved in the logs.


## 1.1.0 (24 September 2025)
- Added `-k` (`--update`) to force Plex to scan the library prior to importing.
- Logger now includes OS information to help with troubleshooting across platforms.
- All fuzzy match clashes during the database build are now logged, not just the first.
- When fuzzy match clashes occur, ListPorter now exits with an error and a link to the FAQ.
- Supplying an invalid Plex library ID now returns a clear error, instead of crashing.
- `.m3u` track paths incorrectly prefixed with `file://` or `file-relative://` are now cleaned automatically.
- Legacy log folders named "Plex Playlist Uploader" and "PlexPU" are no longer deleted.
- Added `-nc` (`--no-check`) to disable GitHub version checking.
- Added header displaying key settings and command line options used.
- Updated GitHub version checking code and publish Powershell script.
- Cleaned up various pieces of code (analyzer suggestions regarding naming, simplifications, and style)
- FAQs have been moved to a separate page to reduce clutter in the main README.

## 1.0.0 (27 June 2025)
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

## 0.9.3 (24 May 2025)
- Fixed version checker incorrectly reporting updates available when already on the latest version.

## 0.9.2 (24 May 2025)
- Updated track discovery to use a more comprehensive Plex API endpoint, resolving issues where some valid items (like orphaned tracks) were previously omitted. Thanks to u/AnalogWalrus and u/spikeygg for spotting and helping to debug.
- Improved performance by over 30% through fewer API calls and reduced track lookups during playlist processing.
- Added hostname and port number of Plex server during connection test.
- Cleaned up version number handling, ensuring consistency and correct handling of pre-releases.
- Improved verbose mode to help in debugging issues.
- Added details about `--verbose` to `--help`.
- Cleaned up various pieces of code.

## 0.9.1 (19 May 2025)
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

## 0.9.0 (16 May 2025)
- Initial release, a C# port from [iTunes Playlist Exporter](https://github.com/mrsilver76/itunes_playlist_exporter).
- Now cross-platform, with support for Windows, Linux (x64 and ARM) and macOS.
- Removed iTunes exporting functionality, now handled by a separate tool called [TuneLift](https://github.com/mrsilver76/tunelift).
- Added automatic version checking with update notifications.
- Playlists are only updated if they have changed, eliminating the need to delete and re-import everything.
- Added `--mirror` option to remove playlists from Plex that no longer exist in the input directory.
- Modified playlists retain their original playlist ID, so external players like Sonos can continue to reference them.
