using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Net.Http;
using System.Text;
using static ListPorter.Helpers;
using System.Reflection;
using System.Diagnostics.Contracts;

namespace ListPorter
{
    /// <summary>
    /// Class representing a Plex playlist with its ratingKey, title, and track count.
    /// </summary>
    public class plexPlaylist
    {
        public long ratingKey { get; set; }
        public string playlistTitle { get; set; } = string.Empty;
        public long trackCount { get; set; }
    }

    /// <summary>
    /// Class representing an audio track with its ratingKey and file path. We will use this
    /// in two ways (1) to store every single song stored in Plex, and (2) to store
    /// the contents of a playlist that we are importing.
    /// </summary>
    public class TrackInfo
    {
        public long ratingKey { get; set; }
        public string filePath { get; set; } = string.Empty;
    }

    class Program
    {
        // User preferences, changed through the command line
        public static string plexHost = "127.0.0.1"; // IP address of the Plex server
        public static int plexPort = 32400; // Port of the Plex server
        public static string plexToken = ""; // Plex token for authentication
        public static int plexLibrary = -1; // Library ID to use
        public static string pathToImport = ""; // Path to import M3U files
        public static bool deleteAll = false; // Delete all playlists before importing
        public static bool mirrorPlaylists = false; // Mirror playlists
        public static string findText = ""; // Text to find in file paths
        public static string replaceText = ""; // Text to replace in file paths
        public static bool verboseMode = false; // Output verbose messages (API calls and lookup results)
        public enum PathStyle
        {
            Auto, // Do nothing
            ForceWindows, // Replace slashes with backslashes
            ForceLinux // Replace backslashes with slashes
        }
        public static PathStyle pathStyle = PathStyle.Auto; // By default, do nothing

        // Internal globals
        public static List<string> M3UFilesToImport = new List<string>(); // List of m3u files to process
        public static string importedPlaylistTitle = ""; // Name of the playlist we're importing
        public static string machineIdentifier = ""; // ID used to upload playlists
        public static string appDataPath = ""; // Path to the app data folder
        public static HashSet<string> processedPlaylistTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // List of processed playlist titles
        public static Version version = Assembly.GetExecutingAssembly().GetName().Version!;
        public static List<TrackInfo> plexTrackList = new List<TrackInfo>(); // List of all audio tracks in the Plex library
        public static List<TrackInfo> importedPlaylist = new List<TrackInfo>(); // List of tracks in the imported playlist
        public static List<plexPlaylist> plexPlaylistList = new List<plexPlaylist>(); // List of playlists fetched from Plex

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Set up various paths and prepare logging
            InitialiseLogger();

            // Parse the arguments
            ParseArguments(args);

            Console.WriteLine($"ListPorter v{OutputVersion()}, Copyright © 2020-{DateTime.Now.Year} Richard Lawrence");
            Console.WriteLine($"Upload standard or extended .m3u playlist files to Plex Media Server.");
            Console.WriteLine($"https://github.com/mrsilver76/listporter\n");
            Console.WriteLine($"This program comes with ABSOLUTELY NO WARRANTY. This is free software,");
            Console.WriteLine($"and you are welcome to redistribute it under certain conditions; see");
            Console.WriteLine($"the documentation for details.");
            Console.WriteLine();

            Logger($"Starting ListPorter...");

            // Don't call any earlier, otherwise plexToken and machineIdentifier will be in the logs
            Logger($"Parsed arguments: {string.Join(" ", args)}", true);

            // Check connectivity
            if (CheckPlexConnectivity() == false)
                System.Environment.Exit(1);

            // Fetch and store all tracks from the Plex library
            FetchAndStoreTracks();

            // Fetch and store all playlists from Plex

            // We're going to display a status update here because if we put this inside the method then
            // it will be called multiple times and we don't want to see the same message over and over again
            Logger($"Fetching playlists from Plex...");
            FetchAndStorePlaylists();
            Logger($"Found {Pluralise(plexPlaylistList.Count, "playlist", "playlists")} matching criteria in library ID {plexLibrary}.");

