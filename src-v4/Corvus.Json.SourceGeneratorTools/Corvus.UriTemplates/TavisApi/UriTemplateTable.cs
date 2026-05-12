// <copyright file="UriTemplateTable.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.UriTemplates.TavisApi;

/// <summary>
/// A Uri Template table.
/// </summary>
public class UriTemplateTable
{
    private readonly Dictionary<string, UriTemplate> templates = new();

    /// <summary>
    /// Get the URI template with the specified key.
    /// </summary>
    /// <param name="key">The key for which to retrieve the URI template.</param>
    /// <returns>The URI template with the given key, or null if no template is found.</returns>
    public UriTemplate? this[string key]
    {
        get
        {
            if (this.templates.TryGetValue(key, out UriTemplate? value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Adds a URI template to the table.
    /// </summary>
    /// <param name="key">The key in the table.</param>
    /// <param name="template">The template to add.</param>
    public void Add(string key, UriTemplate template)
    {
        this.templates.Add(key, template);
    }

    /// <summary>
    /// Match a URL to the table.
    /// </summary>
    /// <param name="url">The URL to match.</param>
    /// <param name="order">The (option) query string parameter ordering constraint.</param>
    /// <returns>The matched template, or null if no template was matched.</returns>
    public TemplateMatch? Match(Uri url, QueryStringParameterOrder order = QueryStringParameterOrder.Strict)
    {
        foreach (KeyValuePair<string, UriTemplate> template in this.templates)
        {
            if (template.Value.IsMatch(url, order))
            {
                IDictionary<string, object>? parameters = template.Value.GetParameters(url, order);
                if (parameters != null)
                {
                    return new TemplateMatch() { Key = template.Key, Parameters = parameters, Template = template.Value };
                }
            }
        }

        return null;
    }
}