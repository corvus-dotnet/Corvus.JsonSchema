// <copyright file="SecurityTagSetUtf8EnumeratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Tests for <see cref="SecurityTagSet.Utf8Enumerator"/> — the string-free unescaped-UTF-8 enumeration must
/// yield exactly what the managed-string <see cref="SecurityTagSet.Enumerator"/> yields, including for values (and keys)
/// that carry JSON escapes.</summary>
[TestClass]
public sealed class SecurityTagSetUtf8EnumeratorTests
{
    [TestMethod]
    public void Empty_set_yields_nothing()
    {
        int count = 0;
        SecurityTagSet.Utf8Enumerator e = SecurityTagSet.Empty.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                count++;
            }
        }
        finally
        {
            e.Dispose();
        }

        count.ShouldBe(0);
    }

    [TestMethod]
    public void Yields_the_same_keys_and_values_as_the_string_enumerator()
    {
        // Includes an escaped value (quote + backslash + newline) and an escaped key, so both scratch regions are exercised.
        SecurityTag[] tags =
        [
            new("sys:tenant", "acme"),
            new("sys:sub", "alice@acme.example"),
            new("weird\"key", "value with \"quote\", back\\slash and \n newline"),
        ];
        SecurityTagSet set = SecurityTagSet.FromTags(tags);

        var seen = new List<(string Key, string Value)>();
        SecurityTagSet.Utf8Enumerator e = set.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                seen.Add((Encoding.UTF8.GetString(e.CurrentKey), Encoding.UTF8.GetString(e.CurrentValue)));
            }
        }
        finally
        {
            e.Dispose();
        }

        // The set sorts/dedups its tags, so compare against the string enumerator's order rather than the input order.
        var expected = new List<(string Key, string Value)>();
        foreach (SecurityTag tag in set)
        {
            expected.Add((tag.Key, tag.Value));
        }

        seen.ShouldBe(expected);
    }
}
