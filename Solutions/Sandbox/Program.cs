using Corvus.Json;

JsonNumber num = 3.0;
JsonNumber floatNum = 3.1;

// But these are safe - they return false if they would truncate
// (notwithstanding precision issues)
if (num.TryGetInt32(out int myInt3))
{
    Console.WriteLine($"Got {num} as int32 {myInt3}.");
}
else
{
    Console.WriteLine($"Couldn't get {num} as int32.");
}

if (floatNum.TryGetInt32(out int myInt4))
{
    Console.WriteLine($"Got {floatNum} as int32 {myInt4}.");
}
else
{
    Console.WriteLine($"Couldn't get {floatNum} as int32.");
}