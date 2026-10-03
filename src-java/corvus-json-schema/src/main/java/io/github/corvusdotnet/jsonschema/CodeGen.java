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

    // Local variable slots of a node method.
    private static final int E = 0;
    private static final int D = 1;
    private static final int X = 2;
    private static final int KIND = 3;

    /** Objects with at most this many declared properties (and nothing else needing a loop) are probed by name. */
    private static final int MAX_PROBED_PROPERTIES = 4;
    /** Declared names up to this many are matched by comparing each; more by a hash lookup. */
    private static final int MAX_SCANNED_NAMES = 8;

    /** Compiles a program, or returns null when it should run on the interpreter. */
    static Compiled compile(Program p) {
        if (p.usesDynamicScope) {
            return null;
        }
        Set<Integer> forced = new HashSet<>();
        while (true) {
            try {
                return new Generator(p, forced).generate();
            } catch (MethodTooLargeException e) {
                // That node runs on the interpreter instead.
                String name = e.getMethodName();
                if (!name.startsWith("n") || !forced.add(Integer.parseInt(name.substring(1)))) {
                    return null;
                }
            } catch (RuntimeException | LinkageError e) {
                return null;
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

        Generator(Program p, Set<Integer> forced) {
            this.p = p;
            this.nodes = p.nodes;
            this.forced = forced;
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
                nextLocal = KIND + 1;
                nodeBody(id);
                mv.visitMaxs(0, 0);
                mv.visitEnd();
            }
            cw.visitEnd();
            byte[] bytes = cw.toByteArray();
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

        /** Pushes the result (0 or 1) of evaluating child {@code id} against the value in local {@code slot}. */
        private void call(int id, int slot) {
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
            if (isTypeOnly(n) && !fallback(t)) {
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, slot);
                pushInt(n.typeMask);
                rt("type", "(" + DOC + "II)Z");
                return;
            }
            request(t);
            mv.visitVarInsn(ALOAD, E);
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, slot);
            mv.visitMethodInsn(INVOKESTATIC, className, "n" + t, NODE_DESC, false);
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
                    || n.inPlaceCycle
                    || n.unevaluatedProperties >= 0
                    || n.unevaluatedItems >= 0
                    || n.dynamicRef != null;
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
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, X);
                if (n.constValue.kind() == JsonDocument.STRING) {
                    constant(utf8(n.constValue.doc.string(n.constValue.node)), "[B");
                    rt("stringIs", "(" + DOC + "I[B)Z");
                } else {
                    constant(n.constValue, "L" + PKG + "SchemaNode$Value;");
                    rt("equal", "(" + DOC + "IL" + PKG + "SchemaNode$Value;)Z");
                }
                returnFalseIfZero();
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
            mv.visitInsn(ICONST_1);
            mv.visitInsn(IRETURN);
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
            if (values.length <= MAX_SCANNED_NAMES) {
                Label ok = new Label();
                for (SchemaNode.Value v : values) {
                    mv.visitVarInsn(ALOAD, D);
                    mv.visitVarInsn(ILOAD, X);
                    constant(utf8(v.doc.string(v.node)), "[B");
                    rt("stringIs", "(" + DOC + "I[B)Z");
                    mv.visitJumpInsn(IFNE, ok);
                }
                returnFalse();
                mv.visitLabel(ok);
                return;
            }
            NameMap map = new NameMap(values.length);
            for (SchemaNode.Value v : values) {
                map.putIfAbsent(utf8(v.doc.string(v.node)), 0);
            }
            mv.visitVarInsn(ALOAD, D);
            mv.visitVarInsn(ILOAD, X);
            constant(map, "L" + PKG + "NameMap;");
            rt("stringIn", "(" + DOC + "IL" + PKG + "NameMap;)Z");
            returnFalseIfZero();
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
                int len = local(2);
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, X);
                rt("length", "(" + DOC + "I)J");
                mv.visitVarInsn(LSTORE, len);
                if (n.minLength > 0) {
                    mv.visitVarInsn(LLOAD, len);
                    mv.visitLdcInsn(n.minLength);
                    mv.visitInsn(LCMP);
                    Label ok = new Label();
                    mv.visitJumpInsn(IFGE, ok);
                    returnFalse();
                    mv.visitLabel(ok);
                }
                if (n.maxLength >= 0) {
                    mv.visitVarInsn(LLOAD, len);
                    mv.visitLdcInsn(n.maxLength);
                    mv.visitInsn(LCMP);
                    Label ok = new Label();
                    mv.visitJumpInsn(IFLE, ok);
                    returnFalse();
                    mv.visitLabel(ok);
                }
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
                // The index of the name among the declared ones, then a switch to its check.
                mv.visitVarInsn(ALOAD, D);
                mv.visitVarInsn(ILOAD, k);
                if (props.length <= MAX_SCANNED_NAMES) {
                    byte[][] utf8 = new byte[props.length][];
                    for (int i = 0; i < props.length; i++) {
                        utf8[i] = props[i].utf8;
                    }
                    constant(utf8, "[[B");
                    rt("indexOf", "(" + DOC + "I[[B)I");
                } else {
                    NameMap map = new NameMap(props.length);
                    for (int i = 0; i < props.length; i++) {
                        map.putIfAbsent(props[i].utf8, i);
                    }
                    constant(map, "L" + PKG + "NameMap;");
                    rt("lookup", "(" + DOC + "IL" + PKG + "NameMap;)I");
                }
                Label[] cases = new Label[props.length];
                for (int i = 0; i < cases.length; i++) {
                    cases[i] = new Label();
                }
                // A declared name continues with the next property unless patterns or additionalProperties follow.
                boolean tail = !patterns.isEmpty();
                Label afterCase = tail ? afterNames : next;
                mv.visitTableSwitchInsn(0, props.length - 1, afterNames, cases);
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
                call(n.not, X);
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
         * anyOf (at least one branch) or oneOf (exactly one). With a discriminator, an object that has the property
         * tries only the branches its value can select.
         */
        private void branches(int[] list, SchemaNode.Discriminator disc, boolean oneOf) {
            Label done = new Label();
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