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

namespace ListPorter
{
    /// <summary>
    /// Provides functionality for managing and synchronizing playlists with Plex, including creating, updating,
    /// mirroring, and resolving track or playlist metadata.
    /// </summary>
    /// <remarks>This class includes methods for processing playlists, mirroring playlists to ensure
    /// consistency with uploaded data, building fuzzy path maps for track matching, and retrieving metadata such as
    /// `ratingKey` values for tracks and playlists.</remarks>
    internal sealed class PlexService
    {

        /// <summary>Total number of import errors</summary>
        public static int TotalImportErrors { get; set; }

        /// <summary>Total number of playlists created</summary>
        public static int TotalPlaylistsCreated { get; set; }

        /// <summary>Total number of playlists updated</summary>
        public static int TotalPlaylistsUpdated { get; set; }

        /// <summary>Total number of playlists deleted</summary>
        public static int TotalPlaylistsDeleted { get; set; }

        /// <summary>Total number of playlists skipped</summary>
        public static int TotalPlaylistsSkipped { get; set; }
        /// <summary>List of processed playlist titles</summary>
        private static HashSet<string> ProcessedPlaylistTitles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Fuzzy paths for matching against the Plex library</summary>
        private static readonly Dictionary<string, long> _fuzzyPathMap = [];
        /// <summary>Characters used as path separators</summary>
        private static readonly char[] _pathSeparators = ['/', '\\'];


        /// <summary>
        /// Processes the given playlist file by syncing it with Plex, creating or updating the playlist as needed.
        /// </summary>
        /// <param name="m3uFilePath">The full path to the m3u file to process.</param>
        public static void ProcessPlaylist(string m3uFilePath)
        {
            bool wasCreated = false; // Flag to track if the playlist was created

            // Step 1: Load the m3u playlist into the importedPlaylist
            bool result = PlaylistImporter.LoadM3UPlaylist(m3uFilePath);
            if (result == false)
                return;

            // Step 2: Try to find the Plex playlist by name
            long ratingKey = GetRatingKeyFromPlaylistName(PlaylistImporter.ImportedPlaylistTitle);

            if (ratingKey == -1)
            {
                // Step 3a: If the playlist does not exist in Plex, create it
                ratingKey = PlexClient.CreatePlexPlaylist(PlaylistImporter.ImportedPlaylistTitle);
                if (ratingKey == -1)
                {
                    Logger.Write($"Failed to create playlist: {PlaylistImporter.ImportedPlaylistTitle}");
                    return;
                }
                Logger.Write($"Created playlist on Plex: {PlaylistImporter.ImportedPlaylistTitle}");
                wasCreated = true; // Mark as created
            }
            else
            {
                // Step 3b: Check if the content is identical to avoid unnecessary updates
                if (PlexClient.IsPlaylistContentIdentical(ratingKey))
                {
                    Logger.Write($"Playlist already up-to-date: {PlaylistImporter.ImportedPlaylistTitle}");
                    ProcessedPlaylistTitles.Add(PlaylistImporter.ImportedPlaylistTitle); // Add to processed list
                    TotalPlaylistsSkipped++; // Increment the skipped playlist counter
                    return;
                }
                // Content isn't identical, so easier approach is to just remove everything
                // already there and then add it again
                PlexClient.DeleteAllItemsInPlaylist(ratingKey);
            }

            // Step 4: Update the playlist content on Plex
            if (wasCreated)
                Logger.Write($"Adding {GrammarHelper.Pluralise(Globals.ImportedPlaylist.Count, "item", "items")} to playlist: {PlaylistImporter.ImportedPlaylistTitle}");
            else
                Logger.Write($"Updating {GrammarHelper.Pluralise(Globals.ImportedPlaylist.Count, "item", "items")} in playlist: {PlaylistImporter.ImportedPlaylistTitle}");
            PlexClient.AddTracksToPlaylist(ratingKey);
            ProcessedPlaylistTitles.Add(PlaylistImporter.ImportedPlaylistTitle); // Add to processed list
            if (wasCreated)
                TotalPlaylistsCreated++; // Increment the created playlist counter
            else
                TotalPlaylistsUpdated++; // Increment the updated playlist counter

            // Step 5: Refresh the Plex playlist list to ensure consistency
            PlexClient.FetchAndStorePlaylists();
        }


