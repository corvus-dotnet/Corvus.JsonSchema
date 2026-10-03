package io.github.corvusdotnet.jsonschema;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Objects;
import java.util.Set;

/**
 * The evaluated properties or items of a node with {@code unevaluatedProperties}/{@code unevaluatedItems}, when they
 * are known at compile time (the TypeScript generator's static and guarded coverage, after the C# fused object plan):
 * declared names, patterns, a prefix of items, or everything. Then the keyword needs no tracking at run time: it
 * applies to the members the coverage leaves out.
 */
final class Coverage {
    final Set<String> names = new LinkedHashSet<>();
    final List<SchemaPattern> patterns = new ArrayList<>();
    int prefix;
    boolean all;

    /** A condition a guarded coverage applies under: an {@code if} schema's result, or a property's presence. */
    static final class Guard {
        /** The {@code if} node, or -1 for a dependency's property. */
        final int ifNode;
        /** Whether the {@code if} must pass (or fail). */
        final boolean holds;
        /** The dependency's property name, or null. */
        final String property;

        Guard(int ifNode, boolean holds, String property) {
            this.ifNode = ifNode;
            this.holds = holds;
            this.property = property;
        }

        @Override
        public boolean equals(Object o) {
            if (!(o instanceof Guard)) {
                return false;
            }
            Guard g = (Guard) o;
            return ifNode == g.ifNode && holds == g.holds && Objects.equals(property, g.property);
        }

        @Override
        public int hashCode() {
            return Objects.hash(ifNode, holds, property);
        }
    }

    /** A coverage that applies when all its guards hold. */
    static final class Guarded {
        final List<Guard> guards;
        final Coverage coverage;

        Guarded(List<Guard> guards, Coverage coverage) {
            this.guards = guards;
            this.coverage = coverage;
        }
    }

    /** The unconditional coverage and the guarded coverages that add to it. */
    static final class Result {
        final Coverage main;
        final List<Guarded> guarded;

        Result(Coverage main, List<Guarded> guarded) {
            this.main = main;
            this.guarded = guarded;
        }
    }

    /** At most this many guarded coverages; beyond it, tracking at run time is cheaper. */
    private static final int MAX_GUARDED = 32;

    private void addPattern(SchemaPattern p) {
        if (!patterns.contains(p)) {
            patterns.add(p);
        }
    }

    /** Whether everything this coverage covers, {@code main} covers too. */
    private boolean within(Coverage main, boolean items) {
        if (main.all) {
            return true;
        }
        if (all) {
            return false;
        }
        if (items) {
            return prefix <= main.prefix;
        }
        return main.names.containsAll(names) && main.patterns.containsAll(patterns);
    }

    private static boolean marks(Program p, int id, boolean object) {
        SchemaNode c = p.nodes[p.fastTarget[id]];
        return object ? c.marksProperties : c.marksItems;
    }

    /**
     * The coverage of node {@code id} for objects or arrays when every contributor is unconditional ($ref and allOf
     * chains), or adds nothing beyond them; null otherwise.
     */
    static Coverage ofStatic(Program p, int id, boolean object) {
        Coverage main = new Coverage();
        List<Coverage> conditional = new ArrayList<>();
        if (!visitStatic(p, id, true, main, conditional, new HashSet<>(), object)) {
            return null;
        }
        for (Coverage c : conditional) {
            if (!c.within(main, !object)) {
                return null;
            }
        }
        return main;
    }

    private static boolean visitStatic(Program p, int id, boolean root, Coverage coverage, List<Coverage> conditional,
            Set<Integer> visited, boolean object) {
        SchemaNode m = p.nodes[id];
        if (!visited.add(id) || m.alwaysTrue || m.alwaysFalse) {
            return true;
        }
        if (m.inPlaceCycle) {
            return false;
        }
        // Conditional contributors (a branch may pass or fail) need a static coverage of their own, which must add
        // nothing to the unconditional one.
        List<Integer> branches = new ArrayList<>();
        addAll(branches, m.anyOf);
        addAll(branches, m.oneOf);
        add(branches, m.ifNode);
        add(branches, m.thenNode);
        add(branches, m.elseNode);
        if (m.dependencies != null) {
            for (SchemaNode.Dependency d : m.dependencies) {
                add(branches, d.schema);
            }
        }
        if (m.dynamicRef != null) {
            return false;
        }
        for (int c : branches) {
            if (!marks(p, c, object)) {
                continue;
            }
            Coverage sub = new Coverage();
            if (!visitStatic(p, p.fastTarget[c], false, sub, conditional, new HashSet<>(), object)) {
                return false;
            }
            conditional.add(sub);
        }
        if (object) {
            if (m.properties != null) {
                for (SchemaNode.Property prop : m.properties) {
                    coverage.names.add(prop.name);
                }
            }
            if (m.patternProperties != null) {
                for (SchemaNode.PatternProperty pp : m.patternProperties) {
                    coverage.addPattern(pp.pattern);
                }
            }
            if (m.additionalProperties >= 0 || (!root && m.unevaluatedProperties >= 0)) {
                coverage.all = true;
            }
        } else {
            coverage.prefix = Math.max(coverage.prefix, m.prefixItems != null ? m.prefixItems.length : 0);
            if (m.items >= 0 || (!root && m.unevaluatedItems >= 0)) {
                coverage.all = true;
            }
            if (m.contains >= 0 && m.containsMarksEvaluated) {
                return false;
            }
        }
        return visitDirect(p, m, coverage, conditional, visited, object, null, null);
    }

