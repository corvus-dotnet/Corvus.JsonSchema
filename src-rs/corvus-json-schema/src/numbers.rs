//! Exact numeric comparisons over `serde_json::Number` (integers stay exact; floats compare by value), and exact
//! `multipleOf` over decimal forms, matching the C# evaluator's normalised-decimal semantics.

use std::cmp::Ordering;

use serde_json::Number;

/// The value of a JSON number: an exact integer, or a double.
#[derive(Clone, Copy, Debug)]
pub(crate) enum Num {
    I(i128),
    F(f64),
}

impl Num {
    #[inline]
    pub fn of(n: &Number) -> Num {
        if let Some(i) = n.as_i64() {
            Num::I(i as i128)
        } else if let Some(u) = n.as_u64() {
            Num::I(u as i128)
        } else {
            let f = n.as_f64().unwrap_or(f64::NAN);
            // A double that holds an integer exactly (1.0, 1e3) is that integer.
            if f.fract() == 0.0 && f.abs() < 1.0e36 { Num::I(f as i128) } else { Num::F(f) }
        }
    }
}

/// Compares an integer with a double exactly.
fn cmp_int_float(i: i128, f: f64) -> Ordering {
    if f.is_nan() {
        return Ordering::Less;
    }
    if f >= 1.7e38 {
        return Ordering::Less;
    }
    if f <= -1.7e38 {
        return Ordering::Greater;
    }
    let fl = f.floor();
    let fi = fl as i128;
    match i.cmp(&fi) {
        Ordering::Equal => {
            if f > fl {
                Ordering::Less
            } else {
                Ordering::Equal
            }
        }
        o => o,
    }
}

#[inline]
pub(crate) fn cmp(a: Num, b: Num) -> Ordering {
    match (a, b) {
        (Num::I(x), Num::I(y)) => x.cmp(&y),
        (Num::I(x), Num::F(y)) => cmp_int_float(x, y),
        (Num::F(x), Num::I(y)) => cmp_int_float(y, x).reverse(),
        (Num::F(x), Num::F(y)) => x.partial_cmp(&y).unwrap_or(Ordering::Equal),
    }
}

/// JSON number equality (1 == 1.0).
#[inline]
pub(crate) fn num_eq(a: &Number, b: &Number) -> bool {
    cmp(Num::of(a), Num::of(b)) == Ordering::Equal
}

/// A decimal: mantissa × 10^exponent, from the shortest round-trip text of the number.
fn decimal(n: Num) -> Option<(i128, i32)> {
    match n {
        Num::I(i) => Some((i, 0)),
        Num::F(f) => {
            if !f.is_finite() {
                return None;
            }
            // `{:e}` gives the shortest round-trip digits: "1.25e-3", "5e0". Formatted on the stack.
            let mut buf = StackText::default();
            std::fmt::Write::write_fmt(&mut buf, format_args!("{f:e}")).ok()?;
            let s = buf.as_str();
            let (mant, exp) = s.split_once('e')?;
            let exp: i32 = exp.parse().ok()?;
            let neg = mant.starts_with('-');
            let mant = mant.trim_start_matches('-');
            let (int_part, frac) = mant.split_once('.').unwrap_or((mant, ""));
            if int_part.len() + frac.len() > 36 {
                return None;
            }
            let mut m: i128 = 0;
            for c in int_part.bytes().chain(frac.bytes()) {
                m = m * 10 + (c - b'0') as i128;
            }
            Some((if neg { -m } else { m }, exp - frac.len() as i32))
        }
    }
}

/// A short text formatted without allocating (a double's `{:e}` form is at most 24 bytes).
struct StackText {
    buf: [u8; 40],
    len: usize,
}

impl Default for StackText {
    fn default() -> Self {
        StackText { buf: [0; 40], len: 0 }
    }
}

impl StackText {
    fn as_str(&self) -> &str {
        std::str::from_utf8(&self.buf[..self.len]).unwrap_or("")
    }
}

impl std::fmt::Write for StackText {
    fn write_str(&mut self, s: &str) -> std::fmt::Result {
        let end = self.len + s.len();
        if end > self.buf.len() {
            return Err(std::fmt::Error);
        }
        self.buf[self.len..end].copy_from_slice(s.as_bytes());
        self.len = end;
        Ok(())
    }
}

