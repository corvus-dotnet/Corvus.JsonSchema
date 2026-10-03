package io.github.corvusdotnet.jsonschema;

import static org.objectweb.asm.Opcodes.ACC_FINAL;
import static org.objectweb.asm.Opcodes.ACC_PUBLIC;
import static org.objectweb.asm.Opcodes.ACC_STATIC;
import static org.objectweb.asm.Opcodes.ACC_SUPER;
import static org.objectweb.asm.Opcodes.ALOAD;
import static org.objectweb.asm.Opcodes.ARRAYLENGTH;
import static org.objectweb.asm.Opcodes.ASTORE;
import static org.objectweb.asm.Opcodes.GOTO;
import static org.objectweb.asm.Opcodes.IADD;
import static org.objectweb.asm.Opcodes.IALOAD;
import static org.objectweb.asm.Opcodes.ICONST_0;
import static org.objectweb.asm.Opcodes.ICONST_1;
import static org.objectweb.asm.Opcodes.IFEQ;
import static org.objectweb.asm.Opcodes.IFGE;
import static org.objectweb.asm.Opcodes.IFGT;
import static org.objectweb.asm.Opcodes.IFLE;
import static org.objectweb.asm.Opcodes.IFLT;
import static org.objectweb.asm.Opcodes.IFNE;
import static org.objectweb.asm.Opcodes.IFNULL;
import static org.objectweb.asm.Opcodes.IF_ICMPEQ;
import static org.objectweb.asm.Opcodes.IF_ICMPGE;
import static org.objectweb.asm.Opcodes.IF_ICMPLE;
import static org.objectweb.asm.Opcodes.IF_ICMPLT;
import static org.objectweb.asm.Opcodes.IF_ICMPNE;
import static org.objectweb.asm.Opcodes.ILOAD;
import static org.objectweb.asm.Opcodes.INVOKESPECIAL;
import static org.objectweb.asm.Opcodes.INVOKESTATIC;
import static org.objectweb.asm.Opcodes.IRETURN;
import static org.objectweb.asm.Opcodes.ISTORE;
import static org.objectweb.asm.Opcodes.LCMP;
import static org.objectweb.asm.Opcodes.LLOAD;
import static org.objectweb.asm.Opcodes.LOR;
import static org.objectweb.asm.Opcodes.LSTORE;
import static org.objectweb.asm.Opcodes.RETURN;
import static org.objectweb.asm.Opcodes.V17;

import io.github.corvusdotnet.jsonschema.SchemaNode.Dependency;
import io.github.corvusdotnet.jsonschema.SchemaNode.PatternProperty;
import io.github.corvusdotnet.jsonschema.SchemaNode.Property;
import java.lang.invoke.MethodHandles;
import java.lang.invoke.MethodType;
import java.nio.charset.StandardCharsets;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashSet;
import java.util.IdentityHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import org.objectweb.asm.ClassWriter;
import org.objectweb.asm.ConstantDynamic;
import org.objectweb.asm.Handle;
import org.objectweb.asm.Label;
import org.objectweb.asm.MethodTooLargeException;
import org.objectweb.asm.MethodVisitor;
import org.objectweb.asm.Opcodes;

/**
 * Compiles a {@link Program} to JVM bytecode: a hidden class with one static method per schema node, containing
 * exactly the checks that node needs, with its children called directly (or tested inline when they are only a type).
 * The JIT then specialises each schema as it would hand-written code, where an interpreter loop would leave it with
 * polymorphic dispatch.
 *
 * <p>Like the TypeScript port's code generator, the methods are fail-fast. Nodes whose evaluation needs state the
 * methods do not keep (evaluated properties and items for {@code unevaluated*}, the depth guard of an in-place cycle)
 * run through the {@link Evaluator}, and so does a schema with a live dynamic scope.
 */
final class CodeGen {
    private CodeGen() {
    }

    /** A compiled schema's entry point. */
    interface Compiled {
        boolean validate(Evaluator e, JsonDocument d, int x);
    }

    private static final String PKG = "io/github/corvusdotnet/jsonschema/";
    private static final String EV = "L" + PKG + "Evaluator;";
    private static final String DOC = "L" + PKG + "JsonDocument;";
    private static final String RT = PKG + "Rt";
    private static final String NODE_DESC = "(" + EV + DOC + "I)Z";
    private static final Handle CLASS_DATA_AT = new Handle(
            Opcodes.H_INVOKESTATIC,
            "java/lang/invoke/MethodHandles",
            "classDataAt",
            "(Ljava/lang/invoke/MethodHandles$Lookup;Ljava/lang/String;Ljava/lang/Class;I)Ljava/lang/Object;",
            false);

    /** Names up to this many are dispatched by length and words (unless the method is too large); more by hashing. */
    private static final int MAX_WORD_DISPATCH = 512;

    /**
     * HotSpot does not compile methods of more than 8000 bytes of bytecode (DontCompileHugeMethods): a node whose
     * method exceeds this is generated again in compact form.
     */
    private static final int MAX_METHOD_SIZE = 7900;

    // Local variable slots of a node method.
    private static final int E = 0;
    private static final int D = 1;
    private static final int X = 2;
    private static final int KIND = 3;

    /** Objects with at most this many declared properties (and nothing else needing a loop) are probed by name. */
    private static final int MAX_PROBED_PROPERTIES = 4;

    /** Compiles a program, or returns null when it should run on the interpreter. */
    static Compiled compile(Program p) {
        Set<Integer> forced = new HashSet<>();
        Set<Integer> compact = new HashSet<>();
        while (true) {
            try {
                Generator g = new Generator(p, forced, compact);
                Compiled c = g.generate();
                if (c != null) {
                    return c;
                }
                // Some methods were too large for the JIT: those nodes are generated again in compact form, or
                // interpreted when they are already compact.
                for (int id : g.huge) {
                    if (!compact.add(id)) {
                        forced.add(id);
                    }
                }
            } catch (MethodTooLargeException e) {
                // That node runs on the interpreter instead.
                String name = e.getMethodName();
                if (!name.startsWith("n") || !forced.add(Integer.parseInt(name.substring(1)))) {
                    return null;
                }
            } catch (RuntimeException | LinkageError e) {
                if (System.getenv("CORVUS_DEBUG_CODEGEN") != null) {
                    e.printStackTrace();
                }
                return null;
            }
        }
    }

    /** Reports the bytecode length of each method of a class file (its Code attribute's code_length). */
    static void codeLengths(byte[] bytes, java.util.function.ObjIntConsumer<String> out) {
        org.objectweb.asm.ClassReader cr = new org.objectweb.asm.ClassReader(bytes);
        char[] buf = new char[cr.getMaxStringLength()];
        int at = cr.header + 6;
        at += 2 + 2 * cr.readUnsignedShort(at);
        // Fields: access, name, descriptor, then attributes.
        int fields = cr.readUnsignedShort(at);
        at += 2;
        for (int f = 0; f < fields; f++) {
            at += 6;
            int attributes = cr.readUnsignedShort(at);
            at += 2;
            for (int a = 0; a < attributes; a++) {
                at += 6 + cr.readInt(at + 2);
            }
        }
        int methods = cr.readUnsignedShort(at);
        at += 2;
        for (int m = 0; m < methods; m++) {
            String name = cr.readUTF8(at + 2, buf);
            at += 6;
            int attributes = cr.readUnsignedShort(at);
            at += 2;
            for (int a = 0; a < attributes; a++) {
                String attribute = cr.readUTF8(at, buf);
                int length = cr.readInt(at + 2);
                if (attribute.equals("Code")) {
                    out.accept(name, cr.readInt(at + 10));
                }
                at += 6 + length;
            }
        }
    }

    private static final class Generator {
        private final Program p;
        private final SchemaNode[] nodes;
        private final Set<Integer> forced;
        private final List<Object> constants = new ArrayList<>();
        private final Map<Object, Integer> constantIndex = new IdentityHashMap<>();
        private final Set<Integer> requested = new HashSet<>();
        private final ArrayDeque<Integer> queue = new ArrayDeque<>();
        private final String className;
        /** The node whose method serves each node: one per class of structurally identical nodes. */
        private final int[] representative;
        private MethodVisitor mv;
        private int nextLocal;
        /** The resource of the node whose method is being written (for the dynamic scope). */
        private int currentResource;
        /** Whether the method being written is in compact form. */
        private boolean compact;

        /** Nodes whose methods are generated in compact form, and those found too large in this generation. */
        private final Set<Integer> compactNodes;
        final List<Integer> huge = new ArrayList<>();

        Generator(Program p, Set<Integer> forced, Set<Integer> compactNodes) {
            this.p = p;
            this.nodes = p.nodes;
            this.forced = forced;
            this.compactNodes = compactNodes;
            this.className = PKG + "CompiledSchema";
            this.representative = Merging.representatives(p, this::fallback);
        }

