using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Net.Http;
using System.Text;
using static Plex_Playlist_Uploader.Helpers;
using System.Reflection;
using System.Diagnostics.Contracts;

namespace Plex_Playlist_Uploader
{
    /// <summary>
    /// Class representing a Plex playlist with its ratingKey, title, and track count.
    /// </summary>
    public class plexPlaylist
    {
        public long ratingKey { get; set; }
        public string playlistTitle { get; set; } = string.Empty;
        public long trackCount { get; set; }

        public static List<plexPlaylist> plexPlaylistList = new List<plexPlaylist>();
    }

    /// <summary>
    /// Class representing a Plex track with its ratingKey and file path. We will use this
    /// to store every single song that we can find within the Plex Media Server.
    /// </summary>
    public class plexTrack
    {
        public long ratingKey { get; set; }
        public string filePath { get; set; } = string.Empty;

        public static List<plexTrack> plexTrackList = new List<plexTrack>();
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
        public static bool logAPIrequests = false; // Log API requests
        public enum PathStyle
        {
            Auto, // Do nothing
            ForceWindows, // Replace slashes with backslashes
            ForceLinux // Replace backslashes with slashes
        }
        public static PathStyle pathStyle = PathStyle.Auto; // By default, do nothing

        // Internal globals
        public static List<string> M3UFilesToImport = new List<string>(); // List of m3u files to process
        public static List<string> importedPlaylist = new List<string>(); // List of files in a playlist
        public static string importedPlaylistTitle = ""; // Name of the playlist we're importing
        public static string machineIdentifier = ""; // ID used to upload playlists
        public static string appDataPath = ""; // Path to the app data folder
        public static HashSet<string> processedPlaylistTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // List of processed playlist titles

        static void Main(string[] args)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version!;
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Set up various paths and prepare logging
            InitialiseLogger();

            // Parse the arguments
            ParseArguments(args);

            Console.WriteLine($"Plex Playlist Uploader v{version.Major}.{version.Minor}.{version.Revision}, Copyright © 2024-{DateTime.Now.Year} Richard Lawrence");
            Console.WriteLine($"Upload standard or extended .m3u playlist files to Plex Media Server.");
            Console.WriteLine($"https://github.com/mrsilver76/plex-playlist-uploader\n");
            Console.WriteLine($"This program comes with ABSOLUTELY NO WARRANTY. This is free software,");
            Console.WriteLine($"and you are welcome to redistribute it under certain conditions; see");
            Console.WriteLine($"the documentation for details.");
            Console.WriteLine();

            Logger($"Starting Plex Playlist Uploader...");

            // Check connectivity
            CheckPlexConnectivity();

            // Fetch and store all tracks from the Plex library
            FetchAndStoreTracks();

            // Fetch and store all playlists from Plex

            // We're going to display a status update here because if we put this inside the method then
            // it will be called multiple times and we don't want to see the same message over and over again
            Logger($"Fetching playlists from Plex.");
            FetchAndStorePlaylists();
            Logger($"Found {plexPlaylist.plexPlaylistList.Count} playlists matching criteria in library ID {plexLibrary}.");

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
            Logger($"Plex Playlist Uploader finished.");
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
            plexPlaylist.plexPlaylistList.Clear();

            string urlPath = "/playlists";
            string responseContent = GetHttpResponse(HttpMethod.Get, urlPath);
            int count = 0;

            // Parse the XML response
            var playlists = XElement.Parse(responseContent)
                                    .Elements("Playlist")
                                    .Where(x => x.Attribute("smart")?.Value == "0" && x.Attribute("playlistType")?.Value == "audio");

            foreach (var playlist in playlists)
            {
                string? ratingKeyValue = playlist.Attribute("ratingKey")?.Value;

                // Ensure the playlist has a ratingKey and all its content is in the current library
                if (!string.IsNullOrEmpty(ratingKeyValue) &&
                    IsAllPlaylistContentInThisLibrary(long.Parse(ratingKeyValue)))
                {
                    Logger($"Found playlist: {playlist.Attribute("title")?.Value}", true);
                    plexPlaylist.plexPlaylistList.Add(new plexPlaylist
                    {
                        ratingKey = long.Parse(ratingKeyValue),
                        playlistTitle = playlist.Attribute("title")?.Value ?? "Unknown",
                        trackCount = long.Parse(playlist.Attribute("leafCount")?.Value ?? "0")
                    });
                    count++;
                }
            }
            Logger($"Found {count} playlists on Plex.", true);
        }

        /// <summary>
        /// Fetches all tracks from the specified Plex library and stores their details 
        /// (ratingKey and file path) in the static list `plexTrack.plexTrackList`.
        /// </summary>

        public static void FetchAndStoreTracks()
        {
            Logger("Searching for audio tracks on Plex. This may take a while...");

            string urlPath = $"/library/sections/{plexLibrary}/all";
            FetchAndStoreTracksRecursive(urlPath);

            Logger($"Found {plexTrack.plexTrackList.Count} audio tracks on Plex.");
        }

        /// <summary>
        /// Recursively fetches tracks and directories from the specified Plex library section 
        /// and stores track details in `plexTrack.plexTrackList`.
        /// </summary>
        /// <param name="urlPath">The API endpoint path to fetch the library contents.</param>

