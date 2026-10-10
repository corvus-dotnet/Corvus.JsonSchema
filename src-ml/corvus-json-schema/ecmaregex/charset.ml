(* The sets of code points of the engine, and case-insensitive matching. This is the port of charset.go and fold.go of
   the ecmaregex package of the Go module.

   Two things differ from the Go source, as they do in the Object Pascal port.

   Go keeps the sets already built for property expressions in a sync.Map, so that a property many patterns name is
   built once. Here each pattern builds the property sets it names, so there is no shared state to guard. That costs a
   pattern some microseconds when it is compiled and nothing when it matches.

   Go builds each of the two tables of case-insensitive matching on first use behind a sync.Once. Here a thread that
   finds no table builds one and publishes it through an [Atomic]. Two threads or domains that race both build the
   table, which is the same either way, and each uses the one it reads. *)

module Ucd = Corvus_json_schema_ucd

(** The last Unicode code point. *)
let max_rune = Ucd.max_rune

type t = { ranges : int array; ascii : string }
(** An immutable set of code points. The ranges are sorted, inclusive pairs that neither overlap nor touch. [ascii]
    repeats the membership of the code points below 128 as 128 bits in 16 bytes, so that the common case is one bit
    test. The Go source holds them in two words of 64 bits. An [int] has 63, so they are bytes here. *)

(** Reports whether the 128 bits hold the code point [c], which is below 128. *)
let ascii_has ascii c = (Char.code (String.get ascii (c lsr 3)) lsr (c land 7)) land 1 <> 0

let compare_int (a : int) (b : int) = compare a b

(** Builds a set from inclusive pairs in any order, which may overlap. With [negate] it builds the complement. *)
let make pairs negate =
  let n = Array.length pairs / 2 in
  (* A range is one number here, its first code point above its last, so that sorting the numbers sorts the ranges. A
     code point has 21 bits, so the number fits an int of 63 bits. *)
  let order = Array.init n (fun i -> (pairs.(2 * i) lsl 32) lor pairs.((2 * i) + 1)) in
  Array.sort compare_int order;
  let merged = Array.make (2 * n) 0 in
  let m = ref 0 in
  Array.iter
    (fun pair ->
      let lo = pair lsr 32 and hi = pair land 0xFFFFFFFF in
      if !m > 0 && lo <= merged.(!m - 1) + 1 then begin
        if hi > merged.(!m - 1) then merged.(!m - 1) <- hi
      end
      else begin
        merged.(!m) <- lo;
        merged.(!m + 1) <- hi;
        m := !m + 2
      end)
    order;
  let merged = Array.sub merged 0 !m in
  let ranges =
    if not negate then merged
    else begin
      let inverse = Array.make (Array.length merged + 2) 0 in
      let k = ref 0 and next = ref 0 and i = ref 0 in
      while !i < Array.length merged do
        if merged.(!i) > !next then begin
          inverse.(!k) <- !next;
          inverse.(!k + 1) <- merged.(!i) - 1;
          k := !k + 2
        end;
        next := merged.(!i + 1) + 1;
        i := !i + 2
      done;
      if !next <= max_rune then begin
        inverse.(!k) <- !next;
        inverse.(!k + 1) <- max_rune;
        k := !k + 2
      end;
      Array.sub inverse 0 !k
    end
  in
  let ascii = Bytes.make 16 '\000' in
  let i = ref 0 in
  while !i < Array.length ranges && ranges.(!i) < 128 do
    let c = ref ranges.(!i) in
    while !c <= ranges.(!i + 1) && !c < 128 do
      Bytes.set ascii (!c lsr 3) (Char.chr (Char.code (Bytes.get ascii (!c lsr 3)) lor (1 lsl (!c land 7))));
      incr c
    done;
    i := !i + 2
  done;
  { ranges; ascii = Bytes.to_string ascii }

let rec search ranges r lo hi =
  if lo >= hi then false
  else
    let mid = (lo + hi) lsr 1 in
    if r > ranges.((2 * mid) + 1) then search ranges r (mid + 1) hi
    else if r < ranges.(2 * mid) then search ranges r lo mid
    else true

(** Reports whether the set holds the code point. *)
let contains s r =
  if r < 128 then r >= 0 && ascii_has s.ascii r else search s.ranges r 0 (Array.length s.ranges / 2)

(** Reports whether the set holds no code point. *)
let is_empty s = Array.length s.ranges = 0

