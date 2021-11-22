// <copyright file="DocumentResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Resolve <see cref="JsonDocument"/> instances from various locations.
    /// </summary>
    public static class DocumentResolver
    {
        /// <summary>
        /// The default document resolver.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This provides a resolver that tries the local filesystem rooted at <see cref="Environment.CurrentDirectory"/>, then
        /// the <see cref="HttpClient"/>.
        /// </para>
        /// <para>
        /// Typically, in production you would configure a container with the document resolver
        /// and provide <see cref="ILogger"/> implementations to track resolution failures.
        /// </para>
        /// <para>
        /// This <see cref="Default"/> resolver gives easy access to a resolver for
        /// common scenarios, without logging.
        /// </para>
        /// </remarks>
        public static readonly IDocumentResolver Default =
            new CompoundDocumentResolver(
                new FileSystemDocumentResolver(),
                new HttpClientDocumentResolver(new HttpClient()));
    }
}
