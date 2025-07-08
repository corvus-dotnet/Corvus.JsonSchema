////using System;
////using Corvus.Json;
////using SourceGenTest2.Model;

////namespace Sandbox.SourceGenerator;
////public static class WorkingWithGeneratedCode
////{
////    public static void Process()
////    {
////        FlimFlam flimFlam = JsonAny.ParseValue("[1,\"hello\",5]"u8);
////        Console.WriteLine(flimFlam);
////        JsonArray array = flimFlam.As<JsonArray>();
////        Console.WriteLine(array);
////        Console.WriteLine(flimFlam.IsValid());
////    }
////}
