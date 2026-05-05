# $formatNumber crashes with undefined error code for certain invalid picture strings

## Summary

Several invalid `$formatNumber` picture strings cause an unhandled crash with `code: undefined` instead of a proper error code. The crash occurs at `splitParts` (jsonata.js:2196) when the regex-based picture parsing fails before reaching the validation phase.

## Version

jsonata v2.1.0

## Steps to Reproduce

```javascript
const jsonata = require('jsonata');

// All of these crash with code: undefined
const patterns = [
  '%%',       // multiple percent signs (no digit chars)
  '\u2030\u2030', // multiple per-mille signs (no digit chars)
  '%\u2030',  // mixed percent + per-mille (no digit chars)
  '---',      // no digit characters at all
];

for (const p of patterns) {
  try {
    await jsonata(`$formatNumber(42, "${p}")`).evaluate({});
  } catch (e) {
    console.log(`Pattern "${p}": code=${e.code}, message=${e.message}`);
  }
}
```

## Expected Behavior

Each pattern should throw a well-defined error:

| Pattern | Expected Error | Reason |
|---------|---------------|--------|
| `%%` | D3082 or similar | Multiple percent markers in picture |
| `‰‰` | D3083 or similar | Multiple per-mille markers in picture |
| `%‰` | D3084 or similar | Mixed percent and per-mille markers |
| `---` | D3085 | No active digit characters in picture |

Per the XPath/XQuery specification (which JSONata's `$formatNumber` is based on), these are all invalid picture strings that should produce specific, documented error codes.

## Actual Behavior

```
Pattern "%%": code=undefined, message=Cannot read properties of undefined (reading 'length')
Pattern "‰‰": code=undefined, message=Cannot read properties of undefined (reading 'length')
Pattern "%‰": code=undefined, message=Cannot read properties of undefined (reading 'length')
Pattern "---": code=undefined, message=Cannot read properties of undefined (reading 'length')
```

The error code is `undefined` and the message is a raw JavaScript TypeError from attempting to access `.length` on an undefined value.

## Analysis

The `splitParts` regex at jsonata.js:2196 fails to match when the picture string contains only passive/special characters and no digit placeholders (`0` or `#`). When the regex returns no match, the subsequent code attempts to destructure the result and accesses properties of `undefined`, leading to the TypeError.

Note: When these special characters appear alongside digit characters (e.g., `"0%0%0"`), the regex matches successfully and the validation phase fires D3086 ("passive character between active characters"). The crash only occurs when the picture has NO digit characters at all.

## Workaround

Validate picture strings before passing them to `$formatNumber`. Ensure the picture contains at least one digit placeholder character (`0` or `#`).

## Notes

Patterns with digits that also have multiple percent/per-mille markers (e.g., `"0%0%0"`) do not crash — they throw D3086 (passive character between active characters). While D3086 is technically correct, a more specific error code identifying the multiple-marker condition would be more helpful for users diagnosing picture string problems.
