// Runnable demo for recipe 003 — References ($ref / $defs).
import { Address, Order } from "./generated.js";

const dec = new TextDecoder();

// `shipTo` and `billTo` both `$ref` the same `#/$defs/address` -> ONE shared `Address` interface.
// Define an address once and reuse it.
const home: Address = { line1: "1 Mill Rd", city: "Cambridge", postcode: "CB1 2AB" };
const bytes = Order.build({ id: "ord-1", shipTo: home, billTo: home });
console.log("order:     ", dec.decode(bytes));
console.log("valid:     ", Order.evaluate(bytes)); // true

// Read through the reference — shipTo IS an Address.
const order = Order.parse(bytes);
console.log("ship city: ", order.shipTo.city); // Cambridge

// Patch a referenced sub-object (the whole member value is replaced; the rest is copied through).
const moved = Order.patch(bytes, {
  shipTo: { line1: "5 King's Parade", city: "Cambridge", postcode: "CB2 1ST" },
});
console.log("moved:     ", dec.decode(moved));
