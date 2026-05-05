# $fromMillis timezone offset — RETRACTED (not a bug)

> Filed as [jsonata-js/jsonata#786](https://github.com/jsonata-js/jsonata/issues/786) — **SHOULD BE CLOSED**

## Resolution

This is **not** a bug in the reference implementation. The JSONata specification defines the timezone
parameter format as `±HHMM` (without a colon separator), not `±HH:MM`. Our original test was passing
the wrong format (`"+05:30"` instead of `"+0530"`).

With the correct format, the reference implementation produces the expected results:

```javascript
$fromMillis(0, undefined, "+0530")  // → "1970-01-01T05:30:00.000+05:30" ✓
$fromMillis(0, undefined, "-0800")  // → "1969-12-31T16:00:00.000-08:00" ✓
```

The Corvus implementation accepts both `±HHMM` and `±HH:MM` formats for backward compatibility,
but tests now use the spec-compliant `±HHMM` format.
