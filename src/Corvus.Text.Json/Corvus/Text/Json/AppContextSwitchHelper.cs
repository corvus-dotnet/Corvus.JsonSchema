// <copyright file="AppContextSwitchHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json
{
    /// <summary>
    /// Provides helper methods for accessing application context switches related to JSON serialization.
    /// </summary>
    internal static class AppContextSwitchHelper
    {
        /// <summary>
        /// Gets a value indicating whether source generation reflection fallback is enabled.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the "Corvus.Text.Json.Serialization.EnableSourceGenReflectionFallback"
        /// switch is enabled; otherwise, <see langword="false"/>.
        /// </value>
        public static bool IsSourceGenReflectionFallbackEnabled { get; } =
            AppContext.TryGetSwitch(
                switchName: "Corvus.Text.Json.Serialization.EnableSourceGenReflectionFallback",
                isEnabled: out bool value)
            ? value : false;

        /// <summary>
        /// Gets a value indicating whether nullable annotations should be respected by default.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the "Corvus.Text.Json.Serialization.RespectNullableAnnotationsDefault"
        /// switch is enabled; otherwise, <see langword="false"/>.
        /// </value>
        public static bool RespectNullableAnnotationsDefault { get; } =
            AppContext.TryGetSwitch(
                switchName: "Corvus.Text.Json.Serialization.RespectNullableAnnotationsDefault",
                isEnabled: out bool value)
            ? value : false;

        /// <summary>
        /// Gets a value indicating whether required constructor parameters should be respected by default.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the "Corvus.Text.Json.Serialization.RespectRequiredConstructorParametersDefault"
        /// switch is enabled; otherwise, <see langword="false"/>.
        /// </value>
        public static bool RespectRequiredConstructorParametersDefault { get; } =
            AppContext.TryGetSwitch(
                switchName: "Corvus.Text.Json.Serialization.RespectRequiredConstructorParametersDefault",
                isEnabled: out bool value)
            ? value : false;
    }
}