type t = Draft4 | Draft6 | Draft7 | Draft2019_09 | Draft2020_12

let rank = function Draft4 -> 0 | Draft6 -> 1 | Draft7 -> 2 | Draft2019_09 -> 3 | Draft2020_12 -> 4

let to_string = function
  | Draft4 -> "draft4"
  | Draft6 -> "draft6"
  | Draft7 -> "draft7"
  | Draft2019_09 -> "draft2019-09"
  | Draft2020_12 -> "draft2020-12"

let is_legacy d = rank d <= 2
let vocab_none = 0
let vocab_core = 1 lsl 0
let vocab_applicator = 1 lsl 1
let vocab_validation = 1 lsl 2
let vocab_meta_data = 1 lsl 3
let vocab_format_annotation = 1 lsl 4
let vocab_format_assertion = 1 lsl 5
let vocab_content = 1 lsl 6
let vocab_unevaluated = 1 lsl 7

let vocab_all_annotating =
  vocab_core lor vocab_applicator lor vocab_validation lor vocab_meta_data lor vocab_format_annotation
  lor vocab_content lor vocab_unevaluated

let known = function
  | "http://json-schema.org/draft-04/schema" -> Some Draft4
  | "http://json-schema.org/draft-06/schema" -> Some Draft6
  | "http://json-schema.org/draft-07/schema" -> Some Draft7
  | "https://json-schema.org/draft/2019-09/schema" -> Some Draft2019_09
  | "https://json-schema.org/draft/2020-12/schema" -> Some Draft2020_12
  | _ -> None

let vocabulary_flag = function
  | "https://json-schema.org/draft/2020-12/vocab/core" | "https://json-schema.org/draft/2019-09/vocab/core" ->
      vocab_core
  | "https://json-schema.org/draft/2020-12/vocab/applicator" | "https://json-schema.org/draft/2019-09/vocab/applicator"
    ->
      vocab_applicator
  | "https://json-schema.org/draft/2020-12/vocab/validation" | "https://json-schema.org/draft/2019-09/vocab/validation"
    ->
      vocab_validation
  | "https://json-schema.org/draft/2020-12/vocab/meta-data" | "https://json-schema.org/draft/2019-09/vocab/meta-data"
    ->
      vocab_meta_data
  | "https://json-schema.org/draft/2020-12/vocab/format-annotation" | "https://json-schema.org/draft/2019-09/vocab/format"
    ->
      vocab_format_annotation
  | "https://json-schema.org/draft/2020-12/vocab/format-assertion" -> vocab_format_assertion
  | "https://json-schema.org/draft/2020-12/vocab/content" | "https://json-schema.org/draft/2019-09/vocab/content" ->
      vocab_content
  | "https://json-schema.org/draft/2020-12/vocab/unevaluated" -> vocab_unevaluated
  | _ -> vocab_none

type subschema_kind = Subschema_none | Subschema_single | Subschema_single_or_array | Subschema_array | Subschema_map

let subschema_kind_of keyword dialect legacy_ref_overrides_siblings =
  let d = rank dialect in
  if keyword = "definitions" || keyword = "$defs" then Subschema_map
  else if legacy_ref_overrides_siblings then Subschema_none
  else
    match keyword with
    | "properties" | "patternProperties" | "dependencies" -> Subschema_map
    | "additionalProperties" | "not" -> Subschema_single
    | "allOf" | "anyOf" | "oneOf" -> Subschema_array
    | "items" -> if d >= 4 then Subschema_single else Subschema_single_or_array
    | "additionalItems" -> if d <= 3 then Subschema_single else Subschema_none
    | "contains" | "propertyNames" -> if d >= 1 then Subschema_single else Subschema_none
    | "if" | "then" | "else" -> if d >= 2 then Subschema_single else Subschema_none
    | "unevaluatedProperties" | "unevaluatedItems" | "contentSchema" ->
        if d >= 3 then Subschema_single else Subschema_none
    | "dependentSchemas" -> if d >= 3 then Subschema_map else Subschema_none
    | "prefixItems" -> if d >= 4 then Subschema_array else Subschema_none
    | _ -> Subschema_none
