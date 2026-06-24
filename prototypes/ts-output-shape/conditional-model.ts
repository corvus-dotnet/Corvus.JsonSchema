// What the generator emits for if/then/else (and dependentSchemas) — design §5.3/§5.4.
// Type-checked under --strict.
//
// The TYPE stays the base object shape: TS cannot express "if the instance matches I, it must also
// match T". The conditional is enforced by the generated VALIDATOR, not the type system.

import { assertNever } from "./runtime"; // in real output: from "@corvus/json-runtime"

export interface Payment {
  readonly method: "card" | "bank";
  readonly cardNumber?: string; // `then`: required when method === "card"
  readonly iban?: string; // `else`: required when method !== "card"
}

// the generated validator is what actually enforces the if/then/else (here a runnable reference: the
// `then` branch (method === "card") requires cardNumber; the `else` branch requires iban):
export function validatePayment(value: unknown): boolean {
  if (typeof value !== "object" || value === null) { return false; }
  const o = value as Record<string, unknown>;
  if (o.method !== "card" && o.method !== "bank") { return false; }
  return o.method === "card" ? typeof o.cardNumber === "string" : typeof o.iban === "string";
}

// This type-checks at compile time but the VALIDATOR rejects it (card with no cardNumber):
const typeOkButInvalid: Payment = { method: "card" };

// Optional convenience overlay: when if/then/else is discriminator-shaped, the generator MAY also
// emit a refined discriminated union so consumers get compile-time narrowing too (design §5.2).
export interface CardPayment {
  readonly method: "card";
  readonly cardNumber: string;
}
export interface BankPayment {
  readonly method: "bank";
  readonly iban: string;
}
export type PaymentRefined = CardPayment | BankPayment;

export function describePayment(p: PaymentRefined): string {
  switch (p.method) {
    case "card":
      return `card ending ${p.cardNumber.slice(-4)}`;
    case "bank":
      return `bank ${p.iban}`;
    default:
      return assertNever(p);
  }
}

export const demoConditional: ReadonlyArray<unknown> = [
  validatePayment(typeOkButInvalid),
  describePayment({ method: "card", cardNumber: "4111111111111111" }),
  describePayment({ method: "bank", iban: "GB00..." }),
];