(** The set of no code point. It also stands where a set is wanted and none applies. *)
let none = make [||] false

(* The classes ECMA-262 defines. \d and \w are ASCII only, \s is WhiteSpace and LineTerminator, and . is everything
   but the four line terminators. *)
let digit_pairs = [| 0x30; 0x39 |]
let word_pairs = [| 0x30; 0x39; 0x41; 0x5A; 0x5F; 0x5F; 0x61; 0x7A |]

let space_pairs =
  [|
    0x9; 0xD; 0x20; 0x20; 0xA0; 0xA0; 0x1680; 0x1680; 0x2000; 0x200A; 0x2028; 0x2029; 0x202F; 0x202F; 0x205F; 0x205F;
    0x3000; 0x3000; 0xFEFF; 0xFEFF;
  |]
[@@ocamlformat "disable"]

let line_terminator_pairs = [| 0x0A; 0x0A; 0x0D; 0x0D; 0x2028; 0x2029 |]
let dot_set = make line_terminator_pairs true
let any_set = make [| 0; max_rune |] false
let digit_set = make digit_pairs false
let not_digit_set = make digit_pairs true
let word_set = make word_pairs false
let not_word_set = make word_pairs true
let space_set = make space_pairs false
let not_space_set = make space_pairs true

(** Reports whether a byte is an ECMA-262 word character. Every word character is ASCII, so a word boundary can be
    decided from the bytes on either side of a position. *)
let is_word_byte b = (b >= 0x61 && b <= 0x7A) || (b >= 0x41 && b <= 0x5A) || (b >= 0x30 && b <= 0x39) || b = 0x5F

(** The long names and the aliases of the General_Category values, each with its short name. *)
let category_names =
  [|
    ("Letter", "L"); ("Lowercase_Letter", "Ll"); ("Uppercase_Letter", "Lu"); ("Titlecase_Letter", "Lt");
    ("Cased_Letter", "LC"); ("Modifier_Letter", "Lm"); ("Other_Letter", "Lo"); ("Mark", "M"); ("Combining_Mark", "M");
    ("Nonspacing_Mark", "Mn"); ("Spacing_Mark", "Mc"); ("Enclosing_Mark", "Me"); ("Number", "N");
    ("Decimal_Number", "Nd"); ("digit", "Nd"); ("Letter_Number", "Nl"); ("Other_Number", "No"); ("Punctuation", "P");
    ("punct", "P"); ("Connector_Punctuation", "Pc"); ("Dash_Punctuation", "Pd"); ("Open_Punctuation", "Ps");
    ("Close_Punctuation", "Pe"); ("Initial_Punctuation", "Pi"); ("Final_Punctuation", "Pf");
    ("Other_Punctuation", "Po"); ("Symbol", "S"); ("Math_Symbol", "Sm"); ("Currency_Symbol", "Sc");
    ("Modifier_Symbol", "Sk"); ("Other_Symbol", "So"); ("Separator", "Z"); ("Space_Separator", "Zs");
    ("Line_Separator", "Zl"); ("Paragraph_Separator", "Zp"); ("Other", "C"); ("Control", "Cc"); ("cntrl", "Cc");
    ("Format", "Cf"); ("Surrogate", "Cs"); ("Private_Use", "Co"); ("Unassigned", "Cn");
    ("L", "L"); ("Ll", "Ll"); ("Lu", "Lu"); ("Lt", "Lt"); ("LC", "LC"); ("Lm", "Lm"); ("Lo", "Lo"); ("M", "M");
    ("Mn", "Mn"); ("Mc", "Mc"); ("Me", "Me"); ("N", "N"); ("Nd", "Nd"); ("Nl", "Nl"); ("No", "No"); ("P", "P");
    ("Pc", "Pc"); ("Pd", "Pd"); ("Ps", "Ps"); ("Pe", "Pe"); ("Pi", "Pi"); ("Pf", "Pf"); ("Po", "Po"); ("S", "S");
    ("Sm", "Sm"); ("Sc", "Sc"); ("Sk", "Sk"); ("So", "So"); ("Z", "Z"); ("Zs", "Zs"); ("Zl", "Zl"); ("Zp", "Zp");
    ("C", "C"); ("Cc", "Cc"); ("Cf", "Cf"); ("Cs", "Cs"); ("Co", "Co"); ("Cn", "Cn");
  |]
