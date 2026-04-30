// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable RCS1251 // Remove unnecessary else - polyfill file uses #if/#else pattern

using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Reserved to be used by the compiler for tracking metadata.
/// This class should not be used by developers in source code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
#if SYSTEM_PRIVATE_CORELIB
public
#else
internal
#endif
static class IsExternalInit
{
}