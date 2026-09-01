// <copyright file="MovedArrayPropertyCorruptionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Patch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Patch.Tests;

/// <summary>
/// Repro tests for https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/954.
/// A <c>move</c> of an array-valued property must leave the builder in a readable
/// state; the report sees reads of the patched root throw
/// "'t' is an invalid end of a number. Expected a delimiter."
/// </summary>
[TestClass]
public class MovedArrayPropertyCorruptionTests
{
    /// <summary>
    /// Minimal corruption repro found by <see cref="JsonPatchFuzzTests"/>: renaming a
    /// property of the ROOT object via move leaves the builder metadata cyclic, and
    /// reading the root overflows the stack.
    /// </summary>
    [TestMethod]
    public void MoveProperty_RenameWithinRootObject_RootRemainsReadable()
    {
        AssertPatchProducesReadableRoot(
            /*lang=json*/ """{"connection":"t25","port":"x"}""",
            /*lang=json*/ """[{"op":"move","from":"/port","path":"/timeout_ms"}]""",
            /*lang=json*/ """{"connection":"t25","timeout_ms":"x"}""");
    }

    /// <summary>
    /// Minimal repro of the exact reported exception: moving a number to the array end
    /// leaves its metadata row referencing shifted content, and reading the root throws
    /// "'t' is an invalid end of a number. Expected a delimiter."
    /// </summary>
    [TestMethod]
    public void MoveArrayItem_ToArrayEnd_ParsedBuilder_RootRemainsReadable()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var builder = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, /*lang=json*/ """[80185,true,"t5"]""");
        JsonElement.Mutable root = builder.RootElement;

        JsonPatchDocument patch = JsonPatchDocument.ParseValue(/*lang=json*/ """[{"op":"move","from":"/0","path":"/-"}]""");
        Assert.IsTrue(JsonPatchExtensions.TryApplyPatch(ref root, in patch), "Patch application should succeed.");

        JsonElement expected = JsonElement.ParseValue(/*lang=json*/ """[true,"t5",80185]""");
        Assert.IsTrue(root.Equals(expected), $"Expected: [true,\"t5\",80185]\nActual: {root}");
    }

    /// <summary>
    /// RFC 6902 evaluates a move's destination path against the document AFTER the
    /// removal. A destination pointer descending through the source's parent array at
    /// a later index must therefore resolve to the post-removal element.
    /// </summary>
    [TestMethod]
    public void MoveArrayItem_DestinationThroughSameArray_ResolvesAfterRemoval()
    {
        AssertPatchProducesReadableRoot(
            /*lang=json*/ """["v",{"a":1},{"b":2}]""",
            /*lang=json*/ """[{"op":"move","from":"/0","path":"/1/x"}]""",
            /*lang=json*/ """[{"a":1},{"b":2,"x":"v"}]""");
    }

    /// <summary>
    /// The same rename, applied to a builder that PARSED the document directly rather
    /// than importing it from a ParsedJsonDocument.
    /// </summary>
    [TestMethod]
    public void MoveProperty_RenameWithinRootObject_ParsedBuilder_RootRemainsReadable()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using var builder = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, /*lang=json*/ """{"connection":"t25","port":"x"}""");
        JsonElement.Mutable root = builder.RootElement;

        JsonPatchDocument patch = JsonPatchDocument.ParseValue(/*lang=json*/ """[{"op":"move","from":"/port","path":"/timeout_ms"}]""");
        Assert.IsTrue(JsonPatchExtensions.TryApplyPatch(ref root, in patch), "Patch application should succeed.");

        JsonElement expected = JsonElement.ParseValue(/*lang=json*/ """{"connection":"t25","timeout_ms":"x"}""");
        Assert.IsTrue(root.Equals(expected), $"Expected: {{\"connection\":\"t25\",\"timeout_ms\":\"x\"}}\nActual: {root}");
    }

    /// <summary>
    /// Minimal candidate: rename an array-valued property within its object, where the
    /// object also carries a number and further properties after the moved one.
    /// </summary>
    [TestMethod]
    public void MoveArrayValuedProperty_RenameWithinObject_RootRemainsReadable()
    {
        AssertPatchProducesReadableRoot(
            /*lang=json*/ """{"o":{"storage":[{"a":1}],"port":502,"title":"x"}}""",
            /*lang=json*/ """[{"op":"move","from":"/o/storage","path":"/o/storages"}]""",
            /*lang=json*/ """{"o":{"port":502,"title":"x","storages":[{"a":1}]}}""");
    }

    /// <summary>
    /// Two consecutive array-property renames, mirroring the reporter's final two
    /// operations, in an object whose remaining properties include numbers followed
    /// by keys starting with 't'.
    /// </summary>
    [TestMethod]
    public void MoveTwoArrayValuedProperties_RenameWithinObject_RootRemainsReadable()
    {
        AssertPatchProducesReadableRoot(
            /*lang=json*/ """{"interfaces":[{"type":"X","title":"T","slave_id":2,"storage":[{"id":"s","charging_module_id":3}],"generator":[{"id":"g","discharge_priority":0}]}]}""",
            /*lang=json*/ """[{"op":"move","from":"/interfaces/0/storage","path":"/interfaces/0/storages"},{"op":"move","from":"/interfaces/0/generator","path":"/interfaces/0/generators"}]""",
            /*lang=json*/ """{"interfaces":[{"type":"X","title":"T","slave_id":2,"storages":[{"id":"s","charging_module_id":3}],"generators":[{"id":"g","discharge_priority":0}]}]}""");
    }

    /// <summary>
    /// The reporter's full document and patch, verbatim (credentials already redacted
    /// in the report). The two final operations move array-valued properties.
    /// </summary>
    private const string ReporterDocumentJson = /*lang=json*/ """{"general":{"influx":{"endpoint":"http://10.0.0.228:8086","bucket":"pvforecastcharging","organization":"Biohof_Stadler","token":"***"},"open_meteo":{"endpoint":"http://10.0.0.228:6070/v1/forecast/"},"smtp":[{"from":"bio.bau1@gmail.com","to":"bio.bau1@gmail.com","smtp_host":"smtp.gmail.com","smtp_port":587,"username":"bio.bau1@gmail.com","password":"***","receive_logs":"Warning"}],"modbus":{"port":502,"log_level":"Warning"},"logging":{"level":"Trace","categories":{"Modbus":"Debug"}}},"schedulers":{"storage_scheduler":{"min_power_tolerance":2000,"power_jump_threshold":500000}},"algorithms":[{"type":"SmartPeakShaving","options":{"mode":"PreferGridFeedIn","only_necessary_peak_shaving_factor":2,"storage_charge_buffer_factor":1.2,"override_house_consumption":3000}},{"type":"PhotovoltaicCharging"},{"type":"ZeroGridImport","options":{"grid_export_power_target":10}}],"interfaces":[{"type":"FroniusGen24ModbusInverter","title":"Wechselrichter Scheune","hostname":"10.0.0.230","slave_id":2,"storage":[{"type":"Mppt160Storage","id":"haus_speicher","title":"Hausspeicher","active":true,"native_controller":{"zero_export":true,"zero_import":true},"charge_priority":0,"discharge_priority":0,"category":"Battery","charging_module_id":3,"discharging_module_id":4,"inverter_efficiency_source":"FixedValue","fixed_value_efficiency_percent":97.7}],"generator":[{"type":"Mppt160Generator","id":"pv_fronius_scheune","active":true,"title":"PV Scheune","discharge_priority":0,"category":"Photovoltaic","inverter_efficiency_source":"FixedValue","fixed_value_efficiency_percent":97.7}]},{"type":"FroniusGen24ModbusMeter","title":"Netzz�hler","hostname":"10.0.0.230","slave_id":200,"grid":[{"type":"AcMeter2xxGrid","id":"grid","active":true,"title":"Netz","export_power_limit":13000}]},{"type":"Evcc","title":"Evcc","connection":{"type":"IpConnection","hostname":"10.0.0.228","port":7070}},{"type":"ForecastedGenerator","title":"Wechselrichter Fronius Werkstatt","max_power":11260,"generator":{"type":"Generator","id":"pv_fronius_werkstatt","active":true,"title":"PV Werkstatt","discharge_priority":0}}],"forecasts":{"latitude":48.558468,"longitude":13.755692,"altitude":594,"photovoltaic_plants":{"pv_fronius_scheune":{"max_power_output":10200,"nominal_efficiency":97.9,"module_arrays":[{"tilt":35,"azimuth":102,"max_power_output":7500,"temperature_coefficient":-0.4},{"tilt":35,"azimuth":282,"max_power_output":7500,"temperature_coefficient":-0.4}]},"pv_fronius_werkstatt":{"max_power_output":11260,"nominal_efficiency":96.8,"module_arrays":[{"tilt":30,"azimuth":191,"max_power_output":14338,"temperature_coefficient":-0.4}]}}}}""";

    private const string ReporterPatchJson = /*lang=json*/ """[{"op":"replace","path":"/interfaces/0/type","value":"SunSpec"},{"op":"add","path":"/interfaces/0/connection","value":{}},{"op":"add","path":"/interfaces/0/connection/type","value":"IpConnection"},{"op":"move","from":"/interfaces/0/hostname","path":"/interfaces/0/connection/hostname"},{"op":"add","path":"/interfaces/0/port","value":502},{"op":"move","from":"/interfaces/0/port","path":"/interfaces/0/connection/port"},{"op":"add","path":"/interfaces/0/cooldown_id","value":" "},{"op":"move","from":"/interfaces/0/cooldown_id","path":"/interfaces/0/connection/cooldown_id"},{"op":"add","path":"/interfaces/0/connection/timeout_ms","value":2000},{"op":"add","path":"/interfaces/0/connection/cooldown_ms","value":1250},{"op":"add","path":"/interfaces/0/expected_hardware_lag_ms","value":0},{"op":"move","from":"/interfaces/0/storage","path":"/interfaces/0/storages"},{"op":"move","from":"/interfaces/0/generator","path":"/interfaces/0/generators"}]""";

    [TestMethod]
    public void ReporterDocumentAndPatch_RootRemainsReadable()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(ReporterDocumentJson);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patchDoc = JsonPatchDocument.ParseValue(ReporterPatchJson);

        bool result = JsonPatchExtensions.TryValidateAndApplyPatch(ref root, in patchDoc);
        Assert.IsTrue(result, "Patch application should succeed.");

        // The report throws here: reading the patched root must not fail, and the whole
        // document must match an independent reference application of the patch.
        string serialized = root.ToString();
        AssertMatchesReference(ReporterDocumentJson, ReporterPatchJson, serialized);
    }

    private static void AssertMatchesReference(string docJson, string patchJson, string actualJson, bool normalizeNumbers = false)
    {
        System.Text.Json.Nodes.JsonNode? reference = System.Text.Json.Nodes.JsonNode.Parse(docJson);
        foreach (System.Text.Json.Nodes.JsonNode? op in System.Text.Json.Nodes.JsonNode.Parse(patchJson)!.AsArray())
        {
            Assert.IsTrue(
                JsonPatchFuzzTests.TryApplyReferenceOperation(ref reference, op!.ToJsonString()),
                $"Reference implementation must accept operation {op.ToJsonString()}.");
        }

        System.Text.Json.Nodes.JsonNode? actual = System.Text.Json.Nodes.JsonNode.Parse(actualJson);
        if (normalizeNumbers)
        {
            reference = NormalizeNumbers(reference);
            actual = NormalizeNumbers(actual);
        }

        string expected = reference?.ToJsonString() ?? "null";
        string actualCanonical = actual?.ToJsonString() ?? "null";
        Assert.AreEqual(expected, actualCanonical, "The patched document must match the reference implementation.");
    }

    /// <summary>
    /// Rewrites every number through the framework's own numeric formatting, so documents that
    /// took different textual routes to the same values compare equal. The YAML conversion
    /// formats non-integer scalars from <see cref="double"/>, and .NET Framework produces a
    /// different (longer, equally round-trippable) representation than .NET's shortest form,
    /// so the YAML-sourced document's number text differs per framework from the JSON source.
    /// </summary>
    private static System.Text.Json.Nodes.JsonNode? NormalizeNumbers(System.Text.Json.Nodes.JsonNode? node)
    {
        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject obj:
                var normalizedObject = new System.Text.Json.Nodes.JsonObject();
                foreach (KeyValuePair<string, System.Text.Json.Nodes.JsonNode?> property in obj)
                {
                    normalizedObject[property.Key] = NormalizeNumbers(property.Value);
                }

                return normalizedObject;
            case System.Text.Json.Nodes.JsonArray array:
                var normalizedArray = new System.Text.Json.Nodes.JsonArray();
                foreach (System.Text.Json.Nodes.JsonNode? item in array)
                {
                    normalizedArray.Add(NormalizeNumbers(item));
                }

                return normalizedArray;
            case System.Text.Json.Nodes.JsonValue value when value.TryGetValue(out long integer):
                return System.Text.Json.Nodes.JsonValue.Create(integer);
            case System.Text.Json.Nodes.JsonValue value when value.TryGetValue(out double number):
                return System.Text.Json.Nodes.JsonValue.Create(number);
            default:
                return node is null ? null : System.Text.Json.Nodes.JsonNode.Parse(node.ToJsonString());
        }
    }

    /// <summary>
    /// YAML-sourced variant of the minimal double-move repro. The reporter's pipeline
    /// parses YAML configuration, whose scalar storage differs from a JSON buffer.
    /// </summary>
    [TestMethod]
    public void MoveTwoArrayValuedProperties_YamlSource_RootRemainsReadable()
    {
        string yaml =
            """
            interfaces:
            - type: X
              title: T
              slave_id: 2
              storage:
              - id: s
                charging_module_id: 3
              generator:
              - id: g
                discharge_priority: 0
            """;

        AssertYamlPatchProducesReadableRoot(
            yaml,
            /*lang=json*/ """[{"op":"move","from":"/interfaces/0/storage","path":"/interfaces/0/storages"},{"op":"move","from":"/interfaces/0/generator","path":"/interfaces/0/generators"}]""",
            /*lang=json*/ """{"interfaces":[{"type":"X","title":"T","slave_id":2,"storages":[{"id":"s","charging_module_id":3}],"generators":[{"id":"g","discharge_priority":0}]}]}""");
    }

    /// <summary>
    /// YAML-sourced variant of the reporter's full document and patch: the JSON from the
    /// report is converted to YAML, parsed as YAML, and then patched.
    /// </summary>
    [TestMethod]
    public void ReporterDocumentAndPatch_YamlSource_RootRemainsReadable()
    {
        string yaml = Yaml.YamlDocument.ConvertToYamlString(ReporterDocumentJson);

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = Yaml.YamlDocument.Parse<JsonElement>(System.Text.Encoding.UTF8.GetBytes(yaml));
        using JsonDocumentBuilder<JsonElement.Mutable> builder = sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patchDoc = JsonPatchDocument.ParseValue(ReporterPatchJson);

        bool result = JsonPatchExtensions.TryValidateAndApplyPatch(ref root, in patchDoc);
        Assert.IsTrue(result, "Patch application should succeed.");

        // The YAML conversion reformats non-integer numbers from double, and the two
        // frameworks produce different (numerically identical) textual forms, so the
        // comparison normalizes number text on both sides.
        string serialized = builder.RootElement.ToString();
        AssertMatchesReference(ReporterDocumentJson, ReporterPatchJson, serialized, normalizeNumbers: true);
    }

    /// <summary>
    /// After the patch, every property of every object in the BUILDER must resolve by
    /// name-based lookup to the same value that sequential enumeration sees. Sequential
    /// serialization does not consult property maps; name lookup does.
    /// </summary>
    [TestMethod]
    public void ReporterDocumentAndPatch_NameBasedLookupsMatchEnumeration()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(ReporterDocumentJson);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patchDoc = JsonPatchDocument.ParseValue(ReporterPatchJson);

        bool result = JsonPatchExtensions.TryValidateAndApplyPatch(ref root, in patchDoc);
        Assert.IsTrue(result, "Patch application should succeed.");

        VerifyNameLookupsRecursively(builder.RootElement, "#");
    }

    private static void VerifyNameLookupsRecursively(in JsonElement.Mutable element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty<JsonElement.Mutable> property in element.EnumerateObject())
                {
                    string name = property.Name;
                    string childPath = $"{path}/{name}";
                    Assert.IsTrue(
                        element.TryGetProperty(name, out JsonElement.Mutable byName),
                        $"Name lookup for '{childPath}' must succeed after patching.");
                    string enumerated = property.Value.ToString();
                    string lookedUp = byName.ToString();
                    Assert.AreEqual(enumerated, lookedUp, $"Name lookup for '{childPath}' must match enumeration.");
                    VerifyNameLookupsRecursively(property.Value, childPath);
                }

                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement.Mutable item in element.EnumerateArray())
                {
                    VerifyNameLookupsRecursively(item, $"{path}/{index}");
                    index++;
                }

                break;
        }
    }

    private static void AssertYamlPatchProducesReadableRoot(string yamlText, string patchJson, string expectedJson)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = Yaml.YamlDocument.Parse<JsonElement>(System.Text.Encoding.UTF8.GetBytes(yamlText));
        using JsonDocumentBuilder<JsonElement.Mutable> builder = sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        bool result = JsonPatchExtensions.TryValidateAndApplyPatch(ref root, in patch);
        Assert.IsTrue(result, "Patch application should succeed.");

        JsonElement expected = JsonElement.ParseValue(expectedJson);
        Assert.IsTrue(root.Equals(expected), $"Expected: {expectedJson}\nActual: {root}");

        string serialized = builder.RootElement.ToString();
        _ = JsonElement.ParseValue(serialized);
    }

    private static void AssertPatchProducesReadableRoot(string docJson, string patchJson, string expectedJson)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse(docJson);
        using JsonDocumentBuilder<JsonElement.Mutable> builder = sourceDoc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(patchJson);

        bool result = JsonPatchExtensions.TryValidateAndApplyPatch(ref root, in patch);
        Assert.IsTrue(result, "Patch application should succeed.");

        JsonElement expected = JsonElement.ParseValue(expectedJson);
        Assert.IsTrue(root.Equals(expected), $"Expected: {expectedJson}\nActual: {root}");
    }
}
