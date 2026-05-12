// <copyright file="IWorkspaceManagedDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Marker interface for documents whose lifecycle is managed by a <see cref="JsonWorkspace"/>.
/// When a workspace is disposed or reset, all registered documents implementing this interface
/// are disposed, returning pooled resources.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by <see cref="IMutableJsonDocument"/> (builders) and by pooled value documents
/// such as <c>FixedJsonValueDocument</c>. Not implemented by caller-owned immutable documents
/// like <c>ParsedJsonDocument</c>.
/// </para>
/// </remarks>
[CLSCompliant(false)]
public interface IWorkspaceManagedDocument : IJsonDocument;