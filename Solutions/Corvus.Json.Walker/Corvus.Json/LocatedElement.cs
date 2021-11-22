// <copyright file="LocatedElement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System.Text.Json;

    /// <summary>
    /// An element located in the schema at a particular scoped location.
    /// </summary>
    public class LocatedElement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LocatedElement"/> class.
        /// </summary>
        /// <param name="absoluteLocation">The <see cref="AbsoluteLocation"/>.</param>
        /// <param name="element">The <see cref="Element"/> at that location.</param>
        /// <param name="contentType">The content type of the located element.</param>
        public LocatedElement(string absoluteLocation, JsonElement element, string contentType)
        {
            this.AbsoluteLocation = absoluteLocation;
            this.Element = element;
            this.ContentType = contentType;
        }

        /// <summary>
        /// Gets the absolute location of the element.
        /// </summary>
        public string AbsoluteLocation { get; }

        /// <summary>
        /// Gets the JSON element at the location.
        /// </summary>
        public JsonElement Element { get; private set; }

        /// <summary>
        /// Gets the content type of the element.
        /// </summary>
        public string ContentType { get; private set; }

        /// <summary>
        /// Updates the <see cref="ContentType"/> to a new value.
        /// </summary>
        /// <param name="element">The JSON element to update.</param>
        /// <param name="contentType">The new value for the content type.</param>
        /// <returns><c>True</c> if the content type was updated.</returns>
        public bool Update(JsonElement element, string contentType)
        {
            // Only if either the element or content type are changed (based on identical backing text)
            // do we do an update.
            if (this.ContentType != contentType || element.GetRawText() != this.Element.GetRawText())
            {
                this.ContentType = contentType;
                this.Element = element;
                return true;
            }

            return false;
        }
    }
}
