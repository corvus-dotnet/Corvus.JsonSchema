// PROVIDER REAL-OUTPUT test for the Temporal value accessors the provider now emits (gap B2, §5.3.1): the
// FOUR RFC 3339 temporal string formats (date / date-time / time / duration) get — IN ADDITION to the
// branded string + `as{Name}` factory — a `{name}AsTemporal` accessor that parses the branded value into its
// matching Temporal type (date -> PlainDate, date-time -> the absolute Instant, time -> PlainTime, duration
// -> Duration), delegating to the shared-runtime converters (toPlainDate/toInstant/toPlainTime/toDuration).
// Non-temporal formats (uuid here) get NO AsTemporal accessor. Run after generating temporal.json into
// out-temporal/:
//   Codegen (temporal.json -> out-temporal/), transpile, and run are all driven by ../run-access.sh.
import * as gen from "./out-temporal/generated.js";
import {
  asDay,
  dayAsTemporal,
  asOccurredAt,
  occurredAtAsTemporal,
  asAtTime,
  atTimeAsTemporal,
  asSpan,
  spanAsTemporal,
} from "./out-temporal/generated.js";
// Temporal is re-exported from the shared runtime (the generated module imports it but does not re-export),
// so consumers get the Temporal types from the package — see the `export { Temporal }` in the runtime.
import { Temporal } from "./out-temporal/corvus-runtime.js";

let pass = 0;
let fail = 0;
function ok(label: string, cond: boolean): void {
  if (cond) { pass++; }
  else { fail++; console.log(`FAIL ${label}`); }
}

// date -> Temporal.PlainDate (year/month/day parsed)
const d = dayAsTemporal(asDay("2020-01-02"));
ok("date accessor returns a PlainDate", d instanceof Temporal.PlainDate);
ok("date parses year", d.year === 2020);
ok("date parses month", d.month === 1);
ok("date parses day", d.day === 2);

// date-time -> Temporal.Instant (the absolute instant; epoch ms parsed from the offset-bearing string)
const inst = occurredAtAsTemporal(asOccurredAt("2020-01-02T03:04:05Z"));
ok("date-time accessor returns an Instant", inst instanceof Temporal.Instant);
ok("date-time parses to the right epoch", inst.epochMilliseconds === 1577934245000);

// time -> Temporal.PlainTime (the wall-clock time; the zone/offset is dropped). The runtime converter
// normalises a trailing `Z` to +00:00 (Temporal.PlainTime.from rejects a bare `Z`), so BOTH a numeric-offset
// and a `Z`-suffixed time parse — a valid RFC 3339 `time` may use either.
const t = atTimeAsTemporal(asAtTime("13:14:15+02:00"));
ok("time accessor returns a PlainTime", t instanceof Temporal.PlainTime);
ok("time parses hour", t.hour === 13);
ok("time parses minute/second", t.minute === 14 && t.second === 15);
const tz = atTimeAsTemporal(asAtTime("13:14:15Z"));
ok("time accessor handles a Z offset (normalised, not thrown)", tz instanceof Temporal.PlainTime && tz.hour === 13 && tz.second === 15);

// duration -> Temporal.Duration (units parsed)
const dur = spanAsTemporal(asSpan("P1Y2M3DT4H5M6S"));
ok("duration accessor returns a Duration", dur instanceof Temporal.Duration);
ok("duration parses units", dur.years === 1 && dur.months === 2 && dur.days === 3 && dur.hours === 4 && dur.minutes === 5 && dur.seconds === 6);

// a non-temporal string format (uuid) gets NO AsTemporal accessor on the generated module.
ok("uuid format gets no AsTemporal accessor", typeof (gen as Record<string, unknown>).idAsTemporal === "undefined");

console.log(`temporal-access: ${pass} passed, ${fail} failed`);
if (fail > 0) { throw new Error(`temporal-access: ${fail} failed`); }
console.log("OK — provider emits Temporal value accessors for date/date-time/time/duration; parse + type are correct.");
