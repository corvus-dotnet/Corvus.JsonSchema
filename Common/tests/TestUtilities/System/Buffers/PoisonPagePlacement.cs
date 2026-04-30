// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
namespace System.Buffers;

/// <summary>
/// Dictates where the poison page should be placed.
/// </summary>
public enum PoisonPagePlacement
{
    /// <summary>
    /// The poison page should be placed immediately after the memory region.
    /// Attempting to access the memory page immediately following the
    /// span will result in an AV.
    /// </summary>
    After,

    /// <summary>
    /// The poison page should be placed immediately before the memory region.
    /// Attempting to access the memory page immediately before the
    /// span will result in an AV.
    /// </summary>
    Before,
}