// Recipe 012 — Discriminated unions (oneOf + const discriminator).
import { type Event, evaluateRoot, buildClick, buildKeyPress, matchEvent } from "./generated.js";
const dec = new TextDecoder();
// A shared `type` discriminator -> Event = Click | KeyPress | Scroll, dispatched by matchEvent.
const describe = (e: Event) =>
  matchEvent(e, {
    click: (c) => `click at ${c.x},${c.y}`,
    keyPress: (k) => `key ${k.key}`,
    scroll: (s) => `scroll ${s.delta}`,
  });
const click = JSON.parse(dec.decode(buildClick({ type: "click", x: 10, y: 20 }))) as Event;
console.log("valid: ", evaluateRoot(click)); // true
console.log(describe(click));
console.log(describe(JSON.parse(dec.decode(buildKeyPress({ type: "keypress", key: "Enter" }))) as Event));
