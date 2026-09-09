// <copyright file="SkipLocalsInitAttribute.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET5_0_OR_GREATER

namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill for <c>System.Runtime.CompilerServices.SkipLocalsInitAttribute</c> on netstandard.
/// </summary>
[AttributeUsage(AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event, Inherited = false)]
internal sealed class SkipLocalsInitAttribute : Attribute
{
}

#endif
