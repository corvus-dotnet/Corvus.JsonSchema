// Recipe 018 — Default values.
import { Settings } from "./generated.js";
// `default` makes a property optional and documents its fallback ("light" / 14). Omitting it is valid.
console.log("empty valid:", Settings.evaluate(Settings.build({}))); // true
// `Settings.defaults` is a readonly literal of the direct property defaults.
console.log("defaults:    ", JSON.stringify(Settings.defaults));
const s = Settings.parse(Settings.build({}));
// The raw value is untouched: an omitted property reads undefined. Read one default with `??`.
console.log("theme:       ", s.theme ?? Settings.defaults.theme);
console.log("fontSize:    ", s.fontSize ?? Settings.defaults.fontSize);
// `withDefaults` returns a copy with every absent default filled.
const filled = Settings.withDefaults(s);
console.log("withDefaults:", JSON.stringify(filled));