[@@ocamlformat "disable"]

(** The names and the aliases of the binary properties ECMA-262 lists, each with its canonical name. *)
let binary_names =
  [|
    ("ASCII", "ASCII"); ("ASCII_Hex_Digit", "ASCII_Hex_Digit"); ("AHex", "ASCII_Hex_Digit");
    ("Alphabetic", "Alphabetic"); ("Alpha", "Alphabetic"); ("Any", "Any"); ("Assigned", "Assigned");
    ("Bidi_Control", "Bidi_Control"); ("Bidi_C", "Bidi_Control"); ("Bidi_Mirrored", "Bidi_Mirrored");
    ("Bidi_M", "Bidi_Mirrored"); ("Case_Ignorable", "Case_Ignorable"); ("CI", "Case_Ignorable"); ("Cased", "Cased");
    ("Changes_When_Casefolded", "Changes_When_Casefolded"); ("CWCF", "Changes_When_Casefolded");
    ("Changes_When_Casemapped", "Changes_When_Casemapped"); ("CWCM", "Changes_When_Casemapped");
    ("Changes_When_Lowercased", "Changes_When_Lowercased"); ("CWL", "Changes_When_Lowercased");
    ("Changes_When_NFKC_Casefolded", "Changes_When_NFKC_Casefolded"); ("CWKCF", "Changes_When_NFKC_Casefolded");
    ("Changes_When_Titlecased", "Changes_When_Titlecased"); ("CWT", "Changes_When_Titlecased");
    ("Changes_When_Uppercased", "Changes_When_Uppercased"); ("CWU", "Changes_When_Uppercased"); ("Dash", "Dash");
    ("Default_Ignorable_Code_Point", "Default_Ignorable_Code_Point"); ("DI", "Default_Ignorable_Code_Point");
    ("Deprecated", "Deprecated"); ("Dep", "Deprecated"); ("Diacritic", "Diacritic"); ("Dia", "Diacritic");
    ("Emoji", "Emoji"); ("Emoji_Component", "Emoji_Component"); ("EComp", "Emoji_Component");
    ("Emoji_Modifier", "Emoji_Modifier"); ("EMod", "Emoji_Modifier"); ("Emoji_Modifier_Base", "Emoji_Modifier_Base");
    ("EBase", "Emoji_Modifier_Base"); ("Emoji_Presentation", "Emoji_Presentation"); ("EPres", "Emoji_Presentation");
    ("Extended_Pictographic", "Extended_Pictographic"); ("ExtPict", "Extended_Pictographic");
    ("Extender", "Extender"); ("Ext", "Extender"); ("Grapheme_Base", "Grapheme_Base"); ("Gr_Base", "Grapheme_Base");
    ("Grapheme_Extend", "Grapheme_Extend"); ("Gr_Ext", "Grapheme_Extend"); ("Hex_Digit", "Hex_Digit");
    ("Hex", "Hex_Digit"); ("IDS_Binary_Operator", "IDS_Binary_Operator"); ("IDSB", "IDS_Binary_Operator");
    ("IDS_Trinary_Operator", "IDS_Trinary_Operator"); ("IDST", "IDS_Trinary_Operator");
    ("ID_Continue", "ID_Continue"); ("IDC", "ID_Continue"); ("ID_Start", "ID_Start"); ("IDS", "ID_Start");
    ("Ideographic", "Ideographic"); ("Ideo", "Ideographic"); ("Join_Control", "Join_Control");
    ("Join_C", "Join_Control"); ("Logical_Order_Exception", "Logical_Order_Exception");
    ("LOE", "Logical_Order_Exception"); ("Lowercase", "Lowercase"); ("Lower", "Lowercase"); ("Math", "Math");
    ("Noncharacter_Code_Point", "Noncharacter_Code_Point"); ("NChar", "Noncharacter_Code_Point");
    ("Pattern_Syntax", "Pattern_Syntax"); ("Pat_Syn", "Pattern_Syntax");
    ("Pattern_White_Space", "Pattern_White_Space"); ("Pat_WS", "Pattern_White_Space");
    ("Quotation_Mark", "Quotation_Mark"); ("QMark", "Quotation_Mark"); ("Radical", "Radical");
    ("Regional_Indicator", "Regional_Indicator"); ("RI", "Regional_Indicator");
    ("Sentence_Terminal", "Sentence_Terminal"); ("STerm", "Sentence_Terminal"); ("Soft_Dotted", "Soft_Dotted");
    ("SD", "Soft_Dotted"); ("Terminal_Punctuation", "Terminal_Punctuation"); ("Term", "Terminal_Punctuation");
    ("Unified_Ideograph", "Unified_Ideograph"); ("UIdeo", "Unified_Ideograph"); ("Uppercase", "Uppercase");
    ("Upper", "Uppercase"); ("Variation_Selector", "Variation_Selector"); ("VS", "Variation_Selector");
    ("White_Space", "White_Space"); ("space", "White_Space"); ("XID_Continue", "XID_Continue");
    ("XIDC", "XID_Continue"); ("XID_Start", "XID_Start"); ("XIDS", "XID_Start");
  |]
