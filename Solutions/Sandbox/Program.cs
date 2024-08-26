using Corvus.Json;
using Feature408;

var documentContent = @"{
    ""size"": null,
    ""extendedSize"": """"
}";
var document = CombinedDocument.Parse(documentContent);

var validationContext = document.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed);
if (!validationContext.IsValid)
{
    Console.WriteLine("Validation failed");
    foreach (var result in validationContext.Results.OrderBy(r => r.Location?.DocumentLocation).ThenBy(r => r.Location?.ValidationLocation))
    {
        if (!result.Valid)
        {
            Console.WriteLine($"{result.Location}: {result.Message}");
        }
    }
}