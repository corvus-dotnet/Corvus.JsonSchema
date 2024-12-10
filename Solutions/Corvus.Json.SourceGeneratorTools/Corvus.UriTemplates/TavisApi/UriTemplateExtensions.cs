// <copyright file="UriTemplateExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from Tavis.UriTemplate https://github.com/tavis-software/Tavis.UriTemplates/blob/master/License.txt
// </licensing>

using System.Reflection;

namespace Corvus.UriTemplates.TavisApi;

/// <summary>
/// Extension methods for URI templates.
/// </summary>
public static class UriTemplateExtensions
{
    /// <summary>
    /// Create a matching regular expression for the uri template.
    /// </summary>
    /// <param name="uriTemplate">The uri template for which to get the regular expression.</param>
    /// <returns>The matching regular expression.</returns>
    public static string CreateMatchingRegex(string uriTemplate)
    {
        return UriTemplateRegexBuilder.CreateMatchingRegex(uriTemplate);
    }

    /// <summary>
    /// Create a matching regular expression for the uri template.
    /// </summary>
    /// <param name="uriTemplate">The uri template for which to get the regular expression.</param>
    /// <returns>The matching regular expression.</returns>
    public static string CreateMatchingRegex2(string uriTemplate)
    {
        return UriTemplateRegexBuilder.CreateMatchingRegex(uriTemplate);
    }

    /// <summary>
    /// Add a parameter to a UriTemplate.
    /// </summary>
    /// <param name="template">The template to which to add the parameter.</param>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>The updated UriTemplate.</returns>
    public static UriTemplate AddParameter(this UriTemplate template, string name, object value)
    {
        template.SetParameter(name, value);

        return template;
    }

    /// <summary>
    /// Adds parameters to a UriTemplate.
    /// </summary>
    /// <param name="template">The template to which to add the parameter.</param>
    /// <param name="parametersObject">The object whose public instance properties represent the parameters to add.</param>
    /// <returns>The updated UriTemplate.</returns>
    public static UriTemplate AddParameters(this UriTemplate template, object parametersObject)
    {
        if (parametersObject != null)
        {
            IEnumerable<PropertyInfo> properties = parametersObject.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo propinfo in properties)
            {
                template.SetParameter(propinfo.Name, propinfo.GetValue(parametersObject, null));
            }
        }

        return template;
    }

    /// <summary>
    /// Adds parameters to a UriTemplate.
    /// </summary>
    /// <param name="uriTemplate">The template to which to add the parameter.</param>
    /// <param name="linkParameters">The dictionary whose key value pairs represent the parameters to add.</param>
    /// <returns>The updated UriTemplate.</returns>
    public static UriTemplate AddParameters(this UriTemplate uriTemplate, IDictionary<string, object?> linkParameters)
    {
        if (linkParameters != null)
        {
            foreach (KeyValuePair<string, object?> parameter in linkParameters)
            {
                uriTemplate.SetParameter(parameter.Key, parameter.Value);
            }
        }

        return uriTemplate;
    }
}