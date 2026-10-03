package io.github.corvusdotnet.jsonschema;

import java.math.BigInteger;

/**
 * Decimal to double conversion without allocation: the Eisel-Lemire algorithm (Daniel Lemire, "Number Parsing at a
 * Gigabyte per Second", and the fast_float library), which is exact for a decimal significand of up to 19 digits
 * (Noble Mushtak and Daniel Lemire, "Fast Number Parsing Without Fallback"). A longer significand is truncated and the
 * result accepted when the truncated and the next value round to the same double; otherwise the caller falls back.
 */
final class FastDouble {
    private FastDouble() {
    }

    private static final int SMALLEST_POWER_OF_TEN = -342;
    private static final int LARGEST_POWER_OF_TEN = 308;
    private static final int MANTISSA_EXPLICIT_BITS = 52;
    private static final int MINIMUM_EXPONENT = -1023;
    private static final int INFINITE_POWER = 0x7ff;
    private static final int MIN_EXPONENT_ROUND_TO_EVEN = -4;
    private static final int MAX_EXPONENT_ROUND_TO_EVEN = 23;

    /** The 128-bit truncated powers of five from 5^-342 to 5^308, high word then low word, normalised. */
    private static final long[] POWERS = powers();

    private static long[] powers() {
        long[] table = new long[2 * (LARGEST_POWER_OF_TEN - SMALLEST_POWER_OF_TEN + 1)];
        BigInteger two128 = BigInteger.ONE.shiftLeft(128);
        BigInteger two127 = BigInteger.ONE.shiftLeft(127);
        int at = 0;
        for (int q = SMALLEST_POWER_OF_TEN; q <= LARGEST_POWER_OF_TEN; q++) {
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

    /** The high 64 bits of the unsigned 128-bit product of a and b. */
    private static long unsignedMultiplyHigh(long a, long b) {
        return Math.multiplyHigh(a, b) + ((a >> 63) & b) + ((b >> 63) & a);
    }

    /**
     * The bits of the double nearest {@code w * 10^q} (w an unsigned significand), or -1 when it cannot be decided
     * here (never for an exact significand).
     */
    static long bits(long w, int q) {
        if (w == 0 || q < SMALLEST_POWER_OF_TEN) {
            return 0;
        }
        if (q > LARGEST_POWER_OF_TEN) {
            return (long) INFINITE_POWER << MANTISSA_EXPLICIT_BITS;
        }
        int lz = Long.numberOfLeadingZeros(w);
        w <<= lz;
        int index = 2 * (q - SMALLEST_POWER_OF_TEN);
        long high = unsignedMultiplyHigh(w, POWERS[index]);
        long low = w * POWERS[index];
        long precisionMask = 0xffffffffffffffffL >>> (MANTISSA_EXPLICIT_BITS + 3);
        if ((high & precisionMask) == precisionMask) {
            long secondHigh = unsignedMultiplyHigh(w, POWERS[index + 1]);
            low += secondHigh;
            if (Long.compareUnsigned(secondHigh, low) > 0) {
                high++;
            }
        }
        int upperbit = (int) (high >>> 63);
        int shift = upperbit + 64 - MANTISSA_EXPLICIT_BITS - 3;
        long mantissa = high >>> shift;
        int power2 = (int) ((((152170 + 65536) * (long) q) >> 16) + 63) + upperbit - lz - MINIMUM_EXPONENT;
        if (power2 <= 0) {
            // Subnormal.
            if (-power2 + 1 >= 64) {
                return 0;
            }
            mantissa >>>= -power2 + 1;
            mantissa += mantissa & 1;
            mantissa >>>= 1;
            power2 = Long.compareUnsigned(mantissa, 1L << MANTISSA_EXPLICIT_BITS) < 0 ? 0 : 1;
            return ((long) power2 << MANTISSA_EXPLICIT_BITS) | (mantissa & ((1L << MANTISSA_EXPLICIT_BITS) - 1));
        }
        // A product exactly halfway between two doubles rounds to even.
        if (Long.compareUnsigned(low, 1) <= 0 && q >= MIN_EXPONENT_ROUND_TO_EVEN && q <= MAX_EXPONENT_ROUND_TO_EVEN
                && (mantissa & 3) == 1) {
            if ((mantissa << shift) == high) {
                mantissa &= ~1L;
            }
        }
        mantissa += mantissa & 1;
        mantissa >>>= 1;
        if (Long.compareUnsigned(mantissa, 2L << MANTISSA_EXPLICIT_BITS) >= 0) {
            mantissa = 1L << MANTISSA_EXPLICIT_BITS;
            power2++;
        }
        mantissa &= ~(1L << MANTISSA_EXPLICIT_BITS);
        if (power2 >= INFINITE_POWER) {
            return (long) INFINITE_POWER << MANTISSA_EXPLICIT_BITS;
        }
        return ((long) power2 << MANTISSA_EXPLICIT_BITS) | mantissa;
    }

    /**
     * The double of the JSON number text in b[start, end) (already validated), or NaN when it must be decided by the
     * slow path (a significand of more than 19 digits whose truncation is ambiguous).
     */
    static double parse(byte[] b, int start, int end) {
        int j = start;
        boolean negative = b[j] == '-';
        if (negative) {
            j++;
        }
        long w = 0;
        int digits = 0;
        long exponent = 0;
        boolean truncated = false;
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
            if (digits == 0 && c == '0') {
                // Leading zeros count only as a fraction's places.
                if (fraction) {
                    exponent--;
                }
                continue;
            }
            if (digits < 19) {
                w = w * 10 + (c - '0');
                digits++;
                if (fraction) {
                    exponent--;
                }
            } else {
                // Beyond 19 digits: dropped, but an integer part's dropped digits still scale the value.
                truncated |= c != '0';
                if (!fraction) {
                    exponent++;
                }
            }
        }
        if (j < end && (b[j] == 'e' || b[j] == 'E')) {
            j++;
            boolean expNegative = false;
            if (b[j] == '+' || b[j] == '-') {
                expNegative = b[j] == '-';
                j++;
            }
            long e = 0;
            for (; j < end && b[j] >= '0' && b[j] <= '9'; j++) {
                if (e < 1_000_000) {
                    e = e * 10 + (b[j] - '0');
                }
            }
            exponent += expNegative ? -e : e;
        }
        int q = (int) Math.max(Math.min(exponent, 100_000), -100_000);
        long bits = bits(w, q);
        if (truncated && bits(w + 1, q) != bits) {
            return Double.NaN;
        }
        double d = Double.longBitsToDouble(bits);
        return negative ? -d : d;
    }
}