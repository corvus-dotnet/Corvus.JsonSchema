using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Corvus.Json.CodeGenerator;

/// <summary>
/// Spectre.Console.Cli command for code generation.
/// </summary>
internal class ValidateDocumentCommand : Command<ValidateDocumentCommand.Settings>
{
    /// <summary>
    /// Settings for the generate command.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        [Description("The path to the schema file to use.")]
        [CommandArgument(0, "<schemaFile>")]
        [NotNull] // <> => NotNull
        public string? SchemaFile { get; init; }

        [Description("The path to the document file to process.")]
        [CommandArgument(1, "<documentFile>")]
        [NotNull] // <> => NotNull
        public string? DocumentFile { get; init; }
    }


    /// <inheritdoc/>
    public override int Execute(CommandContext context, Settings settings)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(settings.SchemaFile); // We will never see this exception if the framework is doing its job; it should have blown up inside the CLI command handling
        ArgumentNullException.ThrowIfNullOrEmpty(settings.DocumentFile); // We will never see this exception if the framework is doing its job; it should have blown up inside the CLI command handling

        var schema = Validator.JsonSchema.FromFile(settings.SchemaFile);
        string sourceString = File.ReadAllText(settings.DocumentFile);
        using var document = JsonDocument.Parse(sourceString);
        ValidationContext result = schema.Validate(document.RootElement, ValidationLevel.Detailed);

        if (result.IsValid)
        {
            AnsiConsole.MarkupLine("[green]Document is valid[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Document is invalid[/]");

            byte[] bytes = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetByteCount(sourceString));
            Encoding.UTF8.GetBytes(sourceString, bytes);
            try
            {
                foreach (ValidationResult error in result.Results)
                {
                    if (error.Location is (var validationLocation, var schemaLocation, var documentLocation) location &&
                        JsonPointerUtilities.TryGetLineAndOffsetForPointer(bytes, location.DocumentLocation.Fragment, out int line, out int chars, out long lineOffset))
                    {
                        AnsiConsole.Markup($"[yellow]{error.Message}[/] ({location.ValidationLocation}, {location.SchemaLocation}, {location.DocumentLocation}");
                        int charCount = Encoding.UTF8.GetCharCount(bytes.AsSpan().Slice((int)lineOffset, chars));
                        AnsiConsole.MarkupLineInterpolated($", {settings.DocumentFile}#{line}:{charCount})");
                    }
                    else
                    {
                        AnsiConsole.WriteLine(')');
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        return 0;
    }
}
