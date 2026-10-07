using System;
using System.Linq;
using System.Text;

namespace SharpBucket.Utility
{
    internal static class UrlHelper
    {
        /// <summary>
        /// Concat url path segments until the first null or empty segment.
        /// </summary>
        /// <exception cref="ArgumentException">A segment contains a dot-segment, '?', '#', '\' or a control character.</exception>
        public static string ConcatPathSegments(params string[] pathSegments)
        {
            var path = new StringBuilder();
            var first = true;
            foreach (var pathSegment in pathSegments)
            {
                if (string.IsNullOrEmpty(pathSegment)) break;

                ValidatePathSegment(pathSegment);

                if (first) first = false;
                else path.Append("/");

                path.Append(pathSegment);
            }
            return path.ToString().Replace("//", "/");
        }

        private static void ValidatePathSegment(string pathSegment)
        {
            if (!IsSafe(pathSegment) || !IsSafe(SafeUnescape(pathSegment)))
            {
                throw new ArgumentException("Path segments must not contain '.', '..', '?', '#', '\\' or control characters.", nameof(pathSegment));
            }
        }

        private static bool IsSafe(string value)
        {
            return value.IndexOfAny(new[] { '?', '#', '\\' }) < 0
                   && !value.Any(char.IsControl)
                   && !value.Split('/').Any(part => part == "." || part == "..");
        }

        private static string SafeUnescape(string value)
        {
            // Unescape twice so that a doubly encoded dot-segment is detected too
            return Uri.UnescapeDataString(Uri.UnescapeDataString(value));
        }
    }
}
