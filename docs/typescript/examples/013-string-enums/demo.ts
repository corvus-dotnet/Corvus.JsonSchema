// Recipe 013 — String enumerations.
import { Status, Task } from "./generated.js";
const dec = new TextDecoder();
const bytes = Task.build({ status: "in_progress", priority: "high" });
console.log("valid:    ", Task.evaluate(bytes)); // true
const task = Task.parse(bytes);
// status is the literal union "todo" | "in_progress" | "done".
const label: Record<Status, string> = { todo: "To do", in_progress: "In progress", done: "Done" };
console.log("label:    ", label[task.status]);
console.log("bad value:", Task.evaluate({ status: "archived" })); // false — not an enum member
