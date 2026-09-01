using Corvus.Text.Json;
using FlagsEnums.Models;

// Combine flags with the native enum operators
FeatureFlags.Flags enabled = FeatureFlags.Flags.DarkMode | FeatureFlags.Flags.Telemetry;

// Create a document directly from the flags. Every declared property is written
// explicitly, so the output is deterministic.
using var doc = AppSettings.Create(name: "my-app", features: enabled);
Console.WriteLine(doc.RootElement.ToString());
// Output: {"features":{"betaFeatures":false,"darkMode":true,"telemetry":true},"name":"my-app"}

// Read the flags back with an implicit conversion; assess them with native HasFlag
FeatureFlags.Flags current = doc.RootElement.Features;
Console.WriteLine($"Dark mode:     {current.HasFlag(FeatureFlags.Flags.DarkMode)}");
Console.WriteLine($"Beta features: {current.HasFlag(FeatureFlags.Flags.BetaFeatures)}");
Console.WriteLine($"Telemetry:     {current.HasFlag(FeatureFlags.Flags.Telemetry)}");

// Modify the set with the usual enum operators and build a new document
current |= FeatureFlags.Flags.BetaFeatures;
current &= ~FeatureFlags.Flags.Telemetry;
using var updated = AppSettings.Create(name: "my-app", features: current);
Console.WriteLine(updated.RootElement.ToString());
// Output: {"features":{"betaFeatures":true,"darkMode":true,"telemetry":false},"name":"my-app"}

// Parsing external JSON works as usual; absent and false properties are both "clear"
using var parsed = ParsedJsonDocument<FeatureFlags>.Parse("""{"darkMode":true}""");
FeatureFlags.Flags fromJson = parsed.RootElement;
Console.WriteLine($"From JSON: {fromJson}");
// Output: From JSON: DarkMode

// The non-throwing form reports whether the value was an object at all
if (parsed.RootElement.TryGetFlags(out FeatureFlags.Flags tried))
{
    Console.WriteLine($"TryGetFlags: {tried}");
    // Output: TryGetFlags: DarkMode
}