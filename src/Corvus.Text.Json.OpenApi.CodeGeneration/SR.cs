// <copyright file="SR.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Resources;

internal static partial class Strings
{
    private static readonly bool s_usingResourceKeys = GetUsingResourceKeysSwitchValue();

    private static bool GetUsingResourceKeysSwitchValue() => AppContext.TryGetSwitch("System.Resources.UseSystemResourceKeys", out bool usingResourceKeys) && usingResourceKeys;

    internal static bool UsingResourceKeys() => s_usingResourceKeys;

    private static string GetResourceString(string resourceKey, string defaultString)
    {
        if (UsingResourceKeys())
        {
            return resourceKey;
        }

        string? resourceString = null;
        try
        {
            resourceString = ResourceManager.GetString(resourceKey);
        }
        catch (System.Resources.MissingManifestResourceException) { }

        return resourceString ?? defaultString;
    }

    internal static string Format(string resourceFormat, object? p1)
    {
        if (UsingResourceKeys())
        {
            return string.Join(", ", resourceFormat, p1);
        }

        return string.Format(resourceFormat, p1);
    }

    internal static string Format(string resourceFormat, object? p1, object? p2)
    {
        if (UsingResourceKeys())
        {
            return string.Join(", ", resourceFormat, p1, p2);
        }

        return string.Format(resourceFormat, p1, p2);
    }
}