        Compiled generate() {
            ClassWriter cw = new ClassWriter(ClassWriter.COMPUTE_FRAMES | ClassWriter.COMPUTE_MAXS) {
                @Override
                protected String getCommonSuperClass(String a, String b) {
                    return "java/lang/Object";
                }
            };
            cw.visit(V17, ACC_FINAL | ACC_SUPER, className, null, "java/lang/Object",
                    new String[] {PKG + "CodeGen$Compiled"});
            MethodVisitor init = cw.visitMethod(ACC_PUBLIC, "<init>", "()V", null, null);
            init.visitCode();
            init.visitVarInsn(ALOAD, 0);
            init.visitMethodInsn(INVOKESPECIAL, "java/lang/Object", "<init>", "()V", false);
            init.visitInsn(RETURN);
            init.visitMaxs(0, 0);
            init.visitEnd();

            int root = representative[p.fastTarget[p.root]];
            request(root);
            MethodVisitor entry = cw.visitMethod(ACC_PUBLIC, "validate", NODE_DESC, null, null);
            entry.visitCode();
            if (p.usesDynamicScope) {
                // The dynamic scope starts with the entry's resource.
                entry.visitVarInsn(ALOAD, 1);
                entry.visitLdcInsn(nodes[root].resourceId);
                entry.visitMethodInsn(INVOKESTATIC, RT, "push", "(" + EV + "I)V", false);
            }
            entry.visitVarInsn(ALOAD, 1);
            entry.visitVarInsn(ALOAD, 2);
            entry.visitVarInsn(ILOAD, 3);
            entry.visitMethodInsn(INVOKESTATIC, className, "n" + root, NODE_DESC, false);
            entry.visitInsn(IRETURN);
            entry.visitMaxs(0, 0);
            entry.visitEnd();

            while (!queue.isEmpty()) {
                int id = queue.poll();
                mv = cw.visitMethod(ACC_STATIC, "n" + id, NODE_DESC, null, null);
                mv.visitCode();
                currentResource = nodes[id].resourceId;
                compact = compactNodes.contains(id);
                nextLocal = KIND + 1;
                nodeBody(id);
                mv.visitMaxs(0, 0);
                mv.visitEnd();
            }
            cw.visitEnd();
            byte[] bytes = cw.toByteArray();
            // Methods the JIT would refuse to compile.
            codeLengths(bytes, (name, length) -> {
                if (name.startsWith("n") && length > MAX_METHOD_SIZE) {
                    huge.add(Integer.parseInt(name.substring(1)));
                }
            });
            if (!huge.isEmpty()) {
                return null;
            }
            try {
                MethodHandles.Lookup lookup = MethodHandles.lookup()
                        .defineHiddenClassWithClassData(bytes, java.util.Collections.unmodifiableList(constants), true);
                return (Compiled) lookup.findConstructor(lookup.lookupClass(), MethodType.methodType(void.class))
                        .invoke();
            } catch (RuntimeException | Error e) {
                throw e;
            } catch (Throwable t) {
                throw new IllegalStateException(t);
            }
        }

        private void request(int id) {
            if (requested.add(id)) {
                queue.add(id);
            }
        }

        private int local(int size) {
            int slot = nextLocal;
            nextLocal += size;
            return slot;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Emission helpers

        /** Loads a schema constant (a class data element, constant to the JIT). */
        private void constant(Object value, String descriptor) {
            Integer i = constantIndex.get(value);
            if (i == null) {
                i = constants.size();
                constants.add(value);
                constantIndex.put(value, i);
            }
            mv.visitLdcInsn(new ConstantDynamic("_", descriptor, CLASS_DATA_AT, i));
        }

        private void rt(String name, String descriptor) {
            mv.visitMethodInsn(INVOKESTATIC, RT, name, descriptor, false);
        }

        private void pushInt(int v) {
            if (v >= -1 && v <= 5) {
                mv.visitInsn(ICONST_0 + v);
            } else if (v >= Byte.MIN_VALUE && v <= Byte.MAX_VALUE) {
                mv.visitIntInsn(Opcodes.BIPUSH, v);
            } else if (v >= Short.MIN_VALUE && v <= Short.MAX_VALUE) {
                mv.visitIntInsn(Opcodes.SIPUSH, v);
            } else {
                mv.visitLdcInsn(v);
            }
        }

        private void returnFalse() {
            mv.visitInsn(ICONST_0);
            mv.visitInsn(IRETURN);
        }

        /** Jumps to a new label past a "return false" when the int on the stack is non-zero (true). */
        private void returnFalseIfZero() {
            Label ok = new Label();
            mv.visitJumpInsn(IFNE, ok);
            returnFalse();
            mv.visitLabel(ok);
        }

        private void returnFalseIfNonZero() {
            Label ok = new Label();
            mv.visitJumpInsn(IFEQ, ok);
            returnFalse();
            mv.visitLabel(ok);
        }

        /** The node a child application runs: pure-$ref chains and one-branch allOf forwards followed. */
        private SchemaNode target(int id) {
            return nodes[p.fastTarget[id]];
        }

        private boolean isTrue(int id) {
            return target(id).alwaysTrue;
        }

        private boolean isFalse(int id) {
            return target(id).alwaysFalse;
        }

        /**
         * Pushes the result (0 or 1) of evaluating child {@code id} against the value in local {@code slot}. A child
         * applied in place (slot x) on an in-place cycle is entered under the depth guard; a child in another resource,
         * under a live dynamic scope, is entered with its resource on the scope.
         */
        private void call(int id, int slot) {
            call(id, slot, slot == X);
        }

        private void call(int id, int slot, boolean guarded) {
            int t = representative[p.fastTarget[id]];
            SchemaNode n = nodes[t];
            if (n.alwaysTrue) {
                mv.visitInsn(ICONST_1);
                return;
            }
            if (n.alwaysFalse) {
                mv.visitInsn(ICONST_0);
                return;
            }
            boolean crosses = p.usesDynamicScope && n.resourceId != currentResource;
            if (isTypeOnly(n) && !fallback(t) && !crosses) {
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, slot);
                pushInt(n.typeMask);
                rt("type", "(" + DOC + "II)Z");
                return;
            }
            request(t);
            Label after = null;
            boolean depth = guarded && n.inPlaceCycle;
            if (depth) {
                Label entered = new Label();
                after = new Label();
                mv.visitVarInsn(ALOAD, E);
                rt("enter", "(" + EV + ")Z");
                mv.visitJumpInsn(IFNE, entered);
                mv.visitInsn(ICONST_0);
                mv.visitJumpInsn(GOTO, after);
                mv.visitLabel(entered);
            }
            if (crosses) {
                mv.visitVarInsn(ALOAD, E);
                pushInt(n.resourceId);
                rt("push", "(" + EV + "I)V");
            }
            mv.visitVarInsn(ALOAD, E);
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, slot);
            mv.visitMethodInsn(INVOKESTATIC, className, "n" + t, NODE_DESC, false);
            if (crosses) {
                mv.visitVarInsn(ALOAD, E);
                rt("pop", "(" + EV + ")V");
            }
            if (depth) {
                mv.visitVarInsn(ALOAD, E);
                rt("leave", "(" + EV + ")V");
                mv.visitLabel(after);
            }
        }

        private static boolean isTypeOnly(SchemaNode n) {
            return n.hasType
                    && !n.alwaysTrue
                    && !n.alwaysFalse
                    && n.constValue == null
                    && n.enumValues == null
                    && !n.hasObjectKeywords()
                    && !n.hasArrayKeywords()
                    && !n.hasStringKeywords()
                    && !n.hasNumberKeywords()
                    && !n.hasInPlaceApplicators();
        }

        /** Whether a node runs on the interpreter. */
        private boolean fallback(int id) {
            SchemaNode n = nodes[id];
            return forced.contains(id)
                    || (n.unevaluatedProperties >= 0 && objectCoverage(id) == null)
                    || (n.unevaluatedItems >= 0 && arrayCoverage(id) == null);
        }

        private final Map<Integer, Coverage.Result> objectCoverages = new java.util.HashMap<>();
        private final Map<Integer, Coverage> arrayCoverages = new java.util.HashMap<>();
        private static final Coverage.Result NO_COVERAGE = new Coverage.Result(null, null);

        /** The compile-time coverage of node id's unevaluatedProperties, or null when it needs tracking. */
        private Coverage.Result objectCoverage(int id) {
            Coverage.Result r = objectCoverages.computeIfAbsent(id, k -> {
                Coverage c = Coverage.ofStatic(p, k, true);
                if (c != null) {
                    return new Coverage.Result(c, List.of());
                }
                Coverage.Result g = Coverage.ofGuarded(p, k);
                return g != null ? g : NO_COVERAGE;
            });
            return r == NO_COVERAGE ? null : r;
        }

