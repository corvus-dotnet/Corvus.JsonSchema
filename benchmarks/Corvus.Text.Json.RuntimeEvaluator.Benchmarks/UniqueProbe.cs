using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

public static class UniqueProbe
{
    public static void Run()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3, 4, 5, 6, 7, 8]");
        IJsonDocument d = doc;
        int index = 0;
        int length = d.GetArrayLength(index);

        Measure("construct+dispose", () =>
        {
            using UniqueItemsHashSet set = new(d, length, stackalloc int[UniqueItemsHashSet.StackAllocBucketSize], stackalloc byte[UniqueItemsHashSet.StackAllocEntrySize]);
            return 0;
        });
        Measure("construct only (no dispose)", () =>
        {
            UniqueItemsHashSet set = new(d, length, stackalloc int[UniqueItemsHashSet.StackAllocBucketSize], stackalloc byte[UniqueItemsHashSet.StackAllocEntrySize]);
            return 0;
        });
        Measure("construct+add all+dispose", () =>
        {
            using UniqueItemsHashSet set = new(d, length, stackalloc int[UniqueItemsHashSet.StackAllocBucketSize], stackalloc byte[UniqueItemsHashSet.StackAllocEntrySize]);
            int n = 0;
            var e = new ArrayEnumerator(d, index);
            while (e.MoveNext())
            {
                if (set.AddItemIfNotExists(e.CurrentIndex))
                {
                    n++;
                }
            }

            return n;
        });
        Measure("GetHashCode(item)", () => d.GetHashCode(index + 1));
    }

    private static void Measure(string name, Func<int> f)
    {
        int sink = 0;
        for (int i = 0; i < 5; i++)
        {
            sink += f();
        }

        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            sink += f();
        }

        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"{name,-32} {bytes / 1000.0,8:F1} B/op");
        GC.KeepAlive(sink);
    }
}
