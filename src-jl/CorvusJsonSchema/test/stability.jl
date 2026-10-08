# Type stability of the hot code. A function passes when its return type is concrete and its optimised code has no
# dynamic dispatch: every call left in it is to a builtin or an intrinsic, and everything else was resolved to one
# method or inlined. The structures of the tape, the nodes, the plans and the evaluator must have no field of an
# abstract type.

# What is not stable in a method: the return type, or the dynamic calls of its optimised code.
function unstable(f, types)
    out = String[]
    concrete(t) = t isa Type && (isconcretetype(t) || t === Union{} ||
                                 (t isa Union && Nothing <: t && isconcretetype(Base.typesplit(t, Nothing))) ||
                                 (t isa DataType && t <: Tuple && all(concrete, t.parameters)))
    typed = Base.code_typed(f, types; optimize=true)
    isempty(typed) && push!(out, "no method")
    for (code, return_type) in typed
        concrete(return_type) || push!(out, "return type $return_type")
        for statement in code.code
            statement isa Expr || continue
            call = statement.head === :(=) ? statement.args[2] : statement
            (call isa Expr && call.head === :call) || continue
            callee = call.args[1]
            if callee isa GlobalRef
                callee = getfield(callee.mod, callee.name)
            end
            (callee isa Core.Builtin || callee isa Core.IntrinsicFunction) && continue
            # The constructor of an exception on a path that throws, which Julia does not optimise.
            (callee isa Type && callee <: Exception) && continue
            push!(out, "dynamic call $call")
        end
    end
    return out
end

