// Recipe 012 — Discriminated unions (oneOf + const discriminator).
import { Click, Event, KeyPress } from "./generated.js";
const dec = new TextDecoder();
// A shared `type` discriminator -> Event = Click | KeyPress | Scroll, dispatched by Event.match.
const describe = (e: Event) =>
  Event.match(e, {
    click: (c) => `click at ${c.x},${c.y}`,
    keyPress: (k) => `key ${k.key}`,
    scroll: (s) => `scroll ${s.delta}`,
  });
const click = Event.parse(Click.build({ type: "click", x: 10, y: 20 }));
console.log("valid: ", Event.evaluate(click)); // true
console.log(describe(click));
console.log(describe(Event.parse(KeyPress.build({ type: "keypress", key: "Enter" }))));
