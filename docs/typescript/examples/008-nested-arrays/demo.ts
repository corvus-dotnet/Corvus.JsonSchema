// Recipe 008 — Arrays of higher rank.
import { Grid } from "./generated.js";
const dec = new TextDecoder();
const bytes = Grid.build({ cells: [[1, 2, 3], [4, 5, 6]] });
console.log("valid:   ", Grid.evaluate(JSON.parse(dec.decode(bytes)))); // true
const grid = JSON.parse(dec.decode(bytes)) as Grid;
console.log("cell 1,2:", grid.cells[1][2]); // 6 — typed readonly (readonly number[])[]
