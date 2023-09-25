using Corvus.Json;

JsonNumber num = 3.0;
JsonNumber floatNum = 3.1;

var lhs = JsonObject.FromProperties(("hello", 1.3M));
var rhs = JsonObject.FromProperties(("hello", 1.3f));

Console.WriteLine($"*1.3M == *1.3f is {lhs["hello"] == rhs["hello"]}");
Console.WriteLine($"*1.3f == *1.3M is {rhs["hello"] == lhs["hello"]}");
Console.WriteLine($"1.3M == 1.3f is {1.3M == (decimal)1.3f}");
Console.WriteLine($"1.3f == 1.3M is {(decimal)1.3f == 1.3M}");


var lhs2 = JsonObject.FromProperties(("hello", 1.3M));
var rhs2 = JsonObject.FromProperties(("hello", 1.4f));

Console.WriteLine($"*1.3M > *1.3f is {lhs["hello"].AsNumber > rhs["hello"].AsNumber}");
Console.WriteLine($"*1.3f > *1.3M is {rhs["hello"].AsNumber > lhs["hello"].AsNumber}");
Console.WriteLine($"1.3M > 1.3f is {1.3M > (decimal)1.3f}");
Console.WriteLine($"1.3f > 1.3M is {(decimal)1.3f > 1.3M}");


Console.WriteLine($"*1.3M > *1.4f is {lhs2["hello"].AsNumber > rhs2["hello"].AsNumber}");
Console.WriteLine($"*1.4f > *1.3M is {rhs2["hello"].AsNumber > lhs2["hello"].AsNumber}");
Console.WriteLine($"1.3M > 1.4f is {1.3M > (decimal)1.4f}");
Console.WriteLine($"1.4f > 1.3M is {(decimal)1.4f > 1.3M}");

var lhs2a = JsonObject.FromProperties(("hello", 1.3M));
var rhs2a = JsonObject.FromProperties(("hello", 1.4d));

Console.WriteLine($"*1.3M > *1.3d is {lhs["hello"].AsNumber > rhs["hello"].AsNumber}");
Console.WriteLine($"*1.3d > *1.3M is {rhs["hello"].AsNumber > lhs["hello"].AsNumber}");
Console.WriteLine($"1.3M > 1.3d is {1.3M > (decimal)1.3d}");
Console.WriteLine($"1.3d > 1.3M is {(decimal)1.3d > 1.3M}");


Console.WriteLine($"*1.3M > *1.4d is {lhs2a["hello"].AsNumber > rhs2a["hello"].AsNumber}");
Console.WriteLine($"*1.4d > *1.3M is {rhs2a["hello"].AsNumber > lhs2a["hello"].AsNumber}");
Console.WriteLine($"1.3M > 1.4d is {1.3M > (decimal)1.4d}");
Console.WriteLine($"1.4d > 1.3M is {(decimal)1.4d > 1.3M}");


var lhs3 = JsonObject.FromProperties(("hello", 0.3M));
var rhs3 = JsonObject.FromProperties(("hello", 0.3));

Console.WriteLine($"*0.3M == *0.3d is {lhs3["hello"].AsNumber == rhs3["hello"].AsNumber}");
Console.WriteLine($"*0.3d == *0.3M is {rhs3["hello"].AsNumber == lhs3["hello"].AsNumber}");
Console.WriteLine($"0.3M == 0.3f is {0.3M == (decimal)0.3d}");
Console.WriteLine($"0.3d == 0.3M is {(decimal)0.3d == 0.3M}");

var lhs3a = JsonObject.FromProperties(("hello", 0.3d));
var rhs3a = JsonObject.FromProperties(("hello", 0.3f));

Console.WriteLine($"*0.3d == *0.3f is {lhs3["hello"].AsNumber == rhs3["hello"].AsNumber}");
Console.WriteLine($"*0.3f == *0.3d is {rhs3["hello"].AsNumber == lhs3["hello"].AsNumber}");
Console.WriteLine($"0.3d == 0.3f is {0.3d == 0.3f}");
Console.WriteLine($"0.3f == 0.3d is {0.3f == 0.3d}");

var lhs4 = JsonObject.FromProperties(("hello", 0.4M));
var rhs4 = JsonObject.FromProperties(("hello", 0.4));

Console.WriteLine($"*0.4M == *0.4d is {lhs4["hello"].AsNumber == rhs4["hello"].AsNumber}");
Console.WriteLine($"*0.4d == *0.4M is {rhs4["hello"].AsNumber == lhs4["hello"].AsNumber}");
Console.WriteLine($"0.4M == 0.4d is {0.4M == (decimal)0.4d}");
Console.WriteLine($"0.4d == 0.4M is {(decimal)0.4d == 0.4M}");

var lhs5 = JsonObject.FromProperties(("hello", 1234567890.1234567891M));
var rhs5 = JsonObject.FromProperties(("hello", 1E29));

try
{
    Console.WriteLine($"*1234567890.1234567891M == *1E29 is {lhs5["hello"].AsNumber == rhs5["hello"].AsNumber}");
}
catch (Exception ex1)
{
    Console.WriteLine($"*1234567890.1234567891M == *1E29: {ex1.Message}");
}


try
{
    Console.WriteLine($"*1E29 == *1234567890.1234567891M is {rhs5["hello"].AsNumber == lhs5["hello"].AsNumber}");
}
catch (Exception ex2)
{
    Console.WriteLine($"*1E29 == *1234567890.1234567891M is {ex2.Message}");
}

try
{
    Console.WriteLine($"1234567890.1234567891M == 1E29 is {1234567890.1234567891M == (decimal)(object)1E29}");
}
catch (Exception ex3)
{
    Console.WriteLine($"1234567890.1234567891M == 1E29: {ex3.Message}");
}


try
{
    Console.WriteLine($"1E29 == 1234567890.1234567891M is {(decimal)(object)1E29 == 1234567890.1234567891M}");
}
catch (Exception ex4)
{
    Console.WriteLine($"1E29 == 1234567890.1234567891M is {ex4.Message}");
}