@testset "type stability" begin
    E, D, B = C.Evaluator, Document, C.Bytes
    hot = [
        # The plans.
        (C.enter_child, (E, C.Child, Int)), (C.enter, (E, UInt8, C.Body, Int)), (C.run_in_place, (E, C.NodeId, Int)),
        (C.run_branch, (E, C.Child, Int)), (C.candidates, (E, C.Branches, Int)), (C.run_body, (E, C.Body, Int)),
        (C.run_keywords, (E, C.Body, Int)), (C.run_apply, (E, Vector{C.Op}, Int)), (C.run_op, (E, C.Op, Int)),
        (C.run_object, (E, C.ObjectPlan, Int)), (C.run_strict_object, (E, C.ObjectPlan, Int)),
        (C.visit_lookup, (E, C.ObjectPlan, Int, Int)), (C.object_rest, (E, C.ObjectPlan, Int, UInt64)),
        (C.visit_values, (E, C.ObjectPlan, Int)), (C.visit_names, (E, C.ObjectPlan, Int, Int)),
        (C.visit_pattern, (E, C.ObjectPlan, Int)), (C.visit_general, (E, C.ObjectPlan, Int)),
        (C.run_array, (E, C.ArrayPlan, Int)), (C.all_of_type, (D, Int, Int, UInt8)), (C.run_leaf, (E, C.Body, Int)),
        (C.run_number, (E, Vector{C.NumberOp}, Int)), (C.run_string, (E, Vector{C.StringOp}, Int)),
        (C.length_ok, (D, Int, UInt64, UInt64)), (C.resolve_dynamic, (E, C.DynamicRefTarget)),
        (C.select_branches, (C.DiscriminatorIndex, C.Discriminator, D, Int)),
        # The fused pass.
        (C.run_fused, (E, C.FusedObject, Int)), (C.run_fused_pass, (E, C.FusedObject, Int, C.FusedPass)),
        (C.fused_entry, (E, C.FusedObject, Int, Int, C.FusedPass)),
        (C.fused_unknown, (E, C.FusedObject, B, Bool, Int, C.FusedPass)),
        (C.resolve_unknown, (E, C.FusedContributor, B, Bool, Int)),
        # The general evaluator.
        (C.eval_node, (E, C.NodeId, Int, C.Bitset)), (C.eval_core, (E, C.NodeId, C.SchemaNode, Int, C.Bitset)),
        (C.eval_object, (E, C.SchemaNode, Int, C.Bitset)), (C.eval_array, (E, C.SchemaNode, Int, C.Bitset)),
        (C.eval_in_place, (E, C.SchemaNode, Int, C.Bitset)),
        (C.eval_in_place_child, (E, C.NodeId, String, Int, C.Bitset, Bool, Bool)),
        (C.eval_number, (E, C.SchemaNode, Int)), (C.eval_string, (E, C.SchemaNode, Int)),
        (C.eval_unevaluated_properties, (E, C.SchemaNode, Int, C.Bitset)),
        (C.eval_unevaluated_items, (E, C.SchemaNode, Int, C.Bitset)), (C.push_scope!, (E, UInt32)),
        (C.new_bits!, (E, Int)), (C.content_ok, (E, B, UInt8)),
        # Patterns, names, values, numbers and formats.
        (C.pattern_match, (C.Pattern, B, Bool)), (C.match_ascii, (C.Sequence, B)), (C.match_chars, (C.Sequence, B)),
        (C.match_list, (C.SeparatedList, B)), (C.match_alternative, (C.Alternative, B, Bool)),
        (C.engine_match, (C.EnginePattern, B)), (C.find, (C.Names, B)),
        (C.find_after, (C.Names, Int, UInt64, UInt64, Int)), (C.find_after_long, (C.Names, B, UInt64, Int)),
        (C.find_next, (C.Names, B, Int)), (C.name_word, (B,)), (C.second_word, (B,)), (C.name_rest, (C.Names, Int, B)),
        (C.rest_equal, (B, Vector{UInt8})), (C.word_at, (Vector{UInt8}, Int, Int)), (C.values_equal, (D, Int, D, Int)), (C.value_hash, (D, Int)),
        (C.all_unique, (D, Int, Vector{UInt64})), (C.str_hash, (B,)),
        (C.compare_numbers, (UInt8, UInt64, UInt8, UInt64)), (C.divides, (C.Divisor, D, Int)),
        (C.divides_text, (Vector{UInt8}, Int, UInt64, Int)), (C.check_string_format, (UInt8, B, Bool)),
        (C.check_number_format, (UInt8, D, Int)), (C.is_uri, (B, Bool, Bool)), (C.is_uri_template, (B,)),
        (C.is_email, (B, Bool)), (C.is_hostname, (B,)), (C.is_ipv6, (B,)), (C.is_date_time, (B,)),
        (C.is_duration, (B,)), (C.is_time, (B,)),
        # The parser.
        (C.parse!, (C.Parser,)), (C.string!, (C.Parser,)), (C.escaped!, (C.Parser, Int, Int, Bool)),
        (C.number!, (C.Parser,)), (C.close!, (C.Parser, Int, Bool)), (C.dedupe!, (C.Parser, Int)),
        (C.decimal_to_float, (Vector{UInt8}, Int, Int)), (C.parse_into!, (C.Parser, D, Vector{UInt8})),
        (C.str, (D, Int)),
        # The entry points.
        (C.run_validation, (Validator, D)), (C.run_text_validation, (Validator, String)),
        (C.run_text_validation, (Validator, Vector{UInt8})), (C.acquire, (Validator,)), (C.release, (Validator, E)),
        (C.release_text, (Validator, E)), (isvalid, (Validator, D)), (isvalid, (Validator, String)),
        (isvalid, (Validator, Vector{UInt8})), (validate, (Validator, D)), (validate, (Validator, String)),
        (validate, (Validator, Vector{UInt8})),
    ]
    problems = String[]
    for (f, types) in hot, problem in unstable(f, types)
        push!(problems, "$f$types: $problem")
    end
    foreach(println, problems)
    @test isempty(problems)

    structures = [Document, C.Bytes, C.Parser, C.Names, C.NameKey, C.Pattern, C.Sequence, C.SequenceItem, C.CharSet,
        C.SeparatedList, C.Alternative, C.SchemaNode, C.ValueRef, C.OptCount, C.NamedNode, C.PatternProperty,
        C.DependencyEntry, C.DynamicRefTarget, C.Discriminator, C.DiscriminatorEntry, C.DiscriminatorValue, C.Divisor,
        C.Child, C.FormatCheck, C.NumberOp, C.StringOp, C.Op, C.Branches, C.DiscriminatorIndex, C.PatternChild,
        C.PlanDependency, C.ObjectPlan, C.ArrayPlan, C.SimpleArray, C.Gate, C.FusedForbidden, C.FusedAbsent,
        C.MaskedValue, C.MergedTests, C.FusedApp, C.AltBranch, C.OptChild, C.FusedPattern, C.FusedContributor,
        C.FusedCondition, C.ValueTest, C.FusedEntry, C.FusedAlternative, C.FusedAltGroup, C.FusedObject, C.Body,
        C.Plan, C.Program, C.FusedPass, C.Evaluator, Validator, C.Bitset, ResultsCollector, SchemaResult]
    abstract_fields = String[]
    for T in structures, (name, t) in zip(fieldnames(T), fieldtypes(T))
        concrete = isconcretetype(t) || (t isa Union && Nothing <: t && isconcretetype(Base.typesplit(t, Nothing)))
        concrete || push!(abstract_fields, "$T.$name::$t")
    end
    foreach(println, abstract_fields)
    @test isempty(abstract_fields)
end
