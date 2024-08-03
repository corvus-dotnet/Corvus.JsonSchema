using Spectre.Console.Cli;

namespace Corvus.Json.CodeGenerator;

class Program
{
    static Task<int> Main(string[] args)
    {
        var app = new CommandApp<GenerateCommand>();
        app.Configure(
            c => 
                c.SetApplicationName("generatejsonschematypes")
                 .AddCommand<GenerateWithDriverCommand>("config"));
        return app.RunAsync(args);
    }
}