fn gcd(mut a: u128, mut b: u128) -> u128 {
    while b != 0 {
        (a, b) = (b, a % b);
    }
    a
}

/// Whether `m * 10^shift` is divisible by `d` (`d > 0`), without forming the (possibly huge) product: after removing
/// the common factor of `m` and `d`, what remains of `d` must divide `10^shift`, so hold only 2s and 5s.
fn divides_scaled(m: u128, shift: u32, d: u128) -> bool {
    let mut rest = d / gcd(m, d);
    for f in [2u128, 5] {
        let mut count = 0u32;
        while rest % f == 0 {
            rest /= f;
            count += 1;
        }
        if count > shift {
            return false;
        }
    }
    rest == 1
}

/// A `multipleOf` divisor with its integer or decimal form worked out once (the C# evaluator's DivisorValue).
#[derive(Clone, Debug)]
pub(crate) struct Divisor {
    int: Option<i128>,
    /// Magnitude of the mantissa and the exponent: the divisor is `m * 10^e`.
    decimal: Option<(u128, i32)>,
}

impl Divisor {
    pub fn new(d: &Number) -> Divisor {
        let n = Num::of(d);
        Divisor {
            int: match n {
                Num::I(i) => Some(i),
                Num::F(_) => None,
            },
            decimal: decimal(n).map(|(m, e)| (m.unsigned_abs(), e)),
        }
    }

    /// Exact `multipleOf`: whether `x / d` is an integer, over the decimal forms of both numbers.
    pub fn divides(&self, x: &Number) -> bool {
        let xn = Num::of(x);
        if let (Num::I(a), Some(b)) = (xn, self.int) {
            return b != 0 && a % b == 0;
        }
        let (Some((am, ae)), Some((bm, be))) = (decimal(xn), self.decimal) else {
            return false;
        };
        let am = am.unsigned_abs();
        if bm == 0 {
            return false;
        }
        if am == 0 {
            return true;
        }
        let shift = ae - be;
        if shift >= 0 {
            divides_scaled(am, shift as u32, bm)
        } else {
            // am must be divisible by bm * 10^-shift; am < 10^37, so a larger power of ten cannot divide it.
            match 10u128.checked_pow((-shift) as u32).and_then(|p| bm.checked_mul(p)) {
                Some(den) => am % den == 0,
                None => false,
            }
        }
    }
}

/// Exact `multipleOf`: whether `x / d` is an integer, over the decimal forms of both numbers.
pub(crate) fn multiple_of(x: &Number, d: &Number) -> bool {
    Divisor::new(d).divides(x)
}

/// The number's text as C# formats it in messages.
pub(crate) fn number_text(n: &Number) -> String {
    n.to_string()
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn n(v: serde_json::Value) -> Number {
        v.as_number().unwrap().clone()
    }

    #[test]
    fn multiple_of_is_exact() {
        assert!(multiple_of(&n(json!(0.0075)), &n(json!(0.0001))));
        assert!(!multiple_of(&n(json!(0.00751)), &n(json!(0.0001))));
        assert!(multiple_of(&n(json!(4.5)), &n(json!(1.5))));
        assert!(!multiple_of(&n(json!(35)), &n(json!(1.5))));
        assert!(multiple_of(&n(json!(10)), &n(json!(5))));
        assert!(!multiple_of(&n(json!(1e308)), &n(json!(0.123456789))));
        assert!(multiple_of(&n(json!(1e308)), &n(json!(0.5))));
        assert!(multiple_of(&n(json!(0)), &n(json!(0.3))));
        assert!(!multiple_of(&n(json!(1e-300)), &n(json!(1e-7))));
    }

    #[test]
    fn comparisons_are_exact() {
        assert_eq!(
            cmp(Num::of(&n(json!(9007199254740993u64))), Num::of(&n(json!(9007199254740992.0)))),
            Ordering::Greater
        );
        assert_eq!(cmp(Num::of(&n(json!(1))), Num::of(&n(json!(1.0)))), Ordering::Equal);
        assert_eq!(cmp(Num::of(&n(json!(-1))), Num::of(&n(json!(-0.5)))), Ordering::Less);
    }
}
