// Unit tests for the middleware composition core (run with `node --test`), against the BUILT dist.
import { test } from "node:test";
import assert from "node:assert/strict";

import { compose } from "../dist/middleware/handler.js";

function wire() {
  return { method: "GET", url: "https://x/y", headers: new Headers(), body: { kind: "none" }, signal: AbortSignal.timeout(1000) };
}

test("compose runs handlers outer-to-inner around the terminal", async () => {
  const order = [];
  const trace = (name) => async (req, next) => {
    order.push(`>${name}`);
    const res = await next(req);
    order.push(`<${name}`);
    return res;
  };
  const terminal = async () => {
    order.push("send");
    return { statusCode: 204, headers: new Headers(), body: null };
  };

  const res = await compose([trace("a"), trace("b")], terminal)(wire());
  assert.equal(res.statusCode, 204);
  // a is outermost, b is inner, send is innermost.
  assert.deepEqual(order, [">a", ">b", "send", "<b", "<a"]);
});

test("compose with no handlers calls the terminal directly", async () => {
  const terminal = async () => ({ statusCode: 200, headers: new Headers(), body: null });
  const res = await compose([], terminal)(wire());
  assert.equal(res.statusCode, 200);
});

test("a handler can short-circuit without calling next", async () => {
  let sent = false;
  const shortCircuit = async () => ({ statusCode: 418, headers: new Headers(), body: null });
  const terminal = async () => {
    sent = true;
    return { statusCode: 200, headers: new Headers(), body: null };
  };
  const res = await compose([shortCircuit], terminal)(wire());
  assert.equal(res.statusCode, 418);
  assert.equal(sent, false);
});