        private static void FetchAndStoreTracksRecursive(string urlPath)
        {
            string responseContent = GetHttpResponse(HttpMethod.Get, urlPath);
            var elements = XElement.Parse(responseContent);

            foreach (var track in elements.Elements("Track"))
            {
                var media = track.Element("Media")?.Element("Part");
                if (media != null && media.Attribute("deletedAt") == null)
                {
                    plexTrack.plexTrackList.Add(new plexTrack
                    {
                        ratingKey = long.Parse(track.Attribute("ratingKey")?.Value ?? "0"),
                        filePath = media.Attribute("file")?.Value ?? "Unknown"
                    });
                }
            }

            foreach (var directory in elements.Elements("Directory"))
            {
                string? childKey = directory.Attribute("key")?.Value;
                if (!string.IsNullOrEmpty(childKey))
                    FetchAndStoreTracksRecursive(childKey);
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

                if (logAPIrequests)
                    Logger($"{method}: {fullUrl}", true);

                try
                {
                    // Send the request synchronously
                    var response = httpClient.Send(request);

                    // Ensure the response is successful
                    response.EnsureSuccessStatusCode();

                    // Read and return the response content
                    string responseText = response.Content.ReadAsStringAsync().Result;
                    if (logAPIrequests)
                        Logger($"Received {responseText.Length} bytes sucessfully.", true);
                    return responseText;
                }
                catch (HttpRequestException ex)
                {
                    Logger($"Error with request: {ex.Message}");
                    throw new Exception($"Error: {ex.Message}");
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
                {
                    continue;
                }

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
                        importedPlaylist.Add(line);
                        added++;
                    }
                }
            }

            // Fallback to using the file name if no #PLAYLIST is found
            if (!playlistTitleFound)
            {
                importedPlaylistTitle = Path.GetFileNameWithoutExtension(filePath);
                Logger($"Missing #PLAYLIST, assuming title: {importedPlaylistTitle}", true);
            }

            if (added == 0)
            {
                Logger($"WARNING: no items in playlist can be added to Plex.");
                return false;
            }

            if (failed > 0)
                Logger($"WARNING: {failed} items could not be matched to Plex database.");

            Logger($"Matched {added} items in playlist: {importedPlaylistTitle}", true);
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
        /// in `plexTrack.plexTrackList`. Matches are case-insensitive.
        /// </summary>
        /// <param name="filePath">The full file path to search for.</param>
        /// <returns>The ratingKey of the matching track, or -1 if no match is found.</returns>

        public static long GetRatingKeyFromFilePath(string filePath)
        {
            var match = plexTrack.plexTrackList
                .FirstOrDefault(track => string.Equals(track.filePath, filePath, StringComparison.OrdinalIgnoreCase));

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
                .Select(filePath => GetRatingKeyFromFilePath(filePath))
                .Where(rk => rk != -1)
                .ToList();

            if (trackRatingKeys.Count == 0)
            {
                Logger("No valid tracks found to add to the playlist.");
                return;
            }

            SendTrackBatchesToPlex(ratingKey, trackRatingKeys);
            Logger($"Added {trackRatingKeys.Count} tracks to playlist.", true);
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
            Logger($"Sent {requests} HTTP requests to Plex to add tracks to playlist.", true);
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
                {
                    Logger($"Ignoring playlist '{playlistRatingKey}' because it contains tracks from a different library (ID: {librarySectionID})", true);
                    return false;
                }
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
            var match = plexPlaylist.plexPlaylistList
                .FirstOrDefault(playlist => string.Equals(playlist.playlistTitle, playlistName, StringComparison.OrdinalIgnoreCase));

            return match?.ratingKey ?? -1;
        }

        /// <summary>
        /// Deletes all playlists stored in plexPlaylist.plexPlaylistList by issuing delete API calls to Plex.
        /// Once completed, it refreshes the list of playlists by calling FetchAndStorePlaylists.
        /// </summary>
        public static void DeleteAllPlaylists()
        {
            foreach (var playlist in plexPlaylist.plexPlaylistList)
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
                {
                    M3UFilesToImport.Add(Path.GetFullPath(file));
                }
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
            Logger($"Adding {importedPlaylist.Count} items to playlist: {importedPlaylistTitle}");
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
            var plexRatingKeys = new List<long>();
            var xmlDoc = XDocument.Parse(playlistXml);

            plexRatingKeys = xmlDoc
                .Descendants("Track")
                .Select(track => long.Parse(track.Attribute("ratingKey")?.Value ?? "-1"))
                .Where(ratingKey => ratingKey != -1)
                .ToList();

            // Step 3: Collect ratingKeys from the imported playlist
            var importedRatingKeys = importedPlaylist
                .Select(filePath => GetRatingKeyFromFilePath(filePath))
                .Where(ratingKey => ratingKey != -1)
                .ToList();

            // Step 4: Compare how many items are in each playlist. If they are different,
            // then they are not identical
            if (plexRatingKeys.Count != importedRatingKeys.Count)
                return false;


            // Step 5. Compare the content order as well.
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
            foreach (var plex in plexPlaylist.plexPlaylistList)
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

                Logger($"Successfully connected to Plex server.");
                Logger($"machineIdentifer is {identifier.Length} characters long. First four are: {identifier[..Math.Min(identifier.Length, 4)]}", true);
                machineIdentifier = identifier;
                return true;
            }
            catch (Exception ex)
            {
                Logger($"Failed to connect to Plex: {ex.Message}");

                // Determine the likely issue based on the exception
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    Logger("Possible cause: Invalid Plex Token.");
                }
                else if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
                {
                    Logger("Possible cause: Incorrect Plex server URL or port.");
                }
                else
                {
                    Logger("General connectivity issue. Please check your network or Plex server settings.");
                }
                return false;
            }
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
