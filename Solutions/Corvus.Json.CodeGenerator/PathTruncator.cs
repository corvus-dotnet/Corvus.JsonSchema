/// <summary>
/// Truncates paths to a given length.
/// </summary>
public class PathTruncator
{
    /// <summary>
    /// Truncate a path.
    /// </summary>
    /// <param name="path">The path to truncate.</param>
    /// <param name="maxLength">The maximum length of the path.</param>
    /// <returns>The truncated path.</returns>
    /// <exception cref="ArgumentException">The directory path is too long to truncation the file name.</exception>
    public static string TruncatePath(string path, int maxLength = 260)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        if (path.Length <= maxLength)
        {
            return path;
        }

        // TODO: figure out a good truncation algorithm.

        return path;
    }
}