[@@ocamlformat "disable"]

(* The two lists as tables. Nothing writes to them once the module is initialised, so reading them needs no lock. *)
let table_of pairs =
  let table = Hashtbl.create (2 * Array.length pairs) in
  Array.iter (fun (name, canonical) -> Hashtbl.replace table name canonical) pairs;
  table

let category_table : (string, string) Hashtbl.t = table_of category_names
let binary_table : (string, string) Hashtbl.t = table_of binary_names

(* Returns the ranges of a table as inclusive pairs, or [None] for no table. *)
let table_pairs = function None -> None | Some table -> Some (Ucd.ranges table)

let category_pairs value =
  match Hashtbl.find_opt category_table value with None -> None | Some short -> table_pairs (Ucd.category short)

(** Returns the ranges of a property expression, or [None] for a name or value that ECMA-262 does not define. Every
    range comes from the tables of the ucd library, which hold one fixed version of Unicode. *)
let property_pairs expr =
  match String.index_opt expr '=' with
  | Some i -> (
      let name = String.sub expr 0 i and value = String.sub expr (i + 1) (String.length expr - i - 1) in
      match name with
      | "General_Category" | "gc" -> category_pairs value
      | "Script" | "sc" -> table_pairs (Ucd.script value)
      | "Script_Extensions" | "scx" -> table_pairs (Ucd.script_extensions value)
      | _ -> None)
  | None -> (
      match category_pairs expr with
      | Some pairs -> Some pairs
      | None -> (
          match Hashtbl.find_opt binary_table expr with
          | None -> None
          | Some "ASCII" -> Some [| 0; 0x7F |]
          | Some "Any" -> Some [| 0; max_rune |]
          | Some canonical -> table_pairs (Ucd.binary canonical)))

(** Resolves the expression between the braces of [\p{...}] to its set, or [None] for a name or value that ECMA-262
    does not define. *)
let property_set expr = match property_pairs expr with None -> None | Some pairs -> Some (make pairs false)

(* Case-insensitive matching exists only inside a modifier group such as (?i:...). ECMA-262 defines it through a
   Canonicalize function. Two characters match when they canonicalize to the same character. The function differs
   between the two grammars. With the u flag it is Unicode simple case folding. With no flag it is the single
   character uppercase mapping of a UTF-16 code unit, which never maps a character outside ASCII into ASCII.

   The parser applies the equivalence to every literal and class as it reads them, so both back ends match a
   case-insensitive group with plain classes. Only a backreference needs the function when matching.

   The folding and the uppercase mapping come from the tables of the ucd library. *)

(** The characters of the Basic Multilingual Plane whose uppercase form is more than one character. {!canonicalize}
    leaves them alone. *)
let multi_upper =
  make
    [|
      0xDF; 0xDF; 0x149; 0x149; 0x1F0; 0x1F0; 0x390; 0x390; 0x3B0; 0x3B0; 0x587; 0x587; 0x1E96; 0x1E9A; 0x1F50; 0x1F50;
      0x1F52; 0x1F52; 0x1F54; 0x1F54; 0x1F56; 0x1F56; 0x1F80; 0x1FAF; 0x1FB2; 0x1FB4; 0x1FB6; 0x1FB7; 0x1FBC; 0x1FBC;
      0x1FC2; 0x1FC4; 0x1FC6; 0x1FC7; 0x1FCC; 0x1FCC; 0x1FD2; 0x1FD3; 0x1FD6; 0x1FD7; 0x1FE2; 0x1FE4; 0x1FE6; 0x1FE7;
      0x1FF2; 0x1FF4; 0x1FF6; 0x1FF7; 0x1FFC; 0x1FFC; 0xFB00; 0xFB06; 0xFB13; 0xFB17;
    |]
    false
