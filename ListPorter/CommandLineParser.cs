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

using System.Globalization;

namespace ListPorter
{
    internal sealed class CommandLineParser
    {
        public static List<string> ParsedFlags = [];

        /// <summary>
        /// Parses command line arguments.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void ParseArguments(string[] args)
        {
            if (args.Length == 0)
                ConsoleOutput.DisplayUsage();

            // Loop through all arguments
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower(CultureInfo.InvariantCulture);

                if (arg == "/?" || arg == "-h" || arg == "--help")
                    ConsoleOutput.DisplayUsage();
                else if (arg == "-s" || arg == "--server" && i + 1 < args.Length)
                {
                    string serverArg = args[i + 1];
                    i++; // Skip next argument as it's the value
                    if (serverArg.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        Globals.UsingSecureConnection = true;
                        serverArg = serverArg["https://".Length..];
                    }
                    else if (serverArg.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        Globals.UsingSecureConnection = false;
                        serverArg = serverArg["http://".Length..];
                    }
                    else
                    {
                        // Default to false or your current default
                        Globals.UsingSecureConnection = false;
                    }
                    // Now split on ':'
                    string[] bits = serverArg.Split(':');

                    if (bits.Length == 1)
                        Globals.PlexHost = bits[0];
                    else if (bits.Length == 2)
                    {
                        Globals.PlexHost = bits[0];
                        if (int.TryParse(bits[1], out int port))
                            Globals.PlexPort = port;
                        else
                            ConsoleOutput.DisplayUsage($"Invalid Plex port ({bits[1]})");
                    }
                    else
                    {
                        ConsoleOutput.DisplayUsage($"Invalid format of Plex host and port ({serverArg})");
                    }
                }
                else if (arg == "-t" || arg == "--token" && i + 1 < args.Length)
                {
                    Globals.PlexToken = args[i + 1];
                    i++; // Skip next argument as it's the value
                }
                else if (arg == "-l" || arg == "--library" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int level))
                    {
                        Globals.PlexLibrary = level;
                        i++; // Skip next argument as it's the value
                    }
                    else
                    {
                        ConsoleOutput.DisplayUsage("Invalid Plex library ID.");
                    }
                }
                else if (arg == "-i" || arg == "--import" && i + 1 < args.Length)
                {
                    Globals.PathToImport = args[i + 1];
                    i++;
                }
                else if (arg == "-b" || arg == "--base-path" && i + 1 < args.Length)
                {
                    Globals.BasePath = args[i + 1];
                    i++;
                }
                else if (arg == "-d" || arg == "--delete-all")
                {
                    Globals.DeleteAll = true;
                    ParsedFlags.Add("Delete-all");
                }
                else if (arg == "-m" || arg == "--mirror")
                {
                    Globals.MirrorPlaylists = true;
                    ParsedFlags.Add("Mirror");
                }
                else if (arg == "-f" || arg == "--find" && i + 1 < args.Length)
                {
                    Globals.FindText = args[i + 1];
                    i++;
                }
                else if (arg == "-r" || arg == "--replace" && i + 1 < args.Length)
                {
                    Globals.ReplaceText = args[i + 1];
                    i++;
                }
                else if (arg == "-u" || arg == "--unix" || arg == "--linux")  // Allow --linux as an alias for --unix
                    Globals.PathStyleOption = Globals.PathStyle.ForceLinux;
                else if (arg == "-w" || arg == "--windows")
                    Globals.PathStyleOption = Globals.PathStyle.ForceWindows;
                else if (arg == "-v" || arg == "--verbose")
                {
                    Globals.VerboseMode = true;
                    ParsedFlags.Add("Verbose");
                }
                else if (arg == "-x" || arg == "--exact-only")  // Disable fuzzy matching
                {
                    Globals.UseFuzzyMatching = false;
                    ParsedFlags.Add("Exact-only");
                }
                else if (arg == "-k" || arg == "--update")  // Update Plex library before importing
                {
                    Globals.UpdateLibrary = true;
                    ParsedFlags.Add("Update");
                }
                else if (arg == "-nc" || arg == "--no-check")
                {
                    Globals.GitHubVersionCheck = false;
                    ParsedFlags.Add("No-check");
                }

                else if (arg[0] == '/' || arg[0] == '-')
                    ConsoleOutput.DisplayUsage($"Unknown option: {arg}");
            }

            // Perform validation of required parameters
            Validate();
        }

        /// <summary>
        /// Validates the required input parameters and configuration settings for the application.
        /// </summary>
        /// <remarks>This method ensures that all necessary parameters are provided and valid before
        /// proceeding. If any required parameter is missing or invalid, the application displays usage instructions and
        /// terminates. Additionally, it configures path rewriting and fuzzy matching behavior based on the provided
        /// inputs.</remarks>
        private static void Validate()
        {
            if (string.IsNullOrEmpty(Globals.PlexToken))
                ConsoleOutput.DisplayUsage("Missing Plex token (-t)");

            if (Globals.PlexLibrary < 0)
                ConsoleOutput.DisplayUsage("Missing Plex library ID (-l)");

            if (string.IsNullOrEmpty(Globals.PathToImport))
                ConsoleOutput.DisplayUsage("Missing path or filename of playlists to import (-i)");

            if (string.IsNullOrEmpty(Globals.FindText) && !string.IsNullOrEmpty(Globals.ReplaceText))
                ConsoleOutput.DisplayUsage($"No text to find defined for replacement text ('{Globals.ReplaceText}')");

            if (Globals.PathToImport != null && !Directory.Exists(Globals.PathToImport) && !File.Exists(Globals.PathToImport))
                ConsoleOutput.DisplayUsage($"Path to import does not exist ({Globals.PathToImport})");

            // If path rewriting is enabled, turn off fuzzy path matching

            if (!string.IsNullOrEmpty(Globals.FindText) || Globals.PathStyleOption != Globals.PathStyle.Auto || !string.IsNullOrEmpty(Globals.BasePath))
            {
                Globals.UsingPathRewriting = true;
                Globals.UseFuzzyMatching = false;
                Logger.Write("Path rewriting enabled, fuzzy path matching is disabled.", true);
            }
        }
    }
}