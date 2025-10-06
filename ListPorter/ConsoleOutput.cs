/*
 * ListPorter - Upload standard or extended .m3u playlist files to Plex Media Server.
 * Copyright (C) 2020-2025 Richard Lawrence
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see
 * < https://www.gnu.org/licenses/>.
 */

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ListPorter
{
    internal sealed class ConsoleOutput
    {
        /// <summary>
        /// Displays the usage information to the user and optional error message.
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        public static void DisplayUsage(string errorMessage = "")
        {
            Console.WriteLine($"Usage: {System.AppDomain.CurrentDomain.FriendlyName} -s <address>[:<port>] -t <token> -l <library> -i <path> [options]\n" +
                                "Upload standard or extended .m3u playlist files to Plex Media Server.\n");


            if (string.IsNullOrEmpty(errorMessage))
                Console.WriteLine($"This is version {VersionHelper.OutputVersion(Globals.ProgramVersion)}, copyright © 2020-{DateTime.Now.Year} Richard Lawrence.\n" +
                                    "Music & multimedia icon by paonkz - Flaticon (https://www.flaticon.com/free-icons/music-and-multimedia)\n");

            Console.WriteLine("Mandatory arguments:\n" +
                                "   -s, --server <address>[:<port>]    Plex server address.\n" +
                                "                                      Add https:// for secure connection.\n" +
                                "                                      (port is optional, defaults to 32400).\n" +
                                "   -t, --token <token>                Plex authentication token.\n" +
                                "   -l, --library <library>            Plex library ID to use.\n" +
                                "   -i, --import <path>                Path to a playlist file or directory.\n\n" +
                                "Optional arguments:\n" +
                                "  Playlist sync options:\n" +
                                "    -d, --delete                      Delete all playlists from library on start.\n" +
                                "    -m, --mirror                      Mirror Plex library to match playlists.\n" +
                                "\n" +
                                "  Path rewriting options:\n" +
                                "    -u, --unix                        Force forward slashes in song paths.\n" +
                                "                                      (for Plex servers running on Linux)\n" +
                                "    -w, --windows                     Force backslashes in song paths.\n" +
                                "                                      (for Plex servers running on Windows)\n" +
                                "    -f, --find <text>                 Find text within the song path.\n" +
                                "    -r, --replace <text>              Replace found text in song path with <text>.\n" +
                                "    -b, --base-path <path>            Base path to use for relative song paths.\n" +
                                "    -x, --exact-only                  Disable fuzzy path matching. Exact matches only.\n" +
                                "\n" +
                                "  Other options:\n" +
                                "    -k, --update                      Trigger a library update before playlist import.\n" +
                                "    -v, --verbose                     Verbose output to log files.\n" +
                                "    -nc, --no-check                   Don't check GitHub for later versions.\n" +
                                "    -h, --help                        Show help message and log file location.\n" +
                                "\n" +
                               $"Logs are written to {Path.Combine(Globals.AppDataPath, "Logs")}");

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {errorMessage}");
                Environment.Exit(-1);
            }
            Environment.Exit(0);
        }

        /// <summary>
        /// Displays the application header and configuration details in the console.
        /// </summary>
        /// <remarks>This method outputs a formatted header, including the application name, version,
        /// copyright information,  and a brief description of its functionality. It also displays key configuration
        /// details such as the Plex  server connection, library ID, import path, and any additional flags or path
        /// rewriting options.</remarks>
        /// <param name="args">The command-line arguments passed to the application, used for logging purposes.</param>
        public static void ShowHeader(string[] args)
        {
            Console.WriteLine(new string('-', 70));
            WriteLeftRight(
                $"\x1b[1;33mListPorter v{VersionHelper.OutputVersion(Globals.ProgramVersion)}\x1b[0m",
                $"Copyright © 2020-{DateTime.Now.Year} Richard Lawrence"
            );
            Console.WriteLine("\x1b[3mUpload standard or extended .m3u playlist files to Plex Media Server.\x1b[0m");
            WriteLeftRight("GNU GPL v2 or later", "https://github.com/mrsilver76/listporter");
            Console.WriteLine(new string('-', 70));

            // Prepare titles + content
            var items = new List<(string Title, string Value)>
            {
                ("Plex server:", $"{(Globals.UsingSecureConnection ? "https://" : "http://")}{Globals.PlexHost}:{Globals.PlexPort}"),
                ("Plex library ID:", Globals.PlexLibrary.ToString(CultureInfo.InvariantCulture)),
                ("Import path:", Globals.PathToImport)
            };
            if (Globals.UsingPathRewriting)
            {
                if (!string.IsNullOrEmpty(Globals.FindText))
                {
                    items.Add(("Find text:", Globals.FindText));
                    items.Add(("Replace with:", string.IsNullOrEmpty(Globals.ReplaceText) ? "(nothing)" : Globals.ReplaceText));
                }                
                items.Add(("Path style:", Globals.PathStyleOption.ToString()));
                if (!string.IsNullOrEmpty(Globals.BasePath))
                    items.Add(("Base path:", string.IsNullOrEmpty(Globals.BasePath) ? "(not set)" : Globals.BasePath));
            }

            // Flags
            if (CommandLineParser.ParsedFlags.Count > 0)
                items.Add(("Other flags:", string.Join(", ", CommandLineParser.ParsedFlags)));

            // Find longest title length
            int pad = items.Max(i => i.Title.Length) + 2;

            // Print everything
            foreach (var (title, value) in items)
                Console.WriteLine($"{title.PadRight(pad)}{value}");

            Console.WriteLine(new string('-', 70));
            Console.WriteLine();

            // Log details
            LogEnvironmentInfo(args);
        }

        /// <summary>
        /// Writes two strings, one aligned to the left and the other to the right, within a specified total width.
        /// </summary>
        /// <remarks>If the combined visible length of the <paramref name="left"/> and <paramref
        /// name="right"/> strings  exceeds the <paramref name="totalWidth"/>, the strings are written directly next to
        /// each other  with a single space in between. ANSI escape sequences (e.g., for text formatting) are ignored 
        /// when calculating the visible length of the strings.</remarks>
        /// <param name="left">The string to be displayed on the left side of the output.</param>
        /// <param name="right">The string to be displayed on the right side of the output.</param>
        /// <param name="totalWidth">The total width of the output, including both strings and any padding between them.  Defaults to 70 if not
        /// specified.</param>
        public static void WriteLeftRight(string left, string right, int totalWidth = 70)
        {
            // Regex to remove ANSI escape sequences
            string ansiRegex = @"\x1B\[[0-9;]*m";

            int visibleLeftLength = Regex.Replace(left, ansiRegex, "").Length;
            int visibleRightLength = Regex.Replace(right, ansiRegex, "").Length;

            if (visibleLeftLength + visibleRightLength >= totalWidth)
                Console.WriteLine(left + " " + right);
            else
                Console.WriteLine(left + new string(' ', totalWidth - visibleLeftLength - visibleRightLength) + right);
        }

        /// <summary>
        /// Output to the logs the environment information, such as .NET version, OS and architecture.
        /// Also includes the parsed command line arguments if any were provided.
        /// </summary>
        /// <param name="args"></param>
        private static void LogEnvironmentInfo(string[] args)
        {
            var dotnet = RuntimeInformation.FrameworkDescription;
            var os = RuntimeInformation.OSDescription.Trim();

            var archName = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

            Logger.Write($"Running {VersionHelper.OutputVersion(Globals.ProgramVersion)} on {dotnet} ({os}, {archName})", true);

            if (args.Length > 0)
                Logger.Write($"Parsed arguments: {string.Join(" ", args)}", true);
        }

        /// <summary>
        /// Checks if there is a later release of the application on GitHub and notifies the user.
        /// </summary>
        public static void CheckLatestRelease()
        {
            // Skip if disabled
            if (Globals.GitHubVersionCheck == false)
                return;

            string gitHubRepo = "mrsilver76/listporter";
            var result = GitHubVersionChecker.CheckLatestRelease(Globals.ProgramVersion, gitHubRepo, Path.Combine(Globals.AppDataPath, "versionCheck.ini"));

            if (result.UpdateAvailable)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"  ℹ️ A new version ({VersionHelper.OutputVersion(result.LatestVersion)}) is available!");
                Console.ResetColor();
                Console.WriteLine($" You are using {VersionHelper.OutputVersion(Globals.ProgramVersion)}");
                Console.WriteLine($"     Get it from https://www.github.com/{gitHubRepo}/");
            }
        }

        /// <summary>
        /// Displays the final results of the playlist import process. If there has been any errors, then some
        /// useful information is also displayed to the console.
        /// </summary>
        public static void DisplayResults()
        {
            Logger.Write($"Playlist statistics: {PlexService.TotalPlaylistsSkipped} skipped, {PlexService.TotalPlaylistsCreated} created, {PlexService.TotalPlaylistsUpdated} updated and {PlexService.TotalPlaylistsDeleted} deleted.");
            Logger.Write($"ListPorter finished.");

            if (PlexService.TotalImportErrors > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($" ⚠️ Warning: {GrammarHelper.Pluralise(PlexService.TotalImportErrors, "track", "tracks")} couldn't be found in the Plex database!");
                Console.ResetColor();
                Console.WriteLine("    This can happen if paths differ or Plex hasn’t scanned new files yet.");
                Console.WriteLine("    For more information, please read the FAQ:");
                Console.WriteLine("      https://github.com/mrsilver76/listporter/FAQ.md#tracks");
            }
        }

        /// <summary>
        /// Display a warning if there were any fuzzy match conflicts during the import process.
        /// </summary>
        /// <param name="count"></param>
        public static void DisplayFuzzyMatchConflicts(int count)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($" ⚠️ Warning: {GrammarHelper.Pluralise(count, "track", "tracks")} have conflicting artist/album/track paths!");
            Console.ResetColor();
            Console.WriteLine("    Fuzzy matching cannot resolve duplicates with the same structure.");
            Console.WriteLine("    For more information, please read the FAQ:");
            Console.WriteLine("      https://github.com/mrsilver76/listporter/FAQ.md#fuzzy");
            
            System.Environment.Exit(-1);
        }

    }
}
