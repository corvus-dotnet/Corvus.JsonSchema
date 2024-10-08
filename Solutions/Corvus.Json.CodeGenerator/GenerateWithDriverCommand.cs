using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;

namespace Corvus.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for code generation.
/// </summary>
internal class GenerateWithDriverCommand : AsyncCommand<GenerateWithDriverCommand.Settings>
{
    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.GenerationSpecificationFile); // We will never see this exception if the framework is doing its job; it should have blown up inside the CLI command handling

        var config = GeneratorConfig.Parse(File.OpenRead(settings.GenerationSpecificationFile));
        return GenerationDriver.GenerateTypes(config);
    }

    /// <summary>
    /// Settings for the generate command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        [Description("The path to the code generation specification file.")]
        [CommandArgument(0, "<generationSpecificationFile>")]
        [NotNull] // <> => NotNull
        public string? GenerationSpecificationFile { get; init; }
    }
}
