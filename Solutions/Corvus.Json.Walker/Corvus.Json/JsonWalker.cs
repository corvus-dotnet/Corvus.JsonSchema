// <copyright file="JsonWalker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Walks a JsonElement to identify and locate entities of interest.
/// </summary>
public class JsonWalker
{
    /// <summary>
    /// Gets the default content type.
    /// </summary>
    public const string DefaultContent = "application/vnd.Corvus.element-default";

    private readonly List<Func<JsonWalker, JsonElement, Task<bool>>> handlers = new();
    private readonly List<Func<JsonWalker, JsonReference, bool, bool, Func<Task<LocatedElement?>>, Task<LocatedElement?>>> resolvers = new();

    private readonly Dictionary<string, LocatedElement> locatedElements = new();

    private readonly IDocumentResolver documentResolver;
    private readonly string baseLocation;

    private readonly List<(string, bool, bool, List<string>, Action<JsonWalker, LocatedElement>)> unresolvedReferences = new();

    private Stack<string> scopedLocationStack = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWalker"/> class.
    /// </summary>
    /// <param name="documentResolver">The document resolver to use.</param>
    public JsonWalker(IDocumentResolver documentResolver)
    {
        this.handlers.Add(DefaultHandler);
        this.resolvers.Add(DefaultResolver);
        this.documentResolver = documentResolver;
        this.baseLocation = "#";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWalker"/> class.
    /// </summary>
    /// <param name="documentResolver">The document resolver to use.</param>
    /// <param name="baseLocation">The initial base location.</param>
    public JsonWalker(IDocumentResolver documentResolver, string baseLocation)
    {
        this.handlers.Add(DefaultHandler);
        this.resolvers.Add(DefaultResolver);
        this.documentResolver = documentResolver;
        this.baseLocation = baseLocation;
    }

    /// <summary>
    /// Gets the element at the current scoped location.
    /// </summary>
    public LocatedElement CurrentElement
    {
        get
        {
            return this.locatedElements[this.PeekLocationStack()];
        }
    }

    /// <summary>
    /// Enumerate the located elements.
    /// </summary>
    /// <returns>An enumerable of the located elements.</returns>
    public IEnumerable<LocatedElement> EnumerateLocatedElements()
    {
        foreach (KeyValuePair<string, LocatedElement> element in this.locatedElements)
        {
            yield return element.Value;
        }
    }

    /// <summary>
    /// Rebases a reference to a "root" document.
    /// </summary>
    /// <param name="reference">The reference to rebase as a root document.</param>
    /// <returns>A <see cref="Task{TResult}"/> which, when complete, provides the artificial reference of the root document.</returns>
    public async Task<string> RebaseReferenceAsRootDocument(string reference)
    {
        var jref = new JsonReference(reference);
        if (!jref.HasFragment)
        {
            // If this really is a root document, then just return the reference
            return reference;
        }

        JsonElement? element = await this.documentResolver.TryResolve(jref);
        if (element is JsonElement e)
        {
            string uri = $"http://endjin.com/jsonwalker/{Guid.NewGuid()}/Schema";
            this.documentResolver.AddDocument(uri, JsonDocument.Parse(e.GetRawText()));
            return uri;
        }

        throw new ArgumentException($"Unable to find the element at location '{reference}'", nameof(reference));
    }

    /// <summary>
    /// Resolves element.
    /// </summary>
    /// <param name="reference">The document reference for which to provide the element.</param>
    /// <returns>A <see cref="Task{T}"/> which, when complete, provides the root element of the document.</returns>
    public async Task<JsonElement?> GetDocumentElement(string reference)
    {
        return await this.documentResolver.TryResolve(new JsonReference(reference).WithFragment(string.Empty)).ConfigureAwait(false);
    }

    /// <summary>
    /// Rebases a reference to a canonical URI contained in a given property..
    /// </summary>
    /// <param name="reference">The reference to rebase as a root document.</param>
    /// <param name="propertyName">The name of the property containing the canonical URI at which to rebase the document.</param>
    /// <returns>A <see cref="Task{TResult}"/> which, when complete, provides the updated reference of the root document.</returns>
    public async Task<string> TryRebaseDocumentToPropertyValue(string reference, string propertyName)
    {
        var jref = new JsonReference(reference);

        JsonElement? element = await this.documentResolver.TryResolve(jref);
        if (element is JsonElement e && e.ValueKind == JsonValueKind.Object)
        {
            if (e.TryGetProperty(propertyName, out JsonElement uri) && uri.ValueKind == JsonValueKind.String)
            {
                string outputUri = uri.GetString()!;
                var outputUriRef = new JsonReference(outputUri);
                if (!outputUriRef.HasFragment)
                {
                    this.documentResolver.AddDocument(outputUri, JsonDocument.Parse(e.GetRawText()));
                    return outputUri;
                }
            }
        }

        return reference;
    }

    /// <summary>
    /// Walks the contents of an object or array.
    /// </summary>
    /// <param name="element">The content to walk.</param>
    /// <returns>A <see cref="Task"/> which completes once the walk is complete.</returns>
    /// <remarks>This is used by handlers to walk into their object properties or array members.</remarks>
    public async Task WalkContentsOfObjectOrArray(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
        {
            // Silently ignore nulls and undefineds.
            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            JsonElement.ObjectEnumerator enumerator = element.EnumerateObject();

            while (enumerator.MoveNext())
            {
                this.PushPropertyToLocationStack(enumerator.Current.Name);
                await this.WalkElement(enumerator.Current.Value).ConfigureAwait(false);
                this.PopLocationStack();
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            JsonElement.ArrayEnumerator enumerator = element.EnumerateArray();

            int index = 0;
            while (enumerator.MoveNext())
            {
                this.PushArrayIndexToLocationStack(index);
                await this.WalkElement(enumerator.Current).ConfigureAwait(false);
                this.PopLocationStack();
                ++index;
            }
        }
        else
        {
            throw new ArgumentException("The element must be an object or an array", nameof(element));
        }
    }

    /// <summary>
    /// Add or update a located element at a specific location.
    /// </summary>
    /// <param name="location">The location at which to add or upate the element.</param>
    /// <param name="locatedElement">The element to add or update.</param>
    public void AddOrUpdateLocatedElement(string location, LocatedElement locatedElement)
    {
        if (!this.locatedElements.ContainsKey(location))
        {
            this.locatedElements.Add(location, locatedElement);
        }
        else
        {
            this.locatedElements.Remove(location);
            this.locatedElements.Add(location, locatedElement);
        }
    }

    /// <summary>
    /// Gets a previously located element.
    /// </summary>
    /// <param name="location">The location for which to find the element.</param>
    /// <returns>The located element.</returns>
    public LocatedElement GetLocatedElement(string location)
    {
        if (this.locatedElements.TryGetValue(location, out LocatedElement? value))
        {
            return value;
        }

        throw new ArgumentException($"Unable to find the element at location '{location}'", nameof(location));
    }

    /// <summary>
    /// Gets a previously located element.
    /// </summary>
    /// <param name="location">The location for which to find the element.</param>
    /// <param name="locatedElement">The element that was found.</param>
    /// <returns>True if the element was located.</returns>
    public bool TryGetLocatedElement(string location, [NotNullWhen(true)] out LocatedElement? locatedElement)
    {
        return this.locatedElements.TryGetValue(location, out locatedElement);
    }

    /// <summary>
    /// Register a handler with the schema walker.
    /// </summary>
    /// <param name="handler">The handler to register.</param>
    /// <remarks>
    /// This function will be called to attempt to handle the element at the
    /// given location. It will be provided the <see cref="JsonWalker"/> and the
    /// <see cref="JsonElement"/> being handled. It should return a <see cref="Task{TResult}"/>
    /// with a <see cref="bool"/> that indicates whether we handled the element.
    /// </remarks>
    public void RegisterHandler(Func<JsonWalker, JsonElement, Task<bool>> handler)
    {
        this.handlers.Insert(this.handlers.Count - 1, handler);
    }

    /// <summary>
    /// Adds an enresolved reference to the list for later resolution.
    /// </summary>
    /// <param name="reference">The reference to resolve.</param>
    /// <param name="isRecursiveReference">A value indicating whether this is a recursive reference.</param>
    /// <param name="isDynamicReference">A value indicating whether this is a dynamic reference.</param>
    /// <param name="postResolutionAction">The action to perform once the item has been resolved.</param>
    public void AddUnresolvedReference(string reference, bool isRecursiveReference, bool isDynamicReference, Action<JsonWalker, LocatedElement> postResolutionAction)
    {
        this.unresolvedReferences.Add((reference, isRecursiveReference, isDynamicReference, this.scopedLocationStack.ToList(), postResolutionAction));
    }

    /// <summary>
    /// Resolves unresolved references.
    /// </summary>
    /// <returns>A <see cref="Task"/> which completes when the references are resolved.</returns>
    public async Task ResolveUnresolvedReferences()
    {
        int iterationCount = 0;
        while (this.unresolvedReferences.Count > 0 && iterationCount < 10)
        {
            iterationCount++;
            var currentList = this.unresolvedReferences.ToList();
            this.unresolvedReferences.Clear();
            foreach ((string reference, bool isRecursiveReference, bool isDynamicReference, List<string> stackLocation, Action<JsonWalker, LocatedElement> postResolutionAction) in currentList)
            {
                if (this.locatedElements.ContainsKey(reference))
                {
                    LocatedElement locatedElement1 = this.locatedElements[reference];
                    if (stackLocation[0] != reference)
                    {
                        stackLocation.Reverse();
                        this.scopedLocationStack = new Stack<string>(stackLocation);
                        postResolutionAction(this, locatedElement1);

                        this.PopLocationStack();
                    }

                    continue;
                }

                stackLocation.Reverse();
                this.scopedLocationStack = new Stack<string>(stackLocation);
                LocatedElement? locatedElement = await this.ResolveReference(new JsonReference(reference), isRecursiveReference, isDynamicReference);
                if (locatedElement is not null)
                {
                    postResolutionAction(this, locatedElement);
                }

                this.PopLocationStack();
            }
        }
    }

    /// <summary>
    /// Registers a resolver with the schema walker.
    /// </summary>
    /// <param name="resolver">The resovler function to resolve a reference.</param>
    /// <remarks>
    /// This function will be provided with the <see cref="JsonWalker"/>, the <see cref="JsonReference"/> to resolve,
    /// and a function to call if you wish to delegate this resolution on to other handlers in the chain, and then work
    /// on that element that has been returned. It returns a <see cref="Task{TResult}"/> which, when complete, provides
    /// the <see cref="LocatedElement"/> or <c>null</c> if the element could not be located.
    /// </remarks>
    public void RegisterResolver(Func<JsonWalker, JsonReference, bool, bool, Func<Task<LocatedElement?>>, Task<LocatedElement?>> resolver)
    {
        this.resolvers.Insert(this.resolvers.Count - 1, resolver);
    }

    /// <summary>
    /// Enumerate the located elements in the location stack.
    /// </summary>
    /// <param name="skip">The number of items to skip of the top of the stack before starting enumeration.</param>
    /// <returns>An enumerable of elements in the located element stack.</returns>
    public IEnumerable<LocatedElement> EnumerateLocationStack(int skip = 0)
    {
        foreach (string location in this.scopedLocationStack.Skip(skip))
        {
            if (this.locatedElements.TryGetValue(location, out LocatedElement? value))
            {
                yield return value;
            }
            else
            {
                throw new InvalidOperationException($"Unable to find the element for the location '{location}'.");
            }
        }
    }

    /// <summary>
    /// Adds or updates a located element in the cache.
    /// </summary>
    /// <param name="element">The element we have located.</param>
    /// <param name="contentType">The content type to associate with the element.</param>
    /// <remarks>This is used by handlers to add an element they have located to the location cache.</remarks>
    /// <returns><c>True</c> if this added or updated the element, otherwise false.</returns>
    public bool AddOrUpdateLocatedElement(JsonElement element, string contentType)
    {
        string location = this.PeekLocationStack();
        if (this.locatedElements.TryGetValue(location, out LocatedElement? value))
        {
            return value.Update(element, contentType);
        }
        else
        {
            this.locatedElements.Add(location, new LocatedElement(location, element, contentType));
            return true;
        }
    }

    /// <summary>
    /// Adds or updates a located element in the cache.
    /// </summary>
    /// <param name="locatedElement">The previously located element which is now being referenced at this location.</param>
    /// <remarks>This is used by handlers to add an element they have previously located to a new place in the location cache.</remarks>
    public void AddOrUpdateLocatedElement(LocatedElement locatedElement)
    {
        string location = this.PeekLocationStack();
        this.AddOrUpdateLocatedElement(location, locatedElement);
    }

    /// <summary>
    /// Walks the schema and identifies referenced elements.
    /// </summary>
    /// <param name="element">The element to walk.</param>
    /// <returns>A <see cref="Task"/> which completes once the elements have been walked.</returns>
    public async Task WalkElement(JsonElement element)
    {
        foreach (Func<JsonWalker, JsonElement, Task<bool>>? handler in this.handlers)
        {
            if (await handler(this, element).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Resolve the reference at the given uri-reference.
    /// </summary>
    /// <param name="reference">The reference to resolve.</param>
    /// <param name="isRecursiveReference">True if the reference is recursive.</param>
    /// <param name="isDynamicReference">True if the reference is dynamic.</param>
    /// <returns>A <see cref="Task"/> which completes when the reference is resolved, providing the location of the resolved element.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0008:Use explicit type", Justification = "GetEnumerator() produces a long and complicated enumerator type that obscures the intent.")]
    public async Task<LocatedElement?> ResolveReference(JsonReference reference, bool isRecursiveReference, bool isDynamicReference)
    {
        // The current location is the current place in the stack, with the reference applied.
        var currentLocation = new JsonReference(this.PeekLocationStack()).Apply(reference);

        var enumerator = this.resolvers.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var result = await enumerator.Current(this, currentLocation, isRecursiveReference, isDynamicReference, () => RecursivelyResolve(enumerator, this, currentLocation, isRecursiveReference, isDynamicReference)).ConfigureAwait(false);
            if (result != null)
            {
                // This mechanism allows us to say "defer resolution and don't delegate to further handlers.
                if (result.Element.ValueKind != JsonValueKind.Undefined)
                {
                    return result;
                }

                return default;
            }
        }

        return default;
    }

    /// <summary>
    /// Peek the top item off the location stack.
    /// </summary>
    /// <returns>The top item off the location stack, or the base location.</returns>
    public string PeekLocationStack()
    {
        return this.scopedLocationStack.Count > 0 ? this.scopedLocationStack.Peek() : this.baseLocation;
    }

    /// <summary>
    /// Pop the current location stack.
    /// </summary>
    public void PopLocationStack()
    {
        this.scopedLocationStack.Pop();
    }

    /// <summary>
    /// Push the given (unencoded) property name to the location stack.
    /// </summary>
    /// <param name="name">The unencoded property name to push to the stack.</param>
    public void PushPropertyToLocationStack(string name)
    {
        var currentLocation = new JsonReference(this.PeekLocationStack());
        this.scopedLocationStack.Push(currentLocation.AppendUnencodedPropertyNameToFragment(name));
    }

    /// <summary>
    /// Push the given array index to the location stack.
    /// </summary>
    /// <param name="index">The array index to push to the stack.</param>
    public void PushArrayIndexToLocationStack(int index)
    {
        var currentLocation = new JsonReference(this.PeekLocationStack());
        this.scopedLocationStack.Push(currentLocation.AppendArrayIndexToFragment(index));
    }

    /// <summary>
    /// Push the given scope change to the location stack.
    /// </summary>
    /// <param name="scope">The scope change to push to the stack.</param>
    public void PushScopeToLocationStack(string scope)
    {
        var currentLocation = new JsonReference(this.PeekLocationStack());
        this.scopedLocationStack.Push(currentLocation.Apply(new JsonReference(scope)));
    }

    private static Task<LocatedElement?> RecursivelyResolve(List<Func<JsonWalker, JsonReference, bool, bool, Func<Task<LocatedElement?>>, Task<LocatedElement?>>>.Enumerator enumerator, JsonWalker walker, JsonReference reference, bool isRecursiveReference, bool isDynamicReference)
    {
        if (enumerator.MoveNext())
        {
            return enumerator.Current(walker, reference, isRecursiveReference, isDynamicReference, () => RecursivelyResolve(enumerator, walker, reference, isRecursiveReference, isDynamicReference));
        }

        return Task.FromResult<LocatedElement?>(default);
    }

    private static async Task<LocatedElement?> DefaultResolver(JsonWalker walker, JsonReference reference, bool isRecursiveReference, bool isDynamicReference, Func<Task<LocatedElement?>> resolve)
    {
        if (walker.locatedElements.TryGetValue(reference.AsDecodedString(), out LocatedElement? element))
        {
            return element;
        }

        JsonElement? jsonElement = await walker.documentResolver.TryResolve(reference).ConfigureAwait(false);
        if (jsonElement is JsonElement resolvedElement)
        {
            walker.PushScopeToLocationStack(reference);
            walker.AddOrUpdateLocatedElement(resolvedElement, DefaultContent);
            await walker.WalkElement(resolvedElement).ConfigureAwait(false);
            walker.PopLocationStack();
            if (walker.locatedElements.TryGetValue(reference, out LocatedElement? resolvedAndLocatedElement))
            {
                return resolvedAndLocatedElement;
            }
        }

        return null;
    }

    /// <summary>
    /// The default handler iterates the elements in the document and adds them to the map.
    /// </summary>
    private static async Task<bool> DefaultHandler(JsonWalker walker, JsonElement element)
    {
        if (!walker.TryAddLocatedElement(element, DefaultContent))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object || element.ValueKind == JsonValueKind.Array)
        {
            await walker.WalkContentsOfObjectOrArray(element).ConfigureAwait(false);
        }

        return true;
    }

    private bool TryAddLocatedElement(JsonElement element, string contentType)
    {
        string location = this.PeekLocationStack();
        if (!this.locatedElements.ContainsKey(location))
        {
            this.locatedElements.Add(location, new LocatedElement(location, element, contentType));
            return true;
        }

        return false;
    }
}