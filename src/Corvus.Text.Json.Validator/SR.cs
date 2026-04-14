// <copyright file="SR.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Resources;

namespace Resources;

internal static partial class Strings
{
    private static readonly bool s_usingResourceKeys = GetUsingResourceKeysSwitchValue();

    private static bool GetUsingResourceKeysSwitchValue() => AppContext.TryGetSwitch("System.Resources.UseSystemResourceKeys", out bool usingResourceKeys) && usingResourceKeys;

    internal static bool UsingResourceKeys() => s_usingResourceKeys;

#if CORECLR || LEGACY_GETRESOURCESTRING_USER
    internal
#else

    private
#endif
    static string GetResourceString(string resourceKey)
    {
        if (UsingResourceKeys())
        {
            return resourceKey;
        }

        string? resourceString = null;
        try
        {
            resourceString =
#if SYSTEM_PRIVATE_CORELIB || NATIVEAOT
                InternalGetResourceString(resourceKey);
#else
                ResourceManager.GetString(resourceKey);
#endif
        }
        catch (MissingManifestResourceException) { }

        return resourceString!;
    }

#if LEGACY_GETRESOURCESTRING_USER
    internal
#else
    private
#endif
    static string GetResourceString(string resourceKey, string defaultString)
    {
        string resourceString = GetResourceString(resourceKey);

        return resourceKey == resourceString || resourceString == null ? defaultString : resourceString;
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

    internal static string Format(string resourceFormat, object? p1, object? p2, object? p3)
    {
        if (UsingResourceKeys())
        {
            return string.Join(", ", resourceFormat, p1, p2, p3);
        }

        return string.Format(resourceFormat, p1, p2, p3);
    }

    internal static string Format(string resourceFormat, params object?[]? args)
    {
        if (args != null)
        {
            if (UsingResourceKeys())
            {
                return resourceFormat + ", " + string.Join(", ", args);
            }

            return string.Format(resourceFormat, args);
        }

        return resourceFormat;
    }

    internal static string Format(IFormatProvider? provider, string resourceFormat, object? p1)
    {
        if (UsingResourceKeys())
        {
            return string.Join(", ", resourceFormat, p1);
        }

        return string.Format(provider, resourceFormat, p1);
    }

    internal static string Format(IFormatProvider? provider, string resourceFormat, object? p1, object? p2)
    {
        if (UsingResourceKeys())
        {
            return string.Join(", ", resourceFormat, p1, p2);
        }

        return string.Format(provider, resourceFormat, p1, p2);
    }

    internal static string Format(IFormatProvider? provider, string resourceFormat, object? p1, object? p2, object? p3)
    {
        if (UsingResourceKeys())
        {
            return string.Join(", ", resourceFormat, p1, p2, p3);
        }

        return string.Format(provider, resourceFormat, p1, p2, p3);
    }

    internal static string Format(IFormatProvider? provider, string resourceFormat, params object?[]? args)
    {
        if (args != null)
        {
            if (UsingResourceKeys())
            {
                return resourceFormat + ", " + string.Join(", ", args);
            }

            return string.Format(provider, resourceFormat, args);
        }

        return resourceFormat;
    }
}