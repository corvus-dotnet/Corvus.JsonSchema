import io.github.corvusdotnet.jsonschema.CompileOptions
import io.github.corvusdotnet.jsonschema.Dialect
import io.github.corvusdotnet.jsonschema.JsonDocument
import io.github.corvusdotnet.jsonschema.JsonSchemaResultsCollector
import io.github.corvusdotnet.jsonschema.ResultsLevel
import io.github.corvusdotnet.jsonschema.Validator
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

/** The library as Kotlin code uses it. */
class KotlinSmokeTest {
    private val schema = """
        {"type": "object", "properties": {"id": {"type": "integer", "minimum": 1}}, "required": ["id"]}
    """.trimIndent()

    @Test
    fun validates() {
        val validator = Validator.compile(schema)
        assertTrue(validator.isValid("""{"id": 3}"""))
        assertFalse(validator.isValid("""{"id": 0}"""))
        assertTrue(validator.isValid(JsonDocument.parse("""{"id": 9}""")))
    }

    @Test
    fun optionsAndResults() {
        val options = CompileOptions.builder()
            .defaultDialect(Dialect.DRAFT7)
            .assertFormat(true)
            .format("even") { it.length % 2 == 0 }
            .build()
        val validator = Validator.compile("""{"format": "even"}""", options)
        assertTrue(validator.isValid("\"ab\""))
        assertFalse(validator.isValid("\"abc\""))

        val collector = JsonSchemaResultsCollector.create(ResultsLevel.DETAILED)
        assertFalse(Validator.compile(schema).evaluate(JsonDocument.parse("{}"), collector))
        assertEquals("/required", collector.results().last().evaluationLocation())
    }
}
