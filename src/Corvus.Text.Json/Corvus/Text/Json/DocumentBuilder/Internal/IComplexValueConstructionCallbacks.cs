// <copyright file="IComplexValueConstructionCallbacks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Optional callbacks a construction target document can implement to observe the structural
/// events a <see cref="ComplexValueBuilder"/> does not otherwise surface through the
/// <see cref="IMutableJsonDocument"/> store operations.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ComplexValueBuilder.Create(IMutableJsonDocument, int)"/> captures the target as
/// this interface once, at creation; a target that does not implement it (such as
/// <see cref="JsonDocumentBuilder{T}"/>) pays only a null test at each container boundary.
/// Together with the store operations — which a text-backed target can classify as property
/// names or values by tracking JSON's strict name/value alternation within an object — these
/// callbacks give the target the complete token stream in document order, allowing it to write
/// the final document text directly during construction.
/// </para>
/// </remarks>
internal interface IComplexValueConstructionCallbacks
{
    /// <summary>
    /// Called immediately before the builder appends a <see cref="JsonTokenType.StartObject"/>
    /// or <see cref="JsonTokenType.StartArray"/> row.
    /// </summary>
    /// <param name="tokenType">The container start token type.</param>
    void OnStartComplex(JsonTokenType tokenType);

    /// <summary>
    /// Called immediately before the builder appends a <see cref="JsonTokenType.EndObject"/>
    /// or <see cref="JsonTokenType.EndArray"/> row.
    /// </summary>
    /// <param name="tokenType">The container end token type.</param>
    void OnEndComplex(JsonTokenType tokenType);

    /// <summary>
    /// Appends an element from another document as the next value, in place of the external
    /// reference rows <see cref="IJsonDocument.AppendElementToMetadataDb"/> would produce.
    /// The target captures the element's content immediately and appends fully-resolved local
    /// rows to <paramref name="db"/>.
    /// </summary>
    /// <param name="sourceDocument">The document containing the element.</param>
    /// <param name="sourceIndex">The index of the element in the source document.</param>
    /// <param name="db">The metadata database under construction.</param>
    void AppendExternalElement(IJsonDocument sourceDocument, int sourceIndex, ref MetadataDb db);

    /// <summary>
    /// Called immediately after the builder removes rows from the metadata database
    /// (for example via <see cref="ComplexValueBuilder.RemoveProperty(System.ReadOnlySpan{byte}, bool, bool)"/>).
    /// </summary>
    /// <param name="rowByteIndex">The byte index of the first removed row.</param>
    /// <param name="rowCount">The number of rows removed.</param>
    void OnRowsRemoved(int rowByteIndex, int rowCount);
}