// <copyright file="PyRegexHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Python.CodeGeneration;

// pattern: a string value must match the ECMA-262 regular expression, transpiled to a Python `regex`-module
// pattern at build time (EcmaRegexPythonTranslator), the peer of the .NET engine's build-time transpilation.
internal sealed class PyRegexHandler : IKeywordValidationHandler, IPyKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IStringRegexValidationProviderKeyword;

    public void Emit(PyModule mod, TypeDeclaration td, IKeyword keyword)
    {
        if (((IValidationRegexProviderKeyword)keyword).TryGetValidationRegularExpressions(td, out IReadOnlyList<string>? regexes))
        {
            foreach (string regex in regexes)
            {
                string py = EcmaRegexPythonTranslator.Translate(regex);
                PyEmit.Check(mod, td, keyword.Keyword, "isinstance(value, str) and not " + mod.Rt("re_test") + "(" + PyEmit.Str(py) + ", value)", 4);
            }
        }
    }
}