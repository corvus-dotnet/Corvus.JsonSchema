(* The tests of the pattern matchers: pattern_test.go of the Go module, the pattern tests of TestSchemaSide.pas of the
   Object Pascal port, and a test that matching allocates nothing. *)

module Check = Corvus_json_schema_test_support.Check
module Pattern = Corvus_json_schema__Pattern
module Pattern_engine = Corvus_json_schema__Pattern_engine
module Document = Corvus_json_schema__Document
module Suite = Corvus_json_schema_test_helpers.Suite

let test_patterns =
  [|
    "";
    ".*";
    "^.*";
    ".*$";
    {p|[\s\S]*|p};
    {p|^[\s\S]*|p};
    {p|^[\s\S]*$|p};
    "^.*$";
    "^[@$_#]";
    {p|^[a-zA-Z0-9_\.\-\|@#]*$|p};
    {p|^[a-zA-Z0-9_\-]*$|p};
    ".+";
    {p|^\{\{[^\W\.\-][\w\.\-]*\}\}$|p};
    "^.{1,256}$";
    {p|^[A-Z0-9_\-\/]+$|p};
    {p|^[a-zA-Z0-9_\.\-]+[\|]?[a-zA-Z0-9_\.\-]+$|p};
    {p|^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$|p};
    "^x-";
    "^[1-5](?:[0-9]{2}|XX)$";
    "(base64key|awskms)://(.*)";
    "^[A-F0-9]{1,32}$";
    "^[a-z][a-z0-9]{0,29}$";
    "^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])$";
    {p|^[\w\*]{0,60}$|p};
    {p|^(?:@[0-9a-z-_.]+\/)?[a-z][0-9a-z-_.]*$|p};
    "^[a-z][a-z0-9_]+$";
    {p|^\d+[:-]\d+$|p};
    "^#[0-9a-fA-F]{6}$";
    "^[^:]+:[^:]+$";
    {p|^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p};
    {p|^.*\.(?:txt|trie)(?:\.gz)?$|p};
    {p|^([-\w_\s]+)(,[-\w_\s]+)*$|p};
    {p|^(!?[-\w_\s]+)|(\*)$|p};
    "^[0-9]+(ns|ms|us|µs|s|m|h)$";
    {p|^\/[^\*\?\&\%]*(\/\*)?$|p};
    "^[^- @#$%^&()!]+$";
    {p|^((\.(?!\.)\/)?\w+\/?)+$|p};
    "^[0-9]{1,}.[0-9]{1,}.[0-9]{1,}$";
    {p|\{.*\}|p};
    "^[a-z]{1,2}$";
    "^abc$";
    {p|^\/|p};
    "^es$";
    {p|^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$|p};
    "^[Ee][Ss]5|[Ee][Ss]6|[Ee][Ss]7$";
    "^[a-z]*a$";
    "^a*a";
    "^a+b?a";
    "^a+b?c*a$";
    "^[a-z]+-?[a-z]+$";
    {p|\bfoo|p};
    {p|^\p{L}+$|p};
    "é";
    "😀";
    {p|[^\d]x|p};
    {p|^\S+$|p};
    "a{2}b{1,}c{0,3}";
    "(?<name>ab)+";
    {p|^[\b]|p};
    {p|\x41|p};
    ".";
    "^.";
    "^.+";
    "(.*)";
    "^(.*)";
    "^.+$";
    "^.{1,3}$";
    "^.{2}$";
    "^.{2,}$";
    "^(ab|cd)$";
    {p|^(?:es|ES|x-|a\.b)$|p};
    "^(a|b|c|d|e|f|g|h|i|j|z)$";
    "^ab|cd$";
    "^x-|es|ms$";
    "a|b";
    {p|^a\$|b|p};
    {p|\Bs|p};
    {p|a\b|p};
    {p|^\p{Lu}|p};
    {p|[\p{L}\d]+$|p};
    {p|^\P{L}+$|p};
    {p|^(?=[^a-c\n]+$)(?=(.*\w)).+$|p};
    {p|^\-a|p};
    {p|[{}[\]]|p};
    "(.+)";
    "^(.+)$";
    "^(.*)$";
    "^a(bc)?$";
    "^(a|b)c|d(e|f)$";
    "^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])$";
    {p|^[Ee][Ss]2015(\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?$|p};
    "^[Ee][Ss]([356]|20(1[567]|2[02])|[Nn][Ee][Xx][Tt])$";
    "^([a-z]+|x)-$";
    "^a|[0-9]{2}$";
    "^(a|b)*$";
    "^(?=a)a|b$";
    "^/.*";
    "^a.*";
    {p|^a\\.*|p};
    {p|(^([0-9]+)\.([0-9]+)$)|(^\{[A-F0-9]{2}(-[A-F0-9]{1}){2}\}$)|p};
    "^[0-9]{1,}.[0-9]{1,}$";
    {p|^3\.1\.\d+(-.+)?$|p};
    "^([A-Za-z_][-A-Za-z0-9_.:]*)$";
    "^a.c$";
    "^[a-z].$";
    "x.y|^z";
    "^es|ms|x-$";
    "^(ab){2}$";
    "^(a|b){2}c$";
    "^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$";
    {p|^([a-z][a-z0-9]{0,3})(\.[a-z][a-z0-9]{0,3})*$|p};
    {p|^([a-z_$][a-z0-9_$]{0,3}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,3})$|p};
    "^([a-z]+)(,[a-z]+)+$";
    "^(a,)*b$";
    "^(ab,)+a$";
    "^a(,a)*$";
    "^[a-z]*(-[a-z]*)*$";
    "^(a-)*a-b$";
    "^(é,)*a$";
    {p|^(?=!+[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p};
    {p|^(?=!+[^a]+$)(?=(.*\w)).+$|p};
    (* Valid only without the u flag (identity escapes). *) {p|^[\&\@\_]+$|p};
    {p|a\&.b|p};
    {p|^[^\%]{1,3}$|p};
    {p|😀|\&|p};
    (* Sequences decided by the length of the string. *) "^a[a-z]{2,5}z$";
    "^.*x$";
    "^-?[0-9-]{0,3}0$";
    {p|^\/[^\*\?]*\/\*$|p};
    "^.{2,}é$";
    "^[^,]*,[^,]$";
    "^(a.*|.+b)$";
    {p|^(.*\.)*[a-z]$|p};
  |]

let alphabet =
  [|
    "a";
    "b";
    "z";
    "A";
    "X";
    "Z";
    "0";
    "1";
    "5";
    "9";
    "_";
    "-";
    ".";
    ":";
    "/";
    "@";
    "#";
    "$";
    "*";
    "!";
    "{";
    "}";
    "|";
    " ";
    "\n";
    "\xC2\xA0";
    "é";
    "µ";
    "😀";
    "\xE2\x80\xA8";
    "x-";
    "es";
    "ES";
    "ms";
    "txt";
    "Au";
    "to";
    "No";
    "ne";
    "2015";
    "Co";
    "re";
    "20";
    "15";
    "22";
    "2";
    ",";
    "a,";
    "ab,";
    "a-";
    "%";
    "&";
    "?";
  |]

(* A small deterministic generator for the tests that compare against a reference (xorshift of the Go tests). The
   state is the uint64 of the Go source, held in an Int64. *)
let seed = ref 0L

let next n =
  seed := Int64.logxor !seed (Int64.shift_left !seed 13);
  seed := Int64.logxor !seed (Int64.shift_right_logical !seed 7);
  seed := Int64.logxor !seed (Int64.shift_left !seed 17);
  Int64.to_int (Int64.unsigned_rem !seed (Int64.of_int n))

(* A text of fewer than most letters of the alphabet. *)
let random_text most =
  let text = Buffer.create 32 in
  for _ = 1 to next most do
    Buffer.add_string text alphabet.(next (Array.length alphabet))
  done;
  Buffer.contents text

(* The UTF-8 of a code point. *)
let rune c =
  let b = Buffer.create 4 in
  Buffer.add_utf_8_uchar b (Uchar.of_int c);
  Buffer.contents b

let scratch = Pattern_engine.create_scratch ()

(* Whether the regular expression engine is there. While the library is a stub it accepts no pattern, and then no
   pattern compiles at all. A test that needs the engine fails by name, once, and the faster matchers are tested
   through Pattern.shape, which needs no engine. *)
let engine_ready = Pattern_engine.valid "a"

let needs_engine name =
  if not engine_ready then Check.fail (name ^ " waits on the regular expression engine, which is still a stub");
  engine_ready

let bytes_is_ascii b = Pattern.is_ascii b 0 (Bytes.length b)

(* The answer of a matcher for a text given every way a caller can give it: as a string, as bytes with and without
   the knowledge that they are ASCII, and within a longer buffer (a match reads no byte outside its text). *)
let check_every_way p text expected =
  let what = Printf.sprintf "%S on %S" (Pattern.source p) text in
  let b = Bytes.of_string text in
  let len = Bytes.length b in
  Check.check (what ^ " as a string") (Pattern.match_string p scratch text = expected);
  Check.check (what ^ " as bytes") (Pattern.matches p scratch b 0 len (bytes_is_ascii b) = expected);
  Check.check (what ^ " not known to be ASCII") (Pattern.matches p scratch b 0 len false = expected);
  let longer = Bytes.of_string ("a\n" ^ text ^ "z!") in
  Check.check (what ^ " within a longer buffer")
    (Pattern.matches p scratch longer 2 len (Pattern.is_ascii longer 2 len) = expected);
  Check.check
    (what ^ " within a longer buffer that is not ASCII")
    (let longer = Bytes.of_string ("é" ^ text ^ "é") in
     Pattern.matches p scratch longer 2 len (Pattern.is_ascii longer 2 len) = expected)

(* Every matcher agrees with the engine on strings over an alphabet that exercises classes, anchors and non-ASCII. *)
let test_matchers_agree_with_the_engine () =
  Check.section "matchers agree with the engine";
  Check.equal_int "test patterns" ~expected:133 (Array.length test_patterns);
  Check.equal_int "letters" ~expected:53 (Array.length alphabet);
  if needs_engine "TestMatchersAgreeWithTheEngine" then begin
    seed := 0x2545F4914F6CDD1DL;
    let engines = ref 0 in
    Array.iter
      (fun source ->
        match (Pattern_engine.compile source, Pattern.compile source) with
        | Error _, _ -> Check.fail (Printf.sprintf "%S does not compile" source)
        | _, Error _ -> Check.fail (Printf.sprintf "%S is not a valid pattern" source)
        | Ok reference, Ok compiled ->
            Check.equal_string "the source" ~expected:source (Pattern.source compiled);
            if Pattern.kind_name compiled = "engine" then incr engines;
            let i = ref 1 in
            while !i <= 4000 do
              let text = random_text 9 in
              let s = Bytes.of_string text in
              let len = Bytes.length s in
              let got = Pattern.matches compiled scratch s 0 len (bytes_is_ascii s) in
              let want = Pattern_engine.is_match scratch reference s 0 len in
              if got <> want then begin
                Check.fail (Printf.sprintf "%S on %S: %b, the engine says %b" source text got want);
                i := 4000
              end
              else begin
                Check.check "" true;
                (* The same text within a longer buffer: a match reads no byte outside its text. *)
                if !i mod 16 = 0 then begin
                  let s = Bytes.of_string ("a\n" ^ text ^ "z!") in
                  let got = Pattern.matches compiled scratch s 2 len (Pattern.is_ascii s 2 len) in
                  if got <> want then begin
                    Check.fail (Printf.sprintf "%S within a longer buffer on %S" source text);
                    i := 4000
                  end
                  else Check.check "" true
                end
              end;
              incr i
            done)
      test_patterns;
    Check.checkf
      (!engines <= Array.length test_patterns / 2)
      "%d of %d patterns run on the engine" !engines (Array.length test_patterns);
    Printf.printf "%d of %d test patterns run on the engine\n" !engines (Array.length test_patterns)
  end

(* More shapes than the Go test has, each chosen to reach a branch of a matcher (a sequence decided by the string's
   length, an alternative of fixed width at the end or anywhere, a list with a final item, counts of nothing), against
   the engine in the same way. (TestMoreShapesAgreeWithTheEngine of the Object Pascal port.) *)
let more_patterns =
  [|
    "^";
    "^$";
    "$";
    "^a{0}$";
    "^(a){0}b$";
    "^(ab){3}$";
    "^.{0,}$";
    "^.{3}?$";
    "^(.{3})$";
    "^(.)$";
    "^(?:a|b)$";
    {p|^(a|\n|\v|\f)$|p};
    "[a-z]{2}$";
    "[a-z]{2}";
    {p|^[\d-]$|p};
    {p|^a\\$|p};
    {p|^a\$|p};
    "(a|b)?c";
    "^((a|b)(a|z)){3}$";
    "^(a|b)(-|:)(e|s)(2|0)(1|5)(a|b)$";
    "^x(?:,x)*$";
    "^(x,)+x$";
    "^(?:x-)*[a-z]+$";
    "^([a-z],[0-9])*$";
    {p|^[a-z]+(\.[a-z]+)+$|p};
    {p|^(?=[^a]+$)(?=.*\w).+$|p};
    {p|^(?=!+[^!]+$)(?=(?:.*\w)).+$|p};
    "^é.*";
    "^a.*b.*";
    {p|^\..*|p};
    "^a|b.*";
    "^[a-z]{4294967295}$";
    {p|^[^\d\n]{2}[\W][\w]?$|p};
    "^a?a?a$";
    "^[ab]*[bc]$";
    "^[ab]+[bc]+$";
    "x-$";
    "^-$|^x-|es$";
    "[0-9]{2}|^a.$";
    "^(es|ms){2}$";
    "^[a-z]*(-[a-z]+)*$";
    "^(a,)+b$";
    "^(,a)*$";
    {p|^\t|\n$|p};
    {p|^[\t\n ]+$|p};
    {p|^\D\W\d\w$|p};
    (* Not in the Object Pascal test. A "$" inside a list is not the end of the list: these are not separated lists. *)
    "^x(,a$)*$";
    "^x$(,a)*$";
    "^(a,$)*b$";
    "^(a$,)*b$";
  |]

(* The faster matchers that have shapes (the two that have none are a few patterns by their text). *)
let shaped_kinds =
  [ "literal"; "sequence"; "separated list"; "line"; "literals"; "alternatives"; "excluded class with word" ]

let test_more_shapes_agree_with_the_engine () =
  Check.section "more shapes agree with the engine";
  (* Which matcher each takes needs no engine. *)
  List.iter
    (fun kind ->
      Check.check
        ("no pattern of these takes matcher " ^ kind)
        (Array.exists
           (fun source -> match Pattern.shape source with Some p -> Pattern.kind_name p = kind | None -> false)
           more_patterns))
    shaped_kinds;
  if needs_engine "TestMoreShapesAgreeWithTheEngine" then begin
    seed := 0x9E3779B97F4A7C15L;
    Array.iter
      (fun source ->
        match (Pattern_engine.compile source, Pattern.compile source) with
        | Error _, _ -> Check.fail (Printf.sprintf "%S does not compile" source)
        | _, Error _ -> Check.fail (Printf.sprintf "%S is not a valid pattern" source)
        | Ok reference, Ok compiled ->
            let i = ref 1 in
            while !i <= 4000 do
              let text = random_text 7 in
              let s = Bytes.of_string text in
              let len = Bytes.length s in
              let got = Pattern.matches compiled scratch s 0 len (bytes_is_ascii s) in
              let want = Pattern_engine.is_match scratch reference s 0 len in
              if got <> want then begin
                Check.fail (Printf.sprintf "%S on %S: %b, the engine says %b" source text got want);
                i := 4000
              end
              else Check.check "" true;
              incr i
            done)
      more_patterns
  end

(* The matcher each pattern is expected to take. *)
let expected_kinds =
  [
    ("everything", [ ""; ".*"; "^.*"; {p|^[\s\S]*$|p}; "^(.*)" ]);
    ( "sequence",
      [
        "^[@$_#]";
        {p|^[a-zA-Z0-9_\-]*$|p};
        "^#[0-9a-fA-F]{6}$";
        "^[a-z][a-z0-9_]+$";
        {p|^\d{4}-\d{2}-\d{2}$|p};
        (* Decided by the length of the string. *)
        "^[a-z]*a$";
        {p|^\/[^\*\?]*\/\*$|p};
      ] );
    ("literal", [ "^x-"; {p|^\/|p}; "^abc$"; {p|^\-a|p} ]);
    ("has content", [ ".+"; "^.+"; "(.+)" ]);
    ("line", [ "^.{1,256}$"; "^.+$"; "^.*$"; "^(.*)$"; "^(.+)$" ]);
    ("literals", [ "^(ab|cd)$"; "^(?:es|ES|x-)$" ]);
    ( "alternatives",
      [
        "^ab|cd$";
        "a|b";
        "^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])$";
        {p|^[Ee][Ss]2015(\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?$|p};
        "^[1-5](?:[0-9]{2}|XX)$";
        {p|(^([0-9]+)\.([0-9]+)$)|(^\{[A-F0-9]{8}(-[A-F0-9]{4}){3}-[A-F0-9]{12}\}$)|p};
        "^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])|([b|B][o|O][t|T][t|T][o|O][m|M])$";
        "^([A-Za-z_][-A-Za-z0-9_.:]*)$";
        {p|^\/[^\*\?\&\%]*(\/\*)?$|p};
        {p|^.*\.(?:txt|trie)(?:\.gz)?$|p};
      ] );
    ( "separated list",
      [
        "^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$";
        {p|^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$|p};
        {p|^([a-z_$][a-z0-9_$]{0,39}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$|p};
      ] );
    ("excluded class with word", [ {p|^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p} ]);
    ( "engine",
      [
        "(base64key|awskms)://(.*)";
        {p|\bfoo|p};
        {p|^\p{L}+$|p};
        {p|^((\.(?!\.)\/)?\w+\/?)+$|p};
        {p|^\1(a)|p};
        {p|a\&.b|p};
        "^[a-z]*a[a-z]*$";
      ] );
  ]

let test_simple_patterns_take_the_fast_matchers () =
  Check.section "simple patterns take the fast matchers";
  (* The choice of a faster matcher needs no engine: a pattern with none is one the engine takes. *)
  List.iter
    (fun (kind, patterns) ->
      List.iter
        (fun source ->
          let got = match Pattern.shape source with Some p -> Pattern.kind_name p | None -> "engine" in
          Check.checkf (got = kind) "%S has the shape of matcher %s, want %s" source got kind)
        patterns)
    expected_kinds;
  if needs_engine "TestSimplePatternsTakeTheFastMatchers" then
    List.iter
      (fun (kind, patterns) ->
        List.iter
          (fun source ->
            match Pattern.compile source with
            | Error _ -> Check.fail (Printf.sprintf "%S is not a valid pattern" source)
            | Ok p ->
                Check.checkf
                  (Pattern.kind_name p = kind)
                  "%S takes matcher %s, want %s" source (Pattern.kind_name p) kind)
          patterns)
      expected_kinds

let test_invalid_patterns_are_rejected () =
  Check.section "invalid patterns are rejected";
  (* While the engine is a stub every pattern is rejected, and this would pass for the wrong reason. *)
  if needs_engine "TestInvalidPatternsAreRejected" then begin
    List.iter
      (fun source -> Check.check (Printf.sprintf "%S compiled" source) (Pattern.compile source = Error Pattern_engine.Invalid))
      [
        "a("; "[a"; "a{2,1}"; "(?<n>a)(?<n>b)"; "*a"; "^(?=a"; {p|(?<n>a)[\k]|p}; {p|^[\k]+(?<n>a)$|p};
        "^a{2147483648,2147483647}$"; "^[a-z]{99999999999999999999,99999999999999999998}$"; "^.{4294967296,1}$"; "(?<m>a)(?:x|(?<m>b))";
      ]
      [@ocamlformat "disable"];
    Check.check "valid is the u flag grammar"
      (Pattern_engine.valid "^a+$" && (not (Pattern_engine.valid {p|a\&.b|p})) && not (Pattern_engine.valid "a("))
  end

(* A class with a member outside ASCII, in the shape "^(?=[^SET]+$)(?=(.*\w)).+$". The shape's set holds ASCII
   characters only. Reading such a member as an entry of the set raised an error when the schema was compiled (v0.1.0
   of the Go module). The pattern has no shape and is matched by the engine. *)
let test_excluded_class_with_a_member_outside_ascii () =
  Check.section "excluded class with a member outside ASCII";
  let word c = c = Char.code '_' || (c >= 0x30 && c <= 0x39) || (c >= 0x61 && c <= 0x7A) || (c >= 0x41 && c <= 0x5A) in
  let cases =
    [
      ({p|^(?=[^é]+$)(?=(.*\w)).+$|p}, fun c -> c = 0xE9);
      ({p|^(?=[^xé]+$)(?=(?:.*\w)).+$|p}, fun c -> c = Char.code 'x' || c = 0xE9);
      ({p|^(?=[^中/]+$)(?=.*\w).+$|p}, fun c -> c = 0x4E2D || c = Char.code '/');
      ({p|^(?=[^😀]+$)(?=(.*\w)).+$|p}, fun c -> c = 0x1F600);
      (* A range that starts in ASCII and ends outside it. *)
      ({p|^(?=[^m-é]+$)(?=(.*\w)).+$|p}, fun c -> c >= Char.code 'm' && c <= 0xE9);
    ]
  in
  let texts =
    [ "C1"; ")a"; "abc"; "d-8p"; "p.q"; "x1"; "a/b"; "---"; "é1"; "aé"; "中a"; "a😀"; "zè"; "0"; "ABC"; "a{"; "al" ]
  in
  (* Whether some character of the text satisfies the test. *)
  let any test text =
    let rec from i =
      i < String.length text
      &&
      let d = String.get_utf_8_uchar text i in
      test (Uchar.to_int (Uchar.utf_decode_uchar d)) || from (i + Uchar.utf_decode_length d)
    in
    from 0
  in
  List.iter
    (fun (source, _) -> Check.check (Printf.sprintf "%S has no shape" source) (Pattern.shape source = None))
    cases;
  if needs_engine "TestExcludedClassWithAMemberOutsideASCII" then
    List.iter
      (fun (source, excluded) ->
        match Pattern.compile source with
        | Error _ -> Check.fail (Printf.sprintf "%S is not a valid pattern" source)
        | Ok p ->
            Check.equal_string (source ^ " is matched by the engine") ~expected:"engine" (Pattern.kind_name p);
            List.iter
              (fun text ->
                let want = any word text && not (any excluded text) in
                Check.checkf (Pattern.match_string p scratch text = want) "%s on %S: want %b" source text want)
              texts)
      cases

(* m checks one answer: of the faster matcher of the pattern by itself when it has one, and of the compiled pattern
   when the engine is there. *)
let m source text expected =
  (match Pattern.shape source with Some p -> check_every_way p text expected | None -> ());
  if engine_ready then
    match Pattern.compile source with
    | Error _ -> Check.fail (Printf.sprintf "%S is not a valid pattern" source)
    | Ok p -> check_every_way p text expected

(* The matchers one by one, on texts chosen for each. (TestMatchersByHand of the Object Pascal port.) *)
let test_matchers_by_hand () =
  Check.section "matchers by hand";
  m "" "anything" true;
  m "^.+" "\na" false;
  m "^.+" "a\n" true;
  m ".+" ("\n\r" ^ rune 0x2028) false;
  m ".+" "\né" true;
  m "^.{1,3}$" "aé😀" true;
  m "^.{1,3}$" "aé😀b" false;
  m "^.{1,3}$" "" false;
  m "^.{2}$" "a\n" false;
  m "^x-" "x-a" true;
  m "^x-" "ax-" false;
  m "^abc$" "abc" true;
  m "^abc$" "abcd" false;
  m "^(a|b|c|d|e|f|g|h|i|j|z)$" "z" true;
  m "^(a|b|c|d|e|f|g|h|i|j|z)$" "y" false;
  m "^(a|b|c|d|e|f|g|h|i|j|z)$" "zz" false;
  m {p|^(?:es|ES|x-|a\.b)$|p} "a.b" true;
  m {p|^(?:es|ES|x-|a\.b)$|p} "aXb" false;
  m "^[a-z][a-z0-9_]{0,29}$" "a_29" true;
  m "^[a-z][a-z0-9_]{0,29}$" ("a" ^ String.make 30 'b') false;
  m "^[a-z]*a$" "bcda" true;
  m "^[a-z]*a$" "bcdb" false;
  m "^.{2,}é$" "abé" true;
  m "^.{2,}é$" "aé" false;
  m "^ab|cd$" "xcd" true;
  m "^ab|cd$" "xab" false;
  m "^a|[0-9]{2}$" "x12" true;
  m "^a|[0-9]{2}$" "x1é" false;
  m "x.y|^z" "aax😀yb" true;
  m "x.y|^z" "aaxy" false;
  m "^([a-z]+)(,[a-z]+)+$" "a,b,c" true;
  m "^([a-z]+)(,[a-z]+)+$" "a" false;
  m "^(a,)*b$" "a,a,b" true;
  m "^(a,)*b$" "a,a," false;
  m {p|^(?=!+[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p} "!!ab" true;
  m {p|^(?=!+[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p} "ab" false;
  m {p|^(?=!+[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p} "!!" false;
  m {p|^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p} "a b" true;
  m {p|^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p} "- -" false;
  m {p|^\p{L}+$|p} "aé中" true;
  m {p|^\p{L}+$|p} "a1" false

(* The faster matchers on answers that can be stated without the engine: for each pattern the matcher its shape takes,
   the texts it matches and the texts it does not. Not in the Go or Object Pascal tests. It covers the faster matchers
   whether or not the engine is there. *)
let hand kind source yes no =
  (match Pattern.shape source with
  | None -> Check.fail (Printf.sprintf "%S has no shape, want %s" source kind)
  | Some p -> Check.equal_string (Printf.sprintf "the shape of %S" source) ~expected:kind (Pattern.kind_name p));
  List.iter (fun text -> m source text true) yes;
  List.iter (fun text -> m source text false) no

let test_fast_matchers_without_the_engine () =
  Check.section "fast matchers without the engine";
  let ls = rune 0x2028 and ps = rune 0x2029 in
  hand "everything" {p|^[\s\S]*$|p} [ ""; "a\nb"; "é" ] [];
  hand "everything" ".*" [ ""; "\n" ] [];
  hand "has content" "." [ "a"; "\n\ra"; "é"; ls ^ "😀" ] [ ""; "\n"; "\r\n" ^ ls ^ ps ];
  hand "has content" "(.+)" [ "\r\nx" ] [ "\r\n" ];
  hand "has content" "^." [ "é"; "a\n" ] [ ""; ps ^ "a"; "\ra" ];
  hand "line" "^.*$" [ ""; "abc"; "é😀" ] [ "a\nb"; ls; "a\r" ];
  hand "line" "^.+$" [ "a"; "😀" ] [ ""; "\n" ];
  hand "line" "^.{2}$" [ "ab"; "é😀" ] [ "a"; "abc"; "é"; "a\n" ];
  hand "line" "^.{2,}$" [ "ab"; "abc"; "éé" ] [ "a"; "é"; "" ];
  hand "line" "^(.{3})$" [ "abc"; "a中c" ] [ "ab"; "abcd" ];
  hand "line" "^.{0,}$" [ ""; "a" ] [ "\r" ];
  hand "sequence" "^.{3}?$" [ "abc" ] [ "abcd"; "ab" ];
  hand "line" "^.{1,256}$" [ "a"; String.make 256 'a' ] [ ""; String.make 257 'a'; String.make 300 'a' ^ "é" ];
  hand "literal" {p|^\/|p} [ "/"; "/a" ] [ "a/"; "" ];
  hand "literal" {p|^\-a|p} [ "-a"; "-ab" ] [ "a-a"; "-" ];
  hand "literal" "^abc$" [ "abc" ] [ "ab"; "abcd"; "xabc"; "" ];
  hand "literal" "^" [ ""; "a" ] [];
  hand "literal" "^$" [ "" ] [ "a"; "\n" ];
  hand "literal" {p|^a\$|p} [ "a$"; "a$b" ] [ "a"; "ba$" ];
  hand "sequence" "^[@$_#]" [ "#x"; "@"; "_é" ] [ "x#"; ""; "é" ];
  hand "sequence" "^#[0-9a-fA-F]{6}$" [ "#00ffAA" ] [ "#00ffA"; "#00ffAAA"; "#00ffAG"; "00ffAAA"; "#00ffAé" ];
  hand "sequence" {p|^\d{4}-\d{2}-\d{2}$|p} [ "2026-10-10" ] [ "2026-1-10"; "2026-10-100"; "2026/10/10" ];
  hand "sequence" "^[a-z][a-z0-9_]+$" [ "ab"; "a_1" ] [ "a"; "aé"; "1a"; "ab-" ];
  hand "sequence" "^[^:]+:[^:]+$" [ "a:b"; "é:😀"; "a" ^ ls ^ ":b" ] [ "a:b:c"; ":b"; "a:"; "ab" ];
  hand "sequence" "^[a-z]{1,2}$" [ "a"; "ab" ] [ ""; "abc"; "A" ];
  hand "sequence" {p|^[^\d\n]{2}[\W][\w]?$|p}
    [ "ab-"; "ab-c"; "é" ^ ls ^ "-"; "ab" ^ ps ]
    [ "a1-"; "ab-cd"; "a\n-"; "abc" ];
  hand "sequence" "^a.c$" [ "abc"; "aéc"; "a😀c" ] [ "a\nc"; "ac"; "abbc"; "a" ^ ls ^ "c" ];
  hand "sequence" "^[a-z]{4294967295}$" [] [ ""; "abc" ];
  (* Decided by the length of the string. *)
  hand "sequence" "^a[a-z]{2,5}z$" [ "abcz"; "abcdefz" ] [ "abz"; "abcdefgz"; "abCz"; "abcy"; "az"; "ébcz" ];
  hand "sequence" "^.*x$" [ "x"; "ax"; "éx"; "xx" ] [ "a\nx"; "xa"; ""; "xé" ];
  hand "sequence" "^[^,]*,[^,]$" [ "ab,c"; ",c"; "é,😀" ] [ "a,b,c"; "ab,"; "abc"; "," ];
  hand "sequence" "^[ab]*[bc]$" [ "aab"; "abc"; "c"; "b" ] [ "ca"; ""; "abca"; "aéc" ];
  hand "sequence" {p|^\/[^\*\?]*\/\*$|p} [ "//*"; "/a/b/*" ] [ "/*"; "/a*/*"; "/a/b" ];
  hand "separated list" "^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$"
    [ "en"; "en-US"; "en-US-x"; "eng-Latn-GB" ]
    [ "en-"; "e"; "en--US"; "en-toolong"; "engl"; ""; "en-US-"; "en-é" ];
  hand "separated list" "^([a-z]+)(,[a-z]+)*$" [ "a"; "a,b"; "ab,cd,ef" ] [ ""; "a,"; ",a"; "a,,b"; "a,B"; "a,é" ];
  hand "separated list" "^([a-z]+)(,[a-z]+)+$" [ "a,b"; "a,b,c" ] [ "a"; "a,"; "" ];
  hand "separated list" "^a(,a)*$" [ "a"; "a,a,a" ] [ "a,a,"; ""; "aa"; "a,b" ];
  hand "separated list" "^(a,)*b$" [ "b"; "a,b"; "a,a,b" ] [ "ab"; "a,a,"; ""; "a,bb"; ",b" ];
  hand "separated list" "^(ab,)+a$" [ "ab,a"; "ab,ab,a" ] [ "a"; "ab,b"; "ab,"; "ab,a," ];
  hand "separated list" {p|^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$|p}
    [ "a"; "a.b.c"; "$x.y_1"; "a." ^ String.make 40 'b' ]
    [ "a..b"; "a.b."; "1a"; ""; ".a"; "a." ^ String.make 41 'b'; "a.é" ];
  hand "separated list" "^[a-z]*(-[a-z]*)*$" [ ""; "a"; "-"; "a--b-"; "ab-cd" ] [ "A"; "a-B"; "a_b" ];
  hand "literals" "^(ab|cd)$" [ "ab"; "cd" ] [ "abc"; ""; "a"; "abcd"; "xab" ];
  hand "literals" {p|^(?:es|ES|x-|a\.b)$|p} [ "es"; "ES"; "x-"; "a.b" ] [ "x"; "aXb"; "Es"; "es " ];
  hand "literals" {p|^(a|\n|\v|\f)$|p} [ "a"; "\n"; "\011"; "\012" ] [ "n"; "\\n"; ""; "\r" ];
  hand "literals" "^(a|b|c|d|e|f|g|h|i|j|z)$"
    [ "a"; "b"; "c"; "d"; "e"; "f"; "g"; "h"; "i"; "j"; "z" ]
    [ "y"; "zz"; ""; "k"; "A"; "é" ];
  hand "literals" "^(alpha|beta|gamma|delta|epsilon|zeta|eta|theta|iota|kappa|lambda|mu|nu|xi|omicron|pi|rho|sigma)$"
    [
      "alpha";
      "beta";
      "gamma";
      "delta";
      "epsilon";
      "zeta";
      "eta";
      "theta";
      "iota";
      "kappa";
      "lambda";
      "mu";
      "nu";
      "xi";
      "omicron";
      "pi";
      "rho";
      "sigma";
    ]
    [ ""; "kapp"; "alphabeta"; "Alpha"; "tau"; "sigm"; "sigmaa"; "μ" ];
  hand "literals" "^(a|b|c|d|e|f|g|h|i||k|a)$" [ ""; "a"; "k" ] [ "j"; "aa"; "|" ];
  hand "alternatives" "^ab|cd$" [ "ab"; "abx"; "xcd"; "cd"; "écd" ] [ "cdx"; "xab"; ""; "a"; "cdé" ];
  hand "alternatives" "a|b" [ "a"; "xxb"; "éb" ] [ "xyz"; ""; "é" ];
  hand "alternatives" "^x-|es|ms$" [ "x-1"; "1es1"; "1ms"; "ms" ] [ "ms1"; "1x-"; "e s" ];
  hand "alternatives" {p|^a\$|b|p} [ "a$x"; "xb" ] [ "a"; "xa$" ];
  hand "alternatives" "^[1-5](?:[0-9]{2}|XX)$" [ "200"; "5XX"; "404" ] [ "600"; "2000"; "2X0"; "20"; "éXX" ];
  hand "alternatives" "^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])$"
    [ "auto"; "AUTOx"; "xnone"; "NoNe"; "|uto"; "a|to"; "énone" ]
    [ "nonex"; "xauto"; "aut"; ""; "none\n" ];
  hand "alternatives" "^a|[0-9]{2}$" [ "x12"; "a"; "ab"; "é12"; "12" ] [ "12x"; "1"; "x1é"; ""; "é1" ];
  hand "alternatives" "x.y|^z" [ "xay"; "zx"; "axéyb"; "aax😀yb" ] [ "x\ny"; "aaxy"; "az"; ""; "x" ^ ls ^ "y" ];
  hand "alternatives" "[0-9]{2}|^a.$" [ "x12x"; "a1"; "a12"; "é12é"; "aé" ] [ "ab1"; "é1é2"; ""; "a\n"; "1" ];
  hand "alternatives" {p|^[Ee][Ss]2015(\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?$|p}
    [ "es2015"; "ES2015.Core"; "es2015.proxy" ]
    [ "es2015."; "es2015.corex"; "es2016"; "es2015.cor" ];
  hand "alternatives" {p|^.*\.(?:txt|trie)(?:\.gz)?$|p}
    [ "a.txt"; "a.trie.gz"; ".txt"; "é.txt.gz"; "a.b.trie" ]
    [ "a.txt.g"; "atxt"; "a\n.txt"; ""; "a.txt.gz.gz"; "a.tx" ];
  hand "alternatives" "^([A-Za-z_][-A-Za-z0-9_.:]*)$" [ "a"; "a-b.c:d"; "_" ] [ "-a"; ""; "a b"; "aé" ];
  hand "alternatives" {p|^\/[^\*\?\&\%]*(\/\*)?$|p} [ "/"; "/a/b"; "/a/*"; "/é" ] [ "/a*"; "a"; "/a/*/"; "/a?" ];
  hand "alternatives" "^(ab){3}$" [ "ababab" ] [ "abab"; "abababab"; "" ];
  hand "alternatives" "^(es|ms){2}$" [ "eses"; "esms"; "msms" ] [ "es"; "esmsx"; "eses\n" ];
  hand "excluded class with word" {p|^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p} [ "a b"; "éa"; "- a -"; "_" ]
    [ "- -"; "a,b"; "é"; ""; "a" ^ ls; "a\r"; "a]"; "a["; "a\n"; "!a" ];
  hand "excluded class with word" {p|^(?=[^a-c\n]+$)(?=(.*\w)).+$|p} [ "d"; "xyz"; "é1" ] [ "a"; "xbz"; "---"; "" ];
  hand "excluded class with word" {p|^(?=!+[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p} [ "!!ab"; "!a"; "!é_" ]
    [ "ab"; "!!"; "!a!b"; ""; "!"; "!-"; "!a,b" ];
  hand "excluded class with word" {p|^(?=[^a]+$)(?=.*\w).+$|p} [ "b"; "!b" ] [ "a"; "ba"; "!" ];
  (* A "$" inside a list is not the end of the list, so these have no shape. *)
  List.iter
    (fun source -> Check.check (Printf.sprintf "%S has no shape" source) (Pattern.shape source = None))
    [ "^x(,a$)*$"; "^x$(,a)*$"; "^(a,$)*b$"; "^(a$,)*b$" ];
  if engine_ready then begin
    m "^x(,a$)*$" "x" true;
    m "^x(,a$)*$" "x,a" true;
    m "^x(,a$)*$" "x,a,a" false;
    m "^x$(,a)*$" "x" true;
    m "^x$(,a)*$" "x,a" false
  end

let test_is_ascii_and_source () =
  Check.section "is_ascii and source";
  let b = Bytes.of_string "aéb" in
  Check.check "is_ascii of nothing" (Pattern.is_ascii b 0 0);
  Check.check "is_ascii of a" (Pattern.is_ascii b 0 1);
  Check.check "is_ascii of aé" (not (Pattern.is_ascii b 0 3));
  Check.check "is_ascii of b" (Pattern.is_ascii b 3 1);
  Check.check "is_ascii of the second byte of é" (not (Pattern.is_ascii b 2 2));
  match Pattern.shape "^x-" with
  | None -> Check.fail "^x- has no shape"
  | Some p -> Check.equal_string "the source" ~expected:"^x-" (Pattern.source p)

(* ---------------------------------------------------------------------------------------------------------------------
   Allocation *)

let hits = ref 0

(* The matching loop that is measured. It allocates nothing itself: the texts and the flags are made beforehand. *)
let run p texts offs lens flags rounds =
  for _ = 1 to rounds do
    for i = 0 to Array.length texts - 1 do
      if Pattern.matches p scratch (Array.get texts i) (Array.get offs i) (Array.get lens i) (Array.get flags i) then
        incr hits
    done
  done

(* The words allocated on the minor heap by the loop, the measurement included. *)
let minor_words p texts offs lens flags rounds =
  let before = Gc.minor_words () in
  run p texts offs lens flags rounds;
  let after = Gc.minor_words () in
  after -. before

let allocation_texts =
  [
    "";
    "a";
    "z";
    "abc";
    "abcd";
    "x-a";
    "ab";
    "cd";
    "xcd";
    "a,b,c";
    "a,a,b";
    "a,a,";
    "!!ab";
    "a b";
    "- -";
    "x12";
    "200";
    "5XX";
    "aax😀yb";
    "abé";
    "aé中";
    "\n";
    "\n\r\xE2\x80\xA8";
    "foo bar";
    "a foo";
    "base64key://x";
    "./a/b";
    "a/b/c/";
    "en-US-x";
    "es2015.Core";
    "a.trie.gz";
    "#00ffAA";
    "bcda";
    "a_29";
    "kappa";
    String.make 300 'a';
    String.make 300 'a' ^ "é";
    "é" ^ String.make 64 'b' ^ ",b";
    "😀😀😀";
    "a\nb";
    "2026-10-10";
  ]

(* A pattern for every matcher and every branch of one, with the matcher it takes. *)
let allocation_patterns =
  [
    ("everything", "");
    ("literal", "^x-");
    ("literal", "^abc$");
    ("sequence", "^[a-z][a-z0-9_]{0,29}$");
    ("sequence", "^#[0-9a-fA-F]{6}$");
    ("sequence", "^[^:]+:[^:]+$");
    ("sequence", "^[a-z]*a$");
    ("sequence", "^.*x$");
    ("separated list", "^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$");
    ("separated list", "^([a-z]+)(,[a-z]+)*$");
    ("separated list", "^(a,)*b$");
    ("has content", ".+");
    ("has content", "^.+");
    ("line", "^.{1,3}$");
    ("line", "^.{1,256}$");
    ("literals", "^(ab|cd)$");
    ("literals", "^(a|b|c|d|e|f|g|h|i|j|z)$");
    ("literals", "^(alpha|beta|gamma|delta|epsilon|zeta|eta|theta|iota|kappa|lambda|mu)$");
    ("alternatives", "^ab|cd$");
    ("alternatives", "a|b");
    ("alternatives", "^a|[0-9]{2}$");
    ("alternatives", "x.y|^z");
    ("alternatives", "[0-9]{2}|^a.$");
    ("alternatives", "^[1-5](?:[0-9]{2}|XX)$");
    ("alternatives", {p|^.*\.(?:txt|trie)(?:\.gz)?$|p});
    ("alternatives", {p|^[Ee][Ss]2015(\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?$|p});
    ("excluded class with word", {p|^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p});
    ("excluded class with word", {p|^(?=!+[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$|p});
    ("engine", {p|^\p{L}+$|p});
    ("engine", {p|\bfoo|p});
    ("engine", "(base64key|awskms)://(.*)");
    ("engine", {p|^(?!x)[\w\/.]+$|p});
    ("engine", "^[a-z]*a[a-z]*$");
    ("engine", "^(é,)*a$");
    (* Valid only without the u flag, so matched by UTF-16 code unit. One with a faster matcher goes to its engine for
       the texts with a character beyond the Basic Multilingual Plane. *)
    ("sequence", {p|^[^\%]{1,3}$|p});
    ("sequence", {p|^.{3}\_?$|p});
    ("engine", {p|\-{0}^(..)\1|p});
    ("engine", {p|\-{0}(?<=\W)\W$|p});
  ]

(* Matching allocates nothing, whichever matcher runs: the words allocated by a loop of matches are those of the same
   measurement with no match in it. Every text is given three ways: by itself with what is known of it, by itself as
   not known to be ASCII, and within a longer buffer. *)
let test_matching_allocates_nothing () =
  Check.section "matching allocates nothing";
  let texts = Array.of_list allocation_texts in
  let n = Array.length texts in
  let buffers = Array.make (3 * n) Bytes.empty in
  let offs = Array.make (3 * n) 0 in
  let lens = Array.make (3 * n) 0 in
  let flags = Array.make (3 * n) false in
  Array.iteri
    (fun i text ->
      let len = String.length text in
      let b = Bytes.of_string text in
      buffers.(3 * i) <- b;
      lens.(3 * i) <- len;
      flags.(3 * i) <- bytes_is_ascii b;
      buffers.((3 * i) + 1) <- b;
      lens.((3 * i) + 1) <- len;
      buffers.((3 * i) + 2) <- Bytes.of_string ("é\n" ^ text ^ "z!é");
      offs.((3 * i) + 2) <- 3;
      lens.((3 * i) + 2) <- len;
      flags.((3 * i) + 2) <- bytes_is_ascii b)
    texts;
  let rounds = 200 in
  let kinds = ref [] in
  let measure kind source p =
    Check.equal_string (Printf.sprintf "the matcher of %S" source) ~expected:kind (Pattern.kind_name p);
    (* Once beforehand, so that the working memory of the engine has grown to what these texts need. *)
    run p buffers offs lens flags 1;
    let empty = minor_words p buffers offs lens flags 0 in
    hits := 0;
    let words = minor_words p buffers offs lens flags rounds -. empty in
    Check.checkf (words = 0.0) "%S (%s) allocates %.0f words in %d matches" source kind words (rounds * 3 * n);
    Check.checkf (!hits > 0) "%S matches none of the texts" source;
    if not (List.mem kind !kinds) then kinds := kind :: !kinds
  in
  List.iter
    (fun (kind, source) ->
      if kind <> "engine" && Pattern_engine.valid source then
        match Pattern.shape source with
        | None -> Check.fail (Printf.sprintf "%S has no shape" source)
        | Some p -> measure kind source p)
    allocation_patterns;
  if needs_engine "The allocation test of the patterns the engine matches" then
    List.iter
      (fun (kind, source) ->
        match Pattern.compile source with
        | Error _ -> Check.fail (Printf.sprintf "%S is not a valid pattern" source)
        | Ok p -> measure kind source p)
      allocation_patterns;
  List.iter
    (fun kind -> Check.check ("no allocation was measured for matcher " ^ kind) (List.mem kind !kinds))
    ("everything" :: "has content" :: shaped_kinds);
  (* The measurement sees an allocation when there is one: match_string copies its text. *)
  (match Pattern.shape "^abc$" with
  | None -> Check.fail "^abc$ has no shape"
  | Some p ->
      let text = Sys.opaque_identity "abcd" in
      let before = Gc.minor_words () in
      for _ = 1 to rounds do
        if Pattern.match_string p scratch text then incr hits
      done;
      let after = Gc.minor_words () in
      Check.checkf
        (after -. before >= float_of_int rounds)
        "the measurement saw %.0f words of %d copies" (after -. before) rounds);
  Printf.printf "matching allocates nothing: %d patterns, %d matches each, matchers %s\n"
    (List.length allocation_patterns)
    (rounds * 3 * n)
    (String.concat ", " (List.rev !kinds))

(* Patterns with an anchor, a word boundary or a lookaround in a place where a matcher for a shape could lose it, and
   patterns with an escaped "$" or "^", which is a literal character. Each has the texts it matches and the texts it
   does not. The answers are V8's: new RegExp(pattern, "u").test(text). (anchorCases of the Go source.) *)
let anchor_cases =
  [
    ({p|^x(,a$)*$|p}, [ "x"; "x,a" ], [ "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^x(,a$)+$|p}, [ "x,a" ], [ "x"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^x(?:,a$)*$|p}, [ "x"; "x,a" ], [ "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^x$(,a)*$|p}, [ "x" ], [ "x,a"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^(x$)(,a)*$|p}, [ "x" ], [ "x,a"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^(?:x$)(,a)+$|p}, [], [ "x"; "x,a"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^$(,a)*$|p}, [ "" ], [ ",a"; ",a,a"; "a" ]);
    ({p|^(a,$)*b$|p}, [ "b" ], [ "a,b"; "a,a,b"; "a,"; ""; "a,a" ]);
    ({p|^(a,$)+b$|p}, [], [ "b"; "a,b"; "a,a,b"; "a,"; ""; "a,a" ]);
    ({p|^(?:a,$)*b$|p}, [ "b" ], [ "a,b"; "a,a,b"; "a,"; ""; "a,a" ]);
    ({p|^([a-z]+)(\.[a-z]+$)*$|p}, [ "a"; "a.b" ], [ "a.b.c"; "a."; "" ]);
    ({p|^([a-z]+\.$)*[a-z]+$|p}, [ "a" ], [ "a.b"; "a.b.c"; ".a"; "" ]);
    ({p|^x(,a$$)*$|p}, [ "x"; "x,a" ], [ "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^x(^,a)*$|p}, [ "x" ], [ "x,a"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^x(,^a)*$|p}, [ "x" ], [ "x,a"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^^x(,a)*$|p}, [ "x"; "x,a"; "x,a,a"; "x,a,a,a" ], [ ""; "x,"; "x,b" ]);
    ({p|^(^x)(,a)*$|p}, [ "x"; "x,a"; "x,a,a"; "x,a,a,a" ], [ ""; "x,"; "x,b" ]);
    ({p|^(^a,)*b$|p}, [ "b"; "a,b" ], [ "a,a,b"; "a,"; ""; "a,a" ]);
    ({p|^(a,)*^b$|p}, [ "b" ], [ "a,b"; "a,a,b"; "a,"; ""; "a,a" ]);
    ({p|^x(,(a$))*$|p}, [ "x"; "x,a" ], [ "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^x(,(?:a$))*$|p}, [ "x"; "x,a" ], [ "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^x(,a|$)*$|p}, [ "x"; "x,a"; "x,a,a"; "x,a,a,a" ], [ ""; "x,"; "x,b" ]);
    ({p|^x(,(a|$))*$|p}, [ "x"; "x,a"; "x,a,a"; "x,a,a,a"; "x," ], [ ""; "x,b" ]);
    ({p|^x((^|,)a)*$|p}, [ "x"; "x,a"; "x,a,a"; "x,a,a,a" ], [ ""; "x,"; "x,b" ]);
    ({p|^(a,)*(b$)$|p}, [ "b"; "a,b"; "a,a,b" ], [ "a,"; ""; "a,a" ]);
    ({p|^(a,)*b$$|p}, [ "b"; "a,b"; "a,a,b" ], [ "a,"; ""; "a,a" ]);
    ({p|^x(,a\b)*$|p}, [ "x"; "x,a"; "x,a,a"; "x,a,a,a" ], [ ""; "x,"; "x,b"; "x,ab" ]);
    ({p|^x(\b,a)*$|p}, [ "x"; "x,a"; "x,a,a"; "x,a,a,a" ], [ ""; "x,"; "x,b" ]);
    ({p|^x(,\Ba)*$|p}, [ "x" ], [ "x,a"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^x(\B,a)*$|p}, [ "x" ], [ "x,a"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^x\b(,a)*$|p}, [ "x"; "x,a"; "x,a,a"; "x,a,a,a" ], [ ""; "x,"; "x,b" ]);
    ({p|^x\B(,a)*$|p}, [], [ "x"; "x,a"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^(a\b,)*b$|p}, [ "b"; "a,b"; "a,a,b" ], [ "a,"; ""; "a,a" ]);
    ({p|^(a,\B)*b$|p}, [ "b" ], [ "a,b"; "a,a,b"; "a,"; ""; "a,a" ]);
    ({p|^x(,a(?=,|$))*$|p}, [ "x"; "x,a"; "x,a,a"; "x,a,a,a" ], [ ""; "x,"; "x,b" ]);
    ({p|^x(,(?!a)a)*$|p}, [ "x" ], [ "x,a"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^x(?=,)(,a)*$|p}, [ "x,a"; "x,a,a"; "x,a,a,a" ], [ "x"; ""; "x,"; "x,b" ]);
    ({p|^x(,a(?<=a))*$|p}, [ "x"; "x,a"; "x,a,a"; "x,a,a,a" ], [ ""; "x,"; "x,b" ]);
    ({p|^x(,(?<!,)a)*$|p}, [ "x" ], [ "x,a"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^x(?=,a$)(,a)*$|p}, [ "x,a" ], [ "x"; "x,a,a"; "x,a,a,a"; ""; "x,"; "x,b" ]);
    ({p|^(a,(?=b))*b$|p}, [ "b"; "a,b" ], [ "a,a,b"; "a,"; ""; "a,a" ]);
    ({p|^(?!a,a)(a,)*b$|p}, [ "b"; "a,b" ], [ "a,a,b"; "a,"; ""; "a,a" ]);
    ({p|^x(,a\$)*$|p}, [ "x"; "x,a$"; "x,a$,a$" ], [ "x,a"; "x,a,a"; "x,a$,a" ]);
    ({p|^x\$(,a)*$|p}, [ "x$"; "x$,a"; "x$,a,a" ], [ "x"; "x,a" ]);
    ({p|^(a\$,)*b$|p}, [ "b"; "a$,b"; "a$,a$,b" ], [ "a,b"; "a$,a,b" ]);
    ({p|^x(,\^a)*$|p}, [ "x"; "x,^a"; "x,^a,^a" ], [ "x,a"; "x,a,a" ]);
    ({p|^(a,)*b\$$|p}, [ "b$"; "a,b$"; "a,a,b$" ], [ "b"; "a,b" ]);
    ({p|^x(,[$^]a)*$|p}, [ "x"; "x,$a"; "x,^a,$a" ], [ "x,a"; "x,a,a" ]);
    ({p|^a$b|p}, [], [ "a"; "ab"; "a$b"; "" ]);
    ({p|^a^b|p}, [], [ "a"; "ab"; "b" ]);
    ({p|^a$$|p}, [ "a" ], [ "ab"; "" ]);
    ({p|^^a|p}, [ "a"; "ab" ], [ "ba" ]);
    ({p|^a\$b$|p}, [ "a$b" ], [ "ab"; "a" ]);
    ({p|^a\b|p}, [ "a"; "a b"; "a," ], [ "ab" ]);
    ({p|^a\Bb|p}, [ "ab" ], [ "a b"; "a" ]);
    ({p|^\ba|p}, [ "a" ], [ "ba"; "" ]);
    ({p|^a(?=b)|p}, [ "ab" ], [ "a"; "ac" ]);
    ({p|^a(?!b)|p}, [ "a"; "ac" ], [ "ab" ]);
    ({p|^[a-c]+\b$|p}, [ "a"; "abc" ], [ "" ]);
    ({p|^[a-c]+$\b|p}, [ "a"; "abc" ], [ "" ]);
    ({p|^$|p}, [ "" ], [ "a" ]);
    ({p|^a$.*|p}, [ "a" ], [ "ab"; "" ]);
    ({p|^a\b.*|p}, [ "a"; "a b" ], [ "ab" ]);
    ({p|^a^.*|p}, [], [ "a"; "ab" ]);
    ({p|^\$.*|p}, [ "$"; "$a" ], [ "a" ]);
    ({p|^a$|b|p}, [ "a"; "ab"; "b"; "cb" ], [ "c" ]);
    ({p|a$|^b|p}, [ "a"; "ba"; "bc" ], [ "ab"; "cb" ]);
    ({p|^(a$|b)$|p}, [ "a"; "b" ], [ "ab"; "" ]);
    ({p|^(a|^b)$|p}, [ "a"; "b" ], [ "ab" ]);
    ({p|^(a|b\b)$|p}, [ "a"; "b" ], [ "ab" ]);
    ({p|^(?:a\$|b)$|p}, [ "a$"; "b" ], [ "a" ]);
    ({p|^(a|$)$|p}, [ "a"; "" ], [ "b" ]);
    ({p|^(^|b)$|p}, [ ""; "b" ], [ "a" ]);
    ({p|^(a$|b)c|p}, [ "bc"; "bcd" ], [ "a"; "ac" ]);
    ({p|(^a|b)c$|p}, [ "ac"; "bc"; "xbc" ], [ "xac"; "acx" ]);
    ({p|a(b|^c)|p}, [ "ab"; "xab" ], [ "ac"; "c" ]);
    ({p|(a$){2}|p}, [], [ "a"; "aa"; "" ]);
    ({p|(^a){2}|p}, [], [ "a"; "aa" ]);
    ({p|x(a$)?b|p}, [ "xb" ], [ "xab"; "xa" ]);
    ({p|^x(a$)?|p}, [ "x"; "xa"; "xab"; "xb" ], []);
    ({p|(a|b$)c|p}, [ "ac" ], [ "bc"; "b" ]);
    ({p|a|\bb|p}, [ "a"; "b"; "c b" ], [ "cb" ]);
    ({p|a\b|c|p}, [ "a"; "c"; "b a" ], [ "ab" ]);
    ({p|(?=a)a|b|p}, [ "a"; "b" ], [ "c" ]);
    ({p|(?<=a)b|c|p}, [ "ab"; "c" ], [ "b" ]);
    ({p|a^|b|p}, [ "b" ], [ "a"; "" ]);
    ({p|$a|b|p}, [ "b" ], [ "a"; "" ]);
    ({p|^|a|p}, [ ""; "b" ], []);
    ({p|a|$|p}, [ ""; "b" ], []);
    ({p|(a|b)$c|p}, [], [ "ac"; "a"; "bc" ]);
    ({p|^(a|b)^c|p}, [], [ "ac"; "c" ]);
    ({p|[a-c]{2}$|^x|p}, [ "ab"; "x"; "yab" ], [ "abx"; "yx" ]);
    ({p|[a-c]\b|x|p}, [ "a"; "x"; "d a" ], [ "ad" ]);
    ({p|^[a-c]+$|^\d$|p}, [ "abc"; "1" ], [ "a1"; "" ]);
    ({p|^(a|b){2}$|p}, [ "ab" ], [ "a"; "abc" ]);
    ({p|^.{1,3}$$|p}, [ "a" ], [ "abcd"; "" ]);
    ({p|^.$^|p}, [], [ "a"; "" ]);
    ({p|^(.*)$|p}, [ ""; "a" ], []);
    ({p|^es$|ms|p}, [ "es"; "xms" ], [ "esx"; "x" ]);
    ({p|^x-$|p}, [ "x-" ], [ "x-a" ]);
    ({p|^x-\b|p}, [ "x-a" ], [ "x-"; "x--" ]);
  ]
  [@ocamlformat "disable"]

(* A shape that cannot express an anchor or an assertion leaves the pattern to the engine. A separated list has no
   place for a "$" at the end of its repeated group or after its first item, so "^x(,a$)*$" is not "^x(,a)*$" and
   does not take "x,a,a", which has a "$" before its second ",a". *)
let test_an_anchor_or_an_assertion_inside_a_shape_is_not_dropped () =
  Check.section "an anchor or an assertion inside a shape is not dropped";
  if needs_engine "TestAnAnchorOrAnAssertionInsideAShapeIsNotDropped" then begin
    List.iter
      (fun (source, matches, fails) ->
        match Pattern.compile source with
        | Error _ -> Check.fail (Printf.sprintf "%S is not a valid pattern" source)
        | Ok p ->
            List.iter (fun text -> check_every_way p text true) matches;
            List.iter (fun text -> check_every_way p text false) fails)
      anchor_cases;
    Printf.printf "%d patterns\n" (List.length anchor_cases);
    Check.check "the cases are the ones this test is about" (List.length anchor_cases > 100)
  end

(* The cases of ecmaregex/v8_cases.json, which gen_cases.js of the Go module writes: the set "fast" is patterns of the
   shapes the faster matchers take that are valid only without the u flag, with V8's answers. ECMA-262 matches such a
   pattern by UTF-16 code unit, so a character beyond the Basic Multilingual Plane is two characters to it. The faster
   matchers read code points, and hand a text with such a character to the engine. *)
let test_patterns_valid_only_without_the_u_flag_match_code_units () =
  Check.section "patterns valid only without the u flag match code units";
  match Suite.read_file (Filename.concat "ecmaregex" "v8_cases.json") with
  | None -> Check.fail "ecmaregex/v8_cases.json is not there"
  | Some data -> (
      match Document.parse_string data with
      | Error e -> Check.fail (Document.error_to_string e)
      | Ok d ->
          let cases = Suite.items d (Suite.member d (Document.root d) "fast") in
          let fast = ref 0 and answers = ref 0 in
          List.iter
            (fun c ->
              let source = Suite.string d (Suite.member d c "p") in
              let valid_unicode = Suite.text d (Suite.member d c "u") = "true" in
              match Pattern.compile source with
              | Error _ -> Check.fail (Printf.sprintf "%S is not a valid pattern" source)
              | Ok p ->
                  if Pattern.kind_name p <> "engine" then incr fast;
                  Check.checkf
                    (Pattern_engine.valid source = valid_unicode)
                    "%S: valid with the u flag is %b, V8 says %b" source (not valid_unicode) valid_unicode;
                  List.iter
                    (fun test ->
                      incr answers;
                      check_every_way p
                        (Suite.string d (Suite.member d test "t"))
                        (Suite.text d (Suite.member d test "want") = "true"))
                    (Suite.items d (Suite.member d c "tests")))
            cases;
          Printf.printf "%d patterns, %d of them with a faster matcher, %d answers\n" (List.length cases) !fast !answers;
          Check.checkf
            (!fast >= List.length cases / 2)
            "only %d of %d patterns have a faster matcher" !fast (List.length cases))

let () =
  test_matchers_agree_with_the_engine ();
  test_more_shapes_agree_with_the_engine ();
  test_simple_patterns_take_the_fast_matchers ();
  test_invalid_patterns_are_rejected ();
  test_excluded_class_with_a_member_outside_ascii ();
  test_matchers_by_hand ();
  test_fast_matchers_without_the_engine ();
  test_is_ascii_and_source ();
  test_matching_allocates_nothing ();
  test_patterns_valid_only_without_the_u_flag_match_code_units ();
  test_an_anchor_or_an_assertion_inside_a_shape_is_not_dropped ();
  Check.finish "pattern"
