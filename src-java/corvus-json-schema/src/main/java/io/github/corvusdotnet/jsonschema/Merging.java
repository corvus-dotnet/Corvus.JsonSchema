package io.github.corvusdotnet.jsonschema;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.function.IntPredicate;

/**
 * Finds structurally identical nodes, whose compiled methods would be identical, so that one method serves them all
 * (the TypeScript generator's merging by partition refinement, and the C# compiler's canonical equivalent subschemas).
 * Fewer methods means less for the JIT to compile: a large schema reaches its steady state sooner and compiles faster.
 *
 * <p>Nodes start in classes by what their own keywords say; the classes are then refined by the classes of the nodes
 * their children are evaluated against, until no class splits. Two nodes left in one class check the same thing in
 * the same way, so either method serves both.
 */
final class Merging {
    private Merging() {
    }

    /** For each node, the node whose method serves it. */
    static int[] representatives(Program p, IntPredicate fallback) {
        SchemaNode[] nodes = p.nodes;
        int count = nodes.length;
        int[] cls = new int[count];
        Map<String, Integer> initial = new HashMap<>();
        int[][] children = new int[count][];
        for (int i = 0; i < count; i++) {
            String key = fallback.test(i) ? "#" + i : key(nodes[i]);
            cls[i] = initial.computeIfAbsent(key, k -> initial.size());
            children[i] = children(p, nodes[i]);
        }
        int classes = initial.size();
        while (true) {
            Map<List<Integer>, Integer> refined = new HashMap<>();
            int[] next = new int[count];
            for (int i = 0; i < count; i++) {
                List<Integer> signature = new ArrayList<>(children[i].length + 1);
                signature.add(cls[i]);
                for (int c : children[i]) {
                    signature.add(cls[c]);
                }
                next[i] = refined.computeIfAbsent(signature, k -> refined.size());
            }
            cls = next;
            if (refined.size() == classes) {
                break;
            }
            classes = refined.size();
        }
        int[] firstOfClass = new int[classes];
        Arrays.fill(firstOfClass, -1);
        int[] representative = new int[count];
        for (int i = 0; i < count; i++) {
            if (firstOfClass[cls[i]] < 0) {
                firstOfClass[cls[i]] = i;
            }
            representative[i] = firstOfClass[cls[i]];
        }
        return representative;
    }

    /** The nodes a node's children are evaluated against, in a fixed order. */
    private static int[] children(Program p, SchemaNode n) {
        List<Integer> out = new ArrayList<>();
        add(p, out, n.ref);
        add(p, out, n.staticDynamicRef);
        addAll(p, out, n.allOf);
        addAll(p, out, n.anyOf);
        addAll(p, out, n.oneOf);
        add(p, out, n.not);
        add(p, out, n.ifNode);
        add(p, out, n.thenNode);
        add(p, out, n.elseNode);
        if (n.properties != null) {
            for (SchemaNode.Property prop : n.properties) {
                add(p, out, prop.node);
            }
        }
        if (n.patternProperties != null) {
            for (SchemaNode.PatternProperty pp : n.patternProperties) {
                add(p, out, pp.node);
            }
        }
        add(p, out, n.additionalProperties);
        add(p, out, n.propertyNames);
        add(p, out, n.items);
        addAll(p, out, n.prefixItems);
        add(p, out, n.contains);
        add(p, out, n.unevaluatedProperties);
        add(p, out, n.unevaluatedItems);
        if (n.dependencies != null) {
            for (SchemaNode.Dependency d : n.dependencies) {
                add(p, out, d.schema);
            }
        }
        return out.stream().mapToInt(Integer::intValue).toArray();
    }

    private static void add(Program p, List<Integer> out, int id) {
        if (id >= 0) {
            out.add(p.fastTarget[id]);
        }
    }

    private static void addAll(Program p, List<Integer> out, int[] ids) {
        if (ids != null) {
            for (int id : ids) {
                out.add(p.fastTarget[id]);
            }
        }
    }

    private static String json(SchemaNode.Value v) {
        return v == null ? "-" : v.toJson();
    }

