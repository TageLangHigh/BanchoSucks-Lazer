// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE-OSU file in the repository root for full licence text.

using System;
using System.Buffers;
using System.IO;

namespace osu.Game.IPC
{
    /// <summary>
    /// Banchosucks: the rules of the overlay data source's little HTTP side, without any networking:
    /// which paths exist, which web pages may read the data, and which file a request for an overlay page maps to.
    /// </summary>
    public static class BanchosucksOverlayHttp
    {
        /// <summary>
        /// tosu's port, which overlays have built in.
        /// </summary>
        public const int DEFAULT_PORT = 24050;

        public const string WEBSOCKET_PATH = "/websocket/v2";
        public const string JSON_PATH = "/json/v2";

        private static readonly SearchValues<char> forbidden_path_characters = SearchValues.Create("\\:\0*?\"<>|");

        public static bool IsWebSocketPath(string path) => trimSlash(path).Equals(WEBSOCKET_PATH, StringComparison.OrdinalIgnoreCase);

        public static bool IsJsonPath(string path) => trimSlash(path).Equals(JSON_PATH, StringComparison.OrdinalIgnoreCase);

        private static string trimSlash(string path) => path.Length > 1 ? path.TrimEnd('/') : path;

        /// <summary>
        /// Whether a request with this <c>Origin</c> header is answered.
        /// </summary>
        /// <remarks>
        /// A websocket to 127.0.0.1 can be opened by any web page in the player's browser; the browser sends the page's
        /// origin along. Only pages that are themselves local may read the game state: overlays served from this port,
        /// from another local server, or opened as a file (OBS browser sources). Programs that are not browsers send no
        /// origin and are let in: they could read much more than this document anyway.
        /// </remarks>
        public static bool IsAllowedOrigin(string? origin)
        {
            if (string.IsNullOrWhiteSpace(origin))
                return true;

            origin = origin.Trim();

            // pages opened from disk
            if (origin.Equals("null", StringComparison.OrdinalIgnoreCase) || origin.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                return false;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            string host = uri.Host;

            return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                   || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
                   || host == "127.0.0.1"
                   || host == "[::1]"
                   || host == "::1";
        }

        /// <summary>
        /// Maps the path of a GET request to a file below <paramref name="root"/> (the folder the player puts overlays into).
        /// </summary>
        /// <param name="root">The overlay folder.</param>
        /// <param name="urlPath">The path of the request, still percent-encoded, without query.</param>
        /// <returns>The full path of the file to serve (which may not exist), or null when the request must be refused.</returns>
        public static string? ResolveStaticFile(string root, string urlPath)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(urlPath) || urlPath[0] != '/')
                return null;

            string decoded;

            try
            {
                decoded = Uri.UnescapeDataString(urlPath);
            }
            catch (UriFormatException)
            {
                return null;
            }

            if (decoded.AsSpan().IndexOfAny(forbidden_path_characters) >= 0)
                return null;

            bool directory = decoded.EndsWith('/');
            string[] segments = decoded.Split('/', StringSplitOptions.RemoveEmptyEntries);

            foreach (string segment in segments)
            {
                // no way up, no hidden files, no names Windows treats specially ("con.", "file ")
                if (segment.StartsWith('.') || segment.EndsWith('.') || segment.EndsWith(' ') || segment.Trim().Length == 0)
                    return null;
            }

            string relative = Path.Combine(segments);

            // "/my-overlay/" and "/my-overlay" both mean its index page
            if (directory || segments.Length == 0 || !Path.HasExtension(segments[^1]))
                relative = Path.Combine(relative, "index.html");

            string fullRoot = Path.GetFullPath(root);
            string full = Path.GetFullPath(Path.Combine(fullRoot, relative));

            if (!full.StartsWith(fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return null;

            return full;
        }

        public static string ContentTypeFor(string file)
        {
            switch (Path.GetExtension(file).ToLowerInvariant())
            {
                case ".html":
                case ".htm":
                    return "text/html; charset=utf-8";

                case ".js":
                case ".mjs":
                    return "text/javascript; charset=utf-8";

                case ".css":
                    return "text/css; charset=utf-8";

                case ".json":
                    return "application/json; charset=utf-8";

                case ".txt":
                    return "text/plain; charset=utf-8";

                case ".svg":
                    return "image/svg+xml";

                case ".png":
                    return "image/png";

                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";

                case ".gif":
                    return "image/gif";

                case ".webp":
                    return "image/webp";

                case ".ico":
                    return "image/x-icon";

                case ".woff":
                    return "font/woff";

                case ".woff2":
                    return "font/woff2";

                case ".ttf":
                    return "font/ttf";

                case ".otf":
                    return "font/otf";

                case ".mp3":
                    return "audio/mpeg";

                case ".ogg":
                    return "audio/ogg";

                case ".wav":
                    return "audio/wav";

                case ".mp4":
                    return "video/mp4";

                case ".webm":
                    return "video/webm";

                default:
                    return "application/octet-stream";
            }
        }
    }
}
