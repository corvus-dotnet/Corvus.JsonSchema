package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;

import java.math.BigInteger;
import java.nio.charset.StandardCharsets;
import java.util.Random;
import org.junit.jupiter.api.Test;

/** Decimal to double conversion agrees with Double.parseDouble, bit for bit. */
class FastDoubleTest {
    /** The table of powers of five, computed with BigInteger, as the generated constants were. */
    private static long[] computePowers() {
        long[] table = new long[2 * (308 + 342 + 1)];
        BigInteger two128 = BigInteger.ONE.shiftLeft(128);
        BigInteger two127 = BigInteger.ONE.shiftLeft(127);
        int at = 0;
        for (int q = -342; q <= 308; q++) {
            BigInteger c;
            if (q < 0) {
                BigInteger power5 = BigInteger.valueOf(5).pow(-q);
                int z = power5.subtract(BigInteger.ONE).bitLength();
                if (q >= -27) {
                    c = BigInteger.ONE.shiftLeft(z + 127).divide(power5).add(BigInteger.ONE);
                } else {
                    c = BigInteger.ONE.shiftLeft(2 * z + 128).divide(power5).add(BigInteger.ONE);
                    while (c.compareTo(two128) >= 0) {
                        c = c.shiftRight(1);
                    }
                }
            } else {
                c = BigInteger.valueOf(5).pow(q);
                while (c.compareTo(two127) < 0) {
                    c = c.shiftLeft(1);
                }
                while (c.compareTo(two128) >= 0) {
                    c = c.shiftRight(1);
                }
            }
            table[at++] = c.shiftRight(64).longValue();
            table[at++] = c.longValue();
        }
        return table;
    }

    @Test
    void powerTableMatchesItsComputation() {
        assertArrayEquals(computePowers(), FastDoubleTable.POWERS);
    }

    private static void check(String text) {
        byte[] b = text.getBytes(StandardCharsets.ISO_8859_1);
        double d = FastDouble.parse(b, 0, b.length);
        if (!Double.isNaN(d)) {
            assertEquals(Double.doubleToRawLongBits(Double.parseDouble(text)), Double.doubleToRawLongBits(d), text);
        }
        // And through the parser, which rejects numbers beyond the range of a double.
        if (Double.isInfinite(Double.parseDouble(text))) {
            org.junit.jupiter.api.Assertions.assertThrows(JsonParseException.class, () -> JsonDocument.parse(text));
            return;
        }
        JsonDocument doc = JsonDocument.parse(text);
        double parsed = doc.doubleValue(doc.root());
        assertEquals(Double.parseDouble(text), parsed, 0.0, text);
    }

    @Test
    void edgeCases() {
        String[] cases = {
            "0", "-0", "0.0", "1", "1.5", "0.1", "0.3", "1e23", "8.98846567431158e307", "1.7976931348623157e308",
            "2.2250738585072014e-308", "2.2250738585072011e-308", "4.9e-324", "5e-324", "2.4703282292062327e-324",
            "2.4703282292062328e-324", "1e-400", "-90.50242899999999", "9007199254740993", "123456789012345678901234567890",
            "0.000000000000000000000000000000000000000001", "1.00000000000000011102230246251565404236316680908203125",
            "1.00000000000000011102230246251565404236316680908203124", "7.2057594037927933e16", "3.14159265358979323846",
            "1e308", "1e-308", "12.34", "0.0075", "1e300", "1e301", "-1e-7",
        };
        for (String c : cases) {
            check(c);
        }
    }

    @Test
    void randomDoubles() {
        Random random = new Random(11);
        for (int i = 0; i < 200_000; i++) {
            double d = Double.longBitsToDouble(random.nextLong());
            if (Double.isNaN(d) || Double.isInfinite(d)) {
                continue;
            }
            check(Double.toString(d).replace("E", "e"));
        }
        for (int i = 0; i < 200_000; i++) {
            StringBuilder sb = new StringBuilder();
            sb.append(1 + random.nextInt(9));
            int digits = random.nextInt(25);
            for (int k = 0; k < digits; k++) {
                sb.append(random.nextInt(10));
            }
            if (random.nextBoolean()) {
                sb.insert(1 + random.nextInt(sb.length()), '.');
                if (sb.charAt(sb.length() - 1) == '.') {
                    sb.append('0');
                }
            }
            sb.append('e').append(random.nextInt(640) - 330);
            check(sb.toString());
        }
    }
}
