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
using System.Text;

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
        /// <summary>Maps normalized artist/title combinations to the Plex tracks that share them</summary>
        private static readonly Dictionary<string, List<Globals.TrackInfo>> _metadataMap = [];
        /// <summary>Maps normalized track titles to the Plex tracks that share them (used when no artist is available)</summary>
        private static readonly Dictionary<string, List<Globals.TrackInfo>> _titleOnlyMap = [];
        /// <summary>Characters used as path separators</summary>
        private static readonly char[] _pathSeparators = ['/', '\\'];
        /// <summary>Tolerance (in seconds) when comparing the duration in an #EXTINF line with the duration stored by Plex</summary>
        private const int DurationToleranceSeconds = 2;


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
            {
                // Even though it failed, we add this to processed list. This is so that
                // using --mirror doesn't delete it
                ProcessedPlaylistTitles.Add(PlaylistImporter.ImportedPlaylistTitle); 
                return;
            }

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
                if (!PlexClient.DeleteAllItemsInPlaylist(ratingKey))
                {
                    Logger.Write($"Skipping update of playlist '{PlaylistImporter.ImportedPlaylistTitle}' because clearing its existing items failed.");
                    // Even though it failed, we add this to processed list. This is so that
                    // using --mirror doesn't delete it
                    ProcessedPlaylistTitles.Add(PlaylistImporter.ImportedPlaylistTitle); 
                    return;
                }
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
                System.Environment.Exit(1);  // Unreachable but included for clarity and safety
            }

            // If we are here then we have built the map successfully
            Logger.Write($"Fuzzy path map built with {_fuzzyPathMap.Count} entries", true);
        }

        /// <summary>
        /// Builds a map of normalized artist/title combinations (and, as a fallback, normalized track
        /// titles) to the tracks in the Plex library. This allows tracks in a playlist to be matched
        /// by the artist and title stored in `#EXTINF` lines when their file paths cannot be matched.
        /// Unlike the fuzzy path map, duplicate keys are allowed: when they occur the duration and
        /// album of the track are used to pick the correct match.
        /// </summary>
        public static void BuildMetadataMap()
        {
            if (!Globals.UseMetadataMatching)
            {
                Logger.Write("Artist/title metadata matching is disabled.");
                return;
            }

            Logger.Write("Building artist/title metadata map...");

            _metadataMap.Clear();
            _titleOnlyMap.Clear();

            foreach (var track in Globals.PlexTrackList)
            {
                if (string.IsNullOrWhiteSpace(track.Title))
                    continue;

                // Index under the album artist. For compilation albums this is often
                // "Various Artists", so also index under the track artist (if different).
                if (track.Artist.Length > 0)
                    AddToMetadataMap(_metadataMap, $"{NormalizeMetadata(track.Artist)}|{NormalizeMetadata(track.Title)}", track);

                if (track.TrackArtist.Length > 0)
                    AddToMetadataMap(_metadataMap, $"{NormalizeMetadata(track.TrackArtist)}|{NormalizeMetadata(track.Title)}", track);

                // Also index by title only, as a last resort for #EXTINF lines which don't provide an artist
                AddToMetadataMap(_titleOnlyMap, NormalizeMetadata(track.Title), track);
            }

            int ambiguousCombinations = _metadataMap.Values.Count(list => list.DistinctBy(t => t.RatingKey).Count() > 1);
            Logger.Write($"Artist/title metadata map built with {GrammarHelper.Pluralise(_metadataMap.Count, "entry", "entries")}" +
                         (ambiguousCombinations > 0 ? $" ({GrammarHelper.Pluralise(ambiguousCombinations, "combination", "combinations")} of these are ambiguous and will need the track duration or album to disambiguate)" : string.Empty), true);
            Logger.Write($"Title-only metadata map built with {GrammarHelper.Pluralise(_titleOnlyMap.Count, "entry", "entries")}", true);
        }

        /// <summary>
        /// Finds the Plex `ratingKey` of a track by matching the artist and title text from an
        /// `#EXTINF` line (e.g. "Kool & The Gang - Celebration") against the metadata stored by
        /// Plex. This is used as a fallback when the track's file path cannot be matched.
        /// </summary>
        /// <param name="extInfText">The artist/title text taken from an #EXTINF line.</param>
        /// <param name="durationSeconds">Duration in seconds taken from the #EXTINF line, or 0 if unknown.</param>
        /// <param name="filePath">The file path of the track from the playlist, used to suggest the album.</param>
        /// <returns>The ratingKey of the matching track, or -1 if no match is found.</returns>
        public static long GetRatingKeyFromMetadata(string extInfText, int durationSeconds, string filePath)
        {
            if (string.IsNullOrWhiteSpace(extInfText))
                return -1;

            // Some playlist writers use dashes other than a plain hyphen in "Artist - Title"
            string text = extInfText.Replace('–', '-').Replace('—', '-').Trim();

            // Work out the possible artist/title combinations. Normally this is "Artist - Title"
            // but we also try splitting on the last separator in case the artist name itself
            // contains a dash. If no separator is found then we assume it is just a title.
            var candidates = new List<(string Artist, string Title)>();
            int first = text.IndexOf(" - ", StringComparison.Ordinal);
            if (first >= 0)
            {
                candidates.Add((text[..first].Trim(), text[(first + 3)..].Trim()));
                int last = text.LastIndexOf(" - ", StringComparison.Ordinal);
                if (last > first)
                    candidates.Add((text[..last].Trim(), text[(last + 3)..].Trim()));
            }
            else
                candidates.Add((string.Empty, text));

            string albumHint = ExtractAlbumHint(filePath);

            foreach (var (artist, title) in candidates)
            {
                string normalizedTitle = NormalizeMetadata(title);
                if (normalizedTitle.Length == 0)
                    continue;

                // Preferred: match on the artist/title combination
                string normalizedArtist = NormalizeMetadata(artist);
                if (normalizedArtist.Length > 0 && _metadataMap.TryGetValue($"{normalizedArtist}|{normalizedTitle}", out var artistMatches))
                {
                    long ratingKey = SelectMetadataMatch(artistMatches, durationSeconds, albumHint, extInfText);
                    if (ratingKey != -1)
                        return ratingKey;
                }

                // Fallback: match on the title alone. This is only accepted when it
                // identifies a single track (or a single track with a duration that fits).
                if (_titleOnlyMap.TryGetValue(normalizedTitle, out var titleMatches))
                {
                    long ratingKey = SelectMetadataMatch(titleMatches, durationSeconds, albumHint, extInfText);
                    if (ratingKey != -1)
                        return ratingKey;
                }
            }

            return -1;
        }

        /// <summary>
        /// Picks the matching track from a list of metadata candidates. If multiple tracks share the
        /// same metadata then the duration and album name (taken from the playlist) are used to
        /// narrow the choice down. Returns -1 when a single match cannot be safely determined.
        /// </summary>
        /// <param name="candidates">Tracks whose normalized metadata matches the requested artist/title.</param>
        /// <param name="durationSeconds">Duration in seconds taken from the #EXTINF line, or 0 if unknown.</param>
        /// <param name="albumHint">Normalized album name suggested by the playlist's file path, or an empty string if unknown.</param>
        /// <param name="extInfText">The original artist/title text, used for logging.</param>
        /// <returns>The ratingKey of the matching track, or -1 if no single match can be determined.</returns>
        static long SelectMetadataMatch(List<Globals.TrackInfo> candidates, int durationSeconds, string albumHint, string extInfText)
        {
            var remaining = candidates.DistinctBy(t => t.RatingKey).ToList();

            if (remaining.Count == 1)
                return remaining[0].RatingKey;

            // Multiple tracks share the same metadata. If the playlist's file path suggests
            // an album then use it to narrow the choice down.
            if (albumHint.Length > 0)
            {
                var albumMatches = remaining.Where(t => NormalizeMetadata(t.Album) == albumHint).ToList();
                if (albumMatches.Count == 1)
                {
                    Logger.Write($"Metadata match for '{extInfText}' was ambiguous, resolved using album '{albumHint}'.", true);
                    return albumMatches[0].RatingKey;
                }
                if (albumMatches.Count > 1)
                    remaining = albumMatches;
            }

            // If the playlist tells us how long the track should be then use that to
            // narrow the choice down further
            if (durationSeconds > 0)
            {
                var durationMatches = remaining
                    .Where(t => t.Duration > 0 && Math.Abs(t.Duration - durationSeconds) <= DurationToleranceSeconds)
                    .ToList();

                if (durationMatches.Count == 1)
                {
                    Logger.Write($"Metadata match for '{extInfText}' was ambiguous, resolved using duration ({durationSeconds}s).", true);
                    return durationMatches[0].RatingKey;
                }
                if (durationMatches.Count > 1)
                    remaining = durationMatches;
            }

            Logger.Write($"Metadata match for '{extInfText}' is ambiguous: {GrammarHelper.Pluralise(remaining.Count, "candidate", "candidates")} found and no album or duration available to choose between them.", true);
            return -1;
        }

        /// <summary>
        /// Normalizes artist, album or title text so that metadata from an m3u file can be compared
        /// with the metadata stored by Plex. The comparison is case-insensitive, ignores accents and
        /// punctuation, and treats "&" as "and" (so "Kool & The Gang" matches "Kool and the Gang").
        /// </summary>
        /// <param name="text">Artist, album or title text to normalize.</param>
        /// <returns>The normalized text, or an empty string if there is nothing left.</returns>
        public static string NormalizeMetadata(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // m3u files often store "&" as the HTML entity "&amp;"
            string normalized = text.Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase);

            // Treat "&" as "and" so that, e.g., "Kool & The Gang" and "Kool and the Gang" match
            normalized = normalized.Replace("&", " and ");

            // Decompose accented characters so that the accents can be stripped below
            normalized = normalized.Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;
                builder.Append(char.IsWhiteSpace(c) ? ' ' : c);
            }

            // Strip any remaining punctuation, lowercase and collapse runs of whitespace
            return string.Join(' ', builder
                .ToString()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => new string(word.Where(char.IsLetterOrDigit).ToArray()))
                .Where(word => word.Length > 0));
        }

        /// <summary>
        /// Suggests the album a track belongs to based on its file path. The second-to-last
        /// folder of a music path is normally the album (e.g. `artist/album/track`).
        /// </summary>
        /// <param name="filePath">The file path of the track, as stored in the playlist.</param>
        /// <returns>The normalized album name, or an empty string if it cannot be determined.</returns>
        static string ExtractAlbumHint(string filePath)
        {
            var parts = filePath.Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return string.Empty;

            return NormalizeMetadata(parts[^2]);
        }

        /// <summary>Adds a track to a metadata map under the given key.</summary>
        static void AddToMetadataMap(Dictionary<string, List<Globals.TrackInfo>> map, string key, Globals.TrackInfo track)
        {
            if (key.Length == 0)
                return;

            if (!map.TryGetValue(key, out var tracks))
                map[key] = [track];
            else
                tracks.Add(track);
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
