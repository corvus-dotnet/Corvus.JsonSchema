// <copyright file="JsonSchemaWalker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.JsonSchema.TypeBuilder.Draft202012
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Corvus.Json;
    using Corvus.Json.JsonSchema.Draft202012;

    /// <summary>
    /// A walker for <see cref="Schema"/>.
    /// </summary>
    internal class JsonSchemaWalker
    {
        /// <summary>
        /// The content type of Schema elements.
        /// </summary>
        public const string SchemaContent = "application/vnd.Corvus.jsonschemawalker.draft201909schemacontent";

        private readonly Dictionary<string, LocatedElement> anchoredSchema = new ();
        private readonly Dictionary<string, LocatedElement> dynamicAnchoredSchema = new ();
        private readonly Stack<LocatedElement> currentSchema = new ();

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
                var draft201909Schema = new Schema(referencedElement.Element);
                if (draft201909Schema.Validate(ValidationContext.ValidContext).IsValid)
                {
                    return draft201909Schema;
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
            string decodedRef = reference.AsDecodedString();

            if (isDynamicReference)
            {
                if (this.dynamicAnchoredSchema.TryGetValue(decodedRef, out LocatedElement? dynamicValue))
                {
                    EnsureSchemaContent(dynamicValue);
                    return this.ResolvePotentiallyRecursiveAnchor(walker, dynamicValue, reference.Fragment.ToString(), isDynamicReference);
                }
                else
                {
                    if (this.currentSchema.Count > 1)
                    {
                        if (this.HasUnknownAnchors(this.currentSchema.Skip(1).Take(1).Single().Element))
                        {
                            // We defer location to later if we have local anchors, but we have not
                            // resolved via an anchor
                            return new LocatedElement(walker.PeekLocationStack(), default, SchemaContent);
                        }
                    }
                }
            }

            if (this.anchoredSchema.TryGetValue(decodedRef, out LocatedElement? value))
            {
                EnsureSchemaContent(value);

                // Don't do recursive resolution if this wasn't resolved to a dynamic anchor...
                return this.ResolvePotentiallyRecursiveAnchor(walker, value, reference.Fragment.ToString(), false);
            }

            if (this.currentSchema.Count > 1)
            {
                if (this.HasUnknownAnchors(this.currentSchema.Skip(1).Take(1).Single().Element))
                {
                    // We defer location to later if we have local anchors, but we have not
                    // resolved via an anchor
                    return new LocatedElement(walker.PeekLocationStack(), default, SchemaContent);
                }
            }

            LocatedElement? resolvedElement = await resolve().ConfigureAwait(false);
            return this.ResolvePotentiallyRecursiveAnchor(walker, resolvedElement, reference.Fragment.ToString(), false);
        }

        private bool HasUnknownAnchors(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.NameEquals("$anchor") || property.NameEquals("$dynamicAnchor"))
                    {
                        if (property.Value.ValueKind == JsonValueKind.String && !this.anchoredSchema.ContainsKey(property.Value.GetString() !))
                        {
                            return true;
                        }
                    }
                    else if (property.NameEquals("enum") || property.NameEquals("const"))
                    {
                        continue;
                    }
                    else if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array)
                    {
                        if (this.HasUnknownAnchors(property.Value))
                        {
                            return true;
                        }
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in element.EnumerateArray())
                {
                    if (this.HasUnknownAnchors(child))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private LocatedElement? ResolvePotentiallyRecursiveAnchor(JsonWalker walker, LocatedElement? locatedElement, string reference, bool isRecursiveReference)
        {
            if (locatedElement is null)
            {
                return default;
            }

            if (!isRecursiveReference)
            {
                return locatedElement;
            }

            // We can't resolve recursive anchors on non-schema content.
            if (locatedElement.ContentType != SchemaContent)
            {
                return locatedElement;
            }

            LocatedElement? lastRecursiveAnchor = null;

            // Enumerate the recursive anchors up the stack, skipping the start location
            foreach (LocatedElement item in walker.EnumerateLocationStack(1))
            {
                if (this.dynamicAnchoredSchema.TryGetValue(new JsonReference(item.AbsoluteLocation).Apply(new JsonReference(reference)), out LocatedElement? dynamiclocatedValue))
                {
                    lastRecursiveAnchor = dynamiclocatedValue;
                }
                else if (lastRecursiveAnchor is null && this.anchoredSchema.TryGetValue(new JsonReference(item.AbsoluteLocation).Apply(new JsonReference(reference)), out LocatedElement? locatedValue))
                {
                    lastRecursiveAnchor = locatedValue;
                }
            }

            return lastRecursiveAnchor ?? locatedElement;
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

            this.currentSchema.Push(walker.GetLocatedElement(walker.PeekLocationStack()));

            try
            {
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

                        // Replace the current dynamic schema
                        this.currentSchema.Pop();
                        this.currentSchema.Push(walker.GetLocatedElement(walker.PeekLocationStack()));
                    }
                }

                if (schema.Anchor.IsNotUndefined())
                {
                    this.anchoredSchema.Add(new JsonReference(walker.PeekLocationStack()).Apply(new JsonReference("#" + schema.Anchor)), walker.CurrentElement);
                }

                if (schema.DynamicAnchor.IsNotNullOrUndefined())
                {
                    this.anchoredSchema.Add(new JsonReference(walker.PeekLocationStack()).Apply(new JsonReference("#" + schema.DynamicAnchor)), walker.CurrentElement);
                    this.dynamicAnchoredSchema.Add(new JsonReference(walker.PeekLocationStack()).Apply(new JsonReference("#" + schema.DynamicAnchor)), walker.CurrentElement);
                }

                if (schema.ValueKind == JsonValueKind.Object)
                {
                    // This is an object schema, not a boolean schema
                    // so we will walk all our properties, giving every handler the chance to deal with it.
                    await walker.WalkContentsOfObjectOrArray(schema.AsJsonElement).ConfigureAwait(false);
                }

                if (schema.DynamicRef.IsNotNullOrUndefined())
                {
                    walker.PushPropertyToLocationStack("$dynamicRef");
                    LocatedElement? referencedElement = await walker.ResolveReference(new JsonReference(schema.DynamicRef.GetUri().OriginalString), isRecursiveReference: false, isDynamicReference: true).ConfigureAwait(false);
                    if (referencedElement is LocatedElement re)
                    {
                        walker.AddOrUpdateLocatedElement(re);
                        EnsureSchemaContent(re);
                    }
                    else
                    {
                        // We are not yet able to resolve the reference, so push this onto the "unhandled elements list)
                        walker.AddUnresolvedReference(schema.DynamicRef.GetUri().OriginalString, false, true, (w, e) =>
                        {
                            w.AddOrUpdateLocatedElement(e);
                            EnsureSchemaContent(e);
                        });
                    }

                    walker.PopLocationStack();
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

                if (schema.RecursiveRef.IsNotNullOrUndefined())
                {
                    walker.PushPropertyToLocationStack("$recursiveRef");

                    LocatedElement? referencedElement = await walker.ResolveReference(new JsonReference(schema.RecursiveRef.GetUri().OriginalString), isRecursiveReference: true, isDynamicReference: false).ConfigureAwait(false);
                    if (referencedElement is LocatedElement re)
                    {
                        walker.AddOrUpdateLocatedElement(re);
                        EnsureSchemaContent(re);
                    }
                    else
                    {
                        // We are not yet able to resolve the reference, so push this onto the "unhandled elements list)
                        walker.AddUnresolvedReference(schema.RecursiveRef.GetUri().OriginalString, true, false, (w, e) =>
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
            finally
            {
                this.currentSchema.Pop();
            }
        }
    }
}