package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.nio.charset.StandardCharsets;
import org.junit.jupiter.api.Test;

/** Numbers compare exactly as the decimals their JSON text writes, beyond what a double holds. */
class NumbersTest {
    private static final String[][] SCHEMA_CASES = {
        {"{\"const\": 0.123456789123456789}", "0.123456789123456789", "true"},
        {"{\"const\": 0.123456789123456789}", "0.123456789123456788", "false"},
        {"{\"enum\": [0.123456789123456789]}", "0.123456789123456790", "false"},
        {"{\"maximum\": 972783798187987123879878123.18878137}", "972783798187987123879878123.188781371", "false"},
        {"{\"exclusiveMaximum\": 972783798187987123879878123.18878137}", "972783798187987123879878123.188781369",
            "true"},
        {"{\"minimum\": -18446744073709551615}", "-18446744073709551616", "false"},
        {"{\"exclusiveMinimum\": -972783798187987123879878123.18878137}", "-972783798187987123879878123.188781369",
            "true"},
        {"{\"uniqueItems\": true}", "[0.123456789123456788, 0.123456789123456789]", "true"},
        {"{\"uniqueItems\": true}", "[1, 1.0]", "false"},
    };

    @Test
    void highPrecisionNumbersCompareExactly() {
        for (String[] c : SCHEMA_CASES) {
            Validator v = Validator.compile(c[0]);
            boolean want = Boolean.parseBoolean(c[2]);
            assertEquals(want, v.isValid(c[1]), c[1] + " against " + c[0]);
            JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.create(ResultsLevel.BASIC);
            boolean collected = v.evaluate(JsonDocument.parse(c[1]), collector);
            assertEquals(want, collected, c[1] + " against " + c[0] + ", collecting");
        }
    }

    @Test
    void textsCompareAsDecimals() {
        String[][] cases = {
            {"0.123456789123456788", "0.123456789123456789", "-1"},
            {"0.12345678912345678900", "0.123456789123456789", "0"},
            {"-18446744073709551616", "-18446744073709551615", "-1"},
            {"18446744073709551616", "18446744073709551615", "1"},
            {"1152921504606846977", "1152921504606846977.5", "-1"},
            {"1152921504606846977", "1152921504606846976.5", "1"},
            {"1152921504606846977", "1152921504606846977.0", "0"},
            {"18446744073709551615", "18446744073709551615.5", "-1"},
            {"1", "1.0", "0"},
            {"100", "1e2", "0"},
            {"0.00", "0", "0"},
            {"-0", "0", "0"},
            {"-0.0", "0.0e5", "0"},
            {"1e300", "1E+300", "0"},
            {"0.01e2", "1", "0"},
            {"12.5e-1", "1.25", "0"},
            {"1e-400", "0", "1"},
            {"-1e-400", "0", "-1"},
            {"9.99", "10", "-1"},
        };
        for (String[] c : cases) {
            JsonDocument a = JsonDocument.parse(c[0]);
            JsonDocument b = JsonDocument.parse(c[1]);
            int want = Integer.parseInt(c[2]);
            assertEquals(want, Integer.signum(Numbers.compare(a, a.root(), b, b.root())), c[0] + " vs " + c[1]);
            assertEquals(-want, Integer.signum(Numbers.compare(b, b.root(), a, a.root())), c[1] + " vs " + c[0]);
            byte[] at = c[0].getBytes(StandardCharsets.US_ASCII);
            byte[] bt = c[1].getBytes(StandardCharsets.US_ASCII);
            int text = Integer.signum(Numbers.compareText(at, 0, at.length, bt, 0, bt.length));
            assertEquals(want, text, c[0] + " as text vs " + c[1]);
        }
    }
}
