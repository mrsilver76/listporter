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
    /// <summary>
    /// Simple application logger that writes timestamped messages to a daily rotating log. Logs older than
    /// 14 days are automatically deleted. The logger also writes messages to the console.
    /// </summary>
    internal static class Logger
    {
        private static StreamWriter? _writer;
        private static bool _initialised;
        private static readonly object _lock = new();

        /// <summary>
        /// Initialise the logger with the specified log folder path. This will create the folder if it doesn't exist,
        /// open today's log file, and delete log files older than 14 days.
        /// </summary> 
        /// <param name="logFolderPath">Path to folder containing logs</param>
        /// <exception cref="ArgumentException"></exception>
        public static void Initialise(string logFolderPath)
        {
            lock (_lock)
            {
                if (_initialised)
                    return;

                if (string.IsNullOrEmpty(logFolderPath))
                    throw new ArgumentException("logFolderPath cannot be null or empty", nameof(logFolderPath));

                Directory.CreateDirectory(logFolderPath);

                foreach (var file in Directory.GetFiles(logFolderPath, "*.log"))
                {
                    DateTime lastModified = File.GetLastWriteTime(file);
                    if ((DateTime.Now - lastModified).TotalDays > 14)
                        File.Delete(file);
                }

                string logFileName = $"log-{DateTime.Now:yyyy-MM-dd}.log";
                string logFilePath = Path.Combine(logFolderPath, logFileName);

                _writer = new StreamWriter(new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    AutoFlush = true
                };

                AppDomain.CurrentDomain.ProcessExit += (_, __) => Shutdown();
                Console.CancelKeyPress += (_, e) =>
                {
                    Write("CTRL+C pressed, shutting down...");
                    Shutdown();
                    e.Cancel = false;
                };
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                {
                    // Make sure that the cursor is visible again if we crash
                    Console.CursorVisible = true;
                };

                // Hide the console cursor to prevent it from distracting from log messages
                Console.CursorVisible = false;

                _initialised = true;
            }
        }

        /// <summary>
        /// Write a message to the log file and console with a timestamp.
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void Write(string message, bool verbose = false)
        {
            lock (_lock)
            {
                if (_writer == null)
                    throw new InvalidOperationException("Logger not initialised. Call Logger.Initialise() first.");

                string tsDate = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string tsTime = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                string logEntry = $"[{tsDate} {tsTime}] {message}";

                _writer.WriteLine(logEntry);
                if (!verbose)
                    Console.WriteLine($"[{tsTime}] {message}");
            }
        }

        /// <summary>
        /// Close the log file cleanly. Called automatically on process exit and CTRL+C, so marked
        /// as private since it doesn't really need to be called explicitly.
        /// </summary>
        private static void Shutdown()
        {
            // Restore the console cursor visibility
            Console.CursorVisible = true;

            lock (_lock)
            {
                if (_writer != null)
                {
                    _writer.Dispose();
                    _writer = null;
                }
                _initialised = false;
            }
        }
    }
}
