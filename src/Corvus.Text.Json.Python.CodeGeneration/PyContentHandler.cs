// <copyright file="PyContentHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// content assertion: contentEncoding (base64) + contentMediaType (application/json), validated on a string
// via the runtime. Annotation-only unless format/content assertion is requested.
internal sealed class PyContentHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 560;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IContentMediaTypeValidationKeyword or IContentEncodingValidationKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        string? media = td.ExplicitContentMediaType();
        string? enc = td.ExplicitContentEncoding();
        if (media is null && enc is null)
        {
            return;
        }

        string cond = "isinstance(value, str) and not " + mod.Rt("fmt_content")
            + "(" + (enc is null ? "None" : PyEmit.Str(enc)) + ", " + (media is null ? "None" : PyEmit.Str(media)) + ", value)";
        PyEmit.Check(mod, td, keyword.Keyword, cond, 4);
    }
}