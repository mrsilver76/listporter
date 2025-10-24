# ListPorter Frequently Asked Questions

Getting started and running the tool

- [I'm struggling to understand how to run or configure ListPorter. What should I do?](#im-struggling-to-understand-how-to-run-or-configure-listporter-what-should-i-do)
- [Can I just double-click on this program from Windows Explorer and it run?](#can-i-just-double-click-on-this-program-from-windows-explorer-and-it-run)
- [Can I run this on a headless Linux server or NAS?](#can-i-run-this-on-a-headless-linux-server-or-nas)

Configuration and command line options

- [Can I import playlists directly to a specific Plex Home user instead of the main/admin account?](#can-i-import-a-playlist-directly-to-a-specific-plex-home-user-instead-of-the-mainadmin-account)
- [What does the `--mirror` option do exactly?](#what-does-the---mirror-option-do-exactly)
- [I'm using `--windows` or `--unix`. Why isn't `--find` matching?](#im-using---windows-or---unix-why-isnt---find-matching)

Logging and debugging

- [Where are the logs stored? What do they show?](#where-are-the-logs-stored-what-do-they-show)
- [I'm getting an error about fuzzy matching conflicts](#im-getting-an-error-about-fuzzy-matching-conflicts)
- [Why do I see a warning that some items failed to match the Plex database?](#why-do-i-see-a-warning-that-some-items-failed-to-match-the-plex-database)

Plex interaction and playlist behavior

- [Does this overwrite existing playlists in Plex?](#does-this-overwrite-existing-playlists-in-plex)
- [Why does the tool clear the contents of existing playlists instead of deleting and recreating them?](#why-does-the-tool-clear-the-contents-of-existing-playlists-instead-of-deleting-and-recreating-them)

---

## I'm struggling to understand how to run or configure ListPorter. What should I do?

ListPorter is a command-line tool, so some familiarity with using a terminal or command prompt is expected. If you’re new to this, you can ask tools like [ChatGPT](https://chat.openai.com/), [Gemini](https://gemini.google.com/), [Claude](https://claude.ai/) or [Copilot](https://copilot.microsoft.com/) to walk you through the process in simple terms. You’ll need your Plex token and the numeric ID of your music library. These can be found via your Plex Web interface 

Here’s a good starting prompt you can use (adjust as needed):

>_I'm using [Windows/macOS/Linux] and I want to use ListPorter to import my M3U playlists into Plex. Here's the link to the project: https://github.com/mrsilver76/listporter. Please guide me step-by-step as if I'm new to the command line. Don't ask for my Plex token._

Bear in mind that AI tools aren’t infallible - they can confidently give incorrect or misleading advice. Always think critically, double-check commands and be cautious when following their suggestions.

>[!CAUTION]
>If you're sharing content with AI tools, make sure you remove or redact your Plex token, IP addresses or any other sensitive information.

## Can I just double-click on this program from Windows Explorer and it run?

The programs expects a number of command lines argument to run, so double-clicking on it in Explorer will not work.

However you can enable this with a couple of steps:

1. Place `ListPorter.exe` wherever you would like to store it.
2. Right-click on `ListPorter.exe`, select "Show more options" and then "Create shortcut".
3. Right-click on the newly created `ListPorter.exe - Shortcut` and select "Properties"
4. In the text box labelled "Target" add the arguments you want to use to the end of the string. Full details of all the arguments are documented [here](#-command-line-options).
5. Click on "OK"
6. To run, double-click on `ListPorter.exe - Shortcut`. You can rename this to something more useful and move it elsewhere if you'd like.
7. Once Plex Playlist Updater has finished running, the pop-up window will close automatically.

## Can I run this on a headless Linux server or NAS?
Yes. The tool is a command-line application and can be run from a headless environment like a Linux server or NAS, provided the [.NET 8.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) is installed and the binary has execute permissions.

If your NAS supports Docker then some people have reported that it's possible to get it running inside a .NET container.

## Can I import a playlist directly to a specific Plex Home user instead of the main/admin account?

Yes, but only by using the individual Plex Home user’s access token.

[Plex Home](https://support.plex.tv/articles/203815766-what-is-plex-home/) allows you to create a group of users who all use Plex together on the same server, typically a family living in the same household. Each Plex Home user can have their own personalized libraries, watch history, and playlists. Unlike separate Plex accounts, Plex Home users exist under a single main/admin account, so by default, playlists you import appear under the admin account unless you use the user’s token.

### How to import playlists for a specific Plex Home user

1. Make sure you already have your [Plex admin token](https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/).
2. Find your server's client identifier by visiting [https://plex.tv/api/resources?X-Plex-Token=ABCD](https://plex.tv/api/resources?X-Plex-Token=ABCD), replacing `ABCD` with your Plex admin token.
3. Locate your server and note its `clientIdentifier`.
4. Get the Plex Home user’s access token by visiting [https://plex.tv/api/servers/WXYZ/shared_servers?X-Plex-Token=ABCD](https://plex.tv/api/servers/WXYZ/shared_servers?X-Plex-Token=ABCD), replacing `WXYZ` with your `clientIdentifier` and `ABCD` with your Plex admin token.
5. Plex Home users may have blank `username` and `email` fields, so you will need to use the `userID` to identify the correct user.
    - To find out what the `userID` is for a user, go to **Settings → Plex Home**, select the user and the `userID` is the last number in the URL.

Once you have that users token, run ListPorter as you would normally with `-t` (`--token`) and the users token.

>[!NOTE]
>Non-admin (Plex Home) users cannot use the `-k` (`--update`) option to force a library refresh. This is a restriction enforced by Plex, not ListPorter.


## What does the `--mirror` option do exactly?
When enabled, `--mirror` will remove any Plex playlists that are not represented in the M3U files you're importing. This allows you to keep your Plex playlists in sync with an external source, such as a local music manager or export directory. Mirroring is one-way only, you cannot use this tool to export changes you've made to your playlists in Plex.

> [!CAUTION]
> Be careful when using `--mirror` with a single file: this will cause all other playlists in the library to be removed, keeping only the one you provided.

## I'm using `--windows` or `--unix`. Why isn't `--find` matching?
The `--windows` and `--unix` options change all slashes in the song paths before the `--find` and `--replace` logic runs. This means that if your `--find` string uses the original slash style (e.g., backslashes on Windows), it won’t match the transformed path.

As an example, lets assume your M3U contains the following:
```
D:\Content\Tunes\Faithless\Reverence\06 Insomnia.mp3
```

If you run the tool with:
```
--unix --find "D:\Content" --replace "/mnt/media"
```
then after `--unix` is actioned, the path is transformed to:
```
D:/Content/Tunes/Faithless/Reverence/06 Insomnia.mp3
```
So the `--find` string `"D:\Content"` doesn't match `"D:/Content"`.

### Correct Usage

Use forward slashes in the `--find` string to match the slash transformation:
```
--unix --find "D:/Content" --replace "/mnt/media"
```

This will correctly transform the path to `/mnt/media/Tunes/Faithless/Reverence/06 Insomnia.mp3`

## Where are the logs stored? What do they show?

The program outputs a more detailed set of logs than displayed on the screen. These can be helpful when trying to debug so, if you raise an issue, please be prepared to share them. 

You can find out where the logs are located by reading the output of `ListPorter -h`. Logs older than 14 days are deleted every time the program runs.

>[!NOTE]
>All log files generated by the program automatically censor the Plex token and Machine ID.

<a name="fuzzy"></a>
## I'm getting an error about fuzzy matching conflicts
This error happens when ListPorter finds multiple tracks in your Plex library with the same artist and album folders and the same track title. Typically, this means you have duplicate files that differ only in the initial part of their folder path.

For example, you might have the following two files in your Plex library:
```
C:\Users\MrSilver76\Music\Faithless\Reverence\06 Insomnia.mp3
D:\Content\Tunes\Faithless\Reverence\06 Insomnia.mp3
```
Fuzzy matching ignores everything before the `Faithless` folder, so if it sees a playlist referencing `Faithless\Reverence\06 Insomnia.mp3` (or `Faithless/Reverence/06 Insomnia.mp3`), it cannot safely decide which of the two tracks above to match. To prevent mistakes, ListPorter reports an error and stops.

### How to fix it

1. ListPorter should show you the problematic tracks, but details will also be in the logs.
2. Locate and either remove, rename, or move the tracks so they are no longer duplicates.
   - Alternatively, use `--find`, `--replace`, and optionally `--base-path` to rewrite the folder paths so they match what Plex expects.
4. Once duplicates are resolved or paths corrected, run ListPorter again. Fuzzy matching should work normally.

<a name="tracks"></a>
## Why do I see a warning that some items failed to match the Plex database?
This warning appears when ListPorter can’t link some playlist items to tracks in Plex because their file paths don’t align closely enough. For example, Plex might store a file as:
```
/media/music/Artist/Album/Track.mp3
```
but your playlist refers to it as:
```
D:\Music\Artist\Album\Track.mp3
```

### How matching works

- ListPorter first checks Plex directly for an exact match.
- If none is found, it uses automatic fuzzy matching (assuming the path ends with `artist/album/track` or `artist\album\track`).
- If these components differ substantially, or are missing, the match will fail.

### How to fix it

1. **Check your Plex library** - make sure the missing tracks really exist in Plex. If you’ve only just added them, Plex may not have finished scanning yet.
   - You can either force a scan in Plex before importing, or use `-k` (`--update`) to have ListPorter trigger a Plex rescan automatically.
2. **Find out what path Plex expects:**
   - Open Plex Web.
   - Navigate to one of the problematic tracks.
   - Click the three dots (…) → Get Info.
   - Look under the Files section for the full stored path.
3. **Rewrite your playlist paths to match**, using:
   - `--find` / `--replace` - search and replace text inside playlist paths. For example, change `D:\Music\` into `/media/music/`.
   - `--unix` / `--windows` - convert between forward slashes (`/`) and backslashes (`\`) to match how Plex has stored paths on different platforms.
   - `--base-path` - prepend a new base folder to playlist entries. Useful if your playlists only store relative paths, or if the root directory differs (e.g. add `/mnt/storage/music/` in front of every entry).

Re-run ListPorter after adjusting. Once the playlist paths align (and Plex has completed scanning), the warnings should disappear.

### Example

Suppose your playlist entry is:
```
D:\Music\Daft Punk\Discovery (2001)\1. One More Time.mp3
```
but Plex has stored it as:
```
/mnt/content/Music/Daft Punk/Discovery (2001)/1. One More Time.mp3
```
You could fix this with:
```
--unix --find "D:/Music/" --replace "/mnt/content/Music/"
```
This rewrites the Windows path into the exact format Plex expects.

>[!NOTE]
>The `--unix` option first converts all `\` in your playlist paths to `/`. That’s why the `--find` argument uses forward slashes (`/`) instead of backslashes (`\`).

## Does this overwrite existing playlists in Plex?
Only if their content has changed. The tool compares the track list in your M3U file with the existing Plex playlist. If they differ, it clears the Plex playlist and re-imports the correct tracks. If they are identical, it skips the update.

## Why does the tool clear the contents of existing playlists instead of deleting and recreating them?

Some external apps and hardware players (such as Sonos) reference Plex playlists by their unique internal ID. If the playlist is deleted and recreated, it gets a new ID, which can break external links or integrations. To maintain compatibility, the tool clears the playlist's contents and repopulates it instead of deleting the entire playlist. This ensures external systems retain their connection to the playlist.