            // Find M3U/M3U8 files to import from the specified directory
            FindM3UToImport(pathToImport);

            // Delete all playlists in Plex if the option is enabled
            if (deleteAll)
                DeleteAllPlaylists();

            // Loop through each M3U file found and process the playlist
            foreach (var m3uFile in M3UFilesToImport)
                ProcessPlaylist(m3uFile);

            // Mirror playlists if the option is enabled
            if (mirrorPlaylists)
                MirrorPlaylists();

            // Finished
            Logger($"ListPorter finished.");
            CheckLatestRelease();
            System.Environment.Exit(0);
        }

        /// <summary>
        /// Fetches all playlists from the Plex server, filters them based on the current library, 
        /// and stores the details in the static list `plexPlaylist.plexPlaylistList`.
        /// Only playlists with all tracks in the specified Plex library are included.
        /// </summary>

        public static void FetchAndStorePlaylists()
        {
            plexPlaylistList.Clear();

            string urlPath = "/playlists";
            string responseContent = GetHttpResponse(HttpMethod.Get, urlPath);
            int count = 0;

            var playlists = XElement.Parse(responseContent)
                                    .Elements("Playlist")
                                    .Where(x => x.Attribute("smart")?.Value == "0" && x.Attribute("playlistType")?.Value == "audio");

            foreach (var playlist in playlists)
            {
                string? ratingKeyStr = playlist.Attribute("ratingKey")?.Value;
                string? title = playlist.Attribute("title")?.Value;
                string? leafCountStr = playlist.Attribute("leafCount")?.Value;

                // Try parse numeric fields
                _ = long.TryParse(ratingKeyStr, out long ratingKey);
                _ = long.TryParse(leafCountStr, out long trackCount);

                // Validation logic
                if (ratingKey <= 0)
                {
                    Logger($"Skipping playlist (invalid ratingKey): title='{title ?? "Unknown"}', ratingKey='{ratingKeyStr ?? "null"}', trackCount='{leafCountStr ?? "null"}'", true);
                    continue;
                }

                if (string.IsNullOrEmpty(title))
                {
                    Logger($"Skipping playlist (missing title): ratingKey='{ratingKey}', trackCount='{leafCountStr ?? "null"}'", true);
                    continue;
                }

                if (trackCount <= 0)
                {
                    Logger($"Skipping playlist (no tracks): title='{title}', ratingKey='{ratingKey}', trackCount='{leafCountStr ?? "null"}'", true);
                    continue;
                }

                // Library check
                if (!IsAllPlaylistContentInThisLibrary(ratingKey))
                {
                    Logger($"Skipping playlist (contains tracks from other libraries): title='{title}', ratingKey='{ratingKey}'", true);
                    continue;
                }

                // If valid, add to list
                Logger($"Found playlist: {title}", true);
                plexPlaylistList.Add(new plexPlaylist
                {
                    ratingKey = ratingKey,
                    playlistTitle = title,
                    trackCount = trackCount
                });
                count++;
            }

            Logger($"Found {Pluralise(count, "playlist", "playlists")} on Plex.", true);
        }

        /// <summary>
        /// Fetches all audio tracks stored in the specified Plex library section and stores their details
        /// (ratingKey and file path) in the static list `TrackInfo.plexTrackList`.
        /// </summary>
        public static void FetchAndStoreTracks()
        {
            Logger("Searching for audio tracks on Plex. This may take a while...");

            const int pageSize = 2000;
            int start = 0;

            while (true)
            {
                string urlPath = $"/library/sections/{plexLibrary}/all?type=10&X-Plex-Container-Start={start}&X-Plex-Container-Size={pageSize}";
                var container = XElement.Parse(GetHttpResponse(HttpMethod.Get, urlPath));

                ExtractTracksFromContainer(container);

                // If there is no next page or we've gone beyond totalSize then break out
                // of the loop
                if (!int.TryParse(container.Attribute("size")?.Value, out int size) ||
                    !int.TryParse(container.Attribute("totalSize")?.Value, out int totalSize) ||
                    size == 0 || start + size >= totalSize)
                    break;

                start += size;
            }

            Logger($"Found {Pluralise(plexTrackList.Count, "audio track", "audio tracks")} on Plex.");
        }

