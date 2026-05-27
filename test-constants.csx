#r "nuget: Testcontainers.Kafka, 4.12.0"
using Testcontainers.Kafka;
using System.Reflection;

var type = typeof(KafkaBuilder);
var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
foreach (var field in fields)
{
    if (field.Name.Contains("Port"))
    {
        Console.WriteLine($"{field.Name} = {field.GetValue(null)}");
    }
}
