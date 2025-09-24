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
    /// A class to handle importing M3U and M3U8 playlist files and matching their contents to a Plex Media Server library.
    /// </summary>
    internal sealed class PlaylistImporter
    {
        /// <summary>List of M3U files to process</summary>
        public static List<string> M3UFilesToImport { get; set; } = [];
        
        /// <summary>Name of the playlist being imported</summary>
        public static string ImportedPlaylistTitle { get; set; } = "";


        /// <summary>
        /// Finds M3U or M3U8 files to import based on the input file path or directory.
        /// If the input is a single M3U/M3U8 file, its full path is added to the global array.
        /// If the input is a directory, all M3U/M3U8 files (non-recursive) in the directory are added to the global array.
        /// </summary>
        /// <param name="filePathOrFile">The file or directory path to search for M3U or M3U8 files.</param>
        public static void FindM3UToImport(string filePathOrFile)
        {
            // Initialize or clear the global array for storing M3U/M3U8 files to import
            M3UFilesToImport = [];

            // Check if the input is a file or directory.
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

            // Count the number of m3u files found and if it's zero, exit the program

            if (M3UFilesToImport.Count == 0)
            {
                Logger.Write($"Could not find any m3u or m3u8 files to import in: {Globals.PathToImport}");
                System.Environment.Exit(0);
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
            ImportedPlaylistTitle = "";
            Globals.ImportedPlaylist.Clear();
            int playlistImportErrors = 0;  // Count of errors during playlist import
            bool verbosity = false;  // Whether to log verbose messages. When playlistImportErrors gets over 5, this will be set to true to reduce the amount of output.

            if (!File.Exists(filePath))
            {
                Logger.Write($"M3U file not found: {filePath}");
                return false;
            }

            Logger.Write($"Loading playlist: {filePath}", true);

            var lines = File.ReadAllLines(filePath);
            bool playlistTitleFound = false;
            int failed = 0, added = 0;

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith('#') && !line.StartsWith("#PLAYLIST:", StringComparison.CurrentCulture))
                    continue;

                if (line.StartsWith("#PLAYLIST:", StringComparison.CurrentCulture))
                {
                    ImportedPlaylistTitle = line[10..].Trim();
                    playlistTitleFound = true;
                    Logger.Write($"Found #PLAYLIST, title is: {PlaylistImporter.ImportedPlaylistTitle}", true);
                }
                else
                {
                    // Some programs appear to store the file path with a prefix like "file-relative://" or "file://"
                    // which isn't strictly part of the M3U specification - so we should remove these.
                    foreach (var prefix in new[] { "file-relative://", "file://" })
                        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            line = line[prefix.Length..];
                            break;
                        }

                    // Now rewrite any changes to the location of the file as specified by the user
                    string rewrittenLine = RewriteLocation(line);

                    // See if we can find the item stored within Plex
                    long ratingKey = PlexService.GetRatingKeyFromFilePath(rewrittenLine);
                    if (ratingKey == -1)
                    {
                        if (rewrittenLine == line)
                            Logger.Write($"No match found in Plex library {Globals.PlexLibrary} for path '{line}' in: {filePath}", verbosity);
                        else
                            Logger.Write($"No match found in Plex library {Globals.PlexLibrary} for path '{rewrittenLine}' (tranformed from '{line}') in: {filePath}", verbosity);

                        failed++;
                        playlistImportErrors++;
                        PlexService.TotalImportErrors++;

                        if (playlistImportErrors == 5)
                        {
                            verbosity = true;  // Switch to verbose logging after 5 errors
                            Logger.Write($"Showing only first 5 unmatched audio tracks in this playlist - see logs for further matching errors.", false);
                        }
                    }
                    else
                    {
                        // Add to the imported playlist
                        Globals.ImportedPlaylist.Add(new Globals.TrackInfo
                        {
                            RatingKey = ratingKey,
                            FilePath = line
                        });
                        added++;

                        if (Globals.VerboseMode)
                            Logger.Write($"Added to playlist: {rewrittenLine} (ratingKey: {ratingKey})", true);
                    }
                }
            }

            // Fallback to using the file name if no #PLAYLIST is found
            if (!playlistTitleFound)
            {
                ImportedPlaylistTitle = Path.GetFileNameWithoutExtension(filePath);
                Logger.Write($"Missing #PLAYLIST, assuming title: {ImportedPlaylistTitle}", true);
            }

            if (added == 0 && failed == 0)
            {
                Logger.Write($"Warning: '{ImportedPlaylistTitle}' is empty!");
                return false;
            }

            if (added == 0 && failed > 0)
            {
                Logger.Write($"Warning: All {GrammarHelper.Pluralise(failed, "item", "items")} in '{ImportedPlaylistTitle}' failed to match Plex database!");
                return false;
            }

            if (added > 0 && failed == 0)
            {
                Logger.Write($"All {GrammarHelper.Pluralise(added, "item", "items")} in '{ImportedPlaylistTitle}' matched to Plex database.", true);
                return true;
            }

            // If we get here, we have a mix of added and failed items
            Logger.Write($"Warning: {GrammarHelper.Pluralise(failed, "item", "items")} could not be matched to Plex database!");
            Logger.Write($"Only {GrammarHelper.Pluralise(added, "item", "items")} will be added to '{ImportedPlaylistTitle}'");
            return true;
        }

        /// <summary>
        /// Takes a path and rewrites it depending on whether useLinuxPaths is configured and if there are any search and replace paramaters defined.
        /// </summary>
        /// <param name="path">Path and filename of a file</param>
        /// <returns>Converted path and filename</returns>
        static string RewriteLocation(string path)
        {
            // Strip any leading or trailing whitespace
            string newPath = path.Trim();

            // Only force the path style if the user has set it

            if (Globals.PathStyleOption == Globals.PathStyle.ForceLinux)
            {
                // Convert Windows-style paths to Linux-style paths
                newPath = newPath.Replace('\\', '/');
            }
            else if (Globals.PathStyleOption == Globals.PathStyle.ForceWindows)
            {
                // Convert Linux-style paths to Windows-style paths
                newPath = newPath.Replace('/', '\\');
            }

            // Do other search and repace here

            if (!string.IsNullOrEmpty(Globals.FindText))
                newPath = newPath.Replace(Globals.FindText, Globals.ReplaceText, StringComparison.CurrentCultureIgnoreCase);

            // If basePath is set, ensure the path is relative to it
            if (!string.IsNullOrEmpty(Globals.BasePath))
            {
                // Remove the . from the beginning of the path
                if (newPath.StartsWith('.'))
                    newPath = newPath[1..];
                // Blindly prepend the base-path, it'll be up the user to correctly set path seperators
                newPath = Globals.BasePath + newPath;

            }

            return newPath;
        }
    }
}
