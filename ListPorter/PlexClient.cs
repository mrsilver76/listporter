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

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace ListPorter
{
    /// <summary>
    /// Provides methods for interacting with a Plex Media Server, including fetching and managing tracks, playlists,
    /// and library updates.
    /// </summary>
    /// <remarks>This class contains static methods to perform various operations on a Plex Media Server, such
    /// as: - Sending HTTP requests to the Plex API. - Fetching and storing tracks and playlists. - Creating, updating,
    /// and deleting playlists. - Checking server connectivity and managing library updates.  The class relies on global
    /// configuration values (e.g., Plex server details, authentication token) and logs detailed information about its
    /// operations.</remarks>
    internal sealed class PlexClient
    {
        private static string _machineIdentifier = string.Empty;

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
            // Bypass SSL certificate validation for self-signed certificates
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            using HttpClient httpClient = new(handler);
            // Construct the full URL
            string scheme = Globals.UsingSecureConnection ? "https" : "http";
            string fullUrl = $"{scheme}://{Globals.PlexHost}:{Globals.PlexPort}{urlPath}";

            // Create the HttpRequestMessage
            var request = new HttpRequestMessage(method, fullUrl);
            request.Headers.Add("X-Plex-Token", Globals.PlexToken);

            // Add the body if needed
            if (!string.IsNullOrEmpty(body) && (method == HttpMethod.Post || method == HttpMethod.Put))
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            if (Globals.VerboseMode)
                Logger.Write($"{method}: {fullUrl}", true);

            try
            {
                // Send the request synchronously
                var response = httpClient.Send(request);

                // Ensure the response is successful
                response.EnsureSuccessStatusCode();

                // Read and return the response content
                string responseText = response.Content.ReadAsStringAsync().Result;
                if (Globals.VerboseMode)
                    Logger.Write($"Received {responseText.Length} bytes sucessfully.", true);
                return responseText;
            }
            catch (HttpRequestException ex)
            {
                Logger.Write($"Error with request: {GetFullExceptionMessage(ex)}");
                throw;
            }
        }

        /// <summary>
        /// Fetches all audio tracks stored in the specified Plex library section and stores their details
        /// (ratingKey and file path) in the static list `TrackInfo.plexTrackList`.
        /// </summary>
        public static void FetchAndStoreTracks()
        {
            Logger.Write("Searching for audio tracks on Plex. This may take a while...");

            const int pageSize = 1000;  // >1000 will soon generate "400 Bad Request"
            int start = 0;

            while (true)
            {
                string urlPath = $"/library/sections/{Globals.PlexLibrary}/all?type=10&X-Plex-Container-Start={start}&X-Plex-Container-Size={pageSize}";
                string xml;

                try
                {
                    xml = GetHttpResponse(HttpMethod.Get, urlPath);
                }
                catch
                {
                    Logger.Write($"Error: Unable to fetch tracks from Plex. Is your library ID '{Globals.PlexLibrary}' correct?");
                    Environment.Exit(1);
                    return;  // Unreachable, but added for clarity
                }

                var container = XElement.Parse(xml);
                ExtractTracksFromContainer(container);

                // If there is no next page or we've gone beyond totalSize then break out
                // of the loop
                if (!int.TryParse(container.Attribute("size")?.Value, out int size) ||
                    !int.TryParse(container.Attribute("totalSize")?.Value, out int totalSize) ||
                    size == 0 || start + size >= totalSize)
                    break;

                start += size;
            }

            Logger.Write($"Found {GrammarHelper.Pluralise(Globals.PlexTrackList.Count, "audio track", "audio tracks")} on Plex.");
        }

        /// <summary>
        /// Extracts audio tracks from a given XML container element and adds them to the static list `TrackInfo.Globals.PlexTrackList`.
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
                    Logger.Write($"Track skipped: missing/invalid ratingKey for: {filePath ?? "[unknown path]"}");
                    continue;
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    Logger.Write($"Track skipped: missing file path for ratingKey {ratingKey}.");
                    continue;
                }

                if (isDeleted)
                    Logger.Write($"{filePath} (ratingKey: {ratingKey}) marked as deleted, adding anyway.", true);

                Globals.PlexTrackList.Add(new Globals.TrackInfo
                {
                    RatingKey = ratingKey,
                    FilePath = filePath
                });

                if (Globals.VerboseMode)
                    Logger.Write($"Found track: {filePath} (ratingKey: {ratingKey})", true);
            }
        }

        /// <summary>
        /// Fetches all playlists from the Plex server, filters them based on the current library, 
        /// and stores the details in the static list `plexPlaylist.plexPlaylistList`.
        /// Only playlists with all tracks in the specified Plex library are included.
        /// </summary>
        /// 
        /// <param name="logStatus">If true, logs the status of the operation to the console.</param>
        public static void FetchAndStorePlaylists(Boolean logStatus = false)
        {
            Globals.PlexPlaylistList.Clear();

            if (logStatus)
                Logger.Write($"Fetching playlists from Plex...");

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
                    Logger.Write($"Skipping playlist (invalid ratingKey): title='{title ?? "Unknown"}', ratingKey='{ratingKeyStr ?? "null"}', trackCount='{leafCountStr ?? "null"}'", true);
                    continue;
                }

                if (string.IsNullOrEmpty(title))
                {
                    Logger.Write($"Skipping playlist (missing title): ratingKey='{ratingKey}', trackCount='{leafCountStr ?? "null"}'", true);
                    continue;
                }

                if (trackCount <= 0)
                {
                    Logger.Write($"Skipping playlist (no tracks): title='{title}', ratingKey='{ratingKey}', trackCount='{leafCountStr ?? "null"}'", true);
                    continue;
                }

                // Library check
                if (!IsAllPlaylistContentInThisLibrary(ratingKey))
                {
                    Logger.Write($"Skipping playlist (contains tracks from other libraries): title='{title}', ratingKey='{ratingKey}'", true);
                    continue;
                }

                // If valid, add to list
                Logger.Write($"Found playlist: {title}", true);
                Globals.PlexPlaylistList.Add(new Globals.PlexPlaylist
                {
                    RatingKey = ratingKey,
                    PlaylistTitle = title,
                    TrackCount = trackCount
                });
                count++;
            }

            if (logStatus)
                Logger.Write($"Found {GrammarHelper.Pluralise(Globals.PlexPlaylistList.Count, "playlist", "playlists")} matching criteria in library ID {Globals.PlexLibrary}.");

            Logger.Write($"Found {GrammarHelper.Pluralise(count, "playlist", "playlists")} on Plex.", true);
        }

        /// <summary>
        /// Updates the specified Plex library by triggering a refresh and waiting for the update process to complete.
        /// </summary>
        /// <remarks>This method sends a request to refresh the Plex library identified by <see
        /// cref="Globals.PlexLibrary"].  It then monitors the library's update status by polling the Plex activities
        /// endpoint until the update is complete.  The method will terminate the application with an error code if the
        /// library ID is invalid or if the update request fails.</remarks>
        public static void UpdatePlexLibrary()
        {
            if (!Globals.UpdateLibrary)
                return;

            Logger.Write($"Requesting Plex updates library ID {Globals.PlexLibrary}...");

            try
            {
                string urlPath = $"/library/sections/{Globals.PlexLibrary}/refresh";
                GetHttpResponse(HttpMethod.Post, urlPath);
            }
            catch
            {
                Logger.Write($"Error: Unable to update Plex library. Is your library ID '{Globals.PlexLibrary}' correct?");
                Environment.Exit(1);
                return;
            }

            // Now we need to wait for the library to finish updating. To do this, we're
            // going to poll /activities every 5 seconds until <Activity> no longer reports
            // a type containing either "refresh" or "update" twice in a row.

            var sw = Stopwatch.StartNew();
            int cleanHit = 0;
            bool waitingMessage = false;

            Logger.Write("Waiting for library to finish updating...");

            while (cleanHit < 2)
            {
                if (cleanHit < 2)
                    Thread.Sleep(5000); // Wait 5 seconds

                string response = GetHttpResponse(HttpMethod.Get, "/activities");
                var doc = XDocument.Parse(response);

                // Check if there is a current activity of type "library.update.section"
                bool active = doc.Descendants("Activity").Any(a => (string?)a.Attribute("type") == "library.update.section");

                if (active)
                    cleanHit = 0; // Reset the counter if we find an active refresh/update
                else
                    cleanHit++; // Increment the counter if no active refresh/update found

                if (sw.Elapsed.Seconds > 60 && cleanHit < 2 && !waitingMessage)
                {
                    Logger.Write($"Status update after 1 minute: still waiting for library to finish updating...");
                    sw.Stop();
                    waitingMessage = true; // Set the flag to avoid repeating this message
                }
            }

            Logger.Write($"Plex library ID {Globals.PlexLibrary} has been updated successfully.");
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
        /// Creates a new Plex playlist with the specified title.
        /// </summary>
        public static long CreatePlexPlaylist(string title)
        {
            string createPlaylistUrl = $"/playlists?uri=server://{_machineIdentifier}/com.plexapp.plugins.library/{Globals.PlexLibrary}&type=audio&smart=0&title={Uri.EscapeDataString(title)}";
            var response = GetHttpResponse(HttpMethod.Post, createPlaylistUrl);

            var doc = XDocument.Parse(response);
            var playlistElement = doc.Descendants("Playlist").FirstOrDefault();
            if (playlistElement != null && long.TryParse(playlistElement.Attribute("ratingKey")?.Value, out long newRatingKey))
            {
                Logger.Write($"Playlist '{title}' created with ratingKey {newRatingKey}.", true);
                return newRatingKey;
            }

            Logger.Write($"Failed to create playlist '{title}'. Response: {response}");
            Logger.Write($"Response XML: {doc}");
            Logger.Write("Fatal error, something is wrong with Plex. Giving up.");
            System.Environment.Exit(1);
            return 0; // Unreachable, but added for clarity
        }

        /// <summary>
        /// Adds tracks to a Plex playlist in batches, ensuring the request does not exceed 1000 characters.
        /// </summary>
        /// <param name="ratingKey">The ratingKey of the Plex playlist to add tracks to.</param>
        public static void AddTracksToPlaylist(long ratingKey)
        {
            if (Globals.ImportedPlaylist.Count == 0)
            {
                Console.WriteLine("No tracks to add to the playlist.");
                return;
            }

            List<long> trackRatingKeys = [.. Globals.ImportedPlaylist
                .Select(track => track.RatingKey)
                .Where(rk => rk != -1)];

            if (trackRatingKeys.Count == 0)
            {
                Logger.Write("No valid tracks found to add to the playlist.");
                return;
            }

            SendTrackBatchesToPlex(ratingKey, trackRatingKeys);
            Logger.Write($"Added {GrammarHelper.Pluralise(trackRatingKeys.Count, "track", "tracks")} to playlist.", true);
        }

        /// <summary>
        /// Sends batches of track ratingKeys to Plex to add them to a playlist.
        /// </summary>
        /// <param name="ratingKey"></param>
        /// <param name="trackRatingKeys"></param>
        public static void SendTrackBatchesToPlex(long ratingKey, List<long> trackRatingKeys)
        {
            const int maxUrlLength = 1024;
            string baseUrl = $"/playlists/{ratingKey}/items?uri=server://{_machineIdentifier}/com.plexapp.plugins.library/library/metadata/";

            StringBuilder currentBatch = new(baseUrl);
            int requests = 0;  // Counter to track number of requests sent

            foreach (var key in trackRatingKeys)
            {
                string keyStr = key.ToString(CultureInfo.CurrentCulture);

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
            Logger.Write($"Sent {GrammarHelper.Pluralise(requests, "HTTP request", "HTTP requests")} to Plex to add tracks to playlist.", true);
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
                if (string.IsNullOrEmpty(librarySectionID) || librarySectionID != Globals.PlexLibrary.ToString(CultureInfo.CurrentCulture))
                    return false;
            }

            // All tracks belong to the current library
            return true;
        }

        /// <summary>
        /// Deletes all playlists stored in plexPlaylist.plexPlaylistList by issuing delete API calls to Plex.
        /// Once completed, it refreshes the list of playlists by calling FetchAndStorePlaylists.
        /// </summary>
        public static void DeleteAllPlaylists()
        {
            if (!Globals.DeleteAll)
                return;

            foreach (var playlist in Globals.PlexPlaylistList)
                DeletePlaylist(playlist.RatingKey);

            Logger.Write($"Deleted all playlists from Plex.");

            // Refresh the list of playlists
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
            List<long> plexRatingKeys = [.. XDocument.Parse(playlistXml)
                .Descendants("Track")
                .Select(track => long.TryParse(track.Attribute("ratingKey")?.Value, out var rk) ? rk : -1)
                .Where(rk => rk != -1)];

            // Step 3: Collect ratingKeys from the imported playlist
            List<long> importedRatingKeys = [.. Globals.ImportedPlaylist
                .Select(track => track.RatingKey)
                .Where(rk => rk != -1)];

            // Step 4: Compare counts
            if (plexRatingKeys.Count != importedRatingKeys.Count)
            {
                Logger.Write($"Playlist '{PlaylistImporter.ImportedPlaylistTitle}' will be updated - {plexRatingKeys.Count} on Plex vs {importedRatingKeys.Count} in import.", true);
                return false;
            }

            // Step 5: Compare order
            for (int i = 0; i < importedRatingKeys.Count; i++)
                if (plexRatingKeys[i] != importedRatingKeys[i])
                {
                    Logger.Write($"Playlist '{PlaylistImporter.ImportedPlaylistTitle}' will be updated - ratingKeys differ at index {i}: Plex={plexRatingKeys[i]}, Import={importedRatingKeys[i]}", true);
                    return false;
                }

            // If all checks passed, the playlists are identical
            return true;
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
                string urlPath = "/";
                string responseContent = GetHttpResponse(HttpMethod.Get, urlPath);

                // Parse the XML response
                var xml = XElement.Parse(responseContent);
                var identifier = xml.Attribute("machineIdentifier")?.Value;
                var myPlexUsername = xml.Attribute("myPlexUsername")?.Value;
                var managedUser = xml.Element("User")?.Attribute("title")?.Value;

                if (string.IsNullOrEmpty(identifier))
                {
                    Logger.Write("Connected to Plex, but machineIdentifer not found in response.");
                    Logger.Write($"Missing machine identifier in output: {responseContent}", true);
                    return false;
                }
                Logger.Write($"Successfully connected to Plex server.");

                Logger.Write($"machineIdentifer is {identifier.Length} characters long. First four are: {identifier[..Math.Min(identifier.Length, 4)]}", true);
                _machineIdentifier = identifier;

                if (!string.IsNullOrEmpty(managedUser))
                    Logger.Write($"Uploading to managed user: {managedUser}");
                else
                    Logger.Write($"Uploading to Plex user: {myPlexUsername}");
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Write($"Failed to connect to Plex server: {ex.Message}");

                // Determine the likely issue based on the exception
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    Logger.Write("Possible cause: The Plex token provided is not valid.");
                    Logger.Write("More info: https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/");
                }
                else if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
                {
                    Logger.Write("Possible cause: The Plex server URL and/or port number is incorrect.");
                }
                else if (ex.Message.Contains("forcibly closed by the remote host"))
                {
                    Logger.Write($"Possible cause: Plex may require HTTPS connections. Try using --server https://{Globals.PlexHost}");
                    Logger.Write("or change your \"Secure Connections\" setting to \"Prefer\" instead.");
                    Logger.Write("More info: https://support.plex.tv/articles/200430283-network/");
                }
                else
                {
                    Logger.Write("General connectivity issue. Please check your network or Plex server settings.");
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
                Logger.Write($"All items in playlist {ratingKey} have been deleted.", true);
            }
            catch (Exception ex)
            {
                Logger.Write($"Failed to delete items from playlist {ratingKey}: {ex.Message}");
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
                Logger.Write($"Failed to delete playlist with ratingKey {ratingKey}: {ex.Message}");
                return;
            }
            finally
            {
                Logger.Write($"Deleted playlist with ratingKey {ratingKey} from Plex.", true);
                PlexService.TotalPlaylistsDeleted++;
            }
        }
    }
}
