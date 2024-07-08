using System.Text.Json;
using Corvus.Json;

namespace Sandbox;

internal static class Metaschema
{
    internal static void AddMetaschema(IDocumentResolver documentResolver)
    {
        documentResolver.AddDocument(
        "http://json-schema.org/draft-04/schema",
        JsonDocument.Parse(File.ReadAllText("./metaschema/draft4/schema.json")));

        documentResolver.AddDocument(
            "http://json-schema.org/draft-06/schema",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft6/schema.json")));

        documentResolver.AddDocument(
            "http://json-schema.org/draft-07/schema",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft7/schema.json")));

        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/schema",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2019-09/schema.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/applicator",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2019-09/meta/applicator.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/content",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2019-09/meta/content.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/core",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2019-09/meta/core.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/format",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2019-09/meta/format.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/hyper-schema",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2019-09/meta/hyper-schema.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/meta-data",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2019-09/meta/meta-data.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2019-09/meta/validation",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2019-09/meta/validation.json")));

        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/schema",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2020-12/schema.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/applicator",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2020-12/meta/applicator.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/content",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2020-12/meta/content.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/core",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2020-12/meta/core.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/format-annotation",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2020-12/meta/format-annotation.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/format-assertion",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2020-12/meta/format-assertion.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/hyper-schema",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2020-12/meta/hyper-schema.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/meta-data",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2020-12/meta/meta-data.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/unevaluated",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2020-12/meta/unevaluated.json")));
        documentResolver.AddDocument(
            "https://json-schema.org/draft/2020-12/meta/validation",
            JsonDocument.Parse(File.ReadAllText("./metaschema/draft2020-12/meta/validation.json")));

    }
}
