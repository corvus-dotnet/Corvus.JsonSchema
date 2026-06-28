using Corvus.Text.Json.TypeScript.Playground.Models;

namespace Corvus.Text.Json.TypeScript.Playground.Services;

/// <summary>
/// The built-in playground examples. Each pairs a JSON Schema with a TypeScript snippet that uses the
/// generated module; the snippets stick to <c>evaluateRoot</c> (always emitted) so they run for any root type.
/// </summary>
public class SampleRegistry
{
    private static readonly PlaygroundSample[] AllSamples =
    [
        new PlaygroundSample(
            "person",
            "Person — object + format",
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "Person",
              "type": "object",
              "required": [ "familyName" ],
              "properties": {
                "familyName": { "type": "string" },
                "givenName": { "type": "string" },
                "birthDate": { "type": "string", "format": "date" },
                "height": { "type": "number" }
              }
            }
            """,
            """
            import { buildPerson, evaluateRoot, asBirthDate } from "./generated.js";

            const dec = new TextDecoder();

            // Build a Person from plain values → canonical UTF-8 JSON bytes (the wire shape).
            const bytes = buildPerson({
              familyName: "Brontë",
              givenName: "Anne",
              birthDate: asBirthDate("1820-01-17"), // format: date — a validating branded factory
              height: 1.52,
            });
            console.log("built:", dec.decode(bytes));

            // Validate untrusted input with the generated evaluator — a boolean, no exceptions.
            console.log("valid:        ", evaluateRoot(JSON.parse(dec.decode(bytes))));
            console.log("missing reqd: ", evaluateRoot({ givenName: "Anne" }));
            """),

        new PlaygroundSample(
            "shape",
            "Shape — oneOf union",
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "Shape",
              "oneOf": [
                {
                  "type": "object",
                  "required": [ "kind", "radius" ],
                  "properties": { "kind": { "const": "circle" }, "radius": { "type": "number" } }
                },
                {
                  "type": "object",
                  "required": [ "kind", "side" ],
                  "properties": { "kind": { "const": "square" }, "side": { "type": "number" } }
                }
              ]
            }
            """,
            """
            import { evaluateRoot } from "./generated.js";

            // oneOf → a discriminated union; exactly one branch must match.
            console.log("circle:       ", evaluateRoot({ kind: "circle", radius: 5 }));
            console.log("square:       ", evaluateRoot({ kind: "square", side: 3 }));
            console.log("bad kind:     ", evaluateRoot({ kind: "triangle", radius: 1 }));
            console.log("missing side: ", evaluateRoot({ kind: "square" }));
            """),

        new PlaygroundSample(
            "todo",
            "TodoList — array of objects",
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "TodoList",
              "type": "array",
              "items": {
                "type": "object",
                "required": [ "task", "done" ],
                "properties": {
                  "task": { "type": "string" },
                  "done": { "type": "boolean" },
                  "priority": { "type": "integer", "minimum": 1, "maximum": 5 }
                }
              }
            }
            """,
            """
            import { evaluateRoot } from "./generated.js";

            console.log("valid:           ", evaluateRoot([{ task: "write docs", done: false, priority: 2 }]));
            console.log("priority too high:", evaluateRoot([{ task: "x", done: true, priority: 9 }]));
            console.log("missing done:    ", evaluateRoot([{ task: "x" }]));
            """),

        new PlaygroundSample(
            "measurement",
            "Measurement — string + numeric formats",
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "Measurement",
              "type": "object",
              "required": [ "id", "takenAt", "celsius" ],
              "properties": {
                "id": { "type": "string", "format": "uuid" },
                "takenAt": { "type": "string", "format": "date-time" },
                "celsius": { "type": "number" },
                "count": { "type": "integer", "format": "int32" }
              }
            }
            """,
            """
            import { evaluateRoot } from "./generated.js";

            // format is asserted in this playground, so malformed uuid / date-time are rejected.
            const id = "f81d4fae-7dec-11d0-a765-00a0c91e6bf6";
            console.log("valid:        ", evaluateRoot({ id, takenAt: "2026-01-01T12:00:00Z", celsius: 21.5, count: 3 }));
            console.log("bad uuid:     ", evaluateRoot({ id: "nope", takenAt: "2026-01-01T12:00:00Z", celsius: 21.5 }));
            console.log("bad date-time:", evaluateRoot({ id, takenAt: "yesterday", celsius: 21.5 }));
            """),
    ];

    /// <summary>
    /// Gets all built-in samples.
    /// </summary>
    public IReadOnlyList<PlaygroundSample> Samples => AllSamples;

    /// <summary>
    /// Gets the default sample (shown on first load).
    /// </summary>
    public PlaygroundSample Default => AllSamples[0];

    /// <summary>
    /// Finds a sample by id, or null if not found.
    /// </summary>
    /// <param name="id">The sample id.</param>
    /// <returns>The sample, or null.</returns>
    public PlaygroundSample? FindById(string id) => Array.Find(AllSamples, s => s.Id == id);
}
