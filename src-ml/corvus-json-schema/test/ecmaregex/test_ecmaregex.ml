(* The tests of the ECMA-262 regular expression engine, ported from ecmaregex_test.go, oracle_test.go and
   suite_test.go of the ecmaregex package of the Go module, in the form TestEcmaRegex.pas of the Object Pascal port
   gives them. The program prints each failure and a final count, and exits with a status other than zero when
   anything failed.

   The oracle, v8_oracle.json, is read from the current directory, which is where dune runs the test. It holds what
   V8 answers for more than 60,000 patterns. The JSON Schema Test Suite is found through the environment variable
   JSON_SCHEMA_TEST_SUITE or CORVUS_JSON_SCHEMA_TEST_SUITE, and then as the submodule at the root of the repository.
   The tests that read the suite are skipped when it is not there, as in Go.

   What differs from the Go tests, as in the Object Pascal port:

   The Go package has two back ends, the regexp package of the standard library and its own backtracking matcher, and
   the tests compare them. Here the two are the automaton over ASCII text (where the pattern has one) and the
   backtracking matcher, so "both back ends" means a pattern as compiled and the same pattern with its automaton
   taken away. Where a Go test names the back end a pattern takes, the test here checks [regular], which says the Go
   package would run the pattern on the regexp package.

   TestTranslationCarriesNoUnicodeClass is not here, because there is no translation to the regexp package. The
   benchmarks are not here.

   The patterns and texts of the tables below are the ones of the Go source, converted by a script. A character that
   the Go source writes as an escape, or that is hard to see, is written as an escape. *)

module Re = Corvus_json_schema_ecmaregex
module Ucd = Corvus_json_schema_ucd

let failures = ref 0

let fail fmt =
  Printf.ksprintf
    (fun message ->
      incr failures;
      (* A broken engine fails tens of thousands of cases. The first few hundred say what is wrong. *)
      if !failures <= 300 then prerr_endline ("FAIL: " ^ message))
    fmt

let log fmt = Printf.ksprintf (fun message -> print_endline ("  " ^ message)) fmt

(* What the comparison with V8 came to: the verdicts on whether a pattern is valid, and the answers on whether a
   pattern matches a text, counted once for each of the two back ends. *)
let oracle_verdicts = ref 0
let oracle_verdicts_agreed = ref 0
let oracle_answers = ref 0
let oracle_answers_agreed = ref 0

(* Compiles a pattern that must be valid. *)
let compiled pattern =
  match Re.compile pattern with
  | Ok re -> Some re
  | Error message ->
      fail "compile(%S): %s" pattern message;
      None

(* The pattern on the backtracking matcher alone. *)
let backtracking pattern =
  match Re.compile_backtrack pattern with
  | Ok re -> re
  | Error message -> failwith (Printf.sprintf "compile_backtrack(%S): %s" pattern message)

let match_bytes re s = Re.is_match re (Bytes.of_string s) 0 (String.length s)

(* The JSON Schema Test Suite. *)

(* Returns the tests directory of the JSON Schema Test Suite, when it is there. *)
let suite_dir =
  lazy
    (let is_dir path = Sys.file_exists path && Sys.is_directory path in
     let from_env name =
       match Sys.getenv_opt name with
       | Some root when root <> "" && is_dir (Filename.concat root "tests") -> Some (Filename.concat root "tests")
       | _ -> None
     in
     match from_env "CORVUS_JSON_SCHEMA_TEST_SUITE" with
     | Some tests -> Some tests
     | None -> (
         match from_env "JSON_SCHEMA_TEST_SUITE" with
         | Some tests -> Some tests
         | None ->
             (* The suite is a submodule at the root of the repository, some directories above this one. *)
             let rec up dir levels =
               let tests = Filename.concat (Filename.concat dir "JSON-Schema-Test-Suite") "tests" in
               if is_dir tests then Some tests
               else if levels = 0 || Filename.dirname dir = dir then None
               else up (Filename.dirname dir) (levels - 1)
             in
             up (Sys.getcwd ()) 12))

let sorted_dir dir =
  let names = Sys.readdir dir in
  Array.sort compare names;
  Array.to_list names

(* Lists the files of the suite that exercise regular expressions, for every draft that has them. *)
let suite_files tests =
  let files =
    List.concat_map
      (fun name ->
        List.filter_map
          (fun draft ->
            let path = Filename.concat (Filename.concat tests draft) name in
            if Sys.file_exists path && not (Sys.is_directory path) then Some path else None)
          (sorted_dir tests))
      [
        Filename.concat "optional" "ecmascript-regex.json";
        Filename.concat (Filename.concat "optional" "format") "regex.json";
        "pattern.json";
        "patternProperties.json";
        Filename.concat "optional" "non-bmp-regex.json";
      ]
  in
  List.sort compare files

(* Evaluates the few keywords the regular expression files of the suite use, taking every pattern through compile and
   is_match and the regex format through valid. *)
let suite_patterns : (string, Re.t) Hashtbl.t = Hashtbl.create 64

let suite_pattern p =
  match Hashtbl.find_opt suite_patterns p with
  | Some re -> re
  | None -> (
      match Re.compile p with
      | Ok re ->
          Hashtbl.replace suite_patterns p re;
          re
      | Error message -> failwith (Printf.sprintf "compile(%S): %s" p message))

let has_type name (data : Json.t) =
  match (name, data) with
  | "string", String _ | "object", Object _ | "array", Array _ | "boolean", Bool _ | "null", Null | "number", Number _
    ->
      true
  | "integer", Number f -> Float.is_integer f
  | "any", _ -> true
  | _ -> false

let rec suite_valid (schema : Json.t) (data : Json.t) =
  match schema with
  | Bool b -> b
  | Object keywords ->
      let properties = match data with Object members -> members | _ -> [] in
      List.for_all
        (fun (keyword, (value : Json.t)) ->
          match (keyword, value) with
          | "$schema", _ -> true
          | "type", String name -> has_type name data
          | "type", Array names ->
              List.exists (function Json.String name -> has_type name data | _ -> failwith "unexpected type") names
          | "maximum", Number maximum -> ( match data with Number f -> f <= maximum | _ -> true)
          | "pattern", String p -> ( match data with String s -> match_bytes (suite_pattern p) s | _ -> true)
          | "format", String "regex" -> ( match data with String s -> Re.valid s | _ -> true)
          | "patternProperties", Object patterns ->
              List.for_all
                (fun (p, sub) ->
                  let re = suite_pattern p in
                  List.for_all
                    (fun (name, property) -> (not (Re.is_match_string re name)) || suite_valid sub property)
                    properties)
                patterns
          | "additionalProperties", _ ->
              let patterns = match List.assoc_opt "patternProperties" keywords with Some (Object p) -> p | _ -> [] in
              List.for_all
                (fun (name, property) ->
                  let additional =
                    List.for_all (fun (p, _) -> not (Re.is_match_string (suite_pattern p) name)) patterns
                  in
                  (not additional) || suite_valid value property)
                properties
          | _ -> failwith (Printf.sprintf "unexpected keyword %S" keyword))
        keywords
  | _ -> failwith "unexpected schema"

