// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

// arazzo-runs — a CLI over the Arazzo durability control plane, generated from
// docs/control-plane/arazzo-control-plane.openapi.json (openapi-client) and driven over HTTP.
if (!Console.IsOutputRedirected)
{
    Banner.Write();
}

return await CliApp.Create().RunAsync(args);