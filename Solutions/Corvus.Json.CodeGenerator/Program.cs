using Spectre.Console.Cli;

namespace Corvus.Json.SchemaGenerator;

class Program
{
    static Task<int> Main(string[] args)
    {
        var app = new CommandApp<GenerateCommand>();
        app.Configure(c => c.SetApplicationName("generatejsonschematypes"));
        return app.RunAsync(args);
    }
}
