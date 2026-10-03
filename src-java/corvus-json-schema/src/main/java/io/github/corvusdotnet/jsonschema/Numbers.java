package io.github.corvusdotnet.jsonschema;

import static io.github.corvusdotnet.jsonschema.JsonDocument.NUM_DOUBLE;
import static io.github.corvusdotnet.jsonschema.JsonDocument.NUM_LONG;
import static io.github.corvusdotnet.jsonschema.JsonDocument.NUM_U64;

import java.math.BigDecimal;

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

    /**
     * A {@code multipleOf} divisor, as the decimal its JSON text writes: a significand without trailing zeros and an
     * exponent. {@code x} is a multiple when {@code x / divisor} is an integer, decided exactly on the decimal digits of
     * x's own text, with long arithmetic only (no allocation): the C# evaluator's decimal semantics.
     */
    static final class Divisor {
        private final boolean isLong;
        private final long value;
        /** The significand (at most 18 digits), or -1 when the divisor's digits do not fit. */
        private final long significand;
        private final int exponent;
        private final BigDecimal decimal;

        Divisor(JsonDocument d, int n) {
            int flag = d.flags(n);
            isLong = flag == NUM_LONG;
            value = d.data(n);
            long[] parsed = new long[2];
            if (decimalOf(d.source, d.count(n), d.sourceEnd, parsed)) {
                significand = parsed[0];
                exponent = (int) parsed[1];
            } else {
                significand = -1;
                exponent = 0;
            }
            decimal = new BigDecimal(d.numberText(n));
        }

        /** Exact {@code multipleOf} of the number value x of a document. */
        boolean divides(JsonDocument d, int x) {
            int flag = d.flags(x);
            long bits = d.data(x);
            if (isLong && flag == NUM_LONG) {
                return value != 0 && bits % value == 0;
            }
            if (significand == 0) {
                return false;
            }
            if (significand > 0) {
                int r = dividesText(d.source, d.count(x), d.sourceEnd, significand, exponent);
                if (r >= 0) {
                    return r == 1;
                }
            }
            // A divisor of more than 18 significant digits, or an exponent beyond an int.
            BigDecimal v = new BigDecimal(d.numberText(x));
            return v.signum() == 0 || v.remainder(decimal).signum() == 0;
        }
    }

    /**
     * Reads the decimal of the number text at {@code start}: out[0] = significand without trailing zeros (at most 18
     * digits), out[1] = exponent. False when the significand does not fit or the exponent is out of range.
     */
    static boolean decimalOf(byte[] b, int start, int end, long[] out) {
        int j = start;
        if (b[j] == '-') {
            j++;
        }
        long m = 0;
        int digits = 0;
        long exponent = 0;
        int pendingZeros = 0;
        boolean fraction = false;
        for (; j < end; j++) {
            int c = b[j];
            if (c == '.') {
                fraction = true;
                continue;
            }
            if (c < '0' || c > '9') {
                break;
            }
            if (fraction) {
                exponent--;
            }
            if (c == '0') {
                // Trailing zeros are held back, so that they move into the exponent if no other digit follows.
                if (digits > 0) {
                    pendingZeros++;
                }
                continue;
            }
            for (; pendingZeros > 0; pendingZeros--) {
                if (digits >= 18) {
                    return false;
                }
                m *= 10;
                digits++;
            }
            if (digits >= 18) {
                return false;
            }
            m = m * 10 + (c - '0');
            digits++;
        }
        exponent += pendingZeros;
        if (j < end && (b[j] == 'e' || b[j] == 'E')) {
            j++;
            boolean negative = false;
            if (b[j] == '+' || b[j] == '-') {
                negative = b[j] == '-';
                j++;
            }
            long e = 0;
            for (; j < end && b[j] >= '0' && b[j] <= '9'; j++) {
                if (e < 1_000_000_000L) {
                    e = e * 10 + (b[j] - '0');
                }
            }
            exponent += negative ? -e : e;
        }
        if (exponent > Integer.MAX_VALUE / 2 || exponent < Integer.MIN_VALUE / 2) {
            return false;
        }
        out[0] = m;
        out[1] = exponent;
        return true;
    }

    /**
     * Whether the number text at {@code start} is a multiple of {@code dm * 10^de} (dm positive, no trailing zeros):
     * 1 or 0, or -1 when the exponents are out of range. The text's digits are streamed modulo what remains of the
     * divisor, so the text may have any number of digits.
     */
    static int dividesText(byte[] b, int start, int limit, long dm, int de) {
        // x = xm * 10^xe with xm the digit string (trailing zeros moved into xe). x / d is an integer exactly when
        // dm divides xm * 10^(xe - de).
        int j = start;
        if (b[j] == '-') {
            j++;
        }
        int digitsStart = j;
        long exponent = 0;
        int lastNonZero = -1;
        boolean fraction = false;
        boolean any = false;
        int end = j;
        for (; end < limit; end++) {
            int c = b[end];
            if (c == '.') {
                fraction = true;
                continue;
            }
            if (c < '0' || c > '9') {
                break;
            }
            if (fraction) {
                exponent--;
            }
            if (c != '0') {
                lastNonZero = end;
                any = true;
            }
        }
        if (!any) {
            // Zero is a multiple of everything.
            return 1;
        }
        // Digits after the last non-zero one are trailing zeros: they move into the exponent.
        int k = end;
        if (end < limit && (b[end] == 'e' || b[end] == 'E')) {
            k++;
            boolean negative = false;
            if (b[k] == '+' || b[k] == '-') {
                negative = b[k] == '-';
                k++;
            }
            long e = 0;
            for (; k < limit && b[k] >= '0' && b[k] <= '9'; k++) {
                if (e < 1_000_000_000L) {
                    e = e * 10 + (b[k] - '0');
                }
            }
            exponent += negative ? -e : e;
        }
        for (int t = lastNonZero + 1; t < end; t++) {
            if (b[t] != '.') {
                exponent++;
            }
        }
        long shift = exponent - de;
        if (shift < 0) {
            // dm * 10^-shift must divide xm, which has no trailing zero, so is not a multiple of 10.
            return 0;
        }
        if (shift > Integer.MAX_VALUE) {
            return -1;
        }
        // Remove from dm the factors of 2 and 5 that 10^shift supplies; what remains must divide xm.
        long rest = dm;
        for (int twos = 0; twos < shift && (rest & 1) == 0; twos++) {
            rest >>>= 1;
        }
        for (int fives = 0; fives < shift && rest % 5 == 0; fives++) {
            rest /= 5;
        }
        if (rest == 1) {
            return 1;
        }
        long r = 0;
        for (int t = digitsStart; t <= lastNonZero; t++) {
            int c = b[t];
            if (c == '.') {
                continue;
            }
            // r < rest <= 10^18, so r * 10 + 9 < 2^64: unsigned arithmetic.
            r = Long.remainderUnsigned(r * 10 + (c - '0'), rest);
        }
        return r == 0 ? 1 : 0;
    }
}
