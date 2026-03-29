// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Spectre.Console.Cli;

namespace Corvus.Text.Json.CodeGenerator;

class Program
{
    static Task<int> Main(string[] args)
    {
        var app = new CommandApp<GenerateCommand>();
        app.Configure(
            c =>
            {
                c.SetApplicationName("generatejsonschematypes");
                c.AddCommand<GenerateWithDriverCommand>("config");
                c.AddCommand<ListNamingHeuristicsCommand>("listNameHeuristics");
                c.AddCommand<ValidateDocumentCommand>("validateDocument");
                c.AddCommand<VersionCommand>("version");
            });
        return app.RunAsync(args);
    }
}