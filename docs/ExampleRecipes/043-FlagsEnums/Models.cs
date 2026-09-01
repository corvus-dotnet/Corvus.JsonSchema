using Corvus.Text.Json;

namespace FlagsEnums.Models;

[JsonSchemaTypeGenerator("app-settings.json")]
public readonly partial struct AppSettings;

[JsonSchemaTypeGenerator("feature-flags.json")]
public readonly partial struct FeatureFlags;