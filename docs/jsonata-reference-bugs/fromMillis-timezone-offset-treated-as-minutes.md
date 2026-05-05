# $fromMillis timezone offset hours treated as minutes

## Summary

`$fromMillis` with a timezone offset string (third parameter) incorrectly treats the hours portion of the offset as minutes. For example, `+05:00` applies a 5-minute offset instead of a 5-hour offset. The minutes portion of the timezone string is silently ignored.

## Version

jsonata v2.1.0

## Steps to Reproduce

```javascript
const jsonata = require('jsonata');

// Epoch for 2021-01-01T00:00:00.000Z
const millis = 1609459200000;

const r1 = await jsonata('$fromMillis(1609459200000, undefined, "+05:00")').evaluate({});
console.log(r1);

const r2 = await jsonata('$fromMillis(1609459200000, undefined, "-05:30")').evaluate({});
console.log(r2);
```

## Expected Behavior

```
2021-01-01T05:00:00.000+05:00
2020-12-31T18:30:00.000-05:30
```

The offset `+05:00` means 5 hours ahead of UTC. The offset `-05:30` means 5 hours and 30 minutes behind UTC.

## Actual Behavior

```
2021-01-01T00:05:00.000+00:05
2020-12-31T22:55:00.000-01:05
```

The implementation treats the hours value (5) as minutes, and ignores the minutes value entirely:
- `+05:00` → applies +5 minutes instead of +5 hours
- `-05:30` → applies -65 minutes (-1h05m) instead of -5h30m

The output timezone label is also wrong: it shows `+00:05` instead of `+05:00`.

## Analysis

The timezone parsing appears to read the hours field but apply it as minutes, and the colon-separated minutes field is not parsed at all. This suggests the parsing code reads the first numeric component after `+`/`-` and passes it directly to a minutes-based offset calculation without multiplying by 60.

## Workaround

Avoid the timezone parameter in `$fromMillis`. Instead, compute the offset manually:

```javascript
(
  $tz_hours := 5;
  $tz_minutes := 0;
  $offset_ms := ($tz_hours * 3600000) + ($tz_minutes * 60000);
  $fromMillis(1609459200000 + $offset_ms)
)
```
