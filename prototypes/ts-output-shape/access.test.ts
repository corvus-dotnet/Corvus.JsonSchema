// ACCESS (design §5.1/§5.3): reading typed values — object props (required/optional/nested),
// array/tuple/labelled element access, and map get + index signature. In Model B "access" is
// structural property/element read on the validated plain value (no getters).
import { suite } from "./harness";
import { greet, type Person } from "./object-model";
import { firstTag, dot, head, type Tags, type Triple, type Labelled, type Vec3 } from "./array-tuple-model";
import { getScore, type Scores, type Config } from "./map-object-model";

const t = suite("access");

// --- objects: required, optional (?:), and nested property access (§5.1) ---
const ada: Person = { name: "Ada", address: { street: "1 St", city: "London" } };
const grace: Person = { name: "Grace", age: 85, address: { street: "2 St", city: "NYC", postcode: "10001" } };
t.eq("required prop", ada.name, "Ada");
t.eq("optional absent -> undefined", ada.age, undefined);
t.eq("optional present", grace.age, 85);
t.eq("nested required prop", grace.address.city, "NYC");
t.eq("nested optional present", grace.address.postcode, "10001");
t.eq("nested optional absent", ada.address.postcode, undefined);
t.eq("optional-driven read (greet, absent)", greet(ada), "Hi Ada");
t.eq("optional-driven read (greet, present)", greet(grace), "Hi Grace (age 85)");

// --- arrays / tuples: positional, typed-per-index, and length (§5.3) ---
const tags: Tags = ["a", "b", "c"];
t.eq("homogeneous array index", tags[1], "b");
t.eq("array length", tags.length, 3);
const triple: Triple = ["x", 7, true];
t.eq("tuple[0] (string) via helper", firstTag(triple), "x");
t.eq("tuple[1] (number)", triple[1], 7);
t.eq("tuple[2] (boolean)", triple[2], true);
const va: Vec3 = [1, 2, 3];
const vb: Vec3 = [4, 5, 6];
t.eq("Vec3 element access (dot)", dot(va, vb), 32);
const labelled: Labelled = ["head", 1, 2, 3];
t.eq("labelled head element", head(labelled), "head");
t.eq("labelled rest element", labelled[2], 2);

// --- map / dictionary: get + declared prop + index signature (§5.3) ---
const scores: Scores = { math: 90, cs: 100 };
t.eq("map get present", getScore(scores, "cs"), 100);
t.eq("map get absent -> undefined", getScore(scores, "nope"), undefined);
const cfg: Config = { name: "app", featureX: "on" };
t.eq("declared prop", cfg.name, "app");
t.eq("index-signature prop", cfg.featureX, "on");

t.done();