        private static final Coverage NO_ITEMS = new Coverage();

        /** The compile-time coverage of node id's unevaluatedItems, or null when it needs tracking. */
        private Coverage arrayCoverage(int id) {
            Coverage c = arrayCoverages.computeIfAbsent(id, k -> {
                Coverage v = Coverage.ofStatic(p, k, false);
                return v != null ? v : NO_ITEMS;
            });
            return c == NO_ITEMS ? null : c;
        }

        // ------------------------------------------------------------------------------------------------------------
        // Nodes

        private void nodeBody(int id) {
            SchemaNode n = nodes[id];
            if (n.alwaysTrue || n.alwaysFalse) {
                mv.visitInsn(n.alwaysTrue ? ICONST_1 : ICONST_0);
                mv.visitInsn(IRETURN);
                return;
            }
            if (fallback(id)) {
                mv.visitVarInsn(ALOAD, E);
                pushInt(id);
                mv.visitVarInsn(ILOAD, X);
                rt("interpret", "(" + EV + "II)Z");
                mv.visitInsn(IRETURN);
                return;
            }
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("kind", "(" + DOC + "I)I");
            mv.visitVarInsn(ISTORE, KIND);

            SchemaNode flat = flatObject(n);
            if (flat != null) {
                // Objects take the merged section and return; the code below still serves every other kind.
                Label notObject = new Label();
                mv.visitVarInsn(ILOAD, KIND);
                pushInt(JsonDocument.OBJECT);
                mv.visitJumpInsn(IF_ICMPNE, notObject);
                objectSection(flat);
                mv.visitInsn(ICONST_1);
                mv.visitInsn(IRETURN);
                mv.visitLabel(notObject);
            }
            if (n.hasType) {
                typeCheck(n.typeMask);
            }
            if (n.constValue != null) {
                if (n.constValue.kind() == JsonDocument.STRING) {
                    enumCheck(new SchemaNode.Value[] {n.constValue});
                } else {
                    mv.visitVarInsn(ALOAD, D);
                    mv.visitVarInsn(ILOAD, X);
                    constant(n.constValue, "L" + PKG + "SchemaNode$Value;");
                    rt("equal", "(" + DOC + "IL" + PKG + "SchemaNode$Value;)Z");
                    returnFalseIfZero();
                }
            }
            if (n.enumValues != null) {
                enumCheck(n.enumValues);
            }
            if (n.hasNumberKeywords()) {
                section(JsonDocument.NUMBER, () -> numberSection(n));
            }
            if (n.hasStringKeywords()) {
                section(JsonDocument.STRING, () -> stringSection(n));
            }
            if (n.hasObjectKeywords()) {
                section(JsonDocument.OBJECT, () -> objectSection(n));
            }
            if (n.hasArrayKeywords()) {
                section(JsonDocument.ARRAY, () -> arraySection(n));
            }
            inPlace(n);
            if (n.unevaluatedProperties >= 0) {
                Coverage.Result r = objectCoverage(id);
                section(JsonDocument.OBJECT, () -> unevaluatedProperties(n, r));
            }
            if (n.unevaluatedItems >= 0) {
                Coverage c = arrayCoverage(id);
                section(JsonDocument.ARRAY, () -> unevaluatedItems(n, c));
            }
            mv.visitInsn(ICONST_1);
            mv.visitInsn(IRETURN);
        }

        /**
         * unevaluatedProperties decided from a compile-time coverage: each property that neither the coverage nor a
         * guarded coverage whose guards hold names is checked against the keyword's schema.
         */
        private void unevaluatedProperties(SchemaNode n, Coverage.Result r) {
            if (r.main.all || isTrue(n.unevaluatedProperties)) {
                return;
            }
            // Each distinct guard is decided once per object, before the pass.
            List<Coverage.Guard> guards = new ArrayList<>();
            for (Coverage.Guarded g : r.guarded) {
                for (Coverage.Guard guard : g.guards) {
                    if (!guards.contains(guard)) {
                        guards.add(guard);
                    }
                }
            }
            int[] guardSlots = new int[guards.size()];
            for (int i = 0; i < guards.size(); i++) {
                Coverage.Guard guard = guards.get(i);
                guardSlots[i] = local(1);
                if (guard.ifNode >= 0) {
                    call(guard.ifNode, X);
                    if (!guard.holds) {
                        mv.visitInsn(ICONST_1);
                        mv.visitInsn(Opcodes.IXOR);
                    }
                } else {
                    Label absent = new Label();
                    Label stored = new Label();
                    property(guard.property);
                    mv.visitJumpInsn(IFLT, absent);
                    mv.visitInsn(ICONST_1);
                    mv.visitJumpInsn(GOTO, stored);
                    mv.visitLabel(absent);
                    mv.visitInsn(ICONST_0);
                    mv.visitLabel(stored);
                }
                mv.visitVarInsn(ISTORE, guardSlots[i]);
            }
            int k = local(1);
            int end = local(1);
            int v = local(1);
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("first", "(" + DOC + "I)I");
            mv.visitVarInsn(ISTORE, k);
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("count", "(" + DOC + "I)I");
            mv.visitInsn(ICONST_1);
            mv.visitInsn(Opcodes.ISHL);
            mv.visitVarInsn(ILOAD, k);
            mv.visitInsn(IADD);
            mv.visitVarInsn(ISTORE, end);
            Label test = new Label();
            Label body = new Label();
            Label next = new Label();
            mv.visitJumpInsn(GOTO, test);
            mv.visitLabel(body);
            coveredBy(r.main, k, next);
            for (Coverage.Guarded g : r.guarded) {
                Label skip = new Label();
                for (Coverage.Guard guard : g.guards) {
                    mv.visitVarInsn(ILOAD, guardSlots[guards.indexOf(guard)]);
                    mv.visitJumpInsn(IFEQ, skip);
                }
                if (g.coverage.all) {
                    mv.visitJumpInsn(GOTO, next);
                } else {
                    coveredBy(g.coverage, k, next);
                }
                mv.visitLabel(skip);
            }
            // Not evaluated by any applicator: the keyword's schema applies to the value.
            mv.visitVarInsn(ILOAD, k);
            mv.visitInsn(ICONST_1);
            mv.visitInsn(IADD);
            mv.visitVarInsn(ISTORE, v);
            call(n.unevaluatedProperties, v);
            returnFalseIfZero();
            mv.visitLabel(next);
            mv.visitIincInsn(k, 2);
            mv.visitLabel(test);
            mv.visitVarInsn(ILOAD, k);
            mv.visitVarInsn(ILOAD, end);
            mv.visitJumpInsn(IF_ICMPLT, body);
        }

        /** Jumps to {@code covered} when the key in local k is one of the coverage's names or matches its patterns. */
        private void coveredBy(Coverage c, int k, Label covered) {
            if (!c.names.isEmpty()) {
                byte[][] names = c.names.stream().map(this::utf8).toArray(byte[][]::new);
                Label[] cases = new Label[names.length];
                Arrays.fill(cases, covered);
                Label none = new Label();
                nameDispatch(k, names, cases, none);
                mv.visitLabel(none);
            }
            for (SchemaPattern pattern : c.patterns) {
                patternTest(pattern, k);
                mv.visitJumpInsn(IFNE, covered);
            }
        }

        /** unevaluatedItems decided from a compile-time coverage: the items after the covered prefix. */
        private void unevaluatedItems(SchemaNode n, Coverage c) {
            if (c.all || isTrue(n.unevaluatedItems)) {
                return;
            }
            int len = local(1);
            int first = local(1);
            int item = local(1);
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("count", "(" + DOC + "I)I");
            mv.visitVarInsn(ISTORE, len);
            if (isFalse(n.unevaluatedItems)) {
                mv.visitVarInsn(ILOAD, len);
                pushInt(c.prefix);
                Label ok = new Label();
                mv.visitJumpInsn(IF_ICMPLE, ok);
                returnFalse();
                mv.visitLabel(ok);
                return;
            }
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("first", "(" + DOC + "I)I");
            mv.visitVarInsn(ISTORE, first);
            forItems(len, first, item, c.prefix, () -> {
                call(n.unevaluatedItems, item);
                returnFalseIfZero();
            });
        }

