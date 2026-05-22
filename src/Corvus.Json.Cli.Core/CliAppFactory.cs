// <copyright file="CliAppFactory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Creates a configured <see cref="CommandApp"/> with all registered subcommands.
/// </summary>
public static class CliAppFactory
{
    /// <summary>
    /// Creates a <see cref="CommandApp"/> with all subcommands registered.
    /// </summary>
    /// <param name="appName">The application name shown in help text.</param>
    /// <returns>A configured <see cref="CommandApp"/>.</returns>
    public static CommandApp Create(string appName)
    {
        var app = new CommandApp();
        app.Configure(
            c =>
            {
                c.SetApplicationName(appName);
                c.AddCommand<GenerateCommand>("jsonschema");
                c.AddCommand<GenerateWithDriverCommand>("config");
                c.AddCommand<ListNamingHeuristicsCommand>("listNameHeuristics");
                c.AddCommand<ValidateDocumentCommand>("validateDocument");
                c.AddCommand<VersionCommand>("version");
                c.AddCommand<JsonLogicCommand>("jsonlogic");
                c.AddCommand<JsonataCommand>("jsonata");
                c.AddCommand<JMESPathCommand>("jmespath");
                c.AddCommand<JsonPathCommand>("jsonpath");
#if NET10_0_OR_GREATER
                c.AddCommand<OpenApiGenerateCommand>("openapi-client")
                    .WithDescription("Generate API client code from an OpenAPI specification.");
                c.AddCommand<OpenApiServerCommand>("openapi-server")
                    .WithDescription("Generate API server stubs from an OpenAPI specification.");
                c.AddCommand<OpenApiCallbackServerCommand>("openapi-callback-server")
                    .WithDescription("Generate server stubs from OpenAPI webhooks and callbacks.");
                c.AddCommand<OpenApiCallbackClientCommand>("openapi-callback-client")
                    .WithDescription("Generate a client for invoking OpenAPI webhooks and callbacks.");
                c.AddCommand<OpenApiShowCommand>("openapi-show")
                    .WithDescription("Display the operation tree of an OpenAPI specification.");
#endif
            });
        return app;
    }
}