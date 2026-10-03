package io.github.corvusdotnet.jsonschema;

import static io.github.corvusdotnet.jsonschema.JsonDocument.NUM_DOUBLE;
import static io.github.corvusdotnet.jsonschema.JsonDocument.NUM_LONG;
import static io.github.corvusdotnet.jsonschema.JsonDocument.NUM_U64;

import java.math.BigDecimal;
import java.math.MathContext;
import java.math.RoundingMode;

/**
 * Exact numeric comparisons over the parser's number representations (a long, an unsigned long beyond a long, or a
 * double): integers stay exact and doubles compare by value, so {@code 1 == 1.0} and {@code 9007199254740993 >
 * 9007199254740992.0}. {@code multipleOf} is decided exactly on decimal forms, so {@code 0.0075} is a multiple of
 * {@code 0.0001}.
 */
final class Numbers {
    private Numbers() {
    }

    private static final double TWO_63 = 0x1p63;
    private static final double TWO_64 = 0x1p64;

    static double u64ToDouble(long bits) {
        // An unsigned long at or above 2^63: halve, convert and double, keeping the rounding bit.
        return ((double) ((bits >>> 1) | (bits & 1))) * 2.0;
    }

    /** Compares two numbers exactly: negative, zero or positive. */
    static int compare(int fa, long a, int fb, long b) {
        if (fa == NUM_LONG) {
            if (fb == NUM_LONG) {
                return Long.compare(a, b);
            }
            if (fb == NUM_DOUBLE) {
                return compareLongDouble(a, Double.longBitsToDouble(b));
            }
            return -1;
        }
        if (fa == NUM_DOUBLE) {
            double x = Double.longBitsToDouble(a);
            if (fb == NUM_DOUBLE) {
                double y = Double.longBitsToDouble(b);
                return x < y ? -1 : x > y ? 1 : 0;
            }
            if (fb == NUM_LONG) {
                return -compareLongDouble(b, x);
            }
            return -compareU64Double(b, x);
        }
        // An unsigned long beyond a long.
        if (fb == NUM_U64) {
            return Long.compareUnsigned(a, b);
        }
        if (fb == NUM_LONG) {
            return 1;
        }
        return compareU64Double(a, Double.longBitsToDouble(b));
    }

    /** Compares a long with a (finite) double exactly. */
    static int compareLongDouble(long a, double d) {
        if (d >= TWO_63) {
            return -1;
        }
        if (d < -TWO_63) {
            return 1;
        }
        double floor = Math.floor(d);
        int c = Long.compare(a, (long) floor);
        if (c != 0) {
            return c;
        }
        return d > floor ? -1 : 0;
    }

    /** Compares an unsigned long at or above 2^63 with a (finite) double exactly. */
    private static int compareU64Double(long u, double d) {
        if (d >= TWO_64) {
            return -1;
        }
        if (d < TWO_63) {
            return 1;
        }
        // Doubles in [2^63, 2^64) are integers.
        long bits = ((long) (d - TWO_63)) ^ Long.MIN_VALUE;
        return Long.compareUnsigned(u, bits);
    }

    /** Whether a number is an integer (what the {@code integer} type accepts). */
    static boolean isInteger(int flag, long bits) {
        if (flag != NUM_DOUBLE) {
            return true;
        }
        double d = Double.longBitsToDouble(bits);
        return d == Math.floor(d) && !Double.isInfinite(d);
    }

    /** The number as an exact decimal: an integer exactly, a double as its shortest round-trip decimal. */
    static BigDecimal decimal(int flag, long bits) {
        switch (flag) {
            case NUM_LONG:
                return BigDecimal.valueOf(bits);
            case NUM_U64:
                return new BigDecimal(Long.toUnsignedString(bits));
            default:
                return shortest(Double.longBitsToDouble(bits));
        }
    }

    /** From Java 19, {@code Double.toString} gives the shortest decimal that rounds to the double. */
    private static final boolean SHORTEST_TO_STRING = Runtime.version().feature() >= 19;

    /** The shortest decimal that rounds to the double (the decimal JSON text that produced it, normalised). */
    static BigDecimal shortest(double d) {
        if (SHORTEST_TO_STRING) {
            return new BigDecimal(Double.toString(d));
        }
        // Before Java 19 Double.toString sometimes gives more digits than needed: round the exact binary value to
        // ever more digits until it reads back as the double.
        BigDecimal exact = new BigDecimal(d);
        for (int p = 1; p < 17; p++) {
            BigDecimal r = exact.round(new MathContext(p, RoundingMode.HALF_EVEN));
            if (r.doubleValue() == d) {
                return r;
            }
        }
        return exact.round(new MathContext(17, RoundingMode.HALF_EVEN));
    }

    /** A {@code multipleOf} divisor with its integer or decimal form worked out once. */
    static final class Divisor {
        private final boolean isLong;
        private final long value;
        private final BigDecimal decimal;

        Divisor(int flag, long bits) {
            isLong = flag == NUM_LONG;
            value = bits;
            decimal = decimal(flag, bits);
        }

        /** Exact {@code multipleOf}: whether x / divisor is an integer, over the decimal forms of both numbers. */
        boolean divides(int flag, long bits) {
            if (isLong && flag == NUM_LONG) {
                return value != 0 && bits % value == 0;
            }
            if (decimal.signum() == 0) {
                return false;
            }
            BigDecimal x = decimal(flag, bits);
            if (x.signum() == 0) {
                return true;
            }
            return x.remainder(decimal).signum() == 0;
        }
    }
}