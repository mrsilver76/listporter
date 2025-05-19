using System.Reflection;
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
                    string[] bits = args[i + 1].Split(':');
                    if (bits.Length == 1)
                    {
                        // We've only been provided with the host
                        plexHost = args[i + 1];
                    }
                    else if (bits.Length == 2)
                    {
                        plexHost = bits[0];
                        if (int.TryParse(bits[1], out int port))
                            plexPort = port;
                        else
                            DisplayUsage($"Invalid Plex port ({bits[1]})");
                    }
                    else
                        DisplayUsage($"Invalid format of Plex host and port ({args[i + 1]})");
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
                else if (arg == "-u" || arg == "--unix")
                    pathStyle = PathStyle.ForceLinux;
                else if (arg == "-w" || arg == "--windows")
                    pathStyle = PathStyle.ForceWindows;
                else if (arg == "-v" || arg == "--verbose")
                    logAPIrequests = true;
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

        }

        /// <summary>
        /// Displays the usage information to the user and optional error message.
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        public static void DisplayUsage(string errorMessage = "")
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version!;

            Console.WriteLine($"Usage: {System.AppDomain.CurrentDomain.FriendlyName} -s <address>[:<port>] -t <token> -l <library> -i <path> [options]\n" +
                                "Upload standard or extended .m3u playlist files to Plex Media Server.\n");


            if (string.IsNullOrEmpty(errorMessage))
                Console.WriteLine($"This is version v{version.Major}.{version.Minor}.{version.Revision}, copyright © 2020-{DateTime.Now.Year} Richard Lawrence.\n" +
                                    "Music & multimedia icon by paonkz - Flaticon (https://www.flaticon.com/free-icons/music-and-multimedia)\n");

            Console.WriteLine("Mandatory arguments:\n" +
                                "   -s, --server <address>[:<port>]    Plex server address (with optional port)\n" +
                                "   -t, --token <token>                Plex authentication token.\n" +
                                "   -l, --library <library>            Plex library ID to use.\n" +
                                "   -i, --import <path>                Path to a playlist file or directory.\n\n" +
                                "Optional arguments:\n" +
                                "  Playlist sync options:\n" +
                                "    -d, --delete                      Delete all playlists from Plex library on start.\n" +
                                "    -m, --mirror                      Mirror Plex library to match playlists provided.\n" +
                                "\n" +
                                "  Path rewriting options:\n" +
                                "    -u, --unix                        Force forward slashes in song paths, for Linux Plex servers.\n" +
                                "    -w, --windows                     Force backslashes in song paths, for Windows Plex servers.\n" +
                                "    -f, --find <text>                 Find text within the song path.\n" +
                                "    -r, --replace <text>              Replace found text in song path with <text>.\n" +
                                "\n" +
                               $"Logs are written to {Path.Combine(appDataPath, "Logs")}\n");

            if (!string.IsNullOrEmpty(errorMessage))
            {
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
                        cachedVersion = Version.Parse(latest.Value.Version);
                    }

                    parser.WriteFile(iniPath, ini); // Always write if we got any response at all
                }
            }

            var localVersion = Assembly.GetExecutingAssembly().GetName().Version!;
            if (cachedVersion != null && cachedVersion > localVersion)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"   A new version ({cachedVersion}) is available!");
                Console.ResetColor();
                Console.WriteLine($" You are using {localVersion.Major}.{localVersion.Minor}.{localVersion.Revision}");
                Console.WriteLine($"    Get it from https://www.github.com/{gitHubRepo}/");
            }
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

            if (Version.TryParse(versionStr ?? "", out Version? parsed))
                cachedVersion = parsed;

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

            Version? localVersion = Assembly.GetExecutingAssembly().GetName().Version!;
            string ua = repo.Replace('/', '.') + "/" + localVersion;
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
    }
}