(* Runs every case of the suite's regular expression files through compile, is_match and valid. *)
let test_suite () =
  match Lazy.force suite_dir with
  | None -> log "SKIPPED: the JSON Schema Test Suite is not there"
  | Some tests ->
      let files = suite_files tests in
      if files = [] then fail "no regular expression files under %s" tests;
      let cases = ref 0 in
      List.iter
        (fun file ->
          match Json.load file with
          | Array groups ->
              List.iter
                (fun group ->
                  let schema = match Json.member "schema" group with Some schema -> schema | None -> Json.Null in
                  List.iter
                    (fun test ->
                      incr cases;
                      let data = match Json.member "data" test with Some data -> data | None -> Json.Null in
                      let want = Json.flag "valid" test in
                      let got = suite_valid schema data in
                      if got <> want then
                        fail "%s: %s: %s: got %b, want %b" file (Json.text "description" group)
                          (Json.text "description" test) got want)
                    (Json.items "tests" group))
                groups
          | _ -> fail "%s is not an array" file)
        files;
      let automata = Hashtbl.fold (fun _ re n -> if Re.engine re = Re.Dfa then n + 1 else n) suite_patterns 0 in
      let regular = Hashtbl.fold (fun _ re n -> if Re.regular re then n + 1 else n) suite_patterns 0 in
      let total = Hashtbl.length suite_patterns in
      log "%d cases in %d files, %d distinct patterns: %d regular (%d of them with an automaton), %d on %s only" !cases
        (List.length files) total regular automata (total - regular) (Re.engine_name Re.Backtrack)

(* Collects, from every file of the suite, each pattern, each patternProperties name and each string instance (the
   instances of the regex format tests are patterns too, and the rest are arbitrary text to parse). *)
let suite_corpus =
  lazy
    (match Lazy.force suite_dir with
    | None -> None
    | Some tests ->
        let seen = Hashtbl.create 4096 in
        let rec walk (v : Json.t) =
          match v with
          | Array items -> List.iter walk items
          | Object members ->
              List.iter
                (fun (name, (value : Json.t)) ->
                  (match value with
                  | String s when name = "pattern" || name = "data" -> Hashtbl.replace seen s ()
                  | Object patterns when name = "patternProperties" ->
                      List.iter (fun (p, _) -> Hashtbl.replace seen p ()) patterns
                  | _ -> ());
                  walk value)
                members
          | _ -> ()
        in
        let rec dir path =
          List.iter
            (fun name ->
              let entry = Filename.concat path name in
              if Sys.is_directory entry then dir entry
              else if Filename.check_suffix name ".json" then walk (Json.load entry))
            (sorted_dir path)
        in
        dir tests;
        Some (List.sort compare (Hashtbl.fold (fun s () all -> s :: all) seen [])))

(* The list of hand-written patterns of the Java port's EcmaRegexValidatorTest, each with whether it is a valid
   ECMA-262 pattern with the u flag and whether it is valid with either grammar. The Java test compares its validator
   with its translator. Here the expectations are written out, and test_oracle_hand_written checks the same list
   against V8. *)
let java_hand =
  [
      ("", true, true); ("a", true, true); ("^a$", true, true); ("a|b", true, true); ("(a)", true, true);
      ("(?:a)", true, true); ("(?=a)", true, true); ("(?!a)", true, true); ("(?<=a)", true, true);
      ("(?<!a)", true, true); ({|(?<n>a)\k<n>|}, true, true); ({|\k<n>|}, false, true);
      ("(?<n>a)(?<n>b)", false, false); ("(?<$x_1>a)", true, true); ("(?<1a>a)", false, false);
      ("a{2}", true, true); ("a{2,}", true, true); ("a{2,3}", true, true); ("a{3,2}", false, false);
      ("a{", false, true); ("a{,2}", false, true); ("{", false, true); ("}", false, true); ("]", false, true);
      ("[", false, false); ("[]", true, true); ("[^]", true, true); ("[a-z]", true, true); ("[z-a]", false, false);
      ({|[\d-z]|}, false, true); ({|[a-\d]|}, false, true); ({|[\b]|}, true, true); ({|[\-]|}, true, true);
      ({|\-|}, false, true); ({|\a|}, false, true); ({|\c|}, false, true); ({|\cA|}, true, true);
      ({|\c1|}, false, true); ({|\0|}, true, true); ({|\01|}, false, true); ({|\1|}, false, true);
      ({|(a)\1|}, true, true); ({|(a)\2|}, false, true); ({|\x4|}, false, true); ({|\x41|}, true, true);
      ({|\u004|}, false, true); ({|A|}, true, true); ({|\u{1F600}|}, true, true); ({|\u{110000}|}, false, true);
      ({|😀|}, true, true); ({|[😀-🙏]|}, true, true); ({|\p{L}|}, true, true); ({|\p{Letter}|}, true, true);
      ({|\p{digit}|}, true, true); ({|\p{Nope}|}, false, true); ({|\p{gc=Lu}|}, true, true);
      ({|\p{Script=Greek}|}, true, true); ({|\p{sc=Grek}|}, true, true); ({|\p{Script=Nope}|}, false, true);
      ({|\P{ASCII}|}, true, true); ({|\p|}, false, true); ({|\p{|}, false, true); ("a**", false, false);
      ("a+?", true, true); ("*a", false, false); ("(?=a)*", false, true); ("^*", false, false);
      ("$+", false, false); ({|\b+|}, false, false); ("a)", false, false); ("(a", false, false);
      ("(?a)", false, false); ({|\/|}, true, true); ({|\.|}, true, true); ({|a\|}, false, false);
      ("[a", false, false); ("x{1}{2}", false, false); ({|\w+@\w+\.\w+|}, true, true);
      ("^[a-z][a-z0-9_]*$", true, true); ({|(?<a>.)\k<a>|}, true, true); ({|[\p{L}\d]|}, true, true);
      ({|\s\S\w\W\d\D|}, true, true);
    ]
  [@ocamlformat "disable"]

let test_java_hand_written_patterns () =
  List.iter
    (fun (pattern, unicode, valid) ->
      if Re.valid pattern <> unicode then fail "valid(%S) = %b, want %b" pattern (not unicode) unicode;
      match Re.compile pattern with
      | Ok re ->
          if not valid then fail "compile(%S) gives no error, want one" pattern;
          if Re.unicode re <> unicode then fail "unicode(compile(%S)) = %b, want %b" pattern (Re.unicode re) unicode;
          if Re.pattern re <> pattern then fail "pattern(compile(%S)) = %S" pattern (Re.pattern re)
      | Error message -> if valid then fail "compile(%S) error %s, want valid" pattern message)
    java_hand;
  log "%d patterns" (List.length java_hand)

(* Ports the Java port's test over the suite's patterns. Every pattern, patternProperties name and string instance in
   the suite is read as a pattern. None may make the parser or a compiler misbehave, a pattern valid accepts must
   compile with the u flag grammar, and a pattern or patternProperties name must compile. *)
let test_suite_corpus () =
  match Lazy.force suite_corpus with
  | None -> log "SKIPPED: the JSON Schema Test Suite is not there"
  | Some corpus ->
      let valid = ref 0 and compiled = ref 0 in
      List.iter
        (fun p ->
          let ok = Re.valid p in
          let result = Re.compile p in
          if ok then begin
            incr valid;
            match result with
            | Ok re when Re.unicode re -> ()
            | Ok _ -> fail "valid(%S) but compile used the Annex B grammar" p
            | Error message -> fail "valid(%S) but compile gives %s" p message
          end;
          match result with
          | Error _ -> ()
          | Ok re ->
              incr compiled;
              if (not ok) && Re.unicode re then fail "compile(%S) used the u flag grammar but valid rejects it" p)
        corpus;
      log "%d strings, %d valid with the u flag, %d compiled" (List.length corpus) !valid !compiled

(* The generator gen_oracle.js uses, so the tests build the patterns the oracle's verdicts are about. The Go source
   holds its state in 32 bits. Here it is an int that is cut to 32 bits after each shift to the left. *)
let xorshift seed =
  let s = !seed in
  let s = s lxor (s lsl 13) land 0xFFFFFFFF in
  let s = s lxor (s lsr 17) in
  let s = s lxor (s lsl 5) land 0xFFFFFFFF in
  seed := s;
  s

(* Builds texts over an alphabet that exercises classes, anchors, line terminators, characters beyond the Basic
   Multilingual Plane and malformed UTF-8. *)
let differential_inputs n =
  let alphabet =
    Array.of_list
      [
      "a"; "b"; "c"; "z"; "A"; "X"; "Z"; "0"; "1"; "5"; "9"; "_"; "-"; "."; ":"; "/"; "@"; "#"; "$"; "*"; "!"; "{";
      "}"; "|"; " "; "\n"; "\r"; "\t"; "\u{a0}"; "é"; "µ"; "😀"; "🐲"; "\u{2028}"; "\u{fffd}"; "x-"; "es"; "ES"; "ms";
      "txt"; "Au"; "to"; "No"; "ne"; "2015"; "Co"; "re"; "20"; "15"; "22"; "2"; ","; "a,"; "ab,"; "a-"; "foo";
      "bar"; "α"; "Ω"; "٣"; "\\"; "&"; "("; ")"; "["; "]"; "^"; "+"; "?"; "<"; ">"; "="; "%"; "\xff"; "\xc3";
      "\xe2\x82"; "\xf0\x9f"; "\xed\xa0\x80";
    ]
    [@ocamlformat "disable"]
  in
  let seed = ref 0x2545f491 in
  let inputs = ref (List.rev ("" :: Array.to_list alphabet)) in
  let count = ref (1 + Array.length alphabet) in
  while !count < n do
    let b = Buffer.create 32 in
    let k = ref (1 + (xorshift seed mod 8)) in
    while !k > 0 do
      Buffer.add_string b alphabet.(xorshift seed mod Array.length alphabet);
      decr k
    done;
    inputs := Buffer.contents b :: !inputs;
    incr count
  done;
  List.rev !inputs

(* The oracle holds what V8 answers for a set of patterns and texts. testdata/gen_oracle.js of the Go package writes
   it. *)
type hand = {
  hp : string;
  hu : bool;  (** [hu] and [hl] say whether the pattern is valid with the u flag and with no flag. *)
  hl : bool;
  hm : string;  (** One bit per input, set for a match, written as hexadecimal digits of four bits each. *)
}

type flagged = {
  fp : string;
  ff : string;  (** The flags the pattern was matched with, out of i, m and s. *)
  fu : bool;
  fm : string;
}

type oracle = {
  node : string;
  inputs : string list;
  short_inputs : string list;
  verdicts : string;
  random_matches : string array;
  hand : hand list;
  flagged : flagged list;
}

(* A pattern that is not valid has no answers. *)
let optional_text name v = match Json.member name v with Some (Json.String s) -> s | _ -> ""

let oracle =
  lazy
    (let json = Json.load "v8_oracle.json" in
     let strings name = List.map (function Json.String s -> s | _ -> failwith name) (Json.items name json) in
     {
       node = Json.text "node" json;
       inputs = strings "inputs";
       short_inputs = strings "shortInputs";
       verdicts = Json.text "verdicts" json;
       random_matches = Array.of_list (strings "randomMatches");
       hand =
         List.map
           (fun h -> { hp = Json.text "p" h; hu = Json.flag "u" h; hl = Json.flag "l" h; hm = optional_text "m" h })
           (Json.items "hand" json);
       flagged =
         List.map
           (fun f -> { fp = Json.text "p" f; ff = Json.text "f" f; fu = Json.flag "u" f; fm = optional_text "m" f })
           (Json.items "flagged" json);
     })

(* Returns bit k of a string written by the oracle's hexBits. *)
let bit hex k =
  let digit = hex.[k / 4] in
  let digit = if digit >= 'a' then Char.code digit - Char.code 'a' + 10 else Char.code digit - Char.code '0' in
  (digit lsr (3 - (k mod 4))) land 1 = 1

(* The patterns for which V8 13.6 answers a case-insensitive modifier group differently from the same pattern with
   the i flag. ECMA-262 gives the two the same meaning. Only the validity of these patterns is taken from the oracle.
   test_oracle_flags checks the behaviour of each through the i flag, where V8 agrees with this engine. *)
let v8_modifier_bugs =
  [ {|^(?i:\u212a)$|}; {|^(?i:\u017f)$|}; {|^(?i:\u1e9e)$|}; {|^(?i:\u03bc)$|}; {|^(?i:\u03bc)\&?$|}; {|(?i:\Bs)|} ]

(* Compares the validity of a pattern with V8's. It returns the pattern compiled for each back end, or None when the
   pattern is not valid. *)
let check_verdict p valid_unicode valid_legacy =
  incr oracle_verdicts;
  let agreed = ref true in
  if Re.valid p <> valid_unicode then begin
    fail "valid(%S) = %b, V8 says %b" p (not valid_unicode) valid_unicode;
    agreed := false
  end;
  match Re.compile p with
  | Error message ->
      if valid_unicode || valid_legacy then
        fail "compile(%S) error %s, V8 valid with u %b, with no flag %b" p message valid_unicode valid_legacy
      else if !agreed then incr oracle_verdicts_agreed;
      None
  | Ok re ->
      if not (valid_unicode || valid_legacy) then begin
        fail "compile(%S) gives no error, V8 valid with u %b, with no flag %b" p valid_unicode valid_legacy;
        None
      end
      else begin
        if Re.unicode re <> valid_unicode then begin
          fail "unicode(compile(%S)) = %b, V8 says %b" p (Re.unicode re) valid_unicode;
          agreed := false
        end;
        if !agreed then incr oracle_verdicts_agreed;
        Some (re, backtracking p)
      end

(* Compares the answers of both back ends with one of V8's. *)
let check_answer p re backtrack s want =
  oracle_answers := !oracle_answers + 2;
  let got = Re.is_match_string re s in
  if got <> want then fail "%S on %S (as compiled, %s): got %b, V8 says %b" p s (Re.engine_name (Re.engine re)) got want
  else incr oracle_answers_agreed;
  let got = match_bytes backtrack s in
  if got <> want then fail "%S on %S (forced backtrack): got %b, V8 says %b" p s got want
  else incr oracle_answers_agreed

(* Compares the answers of both back ends with the oracle's on every text, the texts with a character beyond the Basic
   Multilingual Plane among them: a pattern that is valid only with no flag is matched by UTF-16 code unit, as V8
   matches it. It adds to answers the number of answers compared and to matched the number of those that are a
   match. *)
let check_answers p re backtrack inputs bits answers matched =
  List.iteri
    (fun i s ->
      let want = bit bits i in
      incr answers;
      if want then incr matched;
      check_answer p re backtrack s want)
    inputs

(* Checks the hand-written patterns (the Java port's validator cases, the Rust port's pattern cases and the Go
   package's own) against V8, for validity and for the answer on every text, on both back ends. *)
let test_oracle_hand_written () =
  let o = Lazy.force oracle in
  let patterns = ref 0 and answers = ref 0 and matched = ref 0 in
  List.iter
    (fun h ->
      incr patterns;
      match check_verdict h.hp h.hu h.hl with
      | Some (re, backtrack) when not (List.mem h.hp v8_modifier_bugs) ->
          check_answers h.hp re backtrack o.inputs h.hm answers matched
      | _ -> ())
    o.hand;
  log "%d patterns, %d answers per back end (%d of them a match), from Node %s" !patterns !answers !matched o.node

(* Checks case-insensitive, multiline and dot-all matching. V8 matched each pattern with flags set for the whole
   pattern, and here the pattern is wrapped in the modifier group that means the same. *)
let test_oracle_flags () =
  let o = Lazy.force oracle in
  let answers = ref 0 and matched = ref 0 in
  List.iter
    (fun f ->
      let p = "(?" ^ f.ff ^ ":" ^ f.fp ^ ")" in
      match check_verdict p f.fu (not f.fu) with
      | Some (re, backtrack) -> check_answers p re backtrack o.inputs f.fm answers matched
      | None -> ())
    o.flagged;
  log "%d patterns with flags, %d answers per back end (%d of them a match)" (List.length o.flagged) !answers !matched

(* Ports the Java port's generated test, which reads random patterns over an alphabet of syntax characters. Each
   one's validity is checked against V8, and the first few thousand are also matched on both back ends. *)
let test_oracle_generated () =
  let o = Lazy.force oracle in
  let alphabet = {|ab0\\^$.*+?()[]{}|-,:=!<>dwsbpkuxc1L|} in
  let seed = ref 0x9e3779b9 in
  let counts = [| 0; 0; 0 |] in
  let answers = ref 0 and matched = ref 0 in
  let before = !failures in
  (try
     for i = 0 to String.length o.verdicts - 1 do
       let n = 1 + (xorshift seed mod 8) in
       let b = Buffer.create 8 in
       for _ = 1 to n do
         Buffer.add_char b alphabet.[xorshift seed mod String.length alphabet]
       done;
       let p = Buffer.contents b in
       let verdict = o.verdicts.[i] in
       let k = Char.code verdict - Char.code '0' in
       counts.(k) <- counts.(k) + 1;
       (match check_verdict p (verdict = '1') (verdict = '2') with
       | Some (re, backtrack) when i < Array.length o.random_matches ->
           check_answers p re backtrack o.short_inputs o.random_matches.(i) answers matched
       | _ -> ());
       if !failures > before && i > 200 then raise Exit
     done
   with Exit -> ());
  log
    "%d patterns (%d not valid, %d valid with u, %d valid only with no flag), %d answers per back end (%d of them a \
     match)"
    (String.length o.verdicts) counts.(0) counts.(1) counts.(2) !answers !matched

(* Proves the two back ends the same on the patterns at hand. Every regular pattern is run as compiled and on the
   backtracking matcher alone, and the two must agree on every text. So must the automaton over ASCII text, asked
   directly, where the pattern has one. *)
let test_differential () =
  let o = Lazy.force oracle in
  let patterns = Hashtbl.create 4096 in
  let add p = Hashtbl.replace patterns p () in
  List.iter (fun h -> add h.hp) o.hand;
  List.iter (fun f -> add ("(?" ^ f.ff ^ ":" ^ f.fp ^ ")")) o.flagged;
  List.iter add
    [
      "\u{fffd}"; "^[^\u{fffd}]+$"; "^\u{fffd}+$"; {|^.$|}; {|^..$|}; {|^[^a]$|}; {|\P{Any}|}; {|^\p{Any}*$|};
      {|^[\s\S]{2,4}$|}; {|^(?:\p{L}|\p{N})+$|}; {|\b\w+\b|}; {|\B|}; {|^$|}; {|$|}; {|^|}; {|(?:)|};
      {|^(?:a{2}){2,3}$|}; {|^(?:a|b|ab){1,4}?c|}; {|^.{0,300}$|}; {|^(?:[a-z]{1,3}\d?){2,5}$|};
      "^[\\u0000-\u{ffff}]+$"; {|^[\u{10000}-\u{10ffff}]+$|}; {|(?:a{50}){30}|}; {|^a{1001}$|}; {|^(?:a?){1001}$|};
      {|\x{41}|}; {|^\u{2}$|};
    ]
  [@ocamlformat "disable"];
  (match Lazy.force suite_corpus with Some corpus -> List.iter add corpus | None -> ());
  let patterns = List.sort compare (Hashtbl.fold (fun p () all -> p :: all) patterns []) in
  let inputs = differential_inputs 1500 in
  let regular = ref 0 and others = ref 0 and automata = ref 0 and answers = ref 0 and decided = ref 0 in
  List.iter
    (fun p ->
      match Re.compile p with
      | Error _ -> ()
      | Ok re ->
          if not (Re.regular re) then begin
            incr others;
            if Re.engine re = Re.Dfa then fail "%S has an automaton and is not regular" p
          end
          else begin
            incr regular;
            let backtrack = backtracking p in
            List.iter
              (fun s ->
                incr answers;
                let a = Re.is_match_string re s and b = match_bytes backtrack s in
                if a <> b then
                  fail "%S on %S: as compiled (%s) says %b, the backtracking matcher says %b" p s
                    (Re.engine_name (Re.engine re))
                    a b;
                match Re.Internal.automaton re s with
                | Some answer ->
                    incr decided;
                    if answer <> b then
                      fail "%S on %S: the automaton says %b, the backtracking matcher says %b" p s answer b
                | None -> ())
              inputs;
            if Re.engine re = Re.Dfa then incr automata
          end)
    patterns;
  log "%d regular patterns compared on %d texts each (%d answers), %d patterns on the backtracking matcher only"
    !regular (List.length inputs) !answers !others;
  log "%d of the regular patterns have an automaton, which decided %d of the answers" !automata !decided;
  if !automata = 0 || !decided = 0 then fail "no automaton was compared"

(* Checks which back end a pattern takes in Go, which here is whether it is regular. The regexp package of Go refuses
   a repeat count above 1000, so the last two are not regular. *)
let test_engine_choice () =
  List.iter
    (fun (p, want) ->
      match compiled p with
      | Some re -> if Re.regular re <> want then fail "regular(compile(%S)) = %b, want %b" p (Re.regular re) want
      | None -> ())
    [
      ({|^[a-z][a-z0-9_]*$|}, true); ({|\bfoo|}, true); ({|^\p{L}+$|}, true); ({|(?<name>ab)+|}, true);
      ({|^\/[^\*\?\&\%]*(\/\*)?$|}, true); ({|^.{1,256}$|}, true); ({|^a*?b|}, true); ({|(a)\1|}, false);
      ({|^(?!foo)|}, false); ({|(?=a)|}, false); ({|(?<=a)b|}, false); ({|(?<!a)b|}, false);
      ({|(?<n>a)\k<n>|}, false); ({|^a{1001}$|}, false); ({|(?:a{50}){30}|}, false);
    ]
  [@ocamlformat "disable"]

(* Pins the ECMA-262 behaviours only the backtracking matcher has. The oracle checks most of them against V8 too.
   These are the ones worth reading. The Go source says of them, in order: a backreference to a group that has not
   taken part matches the empty string. A group is reset at the start of each iteration of the loop around it. An
   iteration that consumes nothing ends the loop once the minimum is met. A lookaround is atomic and a negative one
   leaves no group set. A lookbehind matches right to left, so the rightmost group takes the most. Matching is by code
   point. Lazy and counted loops. *)
let test_backtracking_semantics () =
  let cases =
    [
      ({|^\1(a)$|}, "a", true); ({|^(a)?\1b$|}, "b", true); ({|^(?:(a)|b)\1$|}, "b", true);
      ({|^(?:(a)|b)*\1$|}, "ab", true); ({|^(?:(a)|b)*\1$|}, "aba", false); ({|^(?:(a)|b)*\1$|}, "baa", true);
      ({|^(?:a*)*$(?<=a)|}, "aaa", true); ({|^(?:a*)*b(?<=b)|}, "aaa", false); ({|^(?:(?=(a)))?\1b|}, "b", true);
      ({|^(?:(?=(a)))?\1a|}, "a", true); ({|^(?:a?){3,5}b(?<=b)|}, "aab", true); ({|^(?:|a)+b(?<=b)|}, "aab", true);
      ({|^(?=(a+))a*b\1$|}, "aaaba", false); ({|^(?=(a+))a*b\1$|}, "aaabaaa", true); ({|(?!(a))\1b|}, "b", true);
      ({|(?<=(\d+)(\d+))$|}, "1053", true); ({|(?<=\1(a))b|}, "aab", true); ({|(?<=\1(a))b|}, "ab", false);
      ({|(?<=^a*)b|}, "aab", true); ({|(?<!^a*)b|}, "aab", false); ({|(?<=a|bc)x|}, "bcx", true);
      ({|^(?<n>.)\k<n>$|}, "🐲🐲", true); ({|^(?<n>.)\k<n>$|}, "🐲🐉", false); ({|(?<=^.)$|}, "🐲", true);
      ({|(?<=[😀-😎])x|}, "😃x", true); ({|(?<!\u{1F600})x|}, "😀x", false); ({|^(?:a|ab)+?b$(?<=b)|}, "aabb", true);
      ({|^(?:(a)|b){2,}?\1$|}, "abbaa", true); ({|^(?:(a)|b){2,}?\1$|}, "abba", false);
      ({|^(.+?)\1+$|}, "abcabcabc", true); ({|^(.+?)\1+$|}, "abcabcab", false);
      ({|^(?:(\w)(?!\1))+$|}, "abab", true); ({|^(?:(\w)(?!\1))+$|}, "abba", false);
      ({|^(?!.*(.).*\1)[a-c]+$|}, "abc", true); ({|^(?!.*(.).*\1)[a-c]+$|}, "abca", false);
    ]
    [@ocamlformat "disable"]
  in
  List.iter
    (fun (pattern, text, want) ->
      match compiled pattern with
      | None -> ()
      | Some re ->
          if Re.regular re || Re.engine re <> Re.Backtrack then fail "compile(%S) is regular" pattern;
          if Re.is_match_string re text <> want then fail "%S on %S: got %b, want %b" pattern text (not want) want;
          if match_bytes re text <> want then fail "%S on the bytes of %S: got %b, want %b" pattern text (not want) want)
    cases;
  log "%d cases" (List.length cases)

(* Runs the backtracking matcher on texts long enough that a matcher recursing once per character or per choice would
   exhaust its stack. *)
let test_long_input () =
  let long = String.init (2 lsl 20) (fun i -> if i land 1 = 0 then 'a' else 'b') in
  let cases =
    [
      ({|^(?:a|b)*$(?<=b)|}, long, true);
      ({|^(?:a|b)*c(?<=c)|}, long, false);
      ({|^(?:ab)+(?!.)|}, long, true);
      ({|^(?=a)(?:\w\w)*?$|}, long, true);
      ({|^(a|b)*\1$|}, long ^ "b", true);
      ({|(?<=^(?:ab)*)c|}, long ^ "c", true);
      ({|^(?!b).*b$|}, long, true);
      ({|^[ab]{2097152}(?<=b)$|}, long, true);
      ({|^(?:[ab]c?){2097152}(?<=b)$|}, long, true);
    ]
  in
  List.iter
    (fun (pattern, text, want) ->
      match compiled pattern with
      | None -> ()
      | Some re ->
          if Re.regular re then fail "compile(%S) is regular" pattern;
          if Re.is_match_string re text <> want then
            fail "%S on %d bytes: got %b, want %b" pattern (String.length text) (not want) want)
    cases;
  log "%d patterns on %d bytes" (List.length cases) (String.length long)

(* The words the run allocates on the heap of OCaml, small and large: the words of the minor heap and the words
   allocated in the major heap directly. Reading the counters allocates a little itself, which a run of nothing
   measures, and that is taken away. *)
let allocated_words run =
  let measure run =
    let minor = Gc.minor_words () in
    let major = (Gc.quick_stat ()).Gc.major_words -. (Gc.quick_stat ()).Gc.promoted_words in
    run ();
    let minor = Gc.minor_words () -. minor in
    let major = (Gc.quick_stat ()).Gc.major_words -. (Gc.quick_stat ()).Gc.promoted_words -. major in
    minor +. major
  in
  let nothing = measure (fun () -> ()) in
  measure run -. nothing

(* Checks that neither back end allocates in the steady state, through each entry point. *)
let test_allocations () =
  let texts =
    Array.of_list
      [
      "the quick brown fox jumps over the lazy dog 12345"; "abcabcabc"; "a-b_c.d@example.com"; "🐲 naïve café 🐲"; "";
    ]
    [@ocamlformat "disable"]
  in
  let byte_texts = Array.map Bytes.of_string texts in
  let scratch = Re.create_scratch () in
  let sink = ref 0 in
  let patterns =
    [
      ({|^[a-z][a-z0-9_]*$|}, true); ({|\b\d{5}\b|}, true); ({|^\p{L}+$|}, true); ({|(?:fox|dog) \w+|}, true);
      ({|^(.+?)\1+$|}, false); ({|^(?!.*\d{6})(?=.*\bfox\b).+$|}, false); ({|(?<=@)\w+(?:\.\w+)+$|}, false);
      ({|^(?:(\w)(?!\1)|\W){3,}?$|}, false); ({|(?<n>[a-z])\k<n>|}, false); ({|café(?= )|}, false);
      ({|(?i:QUICK|ÉCOLE)|}, true); ({|(\w)(?i:\1)|}, false); ({|(?m:^)\w+(?m:$)|}, false);
      ({|(?i:\bCAF\b)|}, false);
      (* Valid only with no flag, so matched by UTF-16 code unit: the text with characters beyond the Basic
         Multilingual Plane is read a unit at a time. *)
      ({|^\-?[^\d]+$|}, true); ({|\-?\W{2} na.+\W{2}$|}, true); ({|\-{0}(\W)\1 |}, false);
      ({|\-{0}(?<=\W{2}) (?i:CAF\W)(?= (.)(.)$)|}, false);
    ]
    [@ocamlformat "disable"]
  in
  let total = ref 0. in
  (* The measure sees an allocation of two words. *)
  let seen = allocated_words (fun () -> sink := !sink + !(Sys.opaque_identity (ref 1))) in
  if seen < 2. then fail "the measure of allocations saw %.0f words where two were allocated" seen;
  List.iter
    (fun (p, want) ->
      match compiled p with
      | None -> ()
      | Some re ->
          if Re.regular re <> want then fail "regular(compile(%S)) = %b, want %b" p (Re.regular re) want
          else begin
            let name = Re.engine_name (Re.engine re) in
            let check what run =
              (* The first matches grow the working memory to the size the texts need. *)
              run ();
              let words =
                allocated_words (fun () ->
                    for _ = 1 to 200 do
                      run ()
                    done)
              in
              total := !total +. words;
              if words <> 0. then fail "%S on %s: %s allocated %.0f words in 200 runs" p name what words
            in
            check "is_match" (fun () ->
                for t = 0 to Array.length byte_texts - 1 do
                  let text = byte_texts.(t) in
                  if Re.is_match re text 0 (Bytes.length text) then incr sink
                done);
            check "is_match_sub" (fun () ->
                for t = 0 to Array.length texts - 1 do
                  let text = texts.(t) in
                  if Re.is_match_sub re text 0 (String.length text) then incr sink
                done);
            check "is_match_string" (fun () ->
                for t = 0 to Array.length texts - 1 do
                  if Re.is_match_string re texts.(t) then incr sink
                done);
            check "is_match_with" (fun () ->
                for t = 0 to Array.length byte_texts - 1 do
                  let text = byte_texts.(t) in
                  if Re.is_match_with scratch re text 0 (Bytes.length text) then incr sink
                done);
            check "is_match_sub_with" (fun () ->
                for t = 0 to Array.length texts - 1 do
                  let text = texts.(t) in
                  if Re.is_match_sub_with scratch re text 0 (String.length text) then incr sink
                done);
            (* A text in the middle of a larger one, as an evaluator gives it. *)
            let wide = Bytes.of_string "\"the quick brown fox 12345\"" in
            check "is_match on a range" (fun () -> if Re.is_match re wide 1 (Bytes.length wide - 2) then incr sink)
          end)
    patterns;
  let automata =
    List.length
      (List.filter (fun (p, _) -> match Re.compile p with Ok re -> Re.engine re = Re.Dfa | Error _ -> false) patterns)
  in
  log
    "%d patterns (%d with an automaton), each matched 200 times on %d texts through 5 entry points: %.0f words \
     allocated"
    (List.length patterns) automata (Array.length texts) !total

(* Matches one pattern of each back end from many threads at once, first with the working memory of the pattern,
   which the threads contend for, and then with a scratch of its own for each thread. *)
let test_concurrent_use () =
  let threads = 16 in
  List.iter
    (fun p ->
      match compiled p with
      | None -> ()
      | Some re ->
          List.iter
            (fun own_scratch ->
              let wrong = Atomic.make false in
              let run g =
                let scratch = Re.create_scratch () in
                let yes = Bytes.of_string (String.concat "" (List.init (50 + g) (fun _ -> "aa1b2"))) in
                let no = Bytes.cat yes (Bytes.of_string "!") in
                let is_match text =
                  if own_scratch then Re.is_match_with scratch re text 0 (Bytes.length text)
                  else Re.is_match re text 0 (Bytes.length text)
                in
                for i = 1 to 500 do
                  if (not (is_match yes)) || is_match no then Atomic.set wrong true;
                  (* Lets another thread in between two matches, and now and then in the middle of the work. *)
                  if i mod 16 = 0 then Thread.yield ()
                done
              in
              let running = List.init threads (fun g -> Thread.create run g) in
              List.iter Thread.join running;
              if Atomic.get wrong then
                fail "%S gave a wrong answer under concurrent use (%s)" p
                  (if own_scratch then "a scratch for each thread" else "the memory of the pattern"))
            [ false; true ])
    [ {|^(?:[a-z]+\d)+$|}; {|^(?:([a-z])\1?\d)+(?<=\d)$|} ];
  log "2 patterns, %d threads each, with the memory of the pattern and with a scratch for each thread" threads

(* Checks the tables behind case-insensitive matching. The table for the u flag grammar must hold every character
   that has a simple case folding, and each table must agree with canonicalize. *)
let test_fold_tables () =
  List.iter
    (fun unicode_mode ->
      let points, classes = Re.Internal.fold_classes unicode_mode in
      let in_class = Array.make (Ucd.max_rune + 1) false in
      Array.iteri
        (fun i point ->
          in_class.(point) <- Array.length classes.(i) > 0;
          Array.iter
            (fun member ->
              if Re.Internal.canonicalize member unicode_mode <> Re.Internal.canonicalize point unicode_mode then
                fail "u flag %b: U+%04X and U+%04X share a class but not a canonical form" unicode_mode point member)
            classes.(i))
        points;
      (* For each canonical form, the last character seen that has it, or -1. *)
      let seen = Array.make (Ucd.max_rune + 1) (-1) in
      for r = 0 to Ucd.max_rune do
        let key = Re.Internal.canonicalize r unicode_mode in
        let ok = seen.(key) >= 0 in
        if ok || key <> r then begin
          let other = if ok then seen.(key) else key in
          if not (in_class.(r) && in_class.(other)) then
            fail "u flag %b: U+%04X and U+%04X are equivalent but not both in the table" unicode_mode r other
        end;
        seen.(key) <- r
      done;
      log "u flag %b: %d characters in classes" unicode_mode (Array.length points))
    [ true; false ]

(* Checks the modifier groups of ECMAScript 2025 on cases worth reading. test_oracle_flags checks them against V8 at
   large. With the u flag grammar the Kelvin sign and the long s fold to k and s. With the Annex B grammar they do
   not, and the trailing \& is what makes the second pattern of each pair an Annex B one. A name may be shared by
   groups in separate alternatives, and a reference means the one that took part. The last element of a case says
   whether the pattern is regular, which is whether the Go package runs it on the regexp package. *)
let test_modifiers () =
  let cases =
    [
      ({|^(?i:abc)$|}, "aBc", true, true); ({|^a(?i:b)c$|}, "aBc", true, true);
      ({|^a(?i:b)c$|}, "ABc", false, true); ({|^(?i:a(?-i:b)c)$|}, "AbC", true, true);
      ({|^(?i:a(?-i:b)c)$|}, "ABC", false, true); ({|^(?i:k)$|}, "\u{212a}", true, true);
      ({|^(?i:k)\&?$|}, "\u{212a}", false, true); ({|^(?i:s)$|}, "\u{17f}", true, true);
      ({|^(?i:s)\&?$|}, "\u{17f}", false, true); ({|^(?i:\w)$|}, "\u{17f}", true, true);
      ({|^(?i:\W)$|}, "\u{17f}", false, true); ({|(?i:\b)\u212a|}, "\u{212a}", true, false);
      ({|\b\u212a|}, "\u{212a}", false, true); ({|^(?i:[^a-z])$|}, "K", false, true);
      ({|^(?i:[^a-z])$|}, "1", true, true); ({|^(?i:ß)$|}, "\u{1e9e}", true, true);
      ({|^(?i:ß)$|}, "SS", false, true); ({|^(.)(?i:\1)$|}, "aA", true, false); ({|^(.)\1$|}, "aA", false, false);
      ({|^(.)(?i:\1)$|}, "k\u{212a}", true, false); ({|(?<=(?i:\1)(.))x|}, "Aax", true, false);
      ({|^(?s:.)$|}, "\n", true, true); ({|^.$|}, "\n", false, true); ({|^(?s:a.)b.$|}, "a\nb\n", false, true);
      ({|(?m:^)b|}, "a\nb", true, false); ({|^b|}, "a\nb", false, true); ({|a(?m:$)|}, "a\u{2028}b", true, false);
      ({|a(?m:$)|}, "ab", false, false); ({|(?m:^)b|}, "a\rb", true, false);
      ({|(?m:^)b|}, "a\u{2029}b", true, false); ({|^(?:(?<a>x)|(?<a>y))\k<a>$|}, "yy", true, false);
      ({|^(?:(?<a>x)|(?<a>y))\k<a>$|}, "xy", false, false); ({|^(?:(?<a>x)|(?<a>y))+\k<a>$|}, "xyy", true, false);
      ({|^(?:(?<a>x)|(?<a>y))+\k<a>$|}, "xyx", false, false); ({|^(?<\u0061b>.)\k<ab>$|}, "zz", true, false);
    ]
    [@ocamlformat "disable"]
  in
  List.iter
    (fun (pattern, text, want, regular) ->
      match compiled pattern with
      | None -> ()
      | Some re ->
          if Re.regular re <> regular then fail "regular(compile(%S)) = %b, want %b" pattern (Re.regular re) regular;
          if Re.is_match_string re text <> want then fail "%S on %S: got %b, want %b" pattern text (not want) want;
          if match_bytes (backtracking pattern) text <> want then
            fail "%S on %S (forced backtrack): got %b, want %b" pattern text (not want) want)
    cases;
  let invalid =
    [
      {|(?i)a|}; {|(?-:a)|}; {|(?ii:a)|}; {|(?i-i:a)|}; {|(?x:a)|}; {|(?i-m-s:a)|}; {|(?<a>x)(?<a>y)|};
      {|(?<a>x|(?<a>y))|}; {|(?:(?<a>x)|b)(?:(?<a>y)|c)|}; {|(?<a\u0020>x)|}; {|(?<\u0030>x)|};
    ]
    [@ocamlformat "disable"]
  in
  List.iter
    (fun p ->
      if Re.valid p then fail "valid(%S) = true" p;
      match Re.compile p with Ok _ -> fail "compile(%S) gives no error" p | Error _ -> ())
    invalid;
  log "%d cases, %d patterns that are not valid" (List.length cases) (List.length invalid)

(* Checks that every property name, value and alias resolves, and a few memberships. The whole of each set was
   compared with V8 once, code point by code point, when the tables of the Go package were generated. *)
let test_properties () =
  let expressions = ref 0 in
  let check expr =
    incr expressions;
    if not (Re.Internal.property_resolves expr) then fail "\\p{%s} does not resolve" expr;
    if not (Re.valid ("\\p{" ^ expr ^ "}")) then fail "valid(\\p{%s}) = false" expr
  in
  Array.iter check Re.Internal.binary_names;
  Array.iter
    (fun name ->
      check name;
      check ("gc=" ^ name);
      check ("General_Category=" ^ name))
    Re.Internal.category_names;
  Array.iter
    (Array.iter (fun value ->
         check ("sc=" ^ value);
         check ("Script=" ^ value);
         check ("scx=" ^ value);
         check ("Script_Extensions=" ^ value)))
    Ucd.script_names;
  let cases =
    [
      ({|^\p{L}$|}, "é", true); ({|^\p{L}$|}, "1", false); ({|^\p{Lu}$|}, "É", true); ({|^\P{Lu}$|}, "É", false);
      ({|^\p{Nd}$|}, "৪", true); ({|^\p{digit}$|}, "৪", true); ({|^\p{sc=Grek}$|}, "α", true);
      ({|^\p{Script=Greek}$|}, "a", false); ({|^\p{sc=Grek}$|}, "\u{342}", false);
      ({|^\p{scx=Grek}$|}, "\u{342}", true); ({|^\p{sc=Zinh}$|}, "\u{342}", true); ({|^\p{Emoji}$|}, "🐲", true);
      ({|^\p{Emoji_Presentation}$|}, "#", false); ({|^\p{Emoji}$|}, "#", true); ({|^\p{Alphabetic}$|}, "ⅷ", true);
      ({|^\p{Uppercase}$|}, "Ⓐ", true); ({|^\p{ID_Start}$|}, "_", false); ({|^\p{ID_Continue}$|}, "_", true);
      ({|^\p{White_Space}$|}, "\u{85}", true); ({|^\s$|}, "\u{85}", false);
      ({|^\p{White_Space}$|}, "\u{feff}", false); ({|^\s$|}, "\u{feff}", true); ({|^\p{Any}$|}, "\u{10ffff}", true);
      ({|^\p{Assigned}$|}, "\u{10ffff}", false); ({|^\p{Script=Unknown}$|}, "\u{e0080}", true);
      ({|^\p{ASCII}$|}, "é", false); ({|^[\p{Lu}\p{Nd}]+$|}, "A1É৪", true); ({|^[^\p{L}]+$|}, "1 -", true);
      ({|^[^\p{L}]+$|}, "1a", false);
    ]
    [@ocamlformat "disable"]
  in
  List.iter
    (fun (pattern, text, want) ->
      match compiled pattern with
      | None -> ()
      | Some re ->
          if Re.is_match_string re text <> want then fail "%S on %S: got %b, want %b" pattern text (not want) want)
    cases;
  List.iter
    (fun p -> if Re.valid p then fail "valid(%S) = true" p)
    [
      {|\p{Nope}|}; {|\p{L=x}|}; {|\p{gc=Greek}|}; {|\p{sc=L}|}; {|\p{}|}; {|\p{Lu|}; {|\pL|}; {|\p{sc=}|};
      {|\p{ascii}|};
    ]
  [@ocamlformat "disable"];
  log "%d property expressions resolve, %d cases" !expressions (List.length cases)

(* Checks which patterns get an automaton over ASCII text, and its answers at the edges: the empty text, anchors, an
   unanchored search, and a text that is not ASCII (which it must hand back). *)
let test_automaton () =
  (* A word boundary, and more states than an automaton may have, are the ones with none. *)
  List.iter
    (fun (p, want) ->
      match compiled p with
      | None -> ()
      | Some re ->
          if not (Re.regular re) then fail "%S is not regular" p;
          let got = Re.engine re = Re.Dfa in
          if got <> want then fail "%S: automaton %b, want %b" p got want)
    [
      ({|^[a-z][a-z0-9_]*$|}, true); ({|(base64key|awskms)://(.*)|}, true);
      ({|^[Ee][Ss]20(1[5-9]|2[0-2])(\.[a-z]+)?$|}, true); ({|^$|}, true); ({|$|}, true); ({|a*|}, true);
      ({|^(?:a?){40}$|}, true); ({|é+|}, true); ({|\bfoo|}, false); ({|^a{600}$|}, false);
      ({|(?:a{50}){12}|}, false); ({|^(?:[a-z]{1,3}\d?){2,90}$|}, false);
    ]
  [@ocamlformat "disable"];
  List.iter
    (fun (p, s, want) ->
      match compiled p with
      | None -> ()
      | Some re -> (
          let got = Re.is_match_string re s and back = match_bytes (backtracking p) s in
          if got <> want || back <> want || match_bytes re s <> want then
            fail "%S on %S: as compiled %b, the backtracking matcher %b, want %b" p s got back want;
          if Re.engine re <> Re.Dfa then fail "%S: no automaton" p
          else
            match Re.Internal.automaton re s with
            | None -> fail "%S on %S: the automaton did not decide an ASCII text" p s
            | Some answer -> if answer <> want then fail "%S on %S: the automaton says %b, want %b" p s answer want))
    [
      ({|^$|}, "", true); ({|^$|}, "a", false); ({|$|}, "abc", true); ({|^|}, "abc", true); ({|$^|}, "", true);
      ({|$^|}, "a", false); ({|a$|}, "ba", true); ({|a$|}, "ab", false); ({|^a|}, "ab", true);
      ({|^a|}, "ba", false); ({|b|}, "aaab", true); ({|^(a|ab)(c|bcd)$|}, "abcd", true);
      ({|^(a|ab)(c|bcd)$|}, "abc", true); ({|^(a|ab)(c|bcd)$|}, "abcc", false); ({|x{2,3}|}, "axxb", true);
      ({|^x{2,3}$|}, "xxxx", false); ({|^.$|}, "\n", false); ({|^[^a]$|}, "\n", true); ({|é|}, "caf", false);
      ({|^a*$|}, "", true); ({|^(a$|b)c?$|}, "a", true); ({|^(a$|b)c?$|}, "ac", false);
    ]
  [@ocamlformat "disable"];
  (match compiled {|^[a-z]+$|} with
  | None -> ()
  | Some re ->
      if (not (Re.is_match_string re "abc")) || Re.is_match_string re "caf\u{e9}" then
        fail "^[a-z]+$ on abc and on a text that is not ASCII";
      if Re.engine re <> Re.Dfa || Re.Internal.automaton re "caf\u{e9}" <> None then
        fail "the automaton decided a text that is not ASCII");
  match compiled {|^\p{L}+$|} with
  | None -> ()
  | Some re ->
      if
        (not (Re.is_match_string re "caf\u{e9}"))
        || Re.is_match_string re "caf\u{e9} 1"
        || not (Re.is_match_string re "cafe")
      then fail "a text that is not ASCII was not handed to the backtracking matcher"

(* Checks that a match reads only the bytes it is given, which the Go package has no test for because a Go slice
   cannot be read beyond its end. Each case runs on bytes and on a string, with the memory of the pattern and with a
   scratch, as compiled and on the backtracking matcher alone. *)
let test_slices () =
  let text = "xxabcyy" in
  let bytes = Bytes.of_string text in
  let scratch = Re.create_scratch () in
  let check p off len want what =
    List.iter
      (fun (re, back_end) ->
        let answers =
          [
            ("is_match", Re.is_match re bytes off len);
            ("is_match_sub", Re.is_match_sub re text off len);
            ("is_match_with", Re.is_match_with scratch re bytes off len);
            ("is_match_sub_with", Re.is_match_sub_with scratch re text off len);
          ]
        in
        List.iter
          (fun (entry, got) ->
            if got <> want then
              fail "%S on bytes %d+%d of %S (%s, %s): got %b. %s" p off len text entry back_end got what)
          answers)
      (match compiled p with Some re -> [ (re, "as compiled"); (backtracking p, "forced backtrack") ] | None -> [])
  in
  check "^abc$" 2 3 true "^abc$ does not match the middle of xxabcyy";
  check "^abc$" 1 4 false "^abc$ matches more than the bytes it was given";
  check "^abc$" 2 4 false "^abc$ matches more than the bytes it was given";
  check "(?<=x)a" 2 3 false "a lookbehind reads before the bytes it was given";
  check "(?<=x)a" 1 3 true "a lookbehind does not see the byte before";
  check "c(?=y)" 2 3 false "a lookahead reads after the bytes it was given";
  check "c(?=y)" 2 4 true "a lookahead does not see the byte after";
  check {|\ba|} 2 3 true "a word boundary reads before the bytes it was given";
  check {|c\b|} 2 3 true "a word boundary reads after the bytes it was given";
  check {|(?m:^)a|} 2 3 true "a line start is not at the start of the bytes";
  check {|c(?m:$)|} 2 3 true "a line end is not at the end of the bytes";
  check {|(?i:\b)a|} 2 3 true "a case-insensitive word boundary reads before the bytes it was given";
  check "^$" 3 0 true "^$ does not match an empty slice";
  check "^$" 7 0 true "^$ does not match the empty slice at the end";
  check "abc" 0 7 true "abc is not found in the whole text";
  check "abc" 3 4 false "abc is found from its second byte";
  (* A character cut by the end of the range is malformed, and one cut by its start is too. *)
  let cut = "a\u{e9}b" in
  (match compiled "^a.$" with
  | Some re ->
      if (not (Re.is_match_sub re cut 0 2)) || Re.is_match_sub re cut 0 4 then
        fail "a character cut by the end of the range is not read as one malformed byte";
      if not (Re.is_match_sub re cut 0 3) then fail "a\\u{e9} does not match ^a.$"
  | None -> ());
  (match compiled "(?<=^.)b" with
  | Some re ->
      if (not (Re.is_match_sub re cut 2 2)) || Re.is_match_sub re cut 0 4 then
        fail "a character cut by the start of the range is not read as one malformed byte"
  | None -> ());
  (match compiled "^$" with
  | Some re ->
      if not (Re.is_match re Bytes.empty 0 0 && Re.is_match_string re "") then fail "^$ does not match an empty text";
      List.iter
        (fun (off, len) ->
          let raises f = match f () with _ -> false | exception Invalid_argument _ -> true in
          if
            not
              (raises (fun () -> Re.is_match re bytes off len)
              && raises (fun () -> Re.is_match_sub re text off len)
              && raises (fun () -> Re.is_match_with scratch re bytes off len)
              && raises (fun () -> Re.is_match_sub_with scratch re text off len))
          then fail "the range %d+%d of 7 bytes is accepted" off len)
        [ (-1, 2); (0, 8); (8, 0); (3, -1); (5, 3); (max_int, 1); (1, max_int) ]
  | None -> ());
  log "done"

(* The cases of v8_cases.json, which gen_cases.js of the Go package writes: patterns with what V8 says of each, whether
   it is valid with the u flag and with no flag, and whether it matches each text. Where V8 departs from ECMA-262 the
   answers are ECMA-262's, and the case names the departure. The file is a copy of the Go package's. *)
let cases = lazy (Json.load "v8_cases.json")

(* The totals of one set of the cases. *)
type totals = {
  mutable patterns : int;
  mutable answers : int;
  mutable matched : int;
  mutable legacy : int;  (** The patterns that are valid only with no flag. *)
  mutable invalid : int;
  mutable departures : int;  (** The patterns where V8 departs from ECMA-262. *)
}

(* Compares one set of the cases with V8: the validity of each pattern and the answers of both back ends. *)
let check_cases name =
  let t = { patterns = 0; answers = 0; matched = 0; legacy = 0; invalid = 0; departures = 0 } in
  List.iter
    (fun c ->
      let p = Json.text "p" c in
      t.patterns <- t.patterns + 1;
      if Json.member "ecma262" c <> None then t.departures <- t.departures + 1;
      match check_verdict p (Json.flag "u" c) (Json.flag "l" c) with
      | None -> t.invalid <- t.invalid + 1
      | Some (re, backtrack) ->
          if not (Json.flag "u" c) then t.legacy <- t.legacy + 1;
          List.iter
            (fun test ->
              let want = Json.flag "want" test in
              t.answers <- t.answers + 1;
              if want then t.matched <- t.matched + 1;
              check_answer p re backtrack (Json.text "t" test) want)
            (Json.items "tests" c))
    (Json.items name (Lazy.force cases));
  t

(* Checks matching by UTF-16 code unit against V8. A pattern that is valid only with no flag is matched a code unit at
   a time. A character beyond the Basic Multilingual Plane is two units to the dot, to a class, to a quantifier, to a
   group and to a backreference, a match can start and end between them, and such a character in the pattern is two
   atoms. A class range between two of them is an error in both grammars. *)
let test_code_units () =
  let t = check_cases "units" in
  log "%d patterns (%d valid only with no flag, %d not valid), %d answers per back end (%d of them a match)" t.patterns
    t.legacy t.invalid t.answers t.matched;
  if t.legacy < 90 || t.invalid < 3 || t.matched < 500 then fail "the cases are not the ones this test is about"

(* Checks the two readers of a code unit on every position of a text, forwards and backwards. *)
let test_code_unit_positions () =
  let text = "a\u{e9}\u{20ac}\u{10400}\u{10FFFF}z" in
  let want = [| 0x61; 0xE9; 0x20AC; 0xD801; 0xDC00; 0xDBFF; 0xDFFF; 0x7A |] in
  let ends = Array.make (Array.length want) 0 in
  let pos = ref 0 in
  Array.iteri
    (fun k unit ->
      let got, w = Re.Internal.unit_at text !pos in
      if got <> unit then fail "unit_at(%d) = %#x, want %#x" !pos got unit;
      pos := !pos + w;
      ends.(k) <- !pos)
    want;
  if !pos <> String.length text then fail "the units end at %d of %d bytes" !pos (String.length text);
  for k = Array.length want - 1 downto 0 do
    let got, w = Re.Internal.unit_before text ends.(k) in
    if got <> want.(k) then fail "unit_before(%d) = %#x, want %#x" ends.(k) got want.(k);
    let start = if k > 0 then ends.(k - 1) else 0 in
    if ends.(k) - w <> start then fail "unit_before(%d) spans %d bytes, want %d" ends.(k) w (ends.(k) - start)
  done;
  (* Text that is not UTF-8 is read a byte at a time, as it is when matching by code point. *)
  List.iter
    (fun bad ->
      let n = String.length bad in
      let pos = ref 0 in
      while !pos < n do
        let r, w = Re.Internal.unit_at bad !pos in
        if w <> 1 || (r <> 0xFFFD && r <> Char.code bad.[!pos]) then fail "unit_at(%S, %d) = %#x, %d" bad !pos r w;
        pos := !pos + w
      done;
      let pos = ref n in
      while !pos > 0 do
        let r, w = Re.Internal.unit_before bad !pos in
        if w <> 1 || (r <> 0xFFFD && r <> Char.code bad.[!pos - 1]) then
          fail "unit_before(%S, %d) = %#x, %d" bad !pos r w;
        pos := !pos - w
      done)
    [ "\xf0\x9f"; "\xf0\x9f\x98"; "\x80\x80\x80"; "\xf0a\x80\x80"; "\xf8\x88\x80\x80\x80" ];
  log "done"

let () =
  let ran = ref 0 in
  List.iter
    (fun (name, test) ->
      Printf.printf "%s\n%!" name;
      incr ran;
      let before = !failures in
      (try test () with e -> fail "%s raised %s" name (Printexc.to_string e));
      if !failures > before then Printf.printf "  FAILED: %d failures\n%!" (!failures - before))
    [
      ("java hand written patterns", test_java_hand_written_patterns);
      ("suite corpus", test_suite_corpus);
      ("differential", test_differential);
      ("engine choice", test_engine_choice);
      ("backtracking semantics", test_backtracking_semantics);
      ("long input", test_long_input);
      ("allocations", test_allocations);
      ("concurrent use", test_concurrent_use);
      ("fold tables", test_fold_tables);
      ("modifiers", test_modifiers);
      ("properties", test_properties);
      ("automaton", test_automaton);
      ("slices", test_slices);
      ("suite", test_suite);
      ("oracle hand written", test_oracle_hand_written);
      ("oracle flags", test_oracle_flags);
      ("oracle generated", test_oracle_generated);
      ("code units", test_code_units);
      ("code unit positions", test_code_unit_positions);
    ];
  Printf.printf
    "V8 oracle: %d verdicts on validity compared, %d agreed. %d answers on a match compared (each text on both back \
     ends), %d agreed.\n"
    !oracle_verdicts !oracle_verdicts_agreed !oracle_answers !oracle_answers_agreed;
  if !failures <> 0 then begin
    Printf.printf "FAILED: %d failures\n" !failures;
    exit 1
  end;
  Printf.printf "ecmaregex: %d tests, 0 failures\n" !ran