        /** The members of an enum: strings by their bytes (a hashed lookup when there are many), others by value. */
        private void enumCheck(SchemaNode.Value[] values) {
            boolean strings = values.length > 0;
            for (SchemaNode.Value v : values) {
                strings &= v.kind() == JsonDocument.STRING;
            }
            if (!strings) {
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, X);
                constant(values, "[L" + PKG + "SchemaNode$Value;");
                rt("anyEqual", "(" + DOC + "I[L" + PKG + "SchemaNode$Value;)Z");
                returnFalseIfZero();
                return;
            }
            // A string of one of the names: the kind, then the length and word dispatch.
            java.util.LinkedHashMap<String, byte[]> distinct = new java.util.LinkedHashMap<>();
            for (SchemaNode.Value v : values) {
                String text = v.doc.string(v.node);
                distinct.putIfAbsent(text, utf8(text));
            }
            byte[][] names = distinct.values().toArray(new byte[0][]);
            Label ok = new Label();
            Label fail = new Label();
            mv.visitVarInsn(ILOAD, KIND);
            pushInt(JsonDocument.STRING);
            mv.visitJumpInsn(IF_ICMPNE, fail);
            Label[] cases = new Label[names.length];
            Arrays.fill(cases, ok);
            nameDispatch(X, names, cases, fail);
            mv.visitLabel(fail);
            returnFalse();
            mv.visitLabel(ok);
        }

        /**
         * A flat composition, for object values: a node whose in-place applicators are $ref/allOf chains, where the node
         * and every branch are plain object schemas (declared properties, required and count bounds, with no type that
         * excludes objects) and each property name resolves to one schema. Returns a node holding the merged object
         * keywords, so that an object takes one pass instead of a call per branch that each re-test the kind and scan
         * the properties (the C#, Rust and TypeScript flat fused plan); null when the node does not qualify, or fewer
         * than two branches have object keywords.
         */
        private SchemaNode flatObject(SchemaNode n) {
            if (n.ref < 0 && n.staticDynamicRef < 0 && n.allOf == null) {
                return null;
            }
            List<SchemaNode> branches = new ArrayList<>();
            if (!collectFlat(n, branches, new HashSet<>())) {
                return null;
            }
            int effective = 0;
            for (SchemaNode b : branches) {
                if (b.properties != null || (b.required != null && b.required.length > 0) || b.minProperties >= 0
                        || b.maxProperties >= 0) {
                    effective++;
                }
            }
            if (effective < 2) {
                return null;
            }
            SchemaNode merged = new SchemaNode(n.resourceId, n.dialect, n.pointer);
            java.util.LinkedHashMap<String, Integer> properties = new java.util.LinkedHashMap<>();
            List<String> required = new ArrayList<>();
            for (SchemaNode b : branches) {
                if (b.properties != null) {
                    for (Property prop : b.properties) {
                        Integer existing = properties.get(prop.name);
                        if (existing == null || isTrue(existing)) {
                            properties.put(prop.name, prop.node);
                        } else if (!sameCheck(existing, prop.node) && !isTrue(prop.node)) {
                            return null;
                        }
                    }
                }
                if (b.required != null) {
                    for (String r : b.required) {
                        if (!required.contains(r)) {
                            required.add(r);
                        }
                    }
                }
                merged.minProperties = Math.max(merged.minProperties, b.minProperties);
                if (b.maxProperties >= 0) {
                    merged.maxProperties = merged.maxProperties < 0
                            ? b.maxProperties
                            : Math.min(merged.maxProperties, b.maxProperties);
                }
            }
            if (!properties.isEmpty()) {
                merged.properties = properties.entrySet().stream()
                        .map(e -> new Property(e.getKey(), e.getValue()))
                        .toArray(Property[]::new);
            }
            if (!required.isEmpty()) {
                merged.required = required.toArray(new String[0]);
            }
            return merged;
        }

        private boolean collectFlat(SchemaNode m, List<SchemaNode> branches, Set<SchemaNode> visited) {
            if (!visited.add(m)) {
                return true;
            }
            if (m.alwaysTrue) {
                return true;
            }
            if (m.alwaysFalse || m.inPlaceCycle) {
                return false;
            }
            // Below a live dynamic scope, a branch in another resource would push that resource, which one merged
            // pass would skip.
            if (p.usesDynamicScope && m.resourceId != currentResource) {
                return false;
            }
            if (m.constValue != null || m.enumValues != null || (m.hasType && (m.typeMask & SchemaNode.T_OBJECT) == 0)) {
                return false;
            }
            if (m.patternProperties != null || m.additionalProperties >= 0 || m.propertyNames >= 0
                    || m.dependencies != null || m.unevaluatedProperties >= 0 || m.unevaluatedItems >= 0) {
                return false;
            }
            if (m.dynamicRef != null || m.anyOf != null || m.oneOf != null || m.not >= 0 || m.ifNode >= 0) {
                return false;
            }
            branches.add(m);
            List<Integer> children = new ArrayList<>();
            if (m.ref >= 0) {
                children.add(m.ref);
            }
            if (m.staticDynamicRef >= 0) {
                children.add(m.staticDynamicRef);
            }
            if (m.allOf != null) {
                for (int c : m.allOf) {
                    children.add(c);
                }
            }
            for (int c : children) {
                if (!collectFlat(target(c), branches, visited)) {
                    return false;
                }
            }
            return true;
        }

        /** Two branches' schemas for a name are the same check: one node, or type-only tests of one type. */
        private boolean sameCheck(int a, int b) {
            SchemaNode x = target(a);
            SchemaNode y = target(b);
            return x == y || (isTypeOnly(x) && isTypeOnly(y) && x.typeMask == y.typeMask);
        }

        /** Emits a section that runs only for a value of one kind. */
        private void section(int kind, Runnable body) {
            Label skip = new Label();
            mv.visitVarInsn(ILOAD, KIND);
            pushInt(kind);
            mv.visitJumpInsn(IF_ICMPNE, skip);
            body.run();
            mv.visitLabel(skip);
        }

        private void typeCheck(int mask) {
            int kinds = mask & 0x3f;
            if ((mask & SchemaNode.T_INTEGER) != 0 && (mask & SchemaNode.T_NUMBER) == 0) {
                // Integer: a number that is an integer, or one of the other kinds.
                Label ok = new Label();
                Label notNumber = new Label();
                mv.visitVarInsn(ILOAD, KIND);
                pushInt(JsonDocument.NUMBER);
                mv.visitJumpInsn(IF_ICMPNE, notNumber);
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, X);
                rt("integer", "(" + DOC + "I)Z");
                mv.visitJumpInsn(IFNE, ok);
                returnFalse();
                mv.visitLabel(notNumber);
                kindTest(kinds);
                mv.visitJumpInsn(IFNE, ok);
                returnFalse();
                mv.visitLabel(ok);
                return;
            }
            kindTest(kinds | ((mask & SchemaNode.T_INTEGER) != 0 ? JsonDocument.NUMBER : 0));
            returnFalseIfZero();
        }

        /** Pushes whether the value's kind is in a set of kind bits. */
        private void kindTest(int kinds) {
            mv.visitVarInsn(ILOAD, KIND);
            pushInt(kinds);
            mv.visitInsn(Opcodes.IAND);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Numbers and strings

        private void compareNumber(SchemaNode.Num bound) {
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            pushInt(bound.flag);
            mv.visitLdcInsn(bound.bits);
            rt("compare", "(" + DOC + "IIJ)I");
        }

        private void numberSection(SchemaNode n) {
            Label fail = new Label();
            Label done = new Label();
            if (n.assertFormat && n.format != null && n.formatKind.isNumeric()) {
                java.util.function.Predicate<String> custom = p.formats.get(n.format);
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, X);
                if (custom != null) {
                    constant(custom, "Ljava/util/function/Predicate;");
                    rt("customNumberFormat", "(" + DOC + "ILjava/util/function/Predicate;)Z");
                } else {
                    constant(n.formatKind, "L" + PKG + "Formats$Kind;");
                    rt("numericFormat", "(" + DOC + "IL" + PKG + "Formats$Kind;)Z");
                }
                mv.visitJumpInsn(IFEQ, fail);
            }
            if (n.minimum != null) {
                compareNumber(n.minimum);
                mv.visitJumpInsn(IFLT, fail);
            }
            if (n.maximum != null) {
                compareNumber(n.maximum);
                mv.visitJumpInsn(IFGT, fail);
            }
            if (n.exclusiveMinimum != null) {
                compareNumber(n.exclusiveMinimum);
                mv.visitJumpInsn(IFLE, fail);
            }
            if (n.exclusiveMaximum != null) {
                compareNumber(n.exclusiveMaximum);
                mv.visitJumpInsn(IFGE, fail);
            }
            if (n.multipleOf != null) {
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, X);
                constant(n.divisor, "L" + PKG + "Numbers$Divisor;");
                rt("multipleOf", "(" + DOC + "IL" + PKG + "Numbers$Divisor;)Z");
                mv.visitJumpInsn(IFEQ, fail);
            }
            mv.visitJumpInsn(GOTO, done);
            mv.visitLabel(fail);
            returnFalse();
            mv.visitLabel(done);
        }

        private void stringSection(SchemaNode n) {
            if (n.minLength > 0 || n.maxLength >= 0) {
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, X);
                mv.visitLdcInsn(Math.max(n.minLength, 0L));
                mv.visitLdcInsn(n.maxLength);
                rt("lengthWithin", "(" + DOC + "IJJ)Z");
                returnFalseIfZero();
            }
            if (n.pattern != null && !n.pattern.matchesAll()) {
                patternTest(n.pattern, X);
                returnFalseIfZero();
            }
            if (n.assertFormat && n.format != null && !n.formatKind.isNumeric()) {
                java.util.function.Predicate<String> custom = p.formats.get(n.format);
                if (custom != null) {
                    mv.visitVarInsn(ALOAD, D);
                    mv.visitVarInsn(ILOAD, X);
                    constant(custom, "Ljava/util/function/Predicate;");
                    rt("customFormat", "(" + DOC + "ILjava/util/function/Predicate;)Z");
                    returnFalseIfZero();
                } else if (n.formatKind != Formats.Kind.UNKNOWN) {
                    mv.visitVarInsn(ALOAD, E);
                    mv.visitVarInsn(ILOAD, X);
                    constant(n.formatKind, "L" + PKG + "Formats$Kind;");
                    mv.visitInsn(n.dialect.compareTo(Dialect.DRAFT6) <= 0 ? ICONST_1 : ICONST_0);
                    rt("format", "(" + EV + "IL" + PKG + "Formats$Kind;Z)Z");
                    returnFalseIfZero();
                }
            }
            if (n.assertContent) {
                mv.visitVarInsn(ALOAD, E);
                mv.visitVarInsn(ILOAD, X);
                pushInt(n.content);
                rt("content", "(" + EV + "II)Z");
                returnFalseIfZero();
            }
        }

        /** Pushes whether a pattern matches the string value in a local. */
        private void patternTest(SchemaPattern pattern, int slot) {
            if (pattern.matchesAll()) {
                mv.visitInsn(ICONST_1);
                return;
            }
            mv.visitVarInsn(ALOAD, E);
            mv.visitVarInsn(ILOAD, slot);
            constant(pattern, "L" + PKG + "SchemaPattern;");
            rt("pattern", "(" + EV + "IL" + PKG + "SchemaPattern;)Z");
        }

        // ------------------------------------------------------------------------------------------------------------
        // Objects

        /** Pushes the value of property {@code name} of the object x, or -1. */
        private void property(String name) {
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            constant(utf8(name), "[B");
            rt("property", "(" + DOC + "I[B)I");
        }

        private final Map<String, byte[]> names = new java.util.HashMap<>();

        private byte[] utf8(String name) {
            return names.computeIfAbsent(name, k -> k.getBytes(StandardCharsets.UTF_8));
        }

        private void objectSection(SchemaNode n) {
            if (n.minProperties > 0 || n.maxProperties >= 0) {
                if (n.minProperties > 0) {
                    countCheck(n.minProperties, IF_ICMPGE);
                }
                if (n.maxProperties >= 0) {
                    countCheck(n.maxProperties, IF_ICMPLE);
                }
            }
            Property[] props = n.properties != null ? n.properties : new Property[0];
            PatternProperty[] patterns = n.patternProperties != null ? n.patternProperties : new PatternProperty[0];
            boolean apNeeds = n.additionalProperties >= 0 && !isTrue(n.additionalProperties);
            boolean pnNeeds = n.propertyNames >= 0 && !isTrue(n.propertyNames);
            // A pattern property whose schema is true matters only to additionalProperties.
            List<PatternProperty> livePatterns = new ArrayList<>();
            for (PatternProperty pp : patterns) {
                if (apNeeds || !isTrue(pp.node)) {
                    livePatterns.add(pp);
                }
            }
            String[] required = n.required != null ? n.required : new String[0];
            boolean loop = !livePatterns.isEmpty() || apNeeds || pnNeeds || props.length > MAX_PROBED_PROPERTIES;
            if (!loop) {
                objectProbe(props, required);
            } else {
                objectLoop(n, props, livePatterns, required, apNeeds, pnNeeds);
            }
            if (n.dependencies != null) {
                for (Dependency d : n.dependencies) {
                    Label absent = new Label();
                    property(d.name);
                    mv.visitJumpInsn(IFLT, absent);
                    if (d.required != null) {
                        for (String r : d.required) {
                            property(r);
                            Label ok = new Label();
                            mv.visitJumpInsn(IFGE, ok);
                            returnFalse();
                            mv.visitLabel(ok);
                        }
                    }
                    if (d.schema >= 0 && !isTrue(d.schema)) {
                        call(d.schema, X);
                        returnFalseIfZero();
                    }
                    mv.visitLabel(absent);
                }
            }
        }

        /** Fails unless the object's property count compares with {@code bound} by {@code okJump}. */
        private void countCheck(long bound, int okJump) {
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("count", "(" + DOC + "I)I");
            mv.visitLdcInsn((int) Math.min(bound, Integer.MAX_VALUE));
            Label ok = new Label();
            mv.visitJumpInsn(okJump, ok);
            returnFalse();
            mv.visitLabel(ok);
        }

        /** Each declared name looked up directly (required first), for small objects. */
        private void objectProbe(Property[] props, String[] required) {
            List<String> req = Arrays.asList(required);
            List<Property> ordered = new ArrayList<>(Arrays.asList(props));
            ordered.sort((a, b) -> Boolean.compare(req.contains(b.name), req.contains(a.name)));
            int v = local(1);
            for (Property prop : ordered) {
                boolean isRequired = req.contains(prop.name);
                boolean check = !isTrue(prop.node);
                if (!check && !isRequired) {
                    continue;
                }
                property(prop.name);
                mv.visitVarInsn(ISTORE, v);
                Label next = new Label();
                mv.visitVarInsn(ILOAD, v);
                if (isRequired) {
                    Label present = new Label();
                    mv.visitJumpInsn(IFGE, present);
                    returnFalse();
                    mv.visitLabel(present);
                } else {
                    mv.visitJumpInsn(IFLT, next);
                }
                if (check) {
                    call(prop.node, v);
                    returnFalseIfZero();
                }
                mv.visitLabel(next);
            }
            for (String r : required) {
                boolean declared = false;
                for (Property prop : props) {
                    declared |= prop.name.equals(r);
                }
                if (!declared) {
                    property(r);
                    Label ok = new Label();
                    mv.visitJumpInsn(IFGE, ok);
                    returnFalse();
                    mv.visitLabel(ok);
                }
            }
        }

        /** One pass over the object's properties: names dispatched to their schemas, then patterns and additional. */
        private void objectLoop(SchemaNode n, Property[] props, List<PatternProperty> patterns, String[] required,
                boolean apNeeds, boolean pnNeeds) {
            // Required names that are declared are counted as seen in the loop (up to 63); the rest are looked up.
            List<String> bitNames = new ArrayList<>();
            for (String r : required) {
                for (Property prop : props) {
                    if (prop.name.equals(r) && bitNames.size() < 63 && !bitNames.contains(r)) {
                        bitNames.add(r);
                    }
                }
            }
            int seen = bitNames.isEmpty() ? -1 : local(2);
            int first = local(1);
            int end = local(1);
            int k = local(1);
            int v = local(1);
            boolean needsMatched = apNeeds && (props.length > 0 || !patterns.isEmpty());
            int matched = needsMatched ? local(1) : -1;
            if (seen >= 0) {
                mv.visitInsn(Opcodes.LCONST_0);
                mv.visitVarInsn(LSTORE, seen);
            }
            // k runs over the keys: first, first + 2, ... < end.
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("first", "(" + DOC + "I)I");
            mv.visitVarInsn(ISTORE, first);
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("count", "(" + DOC + "I)I");
            mv.visitInsn(ICONST_1);
            mv.visitInsn(Opcodes.ISHL);
            mv.visitVarInsn(ILOAD, first);
            mv.visitInsn(IADD);
            mv.visitVarInsn(ISTORE, end);
            mv.visitVarInsn(ILOAD, first);
            mv.visitVarInsn(ISTORE, k);
            Label test = new Label();
            Label body = new Label();
            Label next = new Label();
            mv.visitJumpInsn(GOTO, test);
            mv.visitLabel(body);
            mv.visitVarInsn(ILOAD, k);
            mv.visitInsn(ICONST_1);
            mv.visitInsn(IADD);
            mv.visitVarInsn(ISTORE, v);
            if (matched >= 0) {
                mv.visitInsn(ICONST_0);
                mv.visitVarInsn(ISTORE, matched);
            }
            if (pnNeeds) {
                call(n.propertyNames, k);
                returnFalseIfZero();
            }
            Label afterNames = new Label();
            if (props.length > 0) {
                Label[] cases = new Label[props.length];
                for (int i = 0; i < cases.length; i++) {
                    cases[i] = new Label();
                }
                // A declared name continues with the next property unless patterns or additionalProperties follow.
                boolean tail = !patterns.isEmpty();
                Label afterCase = tail ? afterNames : next;
                nameDispatch(k, props, cases, afterNames);
                for (int i = 0; i < props.length; i++) {
                    mv.visitLabel(cases[i]);
                    if (!isTrue(props[i].node)) {
                        call(props[i].node, v);
                        returnFalseIfZero();
                    }
                    int bit = bitNames.indexOf(props[i].name);
                    if (bit >= 0) {
                        mv.visitVarInsn(LLOAD, seen);
                        mv.visitLdcInsn(1L << bit);
                        mv.visitInsn(LOR);
                        mv.visitVarInsn(LSTORE, seen);
                    }
                    if (matched >= 0) {
                        mv.visitInsn(ICONST_1);
                        mv.visitVarInsn(ISTORE, matched);
                    }
                    mv.visitJumpInsn(GOTO, afterCase);
                }
            }
            mv.visitLabel(afterNames);
            for (PatternProperty pp : patterns) {
                Label skip = new Label();
                patternTest(pp.pattern, k);
                mv.visitJumpInsn(IFEQ, skip);
                if (!isTrue(pp.node)) {
                    call(pp.node, v);
                    returnFalseIfZero();
                }
                if (matched >= 0) {
                    mv.visitInsn(ICONST_1);
                    mv.visitVarInsn(ISTORE, matched);
                }
                mv.visitLabel(skip);
            }
            if (apNeeds) {
                if (matched >= 0) {
                    mv.visitVarInsn(ILOAD, matched);
                    mv.visitJumpInsn(IFNE, next);
                }
                call(n.additionalProperties, v);
                returnFalseIfZero();
            }
            mv.visitLabel(next);
            mv.visitIincInsn(k, 2);
            mv.visitLabel(test);
            mv.visitVarInsn(ILOAD, k);
            mv.visitVarInsn(ILOAD, end);
            mv.visitJumpInsn(IF_ICMPLT, body);
            if (seen >= 0) {
                mv.visitVarInsn(LLOAD, seen);
                mv.visitLdcInsn(bitNames.size() == 63 ? Long.MAX_VALUE : (1L << bitNames.size()) - 1);
                mv.visitInsn(LCMP);
                returnFalseIfNonZero();
            }
            for (String r : required) {
                if (!bitNames.contains(r)) {
                    property(r);
                    Label ok = new Label();
                    mv.visitJumpInsn(IFGE, ok);
                    returnFalse();
                    mv.visitLabel(ok);
                }
            }
        }

        /**
         * Jumps to {@code cases[i]} when the key in local {@code k} is the i-th declared name, else to {@code none}: a
         * switch on the name's byte length, then for each name of that length a compare of its bytes as words
         * (little-endian, the last one overlapping), against constants (C#'s Utf8NameMap: length, then words).
         */
        private void nameDispatch(int k, Property[] props, Label[] cases, Label none) {
            byte[][] names = new byte[props.length][];
            for (int i = 0; i < props.length; i++) {
                names[i] = props[i].utf8;
            }
            nameDispatch(k, names, cases, none);
        }

        private void nameDispatch(int k, byte[][] names, Label[] cases, Label none) {
            if (names.length > MAX_WORD_DISPATCH || compact) {
                // Many names: a hashed lookup to the index, then a switch (compact code, so the method stays within
                // what the JIT compiles).
                NameMap map = new NameMap(names.length);
                for (int i = 0; i < names.length; i++) {
                    map.putIfAbsent(names[i], i);
                }
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, k);
                constant(map, "L" + PKG + "NameMap;");
                rt("lookup", "(" + DOC + "IL" + PKG + "NameMap;)I");
                mv.visitTableSwitchInsn(0, names.length - 1, none, cases);
                return;
            }
            java.util.TreeMap<Integer, List<Integer>> byLength = new java.util.TreeMap<>();
            for (int i = 0; i < names.length; i++) {
                byLength.computeIfAbsent(names[i].length, x -> new ArrayList<>()).add(i);
            }
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, k);
            rt("count", "(" + DOC + "I)I");
            int[] keys = byLength.keySet().stream().mapToInt(Integer::intValue).toArray();
            Label[] groups = new Label[keys.length];
            for (int g = 0; g < groups.length; g++) {
                groups[g] = new Label();
            }
            mv.visitLookupSwitchInsn(none, keys, groups);
            // The key's bytes are read through locals for its array and offset, loaded once.
            int kb = local(1);
            int ko = local(1);
            keyBytes = kb;
            keyOffset = ko;
            Label[] loads = new Label[groups.length];
            for (int g = 0; g < groups.length; g++) {
                loads[g] = groups[g];
                groups[g] = new Label();
            }
            for (int g = 0; g < keys.length; g++) {
                mv.visitLabel(loads[g]);
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, k);
                rt("bytes", "(" + DOC + "I)[B");
                mv.visitVarInsn(ASTORE, kb);
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, k);
                rt("offset", "(" + DOC + "I)I");
                mv.visitVarInsn(ISTORE, ko);
                int length = keys[g];
                if (length == 0) {
                    mv.visitJumpInsn(GOTO, cases[byLength.get(0).get(0)]);
                    continue;
                }
                // The word positions: 0, 8, ... and the last (overlapping) one, for names longer than 8 bytes.
                List<Integer> positions = new ArrayList<>();
                if (length <= 8) {
                    positions.add(0);
                } else {
                    for (int pos = 0; pos + 8 < length; pos += 8) {
                        positions.add(pos);
                    }
                    positions.add(length - 8);
                }
                wordTrie(k, names, byLength.get(length), positions, 0, Math.min(length, 8), cases, none);
            }
        }

        /**
         * Decides among names of one length word by word: at each position the key's word is loaded once and compared
         * with the distinct words the remaining names have there, so names sharing a prefix share its comparisons.
         */
        private void wordTrie(int k, byte[][] names, List<Integer> candidates, List<Integer> positions, int depth,
                int firstWidth, Label[] cases, Label none) {
            if (depth == positions.size()) {
                // Names are distinct, so one candidate remains.
                mv.visitJumpInsn(GOTO, cases[candidates.get(0)]);
                return;
            }
            int pos = positions.get(depth);
            int width = depth == 0 ? firstWidth : 8;
            java.util.LinkedHashMap<Long, List<Integer>> byWord = new java.util.LinkedHashMap<>();
            for (int index : candidates) {
                byWord.computeIfAbsent(Rt.word(names[index], pos, width), x -> new ArrayList<>()).add(index);
            }
            int word = local(2);
            wordOf(k, pos, width);
            mv.visitVarInsn(LSTORE, word);
            for (Map.Entry<Long, List<Integer>> e : byWord.entrySet()) {
                Label other = new Label();
                mv.visitVarInsn(LLOAD, word);
                mv.visitLdcInsn(e.getKey());
                mv.visitInsn(LCMP);
                mv.visitJumpInsn(IFNE, other);
                wordTrie(k, names, e.getValue(), positions, depth + 1, firstWidth, cases, none);
                mv.visitLabel(other);
            }
            mv.visitJumpInsn(GOTO, none);
        }

        /** The locals holding the current key's array and offset (set by the length dispatch). */
        private int keyBytes;
        private int keyOffset;

        /**
         * Pushes the word of {@code width} bytes at {@code pos} of the key whose array and offset are in locals: a
         * full word inside the key is one load; a shorter one is guarded against the end of the array.
         */
        private void wordOf(int k, int pos, int width) {
            mv.visitVarInsn(ALOAD, keyBytes);
            mv.visitVarInsn(ILOAD, keyOffset);
            if (pos != 0) {
                pushInt(pos);
                mv.visitInsn(IADD);
            }
            if (width == 8) {
                rt("word8", "([BI)J");
            } else {
                pushInt(width);
                rt("wordN", "([BII)J");
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Arrays

        private void arraySection(SchemaNode n) {
            int len = local(1);
            int first = local(1);
            int item = local(1);
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("count", "(" + DOC + "I)I");
            mv.visitVarInsn(ISTORE, len);
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("first", "(" + DOC + "I)I");
            mv.visitVarInsn(ISTORE, first);
            if (n.minItems > 0) {
                lengthCheck(len, n.minItems, IF_ICMPGE);
            }
            if (n.maxItems >= 0) {
                lengthCheck(len, n.maxItems, IF_ICMPLE);
            }
            int prefix = n.prefixItems != null ? n.prefixItems.length : 0;
            for (int i = 0; i < prefix; i++) {
                if (isTrue(n.prefixItems[i])) {
                    continue;
                }
                Label skip = new Label();
                mv.visitVarInsn(ILOAD, len);
                pushInt(i);
                mv.visitJumpInsn(IF_ICMPLE, skip);
                mv.visitVarInsn(ILOAD, first);
                pushInt(i);
                mv.visitInsn(IADD);
                mv.visitVarInsn(ISTORE, item);
                call(n.prefixItems[i], item);
                returnFalseIfZero();
                mv.visitLabel(skip);
            }
            if (n.items >= 0 && !isTrue(n.items)) {
                if (isFalse(n.items)) {
                    mv.visitVarInsn(ILOAD, len);
                    pushInt(prefix);
                    Label ok = new Label();
                    mv.visitJumpInsn(IF_ICMPLE, ok);
                    returnFalse();
                    mv.visitLabel(ok);
                } else {
                    forItems(len, first, item, prefix, () -> {
                        call(n.items, item);
                        returnFalseIfZero();
                    });
                }
            }
            if (n.contains >= 0 && (n.minContains > 0 || n.maxContains >= 0)) {
                int count = local(1);
                mv.visitInsn(ICONST_0);
                mv.visitVarInsn(ISTORE, count);
                Label done = new Label();
                boolean early = n.maxContains < 0;
                forItems(len, first, item, 0, () -> {
                    Label skip = new Label();
                    call(n.contains, item);
                    mv.visitJumpInsn(IFEQ, skip);
                    mv.visitIincInsn(count, 1);
                    if (early) {
                        mv.visitVarInsn(ILOAD, count);
                        mv.visitLdcInsn((int) Math.min(n.minContains, Integer.MAX_VALUE));
                        mv.visitJumpInsn(IF_ICMPGE, done);
                    }
                    mv.visitLabel(skip);
                });
                mv.visitLabel(done);
                if (n.minContains > 0) {
                    lengthCheck(count, n.minContains, IF_ICMPGE);
                }
                if (n.maxContains >= 0) {
                    lengthCheck(count, n.maxContains, IF_ICMPLE);
                }
            }
            if (n.uniqueItems) {
                Label ok = new Label();
                mv.visitVarInsn(ILOAD, len);
                mv.visitInsn(ICONST_1);
                mv.visitJumpInsn(IF_ICMPLE, ok);
                mv.visitVarInsn(ALOAD, E);
                mv.visitVarInsn(ILOAD, X);
                rt("unique", "(" + EV + "I)Z");
                mv.visitJumpInsn(IFNE, ok);
                returnFalse();
                mv.visitLabel(ok);
            }
        }

        private void lengthCheck(int slot, long bound, int okJump) {
            mv.visitVarInsn(ILOAD, slot);
            mv.visitLdcInsn((int) Math.min(bound, Integer.MAX_VALUE));
            Label ok = new Label();
            mv.visitJumpInsn(okJump, ok);
            returnFalse();
            mv.visitLabel(ok);
        }

        /** Runs {@code body} with {@code item} set to each item from index {@code from}. */
        private void forItems(int len, int first, int item, int from, Runnable body) {
            int end = local(1);
            mv.visitVarInsn(ILOAD, first);
            mv.visitVarInsn(ILOAD, len);
            mv.visitInsn(IADD);
            mv.visitVarInsn(ISTORE, end);
            mv.visitVarInsn(ILOAD, first);
            pushInt(from);
            mv.visitInsn(IADD);
            mv.visitVarInsn(ISTORE, item);
            Label test = new Label();
            Label loop = new Label();
            mv.visitJumpInsn(GOTO, test);
            mv.visitLabel(loop);
            body.run();
            mv.visitIincInsn(item, 1);
            mv.visitLabel(test);
            mv.visitVarInsn(ILOAD, item);
            mv.visitVarInsn(ILOAD, end);
            mv.visitJumpInsn(IF_ICMPLT, loop);
        }

        // ------------------------------------------------------------------------------------------------------------
        // In-place applicators

        private void inPlace(SchemaNode n) {
            if (n.ref >= 0 && !isTrue(n.ref)) {
                call(n.ref, X);
                returnFalseIfZero();
            }
            if (n.staticDynamicRef >= 0 && !isTrue(n.staticDynamicRef)) {
                call(n.staticDynamicRef, X);
                returnFalseIfZero();
            }
            if (n.dynamicRef != null) {
                dynamicRef(n.dynamicRef);
            }
            if (n.allOf != null) {
                for (int c : n.allOf) {
                    if (!isTrue(c)) {
                        call(c, X);
                        returnFalseIfZero();
                    }
                }
            }
            if (n.anyOf != null) {
                branches(n.anyOf, n.anyOfDiscriminator, false);
            }
            if (n.oneOf != null) {
                branches(n.oneOf, n.oneOfDiscriminator, true);
            }
            if (n.not >= 0) {
                call(n.not, X, false);
                returnFalseIfNonZero();
            }
            if (n.ifNode >= 0 && (n.thenNode >= 0 && !isTrue(n.thenNode) || n.elseNode >= 0 && !isTrue(n.elseNode))) {
                Label otherwise = new Label();
                Label done = new Label();
                call(n.ifNode, X);
                mv.visitJumpInsn(IFEQ, otherwise);
                if (n.thenNode >= 0 && !isTrue(n.thenNode)) {
                    call(n.thenNode, X);
                    returnFalseIfZero();
                }
                mv.visitJumpInsn(GOTO, done);
                mv.visitLabel(otherwise);
                if (n.elseNode >= 0 && !isTrue(n.elseNode)) {
                    call(n.elseNode, X);
                    returnFalseIfZero();
                }
                mv.visitLabel(done);
            }
        }

        /**
         * A $dynamicRef that stays dynamic: its target is found in the dynamic scope at run time, then the candidate's
         * method runs.
         */
        private void dynamicRef(SchemaNode.DynamicRef d) {
            java.util.TreeSet<Integer> candidates = new java.util.TreeSet<>();
            candidates.add(d.fallback);
            for (int[] r : d.byResource) {
                candidates.add(r[1]);
            }
            int[] keys = candidates.stream().mapToInt(Integer::intValue).toArray();
            Label[] cases = new Label[keys.length];
            for (int i = 0; i < cases.length; i++) {
                cases[i] = new Label();
            }
            Label done = new Label();
            mv.visitVarInsn(ALOAD, E);
            constant(d, "L" + PKG + "SchemaNode$DynamicRef;");
            rt("dynamicTarget", "(" + EV + "L" + PKG + "SchemaNode$DynamicRef;)I");
            // Every candidate is a case, so the default is never taken.
            mv.visitLookupSwitchInsn(cases[0], keys, cases);
            for (int i = 0; i < keys.length; i++) {
                mv.visitLabel(cases[i]);
                call(keys[i], X);
                returnFalseIfZero();
                mv.visitJumpInsn(GOTO, done);
            }
            mv.visitLabel(done);
        }

        /**
         * A discriminator in the generated code: the property found by its words, its string value dispatched by
         * length and words to the branches it selects, which are evaluated in place (anyOf: one passes; oneOf:
         * exactly one). Objects without the property, or with a value that is not a string, take the general paths.
         * Returns false, emitting nothing, when the discriminator has values other than strings.
         */
        private boolean inlineDiscriminator(int[] list, SchemaNode.Discriminator disc, boolean oneOf, Label done) {
            for (SchemaNode.Value v : disc.values) {
                if (v.kind() != JsonDocument.STRING) {
                    return false;
                }
            }
            Label all = new Label();
            Label unknown = new Label();
            mv.visitVarInsn(ILOAD, KIND);
            pushInt(JsonDocument.OBJECT);
            mv.visitJumpInsn(IF_ICMPNE, all);
            // The property's value: one pass over the keys, matching the name by its words.
            int value = local(1);
            findProperty(disc.property, value);
            mv.visitVarInsn(ILOAD, value);
            Label present = new Label();
            mv.visitJumpInsn(IFGE, present);
            if (disc.allRequire) {
                returnFalse();
            } else {
                mv.visitJumpInsn(GOTO, all);
            }
            mv.visitLabel(present);
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, value);
            rt("kind", "(" + DOC + "I)I");
            pushInt(JsonDocument.STRING);
            mv.visitJumpInsn(IF_ICMPNE, unknown);
            byte[][] names = new byte[disc.values.length][];
            Label[] cases = new Label[names.length];
            for (int i = 0; i < names.length; i++) {
                names[i] = utf8(disc.values[i].doc.string(disc.values[i].node));
                cases[i] = new Label();
            }
            nameDispatch(value, names, cases, unknown);
            for (int i = 0; i < names.length; i++) {
                mv.visitLabel(cases[i]);
                subset(list, disc.branches[i], oneOf, done);
            }
            mv.visitLabel(unknown);
            subset(list, disc.unknown, oneOf, done);
            mv.visitLabel(all);
            return emitAll(list, oneOf, done);
        }

        /** Evaluates the selected branches, jumping to done when the keyword holds and returning false otherwise. */
        private void subset(int[] list, int[] selected, boolean oneOf, Label done) {
            if (oneOf) {
                int count = local(1);
                mv.visitInsn(ICONST_0);
                mv.visitVarInsn(ISTORE, count);
                for (int i : selected) {
                    Label skip = new Label();
                    call(list[i], X);
                    mv.visitJumpInsn(IFEQ, skip);
                    mv.visitIincInsn(count, 1);
                    mv.visitLabel(skip);
                }
                mv.visitVarInsn(ILOAD, count);
                mv.visitInsn(ICONST_1);
                mv.visitJumpInsn(IF_ICMPEQ, done);
                returnFalse();
            } else {
                for (int i : selected) {
                    call(list[i], X);
                    mv.visitJumpInsn(IFNE, done);
                }
                returnFalse();
            }
        }

        /** Every branch, without a discriminator; returns true (the code ends at done). */
        private boolean emitAll(int[] list, boolean oneOf, Label done) {
            int[] every = new int[list.length];
            for (int i = 0; i < every.length; i++) {
                every[i] = i;
            }
            subset(list, every, oneOf, done);
            mv.visitLabel(done);
            return true;
        }

        /** Stores in local {@code value} the value of property {@code name} of the object x, or -1. */
        private void findProperty(String name, int value) {
            int k = local(1);
            int end = local(1);
            Label test = new Label();
            Label body = new Label();
            Label next = new Label();
            Label found = new Label();
            Label exit = new Label();
            mv.visitInsn(Opcodes.ICONST_M1);
            mv.visitVarInsn(ISTORE, value);
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("first", "(" + DOC + "I)I");
            mv.visitVarInsn(ISTORE, k);
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            rt("count", "(" + DOC + "I)I");
            mv.visitInsn(ICONST_1);
            mv.visitInsn(Opcodes.ISHL);
            mv.visitVarInsn(ILOAD, k);
            mv.visitInsn(IADD);
            mv.visitVarInsn(ISTORE, end);
            mv.visitJumpInsn(GOTO, test);
            mv.visitLabel(body);
            nameDispatch(k, new byte[][] {utf8(name)}, new Label[] {found}, next);
            mv.visitLabel(found);
            mv.visitVarInsn(ILOAD, k);
            mv.visitInsn(ICONST_1);
            mv.visitInsn(IADD);
            mv.visitVarInsn(ISTORE, value);
            mv.visitJumpInsn(GOTO, exit);
            mv.visitLabel(next);
            mv.visitIincInsn(k, 2);
            mv.visitLabel(test);
            mv.visitVarInsn(ILOAD, k);
            mv.visitVarInsn(ILOAD, end);
            mv.visitJumpInsn(IF_ICMPLT, body);
            mv.visitLabel(exit);
        }

        /** The kinds a type mask accepts (integer as number). */
        private static int kinds(int mask) {
            return (mask & 0x3f) | ((mask & SchemaNode.T_INTEGER) != 0 ? JsonDocument.NUMBER : 0);
        }

        /**
         * When every branch asserts a type and no two accept the same kind, the instance's kind selects the only branch
         * that can pass, for anyOf and oneOf alike (C#'s type dispatch, the TypeScript generator's typeDispatch).
         * Returns false, emitting nothing, when the branches do not qualify.
         */
        private boolean typeDispatch(int[] list) {
            if (list.length < 2) {
                return false;
            }
            int[] owned = new int[list.length];
            int claimed = 0;
            for (int i = 0; i < list.length; i++) {
                SchemaNode c = target(list[i]);
                if (c.alwaysFalse) {
                    continue;
                }
                if (c.alwaysTrue || !c.hasType) {
                    return false;
                }
                int k = kinds(c.typeMask);
                if ((k & claimed) != 0) {
                    return false;
                }
                claimed |= k;
                owned[i] = k;
            }
            Label done = new Label();
            for (int i = 0; i < list.length; i++) {
                if (owned[i] == 0) {
                    continue;
                }
                Label other = new Label();
                kindTest(owned[i]);
                mv.visitJumpInsn(IFEQ, other);
                call(list[i], X);
                returnFalseIfZero();
                mv.visitJumpInsn(GOTO, done);
                mv.visitLabel(other);
            }
            returnFalse();
            mv.visitLabel(done);
            return true;
        }

        /**
         * anyOf (at least one branch) or oneOf (exactly one). With a discriminator, an object that has the property
         * tries only the branches its value can select.
         */
        private void branches(int[] list, SchemaNode.Discriminator disc, boolean oneOf) {
            if (typeDispatch(list)) {
                return;
            }
            Label done = new Label();
            if (disc != null && !compact && inlineDiscriminator(list, disc, oneOf, done)) {
                return;
            }
            if (disc != null) {
                int sel = local(1);
                int j = local(1);
                Label all = new Label();
                mv.visitVarInsn(ILOAD, KIND);
                pushInt(JsonDocument.OBJECT);
                mv.visitJumpInsn(IF_ICMPNE, all);
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, X);
                constant(disc, "L" + PKG + "SchemaNode$Discriminator;");
                rt("select", "(" + DOC + "IL" + PKG + "SchemaNode$Discriminator;)[I");
                mv.visitVarInsn(ASTORE, sel);
                mv.visitVarInsn(ALOAD, sel);
                mv.visitJumpInsn(IFNULL, all);
                // The selected branches, by index.
                int count = oneOf ? local(1) : -1;
                if (oneOf) {
                    mv.visitInsn(ICONST_0);
                    mv.visitVarInsn(ISTORE, count);
                }
                mv.visitInsn(ICONST_0);
                mv.visitVarInsn(ISTORE, j);
                Label test = new Label();
                Label loop = new Label();
                Label next = new Label();
                mv.visitJumpInsn(GOTO, test);
                mv.visitLabel(loop);
                mv.visitVarInsn(ALOAD, sel);
                mv.visitVarInsn(ILOAD, j);
                mv.visitInsn(IALOAD);
                Label[] cases = new Label[list.length];
                for (int i = 0; i < cases.length; i++) {
                    cases[i] = new Label();
                }
                mv.visitTableSwitchInsn(0, list.length - 1, next, cases);
                for (int i = 0; i < list.length; i++) {
                    mv.visitLabel(cases[i]);
                    call(list[i], X);
                    if (oneOf) {
                        mv.visitJumpInsn(IFEQ, next);
                        mv.visitIincInsn(count, 1);
                        mv.visitVarInsn(ILOAD, count);
                        mv.visitInsn(ICONST_1);
                        Label ok = new Label();
                        mv.visitJumpInsn(IF_ICMPLE, ok);
                        returnFalse();
                        mv.visitLabel(ok);
                        mv.visitJumpInsn(GOTO, next);
                    } else {
                        mv.visitJumpInsn(IFNE, done);
                        mv.visitJumpInsn(GOTO, next);
                    }
                }
                mv.visitLabel(next);
                mv.visitIincInsn(j, 1);
                mv.visitLabel(test);
                mv.visitVarInsn(ILOAD, j);
                mv.visitVarInsn(ALOAD, sel);
                mv.visitInsn(ARRAYLENGTH);
                mv.visitJumpInsn(IF_ICMPLT, loop);
                if (oneOf) {
                    mv.visitVarInsn(ILOAD, count);
                    mv.visitInsn(ICONST_1);
                    mv.visitJumpInsn(IF_ICMPEQ, done);
                }
                returnFalse();
                mv.visitLabel(all);
            }
            if (oneOf) {
                int count = local(1);
                mv.visitInsn(ICONST_0);
                mv.visitVarInsn(ISTORE, count);
                for (int c : list) {
                    Label skip = new Label();
                    call(c, X);
                    mv.visitJumpInsn(IFEQ, skip);
                    mv.visitIincInsn(count, 1);
                    mv.visitVarInsn(ILOAD, count);
                    mv.visitInsn(ICONST_1);
                    Label ok = new Label();
                    mv.visitJumpInsn(IF_ICMPLE, ok);
                    returnFalse();
                    mv.visitLabel(ok);
                    mv.visitLabel(skip);
                }
                mv.visitVarInsn(ILOAD, count);
                mv.visitInsn(ICONST_1);
                mv.visitJumpInsn(IF_ICMPEQ, done);
                returnFalse();
            } else {
                for (int c : list) {
                    call(c, X);
                    mv.visitJumpInsn(IFNE, done);
                }
                returnFalse();
            }
            mv.visitLabel(done);
        }
    }
}