    private static boolean visitDirect(Program p, SchemaNode m, Coverage coverage, List<Coverage> conditional,
            Set<Integer> visited, boolean object, List<Guard> guards, GuardedState state) {
        List<Integer> direct = new ArrayList<>();
        add(direct, m.ref);
        add(direct, m.staticDynamicRef);
        addAll(direct, m.allOf);
        for (int c : direct) {
            if (!marks(p, c, object)) {
                continue;
            }
            boolean ok = state == null
                    ? visitStatic(p, p.fastTarget[c], false, coverage, conditional, visited, object)
                    : state.visit(p.fastTarget[c], false, coverage, guards, visited);
            if (!ok) {
                return false;
            }
        }
        return true;
    }

    /**
     * Coverage for objects with guards: like {@link #ofStatic}, but a contribution under {@code if}/{@code then}/
     * {@code else} or a dependency's schema is kept with the conditions it applies under, so that an object's
     * evaluated names are the unconditional coverage plus every guarded coverage whose guards hold. anyOf and oneOf
     * contributions must still add nothing. Null when that fails, a contributor is on an in-place cycle, or there are
     * too many guarded coverages.
     */
    static Result ofGuarded(Program p, int id) {
        GuardedState state = new GuardedState(p, p.nodes[id].resourceId);
        Coverage main = new Coverage();
        if (!state.visit(id, true, main, new ArrayList<>(), new HashSet<>())) {
            return null;
        }
        for (Coverage c : state.unguarded) {
            if (!c.within(main, false)) {
                return null;
            }
        }
        List<Guarded> adding = new ArrayList<>();
        for (Guarded g : state.guarded) {
            if (!g.coverage.within(main, false)) {
                adding.add(g);
            }
        }
        return adding.size() <= MAX_GUARDED ? new Result(main, adding) : null;
    }

    private static final class GuardedState {
        final Program p;
        final int resource;
        final List<Guarded> guarded = new ArrayList<>();
        final List<Coverage> unguarded = new ArrayList<>();

        GuardedState(Program p, int resource) {
            this.p = p;
            this.resource = resource;
        }

        private boolean branch(int c, List<Guard> guards) {
            Coverage sub = new Coverage();
            if (guards == null) {
                unguarded.add(sub);
            } else {
                guarded.add(new Guarded(guards, sub));
            }
            return visit(p.fastTarget[c], false, sub, guards, new HashSet<>());
        }

        private static List<Guard> with(List<Guard> guards, Guard g) {
            if (guards == null) {
                return null;
            }
            List<Guard> out = new ArrayList<>(guards);
            out.add(g);
            return out;
        }

        /** {@code guards} is null inside an anyOf/oneOf branch, where a guard would also need the branch to pass. */
        boolean visit(int id, boolean root, Coverage coverage, List<Guard> guards, Set<Integer> visited) {
            SchemaNode m = p.nodes[id];
            if (!visited.add(id) || m.alwaysTrue || m.alwaysFalse) {
                return true;
            }
            // Below a live dynamic scope, a guard in another resource would be evaluated in another scope.
            if (m.inPlaceCycle || m.dynamicRef != null || (p.usesDynamicScope && m.resourceId != resource)) {
                return false;
            }
            List<Integer> alternatives = new ArrayList<>();
            addAll(alternatives, m.anyOf);
            addAll(alternatives, m.oneOf);
            for (int c : alternatives) {
                if (marks(p, c, true) && !branch(c, null)) {
                    return false;
                }
            }
            if (m.ifNode >= 0) {
                int[][] parts = {{m.ifNode, 1}, {m.thenNode, 1}, {m.elseNode, 0}};
                for (int[] part : parts) {
                    if (part[0] >= 0 && marks(p, part[0], true)
                            && !branch(part[0], with(guards, new Guard(m.ifNode, part[1] == 1, null)))) {
                        return false;
                    }
                }
            }
            if (m.dependencies != null) {
                for (SchemaNode.Dependency d : m.dependencies) {
                    if (d.schema >= 0 && marks(p, d.schema, true)
                            && !branch(d.schema, with(guards, new Guard(-1, true, d.name)))) {
                        return false;
                    }
                }
            }
            if (m.properties != null) {
                for (SchemaNode.Property prop : m.properties) {
                    coverage.names.add(prop.name);
                }
            }
            if (m.patternProperties != null) {
                for (SchemaNode.PatternProperty pp : m.patternProperties) {
                    coverage.addPattern(pp.pattern);
                }
            }
            if (m.additionalProperties >= 0 || (!root && m.unevaluatedProperties >= 0)) {
                coverage.all = true;
            }
            return visitDirect(p, m, coverage, null, visited, true, guards, this);
        }
    }

    private static void add(List<Integer> out, int id) {
        if (id >= 0) {
            out.add(id);
        }
    }

    private static void addAll(List<Integer> out, int[] ids) {
        if (ids != null) {
            for (int id : ids) {
                out.add(id);
            }
        }
    }
}