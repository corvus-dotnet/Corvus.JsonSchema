// <copyright file="JsonRegex.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON regex.
/// </summary>
public readonly partial struct JsonRegex
{
    private static readonly Regex Empty = MyRegex();

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRegex"/> struct.
    /// </summary>
    /// <param name="value">The Regex value.</param>
    public JsonRegex(Regex value)
    {
        this.jsonElementBacking = default;
        this.stringBacking = FormatRegex(value);
        this.backing = Backing.String;
    }

    /// <summary>
    /// Implicit conversion to Regex.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator Regex(JsonRegex value)
    {
        return value.GetRegex();
    }

    /// <summary>
    /// Implicit conversion from Regex.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonRegex(Regex value)
    {
        return new JsonRegex(value);
    }

    /// <summary>
    /// Gets the value as a Regex.
    /// </summary>
    /// <param name="options">The regular experssion options (<see cref="RegexOptions.None"/> by default).</param>
    /// <returns>The value as a Regex.</returns>
    /// <exception cref="InvalidOperationException">The value was not a regex.</exception>
    public Regex GetRegex(RegexOptions options = RegexOptions.None)
    {
        if (this.TryGetRegex(out Regex result, options))
        {
            return result;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Try to get the Regex value.
    /// </summary>
    /// <param name="result">The regex value.</param>
    /// <param name="options">The regular experssion options (<see cref="RegexOptions.None"/> by default).</param>
    /// <returns><c>True</c> if it was possible to get a regex value from the instance.</returns>
    public bool TryGetRegex(out Regex result, RegexOptions options = RegexOptions.None)
    {
        if ((this.backing & Backing.String) != 0)
        {
            return TryParseRegex(this.stringBacking, options, out result);
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            string? str = this.jsonElementBacking.GetString();
            if (str is not null)
            {
                return TryParseRegex(str!, options, out result);
            }
        }

        result = Empty;
        return false;
    }

    private static string FormatRegex(Regex value)
    {
        return value.ToString();
    }

    private static bool TryParseRegex(string text, RegexOptions options, out Regex value)
    {
        try
        {
            value = new Regex(text, options);
            return true;
        }
        catch (Exception)
        {
            value = Empty;
            return false;
        }
    }

    [RegexGenerator(".*", RegexOptions.None)]
    private static partial Regex MyRegex();
}