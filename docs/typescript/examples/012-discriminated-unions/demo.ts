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
const click = JSON.parse(dec.decode(Click.build({ type: "click", x: 10, y: 20 }))) as Event;
console.log("valid: ", Event.evaluate(click)); // true
console.log(describe(click));
console.log(describe(JSON.parse(dec.decode(KeyPress.build({ type: "keypress", key: "Enter" }))) as Event));
