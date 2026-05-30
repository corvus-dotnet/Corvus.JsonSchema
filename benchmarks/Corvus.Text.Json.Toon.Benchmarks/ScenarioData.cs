// <copyright file="ScenarioData.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Toon.Benchmarks;

internal static class ScenarioData
{
    public static string CreateCysharpEncodeBenchmarkJson()
    {
        StringBuilder builder = new("[");
        for (int i = 0; i < 100; ++i)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append($$"""{"Id":{{i}},"Name":"Person {{i}}","Age":{{i * 2}}}""");
        }

        builder.Append(']');
        return builder.ToString();
    }
}