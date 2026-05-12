# $formatNumber infinite loop with zero/negative values and exponent patterns

> Filed as [jsonata-js/jsonata#785](https://github.com/jsonata-js/jsonata/issues/785)

## Summary

`$formatNumber(0, "0e0")` and `$formatNumber(-42, "0e0")` cause an infinite loop in jsonata v2.1.0. Positive numbers with the same pattern work correctly (e.g., `$formatNumber(42, "0e0")` returns `"4e1"`).

## Version

jsonata v2.1.0

## Steps to Reproduce

```javascript
const jsonata = require('jsonata');

// This works correctly:
const r1 = await jsonata('$formatNumber(42, "0e0")').evaluate({});
console.log(r1); // "4e1"

// This hangs forever (infinite loop):
const r2 = await jsonata('$formatNumber(0, "0e0")').evaluate({});

// This also hangs forever:
const r3 = await jsonata('$formatNumber(-42, "0e0")').evaluate({});
```

## Expected Behavior

- `$formatNumber(0, "0e0")` should return `"0e0"`
- `$formatNumber(-42, "0e0")` should return `"-4e1"`

These are valid XPath/XQuery `format-number` picture strings with exponent sub-patterns. The exponent format should work regardless of the sign or zero value of the input number.

## Actual Behavior

The function enters an infinite loop and never returns. The process must be killed externally.

## Analysis

The issue appears to be in the formatting loop that handles the exponent normalization for zero and negative values. When the mantissa is zero or negative, the loop that adjusts the exponent never terminates.

Note: Uppercase `E` (e.g., `"0E0"`) is not recognized as an exponent separator by the reference implementation — it is treated as a passive character, which correctly triggers D3086. Only lowercase `e` triggers the exponent path.

## Workaround

Avoid using exponent patterns (`e`) with values that may be zero or negative. Pre-check the value before formatting:

```javascript
$number > 0 ? $formatNumber($number, "0e0") : $formatNumber($abs($number), "0e0")
```

(This workaround still fails for zero.)
