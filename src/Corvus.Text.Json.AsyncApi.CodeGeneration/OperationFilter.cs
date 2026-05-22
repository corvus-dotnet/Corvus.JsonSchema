// <copyright file="OperationFilter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Filters operations by channel using glob-style patterns.
/// Include patterns are additive (union); exclude patterns subtract from the include set.
/// If no include patterns are specified, all operations are included.
/// </summary>
public sealed class OperationFilter
{
    private readonly Regex[] includeRegexes;
    private readonly Regex[] excludeRegexes;

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationFilter"/> class.
    /// </summary>
    /// <param name="includePaths">
    /// Glob patterns for channels to include. If empty, all channels are included.
    /// Supports <c>*</c> (single segment), <c>**</c> (any depth), and <c>{name}</c> (path parameter).
    /// </param>
    /// <param name="excludePaths">
    /// Glob patterns for channels to exclude from the include set.
    /// </param>
    /// <param name="tags">
    /// Tag names to filter by. If non-empty, only operations with at least one matching tag are included.
    /// </param>
    public OperationFilter(
        IReadOnlyList<string>? includePaths = null,
        IReadOnlyList<string>? excludePaths = null,
        IReadOnlyList<string>? tags = null)
    {
        this.IncludePaths = includePaths ?? Array.Empty<string>();
        this.ExcludePaths = excludePaths ?? Array.Empty<string>();
        this.Tags = tags ?? Array.Empty<string>();
        this.includeRegexes = CompilePatterns(this.IncludePaths);
        this.excludeRegexes = CompilePatterns(this.ExcludePaths);
    }

    /// <summary>
    /// Gets the include patterns.
    /// </summary>
    public IReadOnlyList<string> IncludePaths { get; }

    /// <summary>
    /// Gets the exclude patterns.
    /// </summary>
    public IReadOnlyList<string> ExcludePaths { get; }

    /// <summary>
    /// Gets the tag names to filter by. If empty, tags are not considered.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Determines whether the given channel matches the filter.
    /// </summary>
    /// <param name="path">The channel name to test.</param>
    /// <returns><see langword="true"/> if the channel matches the filter; otherwise, <see langword="false"/>.</returns>
    public bool Matches(ReadOnlySpan<char> path)
    {
        bool included = this.includeRegexes.Length == 0;

        if (!included)
        {
            for (int i = 0; i < this.includeRegexes.Length; i++)
            {
                if (this.includeRegexes[i].IsMatch(path))
                {
                    included = true;
                    break;
                }
            }
        }

        if (!included)
        {
            return false;
        }

        for (int i = 0; i < this.excludeRegexes.Length; i++)
        {
            if (this.excludeRegexes[i].IsMatch(path))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether the given operation tags satisfy the tag filter.
    /// </summary>
    /// <param name="operationTags">The tags declared on the operation.</param>
    /// <returns><see langword="true"/> if no tag filter is set, or if at least
    /// one operation tag matches a filter tag; otherwise <see langword="false"/>.</returns>
    public bool MatchesTags(IReadOnlyList<string> operationTags)
    {
        if (this.Tags.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < operationTags.Count; i++)
        {
            for (int j = 0; j < this.Tags.Count; j++)
            {
                if (string.Equals(operationTags[i], this.Tags[j], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Regex[] CompilePatterns(IReadOnlyList<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return [];
        }

        var result = new Regex[patterns.Count];
        for (int i = 0; i < patterns.Count; i++)
        {
            result[i] = new Regex(
                GlobToRegex(patterns[i]),
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        return result;
    }

    private static string GlobToRegex(string pattern)
    {
        string escaped = Regex.Escape(pattern);

        escaped = escaped.Replace(@"/\*\*", "(/.*)?");
        escaped = escaped.Replace(@"\*\*", ".*");
        escaped = escaped.Replace(@"\*", "[^/]*");

        string regexPattern = "^" + escaped + "$";

        regexPattern = Regex.Replace(regexPattern, @"\\{[^}]*\\}", "[^/]+");

        return regexPattern;
    }
}