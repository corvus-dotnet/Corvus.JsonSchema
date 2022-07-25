// <copyright file="JsonSchemaWalker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.JsonSchema.Draft7;

namespace Corvus.Json.CodeGeneration.Draft7;

/// <summary>
/// A walker for <see cref="Schema"/>.
/// </summary>
internal class JsonSchemaWalker
{
    /// <summary>
    /// The content type of Schema elements.
    /// </summary>
    public const string SchemaContent = "application/vnd.Corvus.jsonschemawalker.draft7schemacontent";

    private readonly Dictionary<string, LocatedElement> anchoredSchema = new();

    /// <summary>
    /// Register this with the given <see cref="JsonWalker"/>.
    /// </summary>
    /// <param name="walker">The walker with which to register this schema walker.</param>
    public void RegisterWith(JsonWalker walker)
    {
        walker.RegisterHandler(this.HandleElement);
        walker.RegisterResolver(this.ResolveReference);
    }

    private static Schema EnsureSchemaContent(LocatedElement referencedElement)
    {
        if (referencedElement.ContentType == JsonWalker.DefaultContent)
        {
            var draft7Schema = new Schema(referencedElement.Element);
            if (draft7Schema.Validate(ValidationContext.ValidContext).IsValid)
            {
                return draft7Schema;
            }
        }
        else if (referencedElement.ContentType != SchemaContent)
        {
            throw new InvalidOperationException("A reference within SchemaContent must be to SchemaContent.");
        }

        return new Schema(referencedElement.Element);
    }

    private async Task<LocatedElement?> ResolveReference(JsonWalker walker, JsonReference reference, bool isRecursiveReference, bool isDynamicReference, Func<Task<LocatedElement?>> resolve)
    {
        if (this.anchoredSchema.TryGetValue(reference.AsDecodedString(), out LocatedElement? value))
        {
            EnsureSchemaContent(value);
            return value;
        }

        return await resolve().ConfigureAwait(false);
    }

    private async Task<bool> HandleElement(JsonWalker walker, JsonElement element)
    {
        var schema = new Schema(element);
        if (!schema.Validate(ValidationContext.ValidContext).IsValid)
        {
            return false;
        }

        // If it is valid as a schema, but empty apart from unkown extensions, we will not handle it.
        if (schema.EmptyButWithUnknownExtensions())
        {
            return false;
        }

        if (schema.ValueKind != JsonValueKind.True && schema.ValueKind != JsonValueKind.False && schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Don't recurse into Enums or Consts as Schema
        if ((walker.EnumerateLocationStack(1).FirstOrDefault() is LocatedElement parent && parent.AbsoluteLocation.EndsWith("enum")) || walker.PeekLocationStack().EndsWith("const"))
        {
            return false;
        }

        // Tell the walker that this is our content.
        if (!walker.AddOrUpdateLocatedElement(element, SchemaContent))
        {
            return true;
        }

        if (schema.Id.IsNotUndefined())
        {
            string currentLocation = walker.PeekLocationStack();

            // Update the scope with the ID value
            walker.PushScopeToLocationStack(schema.Id);

            // If our ID is not the same as our existing URI...
            if (currentLocation != walker.PeekLocationStack())
            {
                // And add our element at that scope too
                if (!walker.AddOrUpdateLocatedElement(element, SchemaContent))
                {
                    walker.PopLocationStack();
                    return true;
                }

                walker.AddOrUpdateLocatedElement(currentLocation, walker.GetLocatedElement(walker.PeekLocationStack()));
            }
        }

        if (schema.ValueKind == JsonValueKind.Object)
        {
            // This is an object schema, not a boolean schema
            // so we will walk all our properties, giving every handler the chance to deal with it.
            await walker.WalkContentsOfObjectOrArray(schema.AsJsonElement).ConfigureAwait(false);
        }

        if (schema.Ref.IsNotNullOrUndefined())
        {
            walker.PushPropertyToLocationStack("$ref");
            LocatedElement? referencedElement = await walker.ResolveReference(new JsonReference(schema.Ref.GetUri().OriginalString), isRecursiveReference: false, isDynamicReference: false).ConfigureAwait(false);
            if (referencedElement is LocatedElement re)
            {
                walker.AddOrUpdateLocatedElement(re);
                EnsureSchemaContent(re);
            }
            else
            {
                // We are not yet able to resolve the reference, so push this onto the "unhandled elements list)
                walker.AddUnresolvedReference(schema.Ref.GetUri().OriginalString, false, false, (w, e) =>
                {
                    w.AddOrUpdateLocatedElement(e);
                    EnsureSchemaContent(e);
                });
            }

            walker.PopLocationStack();
        }

        if (schema.Id.IsNotUndefined())
        {
            // We pushed our ID onto the stack, so pop it back off again.
            walker.PopLocationStack();
        }

        return true;
    }
}