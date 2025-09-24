/*
 * GitHubVersionChecker.cs
 * Version 1.0.2
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *  
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

/*
 * Usage:
 * -------
 * This class provides a simple way to check GitHub for the latest release of your application
 * and determine whether an update is available.
 *
 * 1. Add a reference to IniParser (NuGet: IniParser) to handle caching in an INI file.
 *
 * 2. Call `GitHubVersionChecker.CheckLatestRelease` from your code:
 *
 *      var currentVersion = new Version(1, 0, 2, 0);  // Your app's current version
 *      var repo = "owner/repo";                       // GitHub repository in "owner/repo" format
 *      var iniPath = "path/to/cache.ini";             // File path to store cached version info
 *
 *      var result = GitHubVersionChecker.CheckLatestRelease(currentVersion, repo, iniPath);
 *
 * 3. The returned `VersionCheckResult` contains:
 *      - UpdateAvailable (bool): true if a newer release exists
 *      - LatestVersion (Version?): the latest version found, null if unavailable
 *
 * 4. The method automatically caches the last check in the provided INI file to avoid
 *    unnecessary network calls. It will re-check GitHub if more than 7 days have passed.
 *
 * Notes:
 * -------
 * - The version string in GitHub releases should follow "major.minor.revision" (e.g., "1.0.2").
 * - The check respects semantic versioning but converts the revision to the fourth segment
 *   of System.Version (major.minor.0.revision) for comparison.
 * - The HTTP request uses a simple User-Agent header derived from the repository and current
 *   version. Without one, GitHub will reject the request.
 */

using IniParser;
using IniParser.Model;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ListPorter
{
    /// <summary>
    /// Provides a collection of pre-defined regular expressions for parsing specific patterns in text.
    /// </summary>
    /// <remarks>This class contains static methods that return compiled <see cref="Regex"/> instances for
    /// common patterns.  The methods are generated at compile time and are optimized for performance.</remarks>
    internal static partial class Regexes
    {
        [GeneratedRegex("\"tag_name\"\\s*:\\s*\"([^\"]+)\"")]
        internal static partial Regex TagName();
    }

    /// <summary>
    /// Immutable result of a version check.
    /// </summary>
    internal readonly record struct VersionCheckResult(
        bool UpdateAvailable,  // Indicates if a newer version is available
        Version? LatestVersion  // The latest version available, or null if not found
    );

    /// <summary>
    /// Handles checking Github for newer releases of the application.
    /// </summary>
    internal static class GitHubVersionChecker
    {
        /// <summary>
        /// Checks for the latest release of a software version from a specified GitHub repository and determines
        /// whether an update is available.
        /// </summary>
        /// <remarks>This method checks the specified GitHub repository for the latest release version and
        /// compares it with the provided <paramref name="currentVersion"/>. The result of the check is cached in the
        /// INI file specified by <paramref name="iniPath"/> to avoid redundant network requests. If the cache indicates
        /// that a check is needed, the method fetches the latest release version from GitHub.</remarks>
        /// <param name="currentVersion">The current version of the software.</param>
        /// <param name="gitHubRepo">The GitHub repository to check for the latest release, specified in the format "owner/repo".</param>
        /// <param name="iniPath">The file path to the INI configuration file used to cache version check data.</param>
        /// <returns>A <see cref="VersionCheckResult"/> object containing information about whether an update is available, the
        /// current version and the latest version (if available)</returns>
        public static VersionCheckResult CheckLatestRelease(Version currentVersion, string gitHubRepo, string iniPath)
        {
            var parser = new FileIniDataParser();
            IniData ini = File.Exists(iniPath) ? parser.ReadFile(iniPath) : new IniData();

            if (NeedsCheck(ini, out Version? cachedVersion))
            {
                var latest = TryFetchLatestVersion(gitHubRepo, currentVersion);
                if (latest != null)
                {
                    ini["Version"]["LatestReleaseChecked"] = latest.Value.Timestamp;

                    if (!string.IsNullOrEmpty(latest.Value.Version))
                    {
                        ini["Version"]["LatestReleaseVersion"] = latest.Value.Version;
                        cachedVersion = ParseSemanticVersion(latest.Value.Version);
                    }

                    parser.WriteFile(iniPath, ini);
                }
            }

            bool updateAvailable = cachedVersion != null && cachedVersion > currentVersion;

            return new VersionCheckResult(updateAvailable, cachedVersion);
        }

        /// <summary>
        /// Parses a semantic version string "major.minor.revision" into a Version object "major.minor.0.revision".
        /// Returns null if parsing fails.
        /// </summary>
        private static Version? ParseSemanticVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                return null;

            var parts = versionString.Split('.');
            if (parts.Length != 3)
                return null;

            if (int.TryParse(parts[0], out int major) &&
                int.TryParse(parts[1], out int minor) &&
                int.TryParse(parts[2], out int revision))
            {
                try
                {
                    return new Version(major, minor, 0, revision);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether a check is needed based on the last checked timestamp.
        /// </summary>
        private static bool NeedsCheck(IniData ini, out Version? cachedVersion)
        {
            string dateStr = ini["Version"]["LatestReleaseChecked"];
            string versionStr = ini["Version"]["LatestReleaseVersion"];

            bool hasTimestamp = DateTime.TryParse(dateStr, out DateTime lastChecked);
            bool isExpired = !hasTimestamp || (DateTime.UtcNow - lastChecked.ToUniversalTime()).TotalDays >= 7;

            cachedVersion = ParseSemanticVersion(versionStr);

            return isExpired;
        }

        /// <summary>
        /// Fetches the latest release version from GitHub.
        /// Returns null if the fetch fails.
        /// </summary>
        private static (string? Version, string Timestamp)? TryFetchLatestVersion(string repo, Version currentVersion)
        {
            string url = $"https://api.github.com/repos/{repo}/releases/latest";
            using var client = new HttpClient();

            string ua = repo.Replace('/', '.') + "/" + currentVersion;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(ua);

            try
            {
                var response = client.GetAsync(url).GetAwaiter().GetResult();
                string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                if (!response.IsSuccessStatusCode)
                    return (null, timestamp);

                string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var match = Regexes.TagName().Match(json);

                if (!match.Success)
                    return (null, timestamp);

                string version = match.Groups[1].Value.TrimStart('v', 'V');
                return (version, timestamp);
            }
            catch
            {
                return null;
            }
        }
    }
}