        /// <summary>
        /// Extracts audio tracks from a given XML container element and adds them to the static list `TrackInfo.plexTrackList`.
        /// </summary>
        /// <param name="container"></param>
        private static void ExtractTracksFromContainer(XElement container)
        {
            foreach (var track in container.Elements("Track"))
            {
                var media = track.Element("Media")?.Element("Part");
                string? filePath = media?.Attribute("file")?.Value;
                string? ratingKeyStr = track.Attribute("ratingKey")?.Value;

                bool isDeleted = media?.Attribute("deletedAt") != null;

                if (string.IsNullOrEmpty(ratingKeyStr) || !long.TryParse(ratingKeyStr, out long ratingKey) || ratingKey <= 0)
                {
                    Logger($"Track skipped: missing/invalid ratingKey for: {filePath ?? "[unknown path]"}");
                    continue;
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    Logger($"Track skipped: missing file path for ratingKey {ratingKey}.");
                    continue;
                }

                if (isDeleted)
                    Logger($"{filePath} (ratingKey: {ratingKey}) marked as deleted, adding anyway.", true);

                plexTrackList.Add(new TrackInfo
                {
                    ratingKey = ratingKey,
                    filePath = filePath
                });

                if (verboseMode)
                    Logger($"Found track: {filePath} (ratingKey: {ratingKey})", true);
            }
        }

        /// <summary>
        /// Sends an HTTP request to the Plex API and returns the response as a string.
        /// </summary>
        /// <param name="method">The HTTP method to use (e.g., GET, POST).</param>
        /// <param name="urlPath">The API endpoint path to send the request to.</param>
        /// <param name="body">Optional request body for POST or PUT requests.</param>
        /// <returns>The response content as a string.</returns>
        /// <exception cref="Exception">Thrown if the HTTP request fails.</exception>

        public static string GetHttpResponse(HttpMethod method, string urlPath, string? body = null)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                // Construct the full URL
                string fullUrl = $"http://{plexHost}:{plexPort}{urlPath}";

                // Create the HttpRequestMessage
                var request = new HttpRequestMessage(method, fullUrl);
                request.Headers.Add("X-Plex-Token", plexToken);

                // Add the body if needed
                if (!string.IsNullOrEmpty(body) && (method == HttpMethod.Post || method == HttpMethod.Put))
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                if (verboseMode)
                    Logger($"{method}: {fullUrl}", true);