        /// <summary>
        /// Loops through all playlists that have been processed and deletes any on Plex that are not found in the uploaded list.
        /// </summary>
        public static void MirrorPlaylists()
        {
            Logger.Write($"Mirroring playlists on Plex.");
            foreach (var plex in Globals.PlexPlaylistList)
            {
                if (!ProcessedPlaylistTitles.Contains(plex.PlaylistTitle))
                {
                    Logger.Write($"Not found in uploaded list. Deleting: {plex.PlaylistTitle}");
                    PlexClient.DeletePlaylist(plex.RatingKey);
                }
            }
        }

        /// <summary>
        /// Builds a fuzzy path map for Plex library tracks, allowing for fast case-insensitive matching of
        /// the last three parts of a file path - which is typically artist/album/song.
        /// </summary>
        public static void BuildFuzzyPathMap()
        {
            if (!Globals.UseFuzzyMatching)
            {
                Logger.Write("Fuzzy path matching is disabled.");
                return;
            }

            Logger.Write("Building fuzzy path map...");

            int fuzzyConflictCount = 0;
            _fuzzyPathMap.Clear();

            foreach (var track in Globals.PlexTrackList)
            {
                var parts = track.FilePath.Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    Logger.Write($"Skipping track with insufficient path parts: {track.FilePath}", true);
                    continue;
                }

                var key = string.Join("|", parts[^3..]).ToLowerInvariant();

                // Check if the key already exists in the map
                if (_fuzzyPathMap.TryGetValue(key, out var existingKey))
                {
                    // If the existing key is different, we have a conflict
                    if (existingKey != track.RatingKey)
                    {
                        // Reverse lookup to get the original file path
                        var existingTrack = Globals.PlexTrackList.FirstOrDefault(t => t.RatingKey == existingKey);
                        var existingPath = existingTrack != null ? existingTrack.FilePath : "(unknown path)";

                        Logger.Write($"Fuzzy match conflict: key [{key}] maps to multiple tracks:" +
                                     $"\n  Existing: {existingPath} (RatingKey {existingKey})" +
                                     $"\n  Current : {track.FilePath} (RatingKey {track.RatingKey})");
                        fuzzyConflictCount++;
                        continue;
                    }
                    // The existing key is the same, so it's not an issue - but a bit odd
                    Logger.Write($"Fuzzy match issue: key [{key}] maps twice to same ratingKey ({existingKey})", true);
                    continue;
                }
                else
                    _fuzzyPathMap[key] = track.RatingKey;
            }

            if (fuzzyConflictCount > 0)
            {
                ConsoleOutput.DisplayFuzzyMatchConflicts(fuzzyConflictCount);
                System.Environment.Exit(-1);
            }

            // If we are here then we have built the map successfully
            Logger.Write($"Fuzzy path map built with {_fuzzyPathMap.Count} entries", true);
        }

        /// <summary>
        /// Finds the Plex `ratingKey` of a track by matching a file path with the entries 
        /// in `TrackInfo.plexTrackList`. Matches are case-insensitive. If an exact match
        /// is not found then fuzzy matching is attempted by taking the last three parts of the file path.
        /// This only happens if `useFuzzyMatching` is true, which is the default unless
        /// file path rewriting options are used (e.g., --find, --replace, --unix, --windows).
        /// </summary>
        /// <param name="filePath">The full file path to search for.</param>
        /// <returns>The ratingKey of the matching track, or -1 if no match is found.</returns>
        /// 
        public static long GetRatingKeyFromFilePath(string filePath)
        {
            // First just try to find an exact (case-insensitive) match in the Globals.PlexTrackList

            var match = Globals.PlexTrackList.FirstOrDefault(track =>
                string.Equals(track.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

            // We found a match!
            if (match != null)
                return match.RatingKey;

            // If we aren't using fuzzy matching then return -1 as we cannot find the track
            if (!Globals.UseFuzzyMatching)
                return -1;

            // If we reach here, we will try fuzzy matching

            var parts = filePath.Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                Logger.Write($"Fuzzy match skipped: path too short: {filePath}", true);
                return -1;
            }

            var fuzzyKey = string.Join("|", parts[^3..]).ToLowerInvariant();
            if (Globals.VerboseMode)
                Logger.Write($"Attempting fuzzy match on: {fuzzyKey}", true);

            if (_fuzzyPathMap.TryGetValue(fuzzyKey, out var ratingKey))
                return ratingKey;

            return -1;
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
            var match = Globals.PlexPlaylistList
                .FirstOrDefault(playlist => string.Equals(playlist.PlaylistTitle, playlistName, StringComparison.OrdinalIgnoreCase));

            return match?.RatingKey ?? -1;
        }
    }
}
