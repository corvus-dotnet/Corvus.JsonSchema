// Recipe 018 — Default values.
import { Settings } from "./generated.js";
const dec = new TextDecoder();
// `default` makes a property optional and documents its fallback ("light" / 14). Omitting it is valid.
console.log("empty valid:", Settings.evaluate(Settings.build({}))); // true
const s = Settings.parse(Settings.build({}));
// The default is not applied to the value — the property is simply absent; apply it at the read site.
console.log("theme:      ", s.theme ?? "light");
console.log("fontSize:   ", s.fontSize ?? 14);
