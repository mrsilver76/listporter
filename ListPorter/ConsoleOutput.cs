/*
 * ListPorter - Upload standard or extended .m3u playlist files to Plex Media Server.
 * Copyright (C) 2020-2026 Richard Lawrence
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
using System.Text;
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
            Console.WriteLine($"Usage: {System.AppDomain.CurrentDomain.FriendlyName} -s <address>[:<port>] -t <token> [-l <library>] -i <path> [options]\n" +
                                "Upload standard or extended .m3u playlist files to Plex Media Server.\n");


            if (string.IsNullOrEmpty(errorMessage))
                Console.WriteLine($"This is version {VersionHelper.OutputVersion(Globals.ProgramVersion)}, copyright © 2020-{DateTime.Now.Year} Richard Lawrence.\n" +
                                    "Music & multimedia icon by paonkz - Flaticon (https://www.flaticon.com/free-icons/music-and-multimedia)\n");

            PrintOptionsWithDescriptions();

            Console.WriteLine($"Logs are written to {Path.Combine(Globals.AppDataPath, "Logs")}");

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {errorMessage}");
                Environment.Exit(1);
            }
            Environment.Exit(0);
        }

        /// <summary>
        /// Outputs the command line options and their descriptions in a formatted manner, grouped by sections.
        /// </summary>
        static void PrintOptionsWithDescriptions()
        {
            // Define sections and their options + descriptions
            var sections = new Dictionary<string, (string option, string description)[]>
            {
                ["Mandatory arguments"] =
                [
                    ("-s, --server <address>[:<port>]", "Plex server address. Prefix with https:// for a secure connection. The port is optional and defaults to 32400."),
                    ("-t, --token <token>", "Plex authentication token used to access the server."),
                    ("-i, --import <path>", "Path to a single .m3u or .m3u8 playlist file, or to a directory containing playlist files to import.")
                ],

                ["Library options"] =
                [
                    ("-l, --library <library>", "Numeric ID of the Plex music library that the playlists will be uploaded to. Optional when the server has only one music library. Required when multiple music libraries exist.")
                ],

                ["Playlist sync options"] =
                [
                    ("-d, --delete", "Delete all existing playlists from the Plex library before importing."),
                    ("-m, --mirror", "Mirror the playlists in the Plex library to match the imported files. Any Plex playlist that is not among the playlist files processed in this run will be deleted.")
                ],

                ["Path rewriting options"] =
                [
                    ("-u, --unix", "Force forward slashes in song paths. Use this when the Plex server is running on Linux or another Unix-like system."),
                    ("-w, --windows", "Force backslashes in song paths. Use this when the Plex server is running on Windows."),
                    ("-f, --find <text>", "Find text within each song path so that it can be replaced. Disables fuzzy path matching."),
                    ("-r, --replace <text>", "Replace the text found by --find with <text>. If omitted, the found text is removed. Requires --find."),
                    ("-b, --base-path <path>", "Base path to prepend to relative song paths in the playlist. Disables fuzzy path matching."),
                    ("-x, --exact-only", "Disable fuzzy path matching so that only exact path matches are used. By default, if no exact match is found then the last three parts of the path (artist, album and track) are compared.")
                ],

                ["Other options"] =
                [
                    ("-k, --update", "Trigger a Plex library update and wait for it to complete before importing the playlists."),
                    ("-v, --verbose", "Write verbose output to the log files."),
                    ("-nc, --no-check", "Do not check GitHub for newer versions of ListPorter."),
                    ("/?, -h, --help", "Display this help message and the log file location, then exit.")
                ]
            };

            // Determine max line width (at least 50 chars, or console width - 5)
            int maxLineWidth = Math.Max(Console.WindowWidth, 50) - 5;

            // Find max option length across all sections for consistent column width
            int firstColWidth = 0;
            foreach (var section in sections.Values)
                foreach (var (option, _) in section)
                    firstColWidth = Math.Max(firstColWidth, option.Length);
            firstColWidth += 2; // 2-character gap

            // Print each section followed by the options
            foreach (var (header, options) in sections)
            {
                // Section header
                Console.WriteLine(header + ":");

                // Each option + description
                foreach (var (option, description) in options)
                {
                    // Wrap description
                    var wrapped = WrapText(description, maxLineWidth - firstColWidth - 1); // -1 for extra indent
                    bool firstLine = true;

                    foreach (var line in wrapped)
                    {
                        if (firstLine)
                        {
                            // first line: 1-char indent + option + description
                            Console.WriteLine(" " + option.PadRight(firstColWidth) + line);
                            firstLine = false;
                        }
                        else
                        {
                            // wrapped lines: 1-char indent + firstColWidth spaces + 1 space + text
                            Console.WriteLine(new string(' ', firstColWidth + 2) + line);
                        }
                    }
                }

                Console.WriteLine(); // single newline between sections
            }

        }

        /// <summary>
        /// Given a block of text and a maximum line width, yields lines of text wrapped at word boundaries.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="maxWidth"></param>
        /// <returns></returns>
        private static IEnumerable<string> WrapText(string text, int maxWidth)
        {
            var words = text.Split(' ');
            var line = new StringBuilder();

            foreach (var word in words)
            {
                if (line.Length + word.Length + (line.Length > 0 ? 1 : 0) > maxWidth)
                {
                    yield return line.ToString();
                    line.Clear();
                }

                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }

            if (line.Length > 0) yield return line.ToString();
        }


        /// <summary>
        /// Displays the application header and configuration details in the console.
        /// </summary>
        /// <remarks>This method outputs a formatted header, including the application name, version,
        /// copyright information,  and a brief description of its functionality. It also displays key configuration
        /// details such as the Plex  server connection, library ID, import path, and any additional flags or path
        /// rewriting options.</remarks>
        public static void ShowHeader()
        {
            string line = new('─', 70);
            Console.WriteLine(line);
            Console.WriteLine($"ListPorter {VersionHelper.OutputVersion(Globals.ProgramVersion)}");
            Console.WriteLine($"Copyright © 2020-{DateTime.Now.Year} Richard Lawrence");
            Console.WriteLine("https://github.com/mrsilver76/listporter");
            Console.WriteLine();

            // Prepare titles + content
            var items = new List<(string Title, string Value)>
            {
                ("Plex server:", $"{(Globals.UsingSecureConnection ? "https://" : "http://")}{Globals.PlexHost}:{Globals.PlexPort}"),
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

            // Find longest title length
            int pad = items.Max(i => i.Title.Length) + 2;

            // Print everything
            foreach (var (title, value) in items)
                Console.WriteLine($"{title.PadRight(pad)}{value}");

            Console.WriteLine(line);
            Console.WriteLine();

            // Log details
            LogEnvironmentInfo();
        }

        /// <summary>
        /// Output to the logs the environment information, such as .NET version, OS and architecture.
        /// Also includes the parsed command line arguments if any were provided.
        /// </summary>
        private static void LogEnvironmentInfo()
        {
            var dotnet = RuntimeInformation.FrameworkDescription;
            var os = RuntimeInformation.OSDescription.Trim();

            var archName = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

            Logger.Write($"Running {VersionHelper.OutputVersion(Globals.ProgramVersion)} on {dotnet} ({os}, {archName})", true);

            Logger.Write($"Command line: {Environment.CommandLine}", true);
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
                Console.WriteLine($"  ℹ️ A new version ({VersionHelper.OutputVersion(result.LatestVersion)}) is available!");
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
            
            System.Environment.Exit(1);
        }

    }
}
