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

using System.Reflection;

namespace ListPorter
{
    internal sealed class Globals
    {
        /// <summary>
        /// Class representing a Plex playlist with its ratingKey, title, and track count.
        /// </summary>
        public sealed class PlexPlaylist
        {
            /// <summary>The unique key associated with the playlist.</summary>
            public long RatingKey { get; set; }
            /// <summary>The title of the playlist.</summary>
            public string PlaylistTitle { get; set; } = string.Empty;
            /// <summary>The total number of tracks in the playlist.</summary>
            public long TrackCount { get; set; }
        }

        /// <summary>
        /// Class representing an audio track with its ratingKey and file path. We will use this
        /// in two ways (1) to store every single song stored in Plex, and (2) to store
        /// the contents of a playlist that we are importing.
        /// </summary>
        public sealed class TrackInfo
        {
            /// <summary>The unique key associated with the track.</summary>
            public long RatingKey { get; set; }
            /// <summary>The file path of the track.</summary>
            public string FilePath { get; set; } = string.Empty;
        }

        // Path re-writing options
        public enum PathStyle
        {
            /// <summary>Do nothing</summary>
            Auto,
            /// <summary>Replace slashes with backslashes</summary>
            ForceWindows,
            /// <summary>Replace backslashes with slashes</summary>
            ForceLinux
        }

        #region User defined settings
        /// <summary>IP address of the Plex server</summary>
        public static string PlexHost { get; set; } = "127.0.0.1";

        /// <summary>Port of the Plex server</summary>
        public static int PlexPort { get; set; } = 32400;

        /// <summary>Whether to use HTTPS for the connection</summary>
        public static bool UsingSecureConnection { get; set; }

        /// <summary>Plex token for authentication</summary>
        public static string PlexToken { get; set; } = "";

        /// <summary>Library ID to use</summary>
        public static int PlexLibrary { get; set; } = -1;

        /// <summary>Path to import M3U files</summary>
        public static string PathToImport { get; set; } = "";

        /// <summary>Delete all playlists before importing</summary>
        public static bool DeleteAll { get; set; }

        /// <summary>Mirror playlists</summary>
        public static bool MirrorPlaylists { get; set; }

        /// <summary>Output verbose messages (API calls and lookup results)</summary>
        public static bool VerboseMode { get; set; }

        /// <summary>Update the Plex library before adding playlists</summary>
        public static bool UpdateLibrary { get; set; }

        /// <summary>Text to find in file paths</summary>
        public static string FindText { get; set; } = "";

        /// <summary>Text to replace in file paths</summary>
        public static string ReplaceText { get; set; } = "";

        /// <summary>Path style conversion mode</summary>
        public static PathStyle PathStyleOption { get; set; } = PathStyle.Auto;

        /// <summary>Base path for use with relative paths</summary>
        public static string BasePath { get; set; } = "";

        /// <summary>Check GitHub for new versions</summary>
        public static bool GitHubVersionCheck { get; set; } = true;

        #endregion

        #region Internal settings

        /// <summary>Path to the app data folder</summary>
        public static string AppDataPath { get; set; } = "";

        /// <summary>Current application version</summary>
        public static Version ProgramVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version!;

        /// <summary>List of all audio tracks in the Plex library</summary>
        public static List<TrackInfo> PlexTrackList { get; set; } = [];

        /// <summary>List of tracks in the imported playlist</summary>
        public static List<TrackInfo> ImportedPlaylist { get; set; } = [];

        /// <summary>List of playlists fetched from Plex</summary>
        public static List<PlexPlaylist> PlexPlaylistList { get; set; } = [];

        /// <summary>Whether to use fuzzy matching (disabled if path rewriting is used)</summary>
        public static bool UseFuzzyMatching { get; set; } = true;

        /// <summary>Whether path rewriting (find/replace or Unix/Windows) is in use</summary>
        public static bool UsingPathRewriting { get; set; }
        #endregion
    }
}
