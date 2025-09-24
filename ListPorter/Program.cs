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

    sealed class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Initialize logging
            Logger.Initialise(Path.Combine(Globals.AppDataPath, "Logs"));
            Logger.Write("ListPorter started.", true);

            // Parse command line arguments. Note: we must do this before showing the header
            // because the header shows some of the user-defined settings and also logs the
            // command line used. If we don't do this first, then the Plex Token won't be
            // redacted in the log file.
            CommandLineParser.ParseArguments(args);

            // Show the header
            ConsoleOutput.ShowHeader(args);
            
            // Check connectivity
            if (PlexClient.CheckPlexConnectivity() == false)
                System.Environment.Exit(1);

            // Update the Plex library (if the option is enabled)
            PlexClient.UpdatePlexLibrary();

            // Fetch and store all tracks from the Plex library
            PlexClient.FetchAndStoreTracks();

            // Build fuzzy path map if fuzzy matching is enabled
            PlexService.BuildFuzzyPathMap();

            // Fetch and store all playlists from Plex
            PlexClient.FetchAndStorePlaylists(true);

            // Find M3U/M3U8 files to import from the specified directory
            PlaylistImporter.FindM3UToImport(Globals.PathToImport);

            // Delete all playlists in Plex (if the option is enabled)
            PlexClient.DeleteAllPlaylists();

            // Loop through each M3U file found and process the playlist
            foreach (var m3uFile in PlaylistImporter.M3UFilesToImport)
                PlexService.ProcessPlaylist(m3uFile);

            // Mirror playlists if the option is enabled
            if (Globals.MirrorPlaylists)
                PlexService.MirrorPlaylists();

            ConsoleOutput.DisplayResults();

            // Check for latest release
            ConsoleOutput.CheckLatestRelease();
            System.Environment.Exit(0);
        }
    }
}
