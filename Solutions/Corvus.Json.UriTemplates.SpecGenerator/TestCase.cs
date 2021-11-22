// <copyright file="TestCase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Text;
using System.Text.Json;
using Corvus.Json;

/// <summary>
/// Wraps a <see cref="JsonArray"/> to create a test case.
/// </summary>
#pragma warning disable CA1050 // Declare types in namespaces
public readonly struct TestCase
#pragma warning restore CA1050 // Declare types in namespaces
{
    private readonly JsonString template;
    private readonly JsonAny result;

    /// <summary>
    /// Initializes a new instance of the <see cref="Variable"/> struct.
    /// </summary>
    /// <param name="testcase">The <see cref="JsonArray"/> backing.</param>
    public TestCase(JsonArray testcase)
    {
        int index = 0;

        this.template = default;
        this.result = default;

        foreach (JsonAny item in testcase.EnumerateArray())
        {
            if (index == 0)
            {
                this.template = item;
            }
            else if (index == 1)
            {
                this.result = item;
            }
            else
            {
                throw new InvalidOperationException("A test case must be constructed from an array of two items.");
            }

            ++index;
        }
    }

    /// <summary>
    /// Gets the test case template.
    /// </summary>
    public string Template
    {
        get
        {
            return ((string)this.template).Replace("|", "\\|");
        }
    }

    /// <summary>
    /// Gets the test case result.
    /// </summary>
    public string Result
    {
        get
        {
            JsonArray result;
            if (this.result.ValueKind == JsonValueKind.Array)
            {
                result = this.result;
            }
            else
            {
                result = JsonArray.From(this.result);
            }

            return Encoding.UTF8.GetString(result.GetRawText(new JsonWriterOptions { Indented = false })).Replace("|", "\\|");
        }
    }
}
