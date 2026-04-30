using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaAdditionalTests.Draft202012;

[Trait("Additional-JsonSchemaTests", "Draft202012")]
public class LargeNumbersOfRequiredProperties : IClassFixture<LargeNumbersOfRequiredProperties.Fixture>
{
    private readonly Fixture _fixture;
    public LargeNumbersOfRequiredProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPresentRequiredPropertyIsValid()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance(
            """
            {
                "foo": 1,
                "foo1": 1,
                "foo2": 1,
                "foo3": 1,
                "foo4": 1,
                "foo5": 1,
                "foo6": 1,
                "foo7": 1,
                "foo8": 1,
                "foo9": 1,
                "foo10": 1,
                "foo11": 1,
                "foo12": 1,
                "foo13": 1,
                "foo14": 1,
                "foo15": 1,
                "foo16": 1,
                "foo17": 1,
                "foo18": 1,
                "foo19": 1,
                "foo20": 1,
                "foo21": 1,
                "foo22": 1,
                "foo23": 1,
                "foo24": 1,
                "foo25": 1,
                "foo26": 1,
                "foo27": 1,
                "foo28": 1,
                "foo29": 1,
                "foo30": 1,
                "foo31": 1,
                "foo32": 1,
                "foo33": 1,
                "foo34": 1,
                "foo35": 1,
                "foo36": 1,
                "foo37": 1,
                "foo38": 1,
                "foo39": 1,
                "foo40": 1
            }
            """);
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonPresentRequiredPropertyIsInvalid()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": 1}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresOtherNonObjects()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNull()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresBoolean()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\required.json",
                """
                {
                    "$schema": "https://json-schema.org/draft/2020-12/schema",
                    "properties": {
                        "foo": {},
                        "bar": {}
                    },

                    "required": [
                        "foo",
                        "foo1", "foo2", "foo3", "foo4", "foo5", "foo6", "foo7", "foo8",
                        "foo9", "foo10", "foo11", "foo12", "foo13", "foo14", "foo15", "foo16",
                        "foo17", "foo18", "foo19", "foo20", "foo21", "foo22", "foo23", "foo24",
                        "foo25", "foo26", "foo27", "foo28", "foo29", "foo30", "foo31", "foo32",
                        "foo33", "foo34", "foo35", "foo36", "foo37", "foo38", "foo39", "foo40"
                    ]
                }
                """,
                "JsonSchemaTestSuite.Draft202012.LargeNumbersOfRequiredProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("Additional-JsonSchemaTests", "Draft202012")]
public class ExtremelyLargeNumbersOfRequiredProperties : IClassFixture<ExtremelyLargeNumbersOfRequiredProperties.Fixture>
{
    private readonly Fixture _fixture;
    public ExtremelyLargeNumbersOfRequiredProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPresentRequiredPropertyIsValid()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance(
            """
            {
                "foo": 1,
                "foo1": 1,
                "foo2": 1,
                "foo3": 1,
                "foo4": 1,
                "foo5": 1,
                "foo6": 1,
                "foo7": 1,
                "foo8": 1,
                "foo9": 1,
                "foo10": 1,
                "foo11": 1,
                "foo12": 1,
                "foo13": 1,
                "foo14": 1,
                "foo15": 1,
                "foo16": 1,
                "foo17": 1,
                "foo18": 1,
                "foo19": 1,
                "foo20": 1,
                "foo21": 1,
                "foo22": 1,
                "foo23": 1,
                "foo24": 1,
                "foo25": 1,
                "foo26": 1,
                "foo27": 1,
                "foo28": 1,
                "foo29": 1,
                "foo30": 1,
                "foo31": 1,
                "foo32": 1,
                "foo33": 1,
                "foo34": 1,
                "foo35": 1,
                "foo36": 1,
                "foo37": 1,
                "foo38": 1,
                "foo39": 1,
                "foo40": 1,
                "foo41": 1,
                "foo42": 1,
                "foo43": 1,
                "foo44": 1,
                "foo45": 1,
                "foo46": 1,
                "foo47": 1,
                "foo48": 1,
                "foo49": 1,
                "foo50": 1,
                "foo51": 1,
                "foo52": 1,
                "foo53": 1,
                "foo54": 1,
                "foo55": 1,
                "foo56": 1,
                "foo57": 1,
                "foo58": 1,
                "foo59": 1,
                "foo60": 1,
                "foo61": 1,
                "foo62": 1,
                "foo63": 1,
                "foo64": 1,
                "foo65": 1,
                "foo66": 1,
                "foo67": 1,
                "foo68": 1,
                "foo69": 1,
                "foo70": 1,
                "foo71": 1,
                "foo72": 1,
                "foo73": 1,
                "foo74": 1,
                "foo75": 1,
                "foo76": 1,
                "foo77": 1,
                "foo78": 1,
                "foo79": 1,
                "foo80": 1,
                "foo81": 1,
                "foo82": 1,
                "foo83": 1,
                "foo84": 1,
                "foo85": 1,
                "foo86": 1,
                "foo87": 1,
                "foo88": 1,
                "foo89": 1,
                "foo90": 1,
                "foo91": 1,
                "foo92": 1,
                "foo93": 1,
                "foo94": 1,
                "foo95": 1,
                "foo96": 1,
                "foo97": 1,
                "foo98": 1,
                "foo99": 1,
                "foo100": 1,
                "foo101": 1,
                "foo102": 1,
                "foo103": 1,
                "foo104": 1,
                "foo105": 1,
                "foo106": 1,
                "foo107": 1,
                "foo108": 1,
                "foo109": 1,
                "foo110": 1,
                "foo111": 1,
                "foo112": 1,
                "foo113": 1,
                "foo114": 1,
                "foo115": 1,
                "foo116": 1,
                "foo117": 1,
                "foo118": 1,
                "foo119": 1,
                "foo120": 1,
                "foo121": 1,
                "foo122": 1,
                "foo123": 1,
                "foo124": 1,
                "foo125": 1,
                "foo126": 1,
                "foo127": 1,
                "foo128": 1,
                "foo129": 1,
                "foo130": 1,
                "foo131": 1,
                "foo132": 1,
                "foo133": 1,
                "foo134": 1,
                "foo135": 1,
                "foo136": 1,
                "foo137": 1,
                "foo138": 1,
                "foo139": 1,
                "foo140": 1,
                "foo141": 1,
                "foo142": 1,
                "foo143": 1,
                "foo144": 1,
                "foo145": 1,
                "foo146": 1,
                "foo147": 1,
                "foo148": 1,
                "foo149": 1,
                "foo150": 1,
                "foo151": 1,
                "foo152": 1,
                "foo153": 1,
                "foo154": 1,
                "foo155": 1,
                "foo156": 1,
                "foo157": 1,
                "foo158": 1,
                "foo159": 1,
                "foo160": 1,
                "foo161": 1,
                "foo162": 1,
                "foo163": 1,
                "foo164": 1,
                "foo165": 1,
                "foo166": 1,
                "foo167": 1,
                "foo168": 1,
                "foo169": 1,
                "foo170": 1,
                "foo171": 1,
                "foo172": 1,
                "foo173": 1,
                "foo174": 1,
                "foo175": 1,
                "foo176": 1,
                "foo177": 1,
                "foo178": 1,
                "foo179": 1,
                "foo180": 1,
                "foo181": 1,
                "foo182": 1,
                "foo183": 1,
                "foo184": 1,
                "foo185": 1,
                "foo186": 1,
                "foo187": 1,
                "foo188": 1,
                "foo189": 1,
                "foo190": 1,
                "foo191": 1,
                "foo192": 1,
                "foo193": 1,
                "foo194": 1,
                "foo195": 1,
                "foo196": 1,
                "foo197": 1,
                "foo198": 1,
                "foo199": 1,
                "foo200": 1,
                "foo201": 1,
                "foo202": 1,
                "foo203": 1,
                "foo204": 1,
                "foo205": 1,
                "foo206": 1,
                "foo207": 1,
                "foo208": 1,
                "foo209": 1,
                "foo210": 1,
                "foo211": 1,
                "foo212": 1,
                "foo213": 1,
                "foo214": 1,
                "foo215": 1,
                "foo216": 1,
                "foo217": 1,
                "foo218": 1,
                "foo219": 1,
                "foo220": 1,
                "foo221": 1,
                "foo222": 1,
                "foo223": 1,
                "foo224": 1,
                "foo225": 1,
                "foo226": 1,
                "foo227": 1,
                "foo228": 1,
                "foo229": 1,
                "foo230": 1,
                "foo231": 1,
                "foo232": 1,
                "foo233": 1,
                "foo234": 1,
                "foo235": 1,
                "foo236": 1,
                "foo237": 1,
                "foo238": 1,
                "foo239": 1,
                "foo240": 1,
                "foo241": 1,
                "foo242": 1,
                "foo243": 1,
                "foo244": 1,
                "foo245": 1,
                "foo246": 1,
                "foo247": 1,
                "foo248": 1,
                "foo249": 1,
                "foo250": 1,
                "foo251": 1,
                "foo252": 1,
                "foo253": 1,
                "foo254": 1,
                "foo255": 1,
                "foo256": 1
            }
            """);
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestNonPresentRequiredPropertyIsInvalid()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{\"bar\": 1}");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresOtherNonObjects()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresNull()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIgnoresBoolean()
    {
        DynamicJsonElement dynamicInstance = _fixture.DynamicJsonType.ParseInstance("true");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\required.json",
                """
                {
                    "$schema": "https://json-schema.org/draft/2020-12/schema",
                    "properties": {
                        "foo": {},
                        "bar": {}
                    },

                    "required": [
                        "foo",
                        "foo1", "foo2", "foo3", "foo4", "foo5", "foo6", "foo7", "foo8",
                        "foo9", "foo10", "foo11", "foo12", "foo13", "foo14", "foo15", "foo16",
                        "foo17", "foo18", "foo19", "foo20", "foo21", "foo22", "foo23", "foo24",
                        "foo25", "foo26", "foo27", "foo28", "foo29", "foo30", "foo31", "foo32",
                        "foo33", "foo34", "foo35", "foo36", "foo37", "foo38", "foo39", "foo40",
                        "foo41", "foo42", "foo43", "foo44", "foo45", "foo46", "foo47", "foo48",
                        "foo49", "foo50", "foo51", "foo52", "foo53", "foo54", "foo55", "foo56",
                        "foo57", "foo58", "foo59", "foo60", "foo61", "foo62", "foo63", "foo64",
                        "foo65", "foo66", "foo67", "foo68", "foo69", "foo70", "foo71", "foo72",
                        "foo73", "foo74", "foo75", "foo76", "foo77", "foo78", "foo79", "foo80",
                        "foo81", "foo82", "foo83", "foo84", "foo85", "foo86", "foo87", "foo88",
                        "foo89", "foo90", "foo91", "foo92", "foo93", "foo94", "foo95", "foo96",
                        "foo97", "foo98", "foo99", "foo100", "foo101", "foo102", "foo103", "foo104",
                        "foo105", "foo106", "foo107", "foo108", "foo109", "foo110", "foo111", "foo112",
                        "foo113", "foo114", "foo115", "foo116", "foo117", "foo118", "foo119", "foo120",
                        "foo121", "foo122", "foo123", "foo124", "foo125", "foo126", "foo127", "foo128",
                        "foo129", "foo130", "foo131", "foo132", "foo133", "foo134", "foo135", "foo136",
                        "foo137", "foo138", "foo139", "foo140", "foo141", "foo142", "foo143", "foo144",
                        "foo145", "foo146", "foo147", "foo148", "foo149", "foo150", "foo151", "foo152",
                        "foo153", "foo154", "foo155", "foo156", "foo157", "foo158", "foo159", "foo160",
                        "foo161", "foo162", "foo163", "foo164", "foo165", "foo166", "foo167", "foo168",
                        "foo169", "foo170", "foo171", "foo172", "foo173", "foo174", "foo175", "foo176",
                        "foo177", "foo178", "foo179", "foo180", "foo181", "foo182", "foo183", "foo184",
                        "foo185", "foo186", "foo187", "foo188", "foo189", "foo190", "foo191", "foo192",
                        "foo193", "foo194", "foo195", "foo196", "foo197", "foo198", "foo199", "foo200",
                        "foo201", "foo202", "foo203", "foo204", "foo205", "foo206", "foo207", "foo208",
                        "foo209", "foo210", "foo211", "foo212", "foo213", "foo214", "foo215", "foo216",
                        "foo217", "foo218", "foo219", "foo220", "foo221", "foo222", "foo223", "foo224",
                        "foo225", "foo226", "foo227", "foo228", "foo229", "foo230", "foo231", "foo232",
                        "foo233", "foo234", "foo235", "foo236", "foo237", "foo238", "foo239", "foo240",
                        "foo241", "foo242", "foo243", "foo244", "foo245", "foo246", "foo247", "foo248",
                        "foo249", "foo250", "foo251", "foo252", "foo253", "foo254", "foo255", "foo256"
                    ]
                }
                """,
                "JsonSchemaTestSuite.Draft202012.LargeNumbersOfRequiredProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}