// <copyright file="CorvusOutput.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET8_0_OR_GREATER
/// <summary>
/// Wrapper structure for a string and a length.
/// </summary>
/// <param name="Destination">The destination buffer.</param>
/// <param name="Length">The length of the buffer.</param>
public readonly record struct CorvusOutput(char[] Destination, int Length);
#endif