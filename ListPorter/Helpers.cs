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

using IniParser;
using IniParser.Model;
using System.Text.RegularExpressions;
using static ListPorter.Program;

namespace ListPorter
{
    public static class Helpers
    {
        /// <summary>
        /// Parses command line arguments.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void ParseArguments(string[] args)
        {
            if (args.Length == 0)
                DisplayUsage();

            // Loop through all arguments
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();

                if (arg == "/?" || arg == "-h" || arg == "--help")
                    DisplayUsage();
                else if (arg == "-s" || arg == "--server" && i + 1 < args.Length)
                {
                    string serverArg = args[i + 1];
                    i++; // Skip next argument as it's the value
                    if (serverArg.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        usingSecureConnection = true;
                        serverArg = serverArg.Substring("https://".Length);
                    }
                    else if (serverArg.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        usingSecureConnection = false;
                        serverArg = serverArg.Substring("http://".Length);
                    }
                    else
                    {
                        // Default to false or your current default
                        usingSecureConnection = false;
                    }
                    // Now split on ':'
                    string[] bits = serverArg.Split(':');

                    if (bits.Length == 1)
                        plexHost = bits[0];
                    else if (bits.Length == 2)
                    {
                        plexHost = bits[0];
                        if (int.TryParse(bits[1], out int port))
                            plexPort = port;
                        else
                            DisplayUsage($"Invalid Plex port ({bits[1]})");
                    }
                    else
                    {
                        DisplayUsage($"Invalid format of Plex host and port ({serverArg})");
                    }
                }
                else if (arg == "-t" || arg == "--token" && i + 1 < args.Length)
                {
                    plexToken = args[i + 1];
                    i++; // Skip next argument as it's the value
                }
                else if (arg == "-l" || arg == "--library" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int level))
                    {
                        plexLibrary = level;
                        i++; // Skip next argument as it's the value
                    }
                    else
                    {
                        DisplayUsage("Invalid Plex library ID.");
                    }
                }
                else if (arg == "-i" || arg == "--import" && i + 1 < args.Length)
                {
                    pathToImport = args[i + 1];
                    i++;
                }
                else if (arg == "-b" || arg == "--base-path" && i + 1 < args.Length)
                {
                    basePath = args[i + 1];
                    i++;
                }
                else if (arg == "-d" || arg == "--delete-all")
                    deleteAll = true;
                else if (arg == "-m" || arg == "--mirror")
                    mirrorPlaylists = true;
                else if (arg == "-f" || arg == "--find" && i + 1 < args.Length)
                {
                    findText = args[i + 1];
                    i++;
                }
                else if (arg == "-r" || arg == "--replace" && i + 1 < args.Length)
                {
                    replaceText = args[i + 1];
                    i++;
                }
                else if (arg == "-u" || arg == "--unix" || arg == "--linux")  // Allow --linux as an alias for --unix
                    pathStyle = PathStyle.ForceLinux;
                else if (arg == "-w" || arg == "--windows")
                    pathStyle = PathStyle.ForceWindows;
                else if (arg == "-v" || arg == "--verbose")
                    verboseMode = true;
                else if (arg == "-x" || arg == "--exact-only")  // Disable fuzzy matching
                    useFuzzyMatching = false;
                else if (arg[0] == '/' || arg[0] == '-')
                    DisplayUsage($"Unknown option: {arg}");
            }

            // Sanity checks here

            if (string.IsNullOrEmpty(plexToken))
                DisplayUsage("Missing Plex token (-t)");

            if (plexLibrary < 0)
                DisplayUsage("Missing Plex library ID (-l)");

            if (string.IsNullOrEmpty(pathToImport))
                DisplayUsage("Missing path or filename of playlists to import (-i)");

            if (string.IsNullOrEmpty(findText) && !string.IsNullOrEmpty(replaceText))
                DisplayUsage($"No text to find defined for replacement text ('{replaceText}')");

            if (pathToImport != null && !Directory.Exists(pathToImport) && !File.Exists(pathToImport))
                DisplayUsage($"Path to import does not exist ({pathToImport})");

            // If path rewriting is enabled, turn off fuzzy path matching

            if (!string.IsNullOrEmpty(findText) || pathStyle != PathStyle.Auto || !string.IsNullOrEmpty(basePath))
            {
                usingPathRewriting = true;
                useFuzzyMatching = false;
                Logger("Path rewriting enabled, fuzzy path matching is disabled.", true);
            }
        }

        /// <summary>
        /// Displays the usage information to the user and optional error message.
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        public static void DisplayUsage(string errorMessage = "")
        {
            Console.WriteLine($"Usage: {System.AppDomain.CurrentDomain.FriendlyName} -s <address>[:<port>] -t <token> -l <library> -i <path> [options]\n" +
                                "Upload standard or extended .m3u playlist files to Plex Media Server.\n");


            if (string.IsNullOrEmpty(errorMessage))
                Console.WriteLine($"This is version {OutputVersion(version)}, copyright © 2020-{DateTime.Now.Year} Richard Lawrence.\n" +
                                    "Music & multimedia icon by paonkz - Flaticon (https://www.flaticon.com/free-icons/music-and-multimedia)\n");

            Console.WriteLine("Mandatory arguments:\n" +
                                "   -s, --server <address>[:<port>]    Plex server address.\n" +
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
                                "    -v, --verbose                     Verbose output to log files.\n" +
                                "\n" +
                               $"Logs are written to {Path.Combine(appDataPath, "Logs")}");

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {errorMessage}");
                Environment.Exit(-1);
            }
            Environment.Exit(0);
        }

        /// <summary>
        /// Defines the location for logs and deletes any old log files
        /// </summary>
        public static void InitialiseLogger()
        {
            // Set the path for the application data folder
            appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ListPorter");

            // Set the log folder path to be inside the application data folder
            string logFolderPath = Path.Combine(appDataPath, "Logs");

            // Create the folder if it doesn't exist
            Directory.CreateDirectory(logFolderPath);

            // Delete log files older than 14 days
            var logFiles = Directory.GetFiles(logFolderPath, "*.log");
            foreach (var file in logFiles)
            {
                DateTime lastModified = File.GetLastWriteTime(file);
                if ((DateTime.Now - lastModified).TotalDays > 14)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Logger($"Error deleting log file {file}: {ex.Message}", true);
                    }
                }
            }

            // Delete old "Plex Playlist Uploader" and "PlexPU" folders following name change. It's not worth keeping
            // the old folders around, as they were only used for the first few versions of this app and the logs in
            // them incorrectly expose the Plex token.
            try
            {
                Directory.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Plex Playlist Uploader"));
            }
            catch { } // Ignore errors if the folder doesn't exist
            try
            {
                Directory.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlexPU"));
            }
            catch { } // Ignore errors if the folder doesn't exist
        }

        /// <summary>
        /// Writes a message to the log file for debugging.
        /// </summary>
        /// <param name="message">Message to output</param>
        /// <param name="verbose">Verbose output, only for the logs</param>

        public static void Logger(string message, bool verbose = false)
        {
            // Define the path and filename for this log
            string logFile = DateTime.Now.ToString("yyyy-MM-dd");
            logFile = Path.Combine(appDataPath, "Logs", $"log-{logFile}.log");

            // Define the timestamp
            string tsTime = DateTime.Now.ToString("HH:mm:ss");
            string tsDate = DateTime.Now.ToString("yyyy-MM-dd");

            // Don't store the Plex Token or Machine ID in any logs
            if (!string.IsNullOrEmpty(plexToken))
                message = message.Replace(plexToken, "[PLEXTOKEN]", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(machineIdentifier))
                message = message.Replace(machineIdentifier, "[MACHINEID]", StringComparison.OrdinalIgnoreCase);

            // Write to file
            File.AppendAllText(logFile, $"[{tsDate} {tsTime}] {message}{Environment.NewLine}");

            // If this isn't verbose output for the logfiles, then output to the console
            if (!verbose)
                Console.WriteLine($"[{tsTime}] {message}");
        }

        /// <summary>
        /// Checks if there is a later release of the application on GitHub and notifies the user.
        /// </summary>
        public static void CheckLatestRelease()
        {
            string gitHubRepo = "mrsilver76/listporter";
            string iniPath = Path.Combine(appDataPath, "versionCheck.ini");

            var parser = new FileIniDataParser();
            IniData ini = File.Exists(iniPath) ? parser.ReadFile(iniPath) : new IniData();

            if (NeedsCheck(ini, out Version? cachedVersion))
            {
                var latest = TryFetchLatestVersion(gitHubRepo);
                if (latest != null)
                {
                    ini["Version"]["LatestReleaseChecked"] = latest.Value.Timestamp;

                    if (!string.IsNullOrEmpty(latest.Value.Version))
                    {
                        ini["Version"]["LatestReleaseVersion"] = latest.Value.Version;
                        cachedVersion = ParseSemanticVersion(latest.Value.Version);
                    }

                    parser.WriteFile(iniPath, ini); // Always write if we got any response at all
                }
            }

            if (cachedVersion != null && cachedVersion > version)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(      $" ℹ️ A new version ({OutputVersion(cachedVersion)}) is available!");
                Console.ResetColor();
                Console.WriteLine($" You are using {OutputVersion(version)}");
                Console.WriteLine(  $"    Get it from https://www.github.com/{gitHubRepo}/");
            }
        }

        /// <summary>
        /// Takes a semantic version string in the format "major.minor.revision" and returns a Version object in
        /// the format "major.minor.0.revision"
        /// </summary>
        /// <param name="versionString"></param>
        /// <returns></returns>
        public static Version? ParseSemanticVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                return null;

            var parts = versionString.Split('.');
            if (parts.Length != 3)
                return null;

            if (int.TryParse(parts[0], out int major) &&
                int.TryParse(parts[1], out int minor) &&
                int.TryParse(parts[2], out int revision))
            {
                try
                {
                    return new Version(major, minor, 0, revision);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Compares the last checked date and version in the INI file to determine if a check is needed.
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="cachedVersion"></param>
        /// <returns></returns>
        private static bool NeedsCheck(IniData ini, out Version? cachedVersion)
        {
            cachedVersion = null;

            string dateStr = ini["Version"]["LatestReleaseChecked"];
            string versionStr = ini["Version"]["LatestReleaseVersion"];

            bool hasTimestamp = DateTime.TryParse(dateStr, out DateTime lastChecked);
            bool isExpired = !hasTimestamp || (DateTime.UtcNow - lastChecked.ToUniversalTime()).TotalDays >= 7;

            cachedVersion = ParseSemanticVersion(versionStr);

            return isExpired;
        }

        /// <summary>
        /// Fetches the latest version from the GitHub repo by looking at the releases/latest page.
        /// </summary>
        /// <param name="repo">The name of the repo</param>
        /// <returns>Version and today's date and time</returns>
        private static (string? Version, string Timestamp)? TryFetchLatestVersion(string repo)
        {
            string url = $"https://api.github.com/repos/{repo}/releases/latest";
            using var client = new HttpClient();

            string ua = repo.Replace('/', '.') + "/" + OutputVersion(version);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(ua);

            try
            {
                var response = client.GetAsync(url).GetAwaiter().GetResult();
                string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                if (!response.IsSuccessStatusCode)
                {
                    // Received response, but it's a client or server error (e.g., 404, 500)
                    return (null, timestamp);  // Still update "last checked"
                }

                string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var match = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                if (!match.Success)
                {
                    return (null, timestamp);  // Response body not as expected
                }

                string version = match.Groups[1].Value.TrimStart('v', 'V');
                return (version, timestamp);
            }
            catch
            {
                // This means we truly couldn't reach GitHub at all
                return null;
            }
        }

        /// <summary>
        /// Pluralises a string based on the number provided.
        /// </summary>
        /// <param name="number"></param>
        /// <param name="singular"></param>
        /// <param name="plural"></param>
        /// <returns></returns>
        public static string Pluralise(int number, string singular, string plural)
        {
            return number == 1 ? $"{number} {singular}" : $"{number:N0} {plural}";
        }

        /// <summary>
        /// Given a .NET Version object, outputs the version in a semantic version format.
        /// If the build number is greater than 0, it appends `-preX` to the version string.
        /// </summary>
        /// <returns></returns>
        public static string OutputVersion(Version? netVersion)
        {
            if (netVersion == null)
                return "0.0.0";

            // Use major.minor.revision from version, defaulting patch to 0 if missing
            int major = netVersion.Major;
            int minor = netVersion.Minor;
            int revision = netVersion.Revision >= 0 ? netVersion.Revision : 0;

            // Build the base semantic version string
            string result = $"{major}.{minor}.{revision}";

            // Append `-preX` if build is greater than 0
            if (netVersion.Build > 0)
            {
                result += $"-pre{netVersion.Build}";
            }

            return result;
        }
    }
}