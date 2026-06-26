// Recipe 008 — Arrays of higher rank.
import { type Grid, evaluateRoot, buildGrid } from "./generated.js";
const dec = new TextDecoder();
const bytes = buildGrid({ cells: [[1, 2, 3], [4, 5, 6]] });
console.log("valid:   ", evaluateRoot(JSON.parse(dec.decode(bytes)))); // true
const grid = JSON.parse(dec.decode(bytes)) as Grid;
console.log("cell 1,2:", grid.cells[1][2]); // 6 — typed readonly (readonly number[])[]