    private static String num(SchemaNode.Num n) {
        return n == null ? "-" : n.text;
    }

    /** What a node's own keywords say (everything its method's code depends on, but its children's identities). */
    private static String key(SchemaNode n) {
        StringBuilder k = new StringBuilder(64);
        k.append(n.alwaysTrue ? 'T' : n.alwaysFalse ? 'F' : 'n');
        k.append('|').append(n.hasType ? n.typeMask : -1);
        k.append('|').append(json(n.constValue));
        if (n.enumValues != null) {
            k.append("|e");
            for (SchemaNode.Value v : n.enumValues) {
                k.append(',').append(v.toJson());
            }
        }
        k.append('|').append(n.ref >= 0).append(n.staticDynamicRef >= 0);
        k.append('|').append(n.allOf != null ? n.allOf.length : -1);
        k.append('|').append(n.anyOf != null ? n.anyOf.length : -1);
        k.append('|').append(n.oneOf != null ? n.oneOf.length : -1);
        k.append('|').append(n.not >= 0).append(n.ifNode >= 0).append(n.thenNode >= 0).append(n.elseNode >= 0);
        if (n.properties != null) {
            k.append("|p");
            for (SchemaNode.Property prop : n.properties) {
                k.append(',').append(prop.name.length()).append(':').append(prop.name);
            }
        }
        if (n.patternProperties != null) {
            k.append("|pp");
            for (SchemaNode.PatternProperty pp : n.patternProperties) {
                k.append(',').append(pp.pattern.source.length()).append(':').append(pp.pattern.source);
            }
        }
        k.append('|').append(n.additionalProperties >= 0).append(n.propertyNames >= 0);
        if (n.required != null) {
            k.append("|r");
            for (String r : n.required) {
                k.append(',').append(r.length()).append(':').append(r);
            }
        }
        if (n.dependencies != null) {
            k.append("|d");
            for (SchemaNode.Dependency d : n.dependencies) {
                k.append(',').append(d.name.length()).append(':').append(d.name).append(d.schema >= 0);
                if (d.required != null) {
                    for (String r : d.required) {
                        k.append(';').append(r.length()).append(':').append(r);
                    }
                }
            }
        }
        k.append('|').append(n.minProperties).append(',').append(n.maxProperties);
        k.append('|').append(n.prefixItems != null ? n.prefixItems.length : -1).append(n.items >= 0);
        k.append('|').append(n.contains >= 0).append(',').append(n.minContains).append(',').append(n.maxContains);
        k.append('|').append(n.minItems).append(',').append(n.maxItems).append(n.uniqueItems);
        k.append('|').append(n.minLength).append(',').append(n.maxLength);
        k.append('|').append(n.pattern != null ? n.pattern.source.length() + ":" + n.pattern.source : "-");
        k.append('|').append(n.format != null ? n.format.length() + ":" + n.format : "-").append(n.formatKind)
                .append(n.assertFormat).append(n.dialect.compareTo(Dialect.DRAFT6) <= 0);
        k.append('|').append(n.content).append(n.assertContent);
        k.append('|').append(num(n.minimum)).append(',').append(num(n.maximum)).append(',')
                .append(num(n.exclusiveMinimum)).append(',').append(num(n.exclusiveMaximum)).append(',')
                .append(num(n.multipleOf));
        k.append('|').append(n.unevaluatedProperties >= 0).append(n.unevaluatedItems >= 0);
        k.append('|').append(discriminator(n.oneOfDiscriminator)).append(discriminator(n.anyOfDiscriminator));
        return k.toString();
    }

    private static String discriminator(SchemaNode.Discriminator d) {
        if (d == null) {
            return "-";
        }
        StringBuilder k = new StringBuilder("D").append(d.property.length()).append(':').append(d.property)
                .append(d.allRequire);
        for (int i = 0; i < d.values.length; i++) {
            k.append(',').append(d.values[i].toJson()).append('=').append(Arrays.toString(d.branches[i]));
        }
        return k.append('/').append(Arrays.toString(d.unknown)).toString();
    }
}