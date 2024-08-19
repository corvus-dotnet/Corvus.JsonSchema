using Spectre.Console.Cli;

namespace Corvus.Json.CodeGenerator;

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
            });
        return app.RunAsync(args);
    }
}