[@@ocamlformat "disable"]

(** The Canonicalize function of ECMA-262 for case-insensitive matching. It does not allocate. *)
let canonicalize r unicode_mode =
  if unicode_mode then (* Every character of a simple case folding class folds to the same character. *)
    Ucd.fold r
  else if
    (* A character beyond the Basic Multilingual Plane is two code units, and neither has an uppercase form. *)
    r > 0xFFFF || contains multi_upper r
  then r
  else
    let upper = Ucd.to_upper r in
    if (r >= 128 && upper < 128) || upper > 0xFFFF then r else upper

type fold_table = { points : int array; classes : int array array }
(** The characters that are equivalent to some other character, in ascending order, each with all the members of its
    equivalence class. *)

(* Builds the table from the classes, each the list of its members by the character they canonicalize to. *)
let new_fold_table (classes : (int, int list) Hashtbl.t) =
  let by_member = Hashtbl.create 4096 in
  let points = ref [] in
  Hashtbl.iter
    (fun _ members ->
      match members with
      | [] | [ _ ] -> ()
      | _ ->
          let class_ = Array.of_list (List.rev members) in
          Array.iter
            (fun member ->
              points := member :: !points;
              Hashtbl.replace by_member member class_)
            class_)
    classes;
  let points = Array.of_list !points in
  Array.sort compare_int points;
  { points; classes = Array.map (Hashtbl.find by_member) points }

let add_member classes key member =
  Hashtbl.replace classes key (member :: (match Hashtbl.find_opt classes key with Some members -> members | None -> []))

let build_fold_table unicode_mode =
  let classes = Hashtbl.create 4096 in
  if unicode_mode then
    (* A class is a character that others fold to, with those others. *)
    Array.iter
      (fun r ->
        let key = Ucd.fold r in
        if not (Hashtbl.mem classes key) then Hashtbl.replace classes key [ key ];
        add_member classes key r)
      (Ucd.folding ())
  else
    for r = 0 to 0xFFFF do
      add_member classes (canonicalize r false) r
    done;
  new_fold_table classes

let unicode_fold_table : fold_table option Atomic.t = Atomic.make None
let legacy_fold_table : fold_table option Atomic.t = Atomic.make None

(** Returns the table of the grammar in use. It is built on first use, which only a pattern with a case-insensitive
    group reaches. *)
let fold_classes unicode_mode =
  let slot = if unicode_mode then unicode_fold_table else legacy_fold_table in
  match Atomic.get slot with
  | Some table -> table
  | None ->
      let table = build_fold_table unicode_mode in
      Atomic.set slot (Some table);
      table

(* The first index of [points] whose element is at least [lo]. *)
let rec first_at_least points lo from until =
  if from >= until then from
  else
    let mid = (from + until) lsr 1 in
    if points.(mid) >= lo then first_at_least points lo from mid else first_at_least points lo (mid + 1) until

(** Returns the set of every character equivalent to some member of the set. *)
let fold_closure set unicode_mode =
  let table = fold_classes unicode_mode in
  let extra = ref [] in
  let i = ref 0 in
  while !i < Array.length set.ranges do
    let lo = set.ranges.(!i) and hi = set.ranges.(!i + 1) in
    let k = ref (first_at_least table.points lo 0 (Array.length table.points)) in
    while !k < Array.length table.points && table.points.(!k) <= hi do
      Array.iter (fun member -> if not (contains set member) then extra := member :: member :: !extra) table.classes.(!k);
      incr k
    done;
    i := !i + 2
  done;
  if !extra = [] then set else make (Array.append (Array.of_list !extra) set.ranges) false

(* With the u flag and case-insensitive matching, \w and \b also count the two characters that fold to an ASCII
   letter (U+017F, the long s, and U+212A, the Kelvin sign) as word characters. *)
let fold_word_pairs = [| 0x30; 0x39; 0x41; 0x5A; 0x5F; 0x5F; 0x61; 0x7A; 0x17F; 0x17F; 0x212A; 0x212A |]
let fold_word_set = make fold_word_pairs false
let not_fold_word_set = make fold_word_pairs true
let is_fold_word_rune r = (r < 128 && is_word_byte r) || r = 0x17F || r = 0x212A
