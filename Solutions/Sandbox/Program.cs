using Sandbox.SourceGenerator;

using Corvus.Json;

var dt = DateTime.Parse("2025-04-09T12:00:00+01:00");
var jdt = new JsonDateTime(dt);

var jdta = new JsonDateTime("2025-04-09T12:00:00+01"); // IsValid(): false
var jdtb = new JsonDateTime("2025-04-09T12:00:00+01:00"); // IsValid(): true


Console.WriteLine(jdt.IsValid());
Console.WriteLine(jdta.IsValid());
Console.WriteLine(jdtb.IsValid());

Console.ReadLine();