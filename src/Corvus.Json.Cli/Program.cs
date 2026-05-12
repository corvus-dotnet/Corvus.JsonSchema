// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGenerator;

if (!Console.IsOutputRedirected)
{
    Banner.Write();
}

CliDefaults.DefaultEngine = Engine.V5;

return await CliAppFactory.Create("corvusjson").RunAsync(args);