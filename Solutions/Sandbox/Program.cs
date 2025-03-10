using Sandbox.SourceGenerator;

using Corvus.Json;

var dt1 = DateTime.Parse("2025-03-10T14:55:00.123456Z");
var dt2 = DateTime.Parse("2025-03-10T14:55:00.1234567Z");

var jdt1 = new JsonDateTime(dt1);
var jdt2 = new JsonDateTime(dt2);

jdt1.IsValid(); // true
jdt2.IsValid(); // false

Console.ReadLine();