using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaAdditionalTests.Draft202012;

[Trait("Additional-JsonSchemaTests", "Draft202012")]
public class BenchmarkRepro : IClassFixture<BenchmarkRepro.Fixture>
{
    private readonly Fixture _fixture;

    public BenchmarkRepro(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNullIsNotAPerson()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "draft2020-12\\typeAndFormat.json",
                """
                {
                    "$schema": "https://json-schema.org/draft/2020-12/schema",
                    "title": "JSON Schema for a Person entity coming back from a 3rd party API (e.g. a storage format in a database)",
                    "$ref": "#/$defs/PersonArray",
                    "$defs": {
                        "PersonArray": {
                            "type": "array",
                            "unevaluatedItems": {
                                "$ref": "#/$defs/Person"
                            }
                        },
                        "Person": {
                            "type": "object",

                            "required": [ "name" ],
                            "properties": {
                                "name": { "$ref": "#/$defs/PersonName" },
                                "age": { "$ref": "#/$defs/Age" },
                                "competedInYears": { "$ref": "#/$defs/CompetedInYears" }
                            },
                            "unevaluatedProperties": false
                        },
                        "HeightRangeDouble": {
                            "type": "number",
                            "minimum": 0,
                            "maximum": 3.0
                        },
                        "PersonName": {
                            "type": "object",
                            "description": "A name of a person.",
                            "required": [ "firstName" ],
                            "properties": {
                                "firstName": {
                                    "$ref": "#/$defs/NameComponent",
                                    "description": "The person's first name."
                                },
                                "lastName": {
                                    "$ref": "#/$defs/NameComponent",
                                    "description": "The person's last name."
                                },
                                "otherNames": {
                                    "$ref": "#/$defs/OtherNames",
                                    "description": "Other (middle) names for the person"
                                }
                            }
                        },
                        "OtherNames": {
                            "oneOf": [
                                { "$ref": "#/$defs/NameComponent" },
                                { "$ref": "#/$defs/NameComponentArray" }
                            ]
                        },
                        "NameComponentArray": {
                            "type": "array",
                            "items": {
                                "$ref": "#/$defs/NameComponent"
                            }
                        },
                        "NameComponent": {
                            "type": "string",
                            "minLength": 1,
                            "maxLength": 256
                        },
                        "CompetedInYears": {
                            "type": "array",
                            "items": { "$ref": "#/$defs/Year" }
                        },
                        "Year": {
                            "type": "number",
                            "format": "int32"
                        },
                        "Age": {
                            "type": "number",
                            "minimum": 0,
                            "maximum": 130
                        }
                    }
                }                
                """,
                "JsonSchemaTestSuite.Draft202012.BenchmarkRepro",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                optionalAsNullable: true,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}