                try
                {
                    // Send the request synchronously
                    var response = httpClient.Send(request);

                    // Ensure the response is successful
                    response.EnsureSuccessStatusCode();

                    // Read and return the response content
                    string responseText = response.Content.ReadAsStringAsync().Result;
                    if (verboseMode)
                        Logger($"Received {responseText.Length} bytes sucessfully.", true);
                    return responseText;
                }
                catch (HttpRequestException ex)
                {
                    string fullMessage = GetFullExceptionMessage(ex);
                    throw new Exception($"Error with request: {fullMessage}");
                }
            }
        }

        /// <summary>
        /// Loads an M3U playlist from a file, parses it, and populates the importedPlaylist list.
        /// If no #PLAYLIST directive is found, the filename (excluding path and extension) is used as the playlist title.
        /// </summary>
        /// <param name="filePath">The full path to the M3U file.</param>
        /// <returns>true if at least one item within the playlist was loaded successfully</returns>
        public static bool LoadM3UPlaylist(string filePath)
        {
            importedPlaylistTitle = "";
            importedPlaylist.Clear();

            if (!File.Exists(filePath))
            {
                Logger($"M3U file not found: {filePath}");
                return false;
            }

            Logger($"Loading playlist: {filePath}");

            var lines = File.ReadAllLines(filePath);
            bool playlistTitleFound = false;
            int failed = 0, added = 0;

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith('#') && !line.StartsWith("#PLAYLIST:"))
                    continue;

                if (line.StartsWith("#PLAYLIST:"))
                {
                    importedPlaylistTitle = line.Substring(10).Trim();
                    playlistTitleFound = true;
                    Logger($"Found #PLAYLIST, title is: {importedPlaylistTitle}", true);
                }
                else
                {
                    // Rewrite any changes to the location of the file
                    line = RewriteLocation(line);

                    // See if we can find the item stored within Plex
                    long ratingKey = GetRatingKeyFromFilePath(line);

                    if (ratingKey == -1)
                    {
                        Logger($"Warning: No Plex ratingKey for: {line}", true);
                        failed++;
                    }
                    else
                    {
                        // Add to the imported playlist
                        importedPlaylist.Add(new TrackInfo
                        {
                            ratingKey = ratingKey,
                            filePath = line
                        });
                        added++;

                        if (verboseMode)
                            Logger($"Added to playlist: {line} (ratingKey: {ratingKey})", true);
                    }
                }
            }

            // Fallback to using the file name if no #PLAYLIST is found
            if (!playlistTitleFound)
            {
                importedPlaylistTitle = Path.GetFileNameWithoutExtension(filePath);
                Logger($"Missing #PLAYLIST, assuming title: {importedPlaylistTitle}", true);
            }

            if (added == 0 && failed == 0)
            {
                Logger($"Warning: '{importedPlaylistTitle}' is empty!");
                return false;
            }

            if (added == 0 && failed > 0)
            {
                Logger($"Warning: All {Pluralise(failed, "item", "items")} in '{importedPlaylistTitle}' failed to match Plex database!");
                return false;
            }

            if (added > 0 && failed == 0)
            {
                Logger($"All {Pluralise(added, "item", "items")} in '{importedPlaylistTitle}' matched to Plex database.", true);
                return true;
            }

            // If we get here, we have a mix of added and failed items
            Logger($"Warning: {Pluralise(failed, "item", "items")} could not be matched to Plex database!");
            Logger($"Only {Pluralise(added, "item", "items")} will be added to '{importedPlaylistTitle}'");
            return true;
        }

        /// <summary>
        /// Takes a path and rewrites it depending on whether useLinuxPaths is configured and if there are any search and replace paramaters defined.
        /// </summary>
        /// <param name="path">Path and filename of a file</param>
        /// <returns>Converted path and filename</returns>
        static string RewriteLocation(string path)
        {
            string newPath = path;

            // Only force the path style if the user has set it

            if (pathStyle == PathStyle.ForceLinux)
            {
                // Convert Windows-style paths to Linux-style paths
                newPath = newPath.Replace('\\', '/');
            }
            else if (pathStyle == PathStyle.ForceWindows)
            {
                // Convert Linux-style paths to Windows-style paths
                newPath = newPath.Replace('/', '\\');
            }

            // Do other search and repace here

            if (!string.IsNullOrEmpty(findText))
                newPath = newPath.Replace(findText, replaceText, StringComparison.CurrentCultureIgnoreCase);

            return newPath;
        }


        /// <summary>
        /// Finds the Plex `ratingKey` of a track by matching a file path with the entries 
        /// in `TrackInfo.plexTrackList`. Matches are case-insensitive.
        /// </summary>
        /// <param name="filePath">The full file path to search for.</param>
        /// <returns>The ratingKey of the matching track, or -1 if no match is found.</returns>
        public static long GetRatingKeyFromFilePath(string filePath)
        {
            var match = plexTrackList.FirstOrDefault(track => string.Equals(track.filePath, filePath, StringComparison.OrdinalIgnoreCase));

            return match?.ratingKey ?? -1;
        }

        /// <summary>
        /// Creates a new Plex playlist with the specified title.
        /// </summary>
        public static long CreatePlexPlaylist(string title)
        {
            string createPlaylistUrl = $"/playlists?uri=server://{machineIdentifier}/com.plexapp.plugins.library/{plexLibrary}&type=audio&smart=0&title={Uri.EscapeDataString(title)}";
            var response = GetHttpResponse(HttpMethod.Post, createPlaylistUrl);

            var doc = XDocument.Parse(response);
            var playlistElement = doc.Descendants("Playlist").FirstOrDefault();
            if (playlistElement != null && long.TryParse(playlistElement.Attribute("ratingKey")?.Value, out long newRatingKey))
            {
                Logger($"Playlist '{title}' created with ratingKey {newRatingKey}.", true);
                return newRatingKey;
            }

            throw new Exception("Failed to create the playlist.");
        }

        /// <summary>
        /// Adds tracks to a Plex playlist in batches, ensuring the request does not exceed 1000 characters.
        /// </summary>
        /// <param name="ratingKey">The ratingKey of the Plex playlist to add tracks to.</param>
        public static void AddTracksToPlaylist(long ratingKey)
        {
            if (importedPlaylist.Count == 0)
            {
                Console.WriteLine("No tracks to add to the playlist.");
                return;
            }

            List<long> trackRatingKeys = importedPlaylist
                .Select(track => track.ratingKey)
                .Where(rk => rk != -1)
                .ToList();

            if (trackRatingKeys.Count == 0)
            {
                Logger("No valid tracks found to add to the playlist.");
                return;
            }

            SendTrackBatchesToPlex(ratingKey, trackRatingKeys);
            Logger($"Added {Pluralise(trackRatingKeys.Count, "track", "tracks")} to playlist.", true);
        }

        /// <summary>
        /// Sends batches of track ratingKeys to Plex to add them to a playlist.
        /// </summary>
        /// <param name="ratingKey"></param>
        /// <param name="trackRatingKeys"></param>
        public static void SendTrackBatchesToPlex(long ratingKey, List<long> trackRatingKeys)
        {
            const int maxUrlLength = 1024;
            string baseUrl = $"/playlists/{ratingKey}/items?uri=server://{machineIdentifier}/com.plexapp.plugins.library/library/metadata/";

            StringBuilder currentBatch = new StringBuilder(baseUrl);
            int requests = 0;  // Counter to track number of requests sent

            foreach (var key in trackRatingKeys)
            {
                string keyStr = key.ToString();

                if (currentBatch.Length + keyStr.Length + 1 > maxUrlLength) // +1 for comma
                {
                    // Send the current batch
                    GetHttpResponse(HttpMethod.Put, currentBatch.ToString().TrimEnd(','));
                    currentBatch.Clear();
                    currentBatch.Append(baseUrl);
                    requests++;  // Increment the request count
                }

                currentBatch.Append(keyStr).Append(',');
            }

            if (currentBatch.Length > baseUrl.Length)
            {
                GetHttpResponse(HttpMethod.Put, currentBatch.ToString().TrimEnd(','));
                requests++;  // Increment the request count for the last batch
            }

            // Log the number of requests made (log to text file only)
            Logger($"Sent {Pluralise(requests, "HTTP request", "HTTP requests")} to Plex to add tracks to playlist.", true);
        }

        /// <summary>
        /// Clears all items from a Plex playlist.
        /// </summary>
        public static void ClearPlaylist(long ratingKey)
        {
            string clearPlaylistUrl = $"/playlists/{ratingKey}/items";
            GetHttpResponse(HttpMethod.Delete, clearPlaylistUrl);
            Logger($"Cleared all items from playlist {ratingKey}.");
        }

        /// <summary>
        /// Checks if all tracks in a specified Plex playlist belong to the current library.
        /// </summary>
        /// <param name="ratingKey">The ratingKey of the playlist to check.</param>
        /// <returns>True if all tracks belong to the current library; otherwise, false.</returns>

        public static bool IsAllPlaylistContentInThisLibrary(long playlistRatingKey)
        {
            // Fetch the playlist details
            string urlPath = $"/playlists/{playlistRatingKey}/items";
            string responseContent = GetHttpResponse(HttpMethod.Get, urlPath);

            // Parse the XML response
            var tracks = XElement.Parse(responseContent).Elements("Track");

            foreach (var track in tracks)
            {
                string? librarySectionID = track.Attribute("librarySectionID")?.Value;

                // If librarySectionID is missing or doesn't match the current library, return false
                if (string.IsNullOrEmpty(librarySectionID) || librarySectionID != plexLibrary.ToString())
                    return false;
            }

            // All tracks belong to the current library
            return true;
        }

        /// <summary>
        /// Retrieves the ratingKey of a playlist by its name from plexPlaylist.plexPlaylistList.
        /// Matching is case-insensitive.
        /// </summary>
        /// <param name="playlistName">The name of the playlist to locate.</param>
        /// <returns>
        /// The ratingKey of the matching playlist, or -1 if no matching playlist is found.
        /// </returns>
        public static long GetRatingKeyFromPlaylistName(string playlistName)
        {
            var match = plexPlaylistList
                .FirstOrDefault(playlist => string.Equals(playlist.playlistTitle, playlistName, StringComparison.OrdinalIgnoreCase));

            return match?.ratingKey ?? -1;
        }

        /// <summary>
        /// Deletes all playlists stored in plexPlaylist.plexPlaylistList by issuing delete API calls to Plex.
        /// Once completed, it refreshes the list of playlists by calling FetchAndStorePlaylists.
        /// </summary>
        public static void DeleteAllPlaylists()
        {
            foreach (var playlist in plexPlaylistList)
                DeletePlaylist(playlist.ratingKey);

            Logger($"Deleted all playlists from Plex.");

            // Refresh the list of playlists
            FetchAndStorePlaylists();
        }

        /// <summary>
        /// Finds M3U or M3U8 files to import based on the input file path or directory.
        /// If the input is a single M3U/M3U8 file, its full path is added to the global array.
        /// If the input is a directory, all M3U/M3U8 files (non-recursive) in the directory are added to the global array.
        /// </summary>
        /// <param name="filePathOrFile">The file or directory path to search for M3U or M3U8 files.</param>
        public static void FindM3UToImport(string filePathOrFile)
        {
            // Initialize or clear the global array for storing M3U/M3U8 files to import
            M3UFilesToImport = new List<string>();

            if (File.Exists(filePathOrFile))
            {
                // Check if the file has a .m3u or .m3u8 extension
                string extension = Path.GetExtension(filePathOrFile).ToLowerInvariant();
                if (extension == ".m3u" || extension == ".m3u8")
                {
                    M3UFilesToImport.Add(Path.GetFullPath(filePathOrFile));
                }
                else
                {
                    throw new FileNotFoundException($"The file '{filePathOrFile}' is not a valid M3U or M3U8 file.");
                }
            }
            else if (Directory.Exists(filePathOrFile))
            {
                // If the input is a directory, find all .m3u and .m3u8 files in the directory (non-recursive)
                var m3uFiles = Directory.GetFiles(filePathOrFile, "*.m3u");
                var m3u8Files = Directory.GetFiles(filePathOrFile, "*.m3u8");

                foreach (var file in m3uFiles.Concat(m3u8Files))
                    M3UFilesToImport.Add(Path.GetFullPath(file));
            }
            else
            {
                throw new FileNotFoundException($"The path '{filePathOrFile}' does not exist or is not valid.");
            }
        }

        /// <summary>
        /// Processes the given playlist file by syncing it with Plex, creating or updating the playlist as needed.
        /// </summary>
        /// <param name="m3uFilePath">The full path to the m3u file to process.</param>
        public static void ProcessPlaylist(string m3uFilePath)
        {
            // Step 1: Load the m3u playlist into the importedPlaylist
            bool result = LoadM3UPlaylist(m3uFilePath);
            if (result == false)
                return;

            // Step 2: Try to find the Plex playlist by name
            long ratingKey = GetRatingKeyFromPlaylistName(importedPlaylistTitle);

            if (ratingKey == -1)
            {
                // Step 3a: If the playlist does not exist in Plex, create it
                ratingKey = CreatePlexPlaylist(importedPlaylistTitle);
                if (ratingKey == -1)
                {
                    Logger($"Failed to create playlist: {importedPlaylistTitle}");
                    return;
                }
                Logger($"Created playlist on Plex: {importedPlaylistTitle}");
            }
            else
            {
                // Step 3b: Check if the content is identical to avoid unnecessary updates
                if (IsPlaylistContentIdentical(ratingKey))
                {
                    Logger($"Playlist already up-to-date: {importedPlaylistTitle}");
                    processedPlaylistTitles.Add(importedPlaylistTitle); // Add to processed list
                    return;
                }
                // Content isn't identical, so easier approach is to just remove everything
                // already there and then add it again
                DeleteAllItemsInPlaylist(ratingKey);
            }

            // Step 4: Update the playlist content on Plex
            Logger($"Adding {Pluralise(importedPlaylist.Count, "item", "items")} to playlist: {importedPlaylistTitle}");
            AddTracksToPlaylist(ratingKey);
            processedPlaylistTitles.Add(importedPlaylistTitle); // Add to processed list

            // Step 5: Refresh the Plex playlist list to ensure consistency
            FetchAndStorePlaylists();
        }

        /// <summary>
        /// Compares the content of a Plex playlist with the imported playlist to determine if they are identical.
        /// </summary>
        /// <param name="ratingKey">The ratingKey of the Plex playlist to check.</param>
        /// <returns>True if the playlists have identical content, otherwise false.</returns>
        public static bool IsPlaylistContentIdentical(long ratingKey)
        {
            // Step 1: Fetch the current content of the Plex playlist
            string fetchPlaylistUrl = $"/playlists/{ratingKey}/items";
            string playlistXml = GetHttpResponse(HttpMethod.Get, fetchPlaylistUrl);

            if (string.IsNullOrEmpty(playlistXml))
            {
                Console.WriteLine("Failed to fetch the playlist content. Nothing returned.");
                return false;
            }

            // Step 2: Parse the Plex playlist ratingKeys from the XML response
            List<long> plexRatingKeys = XDocument.Parse(playlistXml)
                .Descendants("Track")
                .Select(track => long.TryParse(track.Attribute("ratingKey")?.Value, out var rk) ? rk : -1)
                .Where(rk => rk != -1)
                .ToList();

            // Step 3: Collect ratingKeys from the imported playlist
            List<long> importedRatingKeys = importedPlaylist
                .Select(track => track.ratingKey)
                .Where(rk => rk != -1)
                .ToList();

            // Step 4: Compare counts
            if (plexRatingKeys.Count != importedRatingKeys.Count)
                return false;

            // Step 5: Compare order
            for (int i = 0; i < importedRatingKeys.Count; i++)
                if (plexRatingKeys[i] != importedRatingKeys[i])
                    return false;

            // If all checks passed, the playlists are identical
            return true;
        }

        /// <summary>
        /// Loops through all playlists that have been processed and deletes any on Plex that are not found in the uploaded list.
        /// </summary>
        public static void MirrorPlaylists()
        {
            Logger($"Mirroring playlists on Plex.");
            foreach (var plex in plexPlaylistList)
            {
                if (!processedPlaylistTitles.Contains(plex.playlistTitle))
                {
                    Logger($"Not found in uploaded list. Deleting: {plex.playlistTitle}");
                    DeletePlaylist(plex.ratingKey);
                }
            }
        }

        /// <summary>
        /// Checks connectivity to the Plex server by querying the /identity/ endpoint.
        /// Extracts the machineIdentifier if the connection is successful.
        /// </summary>
        /// <returns>True if the Plex server is reachable and the machineIdentifier is retrieved, otherwise False.</returns>
        public static bool CheckPlexConnectivity()
        {
            try
            {
                string urlPath = "/identity/";
                string responseContent = GetHttpResponse(HttpMethod.Get, urlPath);

                // Parse the XML response
                var xml = XElement.Parse(responseContent);
                var identifier = xml.Attribute("machineIdentifier")?.Value;

                if (string.IsNullOrEmpty(identifier))
                {
                    Logger("Connected to Plex, but machineIdentifer not found in response.");
                    Logger($"Missing machine identifier in output: {responseContent}", true);
                    return false;
                }

                Logger($"Successfully connected to Plex server at {plexHost} port {plexPort}.");
                Logger($"machineIdentifer is {identifier.Length} characters long. First four are: {identifier[..Math.Min(identifier.Length, 4)]}", true);
                machineIdentifier = identifier;
                return true;
            }
            catch (Exception ex)
            {
                Logger($"Failed to connect to Plex server at {plexHost} port {plexPort}: {ex.Message}");

                // Determine the likely issue based on the exception
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    Logger("Possible cause: The Plex token provided is not valid.");
                    Logger("More info: https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/");
                }
                else if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
                {
                    Logger("Possible cause: The Plex server URL and/or port number is incorrect.");
                }
                else if (ex.Message.Contains("forcibly closed by the remote host"))
                {
                    Logger("Possible cause: Plex is set to require secure connections, change to \"prefer\" instead.");
                    Logger("More info: https://support.plex.tv/articles/200430283-network/");
                }
                else
                {
                    Logger("General connectivity issue. Please check your network or Plex server settings.");
                }
                return false;
            }
        }

        /// <summary>
        /// Given an exception, this method will return a string containing the full message of the exception
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static string GetFullExceptionMessage(Exception ex)
        {
            var messages = new List<string>();
            var seen = new HashSet<string>();

            while (ex != null)
            {
                if (!string.IsNullOrWhiteSpace(ex.Message) && seen.Add(ex.Message))
                    messages.Add(ex.Message);

                ex = ex.InnerException!;
            }

            return string.Join(" | ", messages);
        }


        /// <summary>
        /// Deletes all items in a Plex playlist without deleting the playlist itself.
        /// </summary>
        /// <param name="ratingKey">The ratingKey of the Plex playlist to clear.</param>
        public static void DeleteAllItemsInPlaylist(long ratingKey)
        {
            // URL to delete all items from the playlist
            string clearPlaylistUrl = $"/playlists/{ratingKey}/items";

            // Send the DELETE request to clear all items
            try
            {
                GetHttpResponse(HttpMethod.Delete, clearPlaylistUrl);
                Logger($"All items in playlist {ratingKey} have been deleted.", true);
            }
            catch (Exception ex)
            {
                Logger($"Failed to delete items from playlist {ratingKey}: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a specific playlist from Plex using its ratingKey. All contents and the
        /// playlist itself will be deleted.
        /// </summary>
        /// <param name="ratingKey"></param>
        public static void DeletePlaylist(long ratingKey)
        {
            try
            {
                string deleteUrl = $"/playlists/{ratingKey}";
                GetHttpResponse(HttpMethod.Delete, deleteUrl);
            }
            catch (Exception ex)
            {
                Logger($"Failed to delete playlist with ratingKey {ratingKey}: {ex.Message}");
                return;
            }
            finally
            {
                Logger($"Deleted playlist with ratingKey {ratingKey} from Plex.", true);
            }
        }
    }
}
