using System.Text;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.TypeScript.CodeGeneration;

namespace TsProviderSpike;

// EXTENSION DEMO (lives in the spike, NOT the production provider): a consumer-supplied handler for a
// capability the base set omits, registered via RegisterValidationHandlers. Proves the extensibility
// seam works across assemblies (it implements the public ITsKeywordEmitter).
internal sealed class TsFormatExtensionHandler : IKeywordValidationHandler, ITsKeywordEmitter
{
    public uint ValidationHandlerPriority => 500;

    public bool HandlesKeyword(IKeyword keyword) => keyword is IFormatProviderKeyword;

    public void Emit(StringBuilder sb, TypeDeclaration td, IKeyword keyword)
    {
        if (((IFormatProviderKeyword)keyword).TryGetFormat(td, out string? format) && format == "email")
        {
            sb.Append("  if (typeof value === \"string\" && !value.includes(\"@\")) { return false; } // EXTENSION: format=email\n");
        }
    }
}
