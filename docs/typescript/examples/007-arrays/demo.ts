// Recipe 007 — Strongly typed arrays.
import { type Cart, evaluateRoot, buildCart, produceCart } from "./generated.js";
const dec = new TextDecoder();
const bytes = buildCart({ items: [{ sku: "A1", qty: 2 }, { sku: "B2", qty: 1 }] });
console.log("valid:    ", evaluateRoot(JSON.parse(dec.decode(bytes)))); // true
const cart = JSON.parse(dec.decode(bytes)) as Cart;
console.log("first sku:", cart.items[0].sku);                          // A1
console.log("total qty:", cart.items.reduce((n, i) => n + i.qty, 0));  // 3
console.log("empty:    ", evaluateRoot({ items: [] }));                // false — minItems 1
const more = produceCart(bytes, (d) => { d.items.push({ sku: "C3", qty: 5 }); });
console.log("appended: ", dec.decode(more));
