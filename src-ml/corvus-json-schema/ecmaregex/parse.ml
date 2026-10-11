(* The parser of the engine. This is the port of parse.go of the ecmaregex package of the Go module. *)

module Ucd = Corvus_json_schema_ucd

type kind =
  | Empty
  | Char
  | Set
  | Cat
  | Alt
  | Group
  | Repeat
  | Bol
  | Eol
  | Line_start  (** [^] inside a group with the m modifier. *)
  | Line_end  (** [$] inside a group with the m modifier. *)
  | Word_boundary
  | Not_word_boundary
  | Look
  | Backref

(** The max of a quantifier with no upper bound. *)
let unbounded = -1

(** Caps the bounds of a counted quantifier. No text that fits in memory reaches a larger count. It is the largest
    32-bit integer, as in the Go source, and an [int] holds it. *)
let max_count = 0x7FFFFFFF

(** Caps the depth of groups, which bounds the recursion of the parser and of the compilers. *)
let max_nesting = 1000

type node = {
  kind : kind;
  r : int;  (** The code point of a [Char]. *)
  set : Charset.t;  (** The class of a [Set]. *)
  subs : node array;  (** The children of [Cat] and [Alt], and the one child of [Group], [Repeat] and [Look]. *)
  min : int;  (** [min], [max] and [lazy_] describe a [Repeat]. *)
  max : int;
  lazy_ : bool;
  group : int;  (** The number of the capturing group of a [Group], counted from 1. *)
  mutable groups : int array;
      (** The groups a [Backref] names. A name can belong to several groups in separate alternatives, of which at most
          one has taken part at any time. *)
  name : string;  (** The group name of a named backreference until it is resolved. *)
  fold : bool;  (** Marks a [Backref], [Word_boundary] or [Not_word_boundary] inside a case-insensitive group. *)
  behind : bool;  (** [behind] and [negative] describe a [Look]. *)
  negative : bool;
}
(** One element of the syntax tree of a pattern. *)

let empty_node =
  {
    kind = Empty;
    r = 0;
    set = Charset.none;
    subs = [||];
    min = 0;
    max = 0;
    lazy_ = false;
    group = 0;
    groups = [||];
    name = "";
    fold = false;
    behind = false;
    negative = false;
  }

type branch = { disjunction : int; mutable alternative : int }
(** One alternative of one disjunction. *)

type named_group = { number : int; group_path : (int * int) array }
(** A capturing group with a name, and the alternatives that enclose it, outermost first. *)

type parser = {
  p : int array;  (** The pattern as code points with the u flag, and as UTF-16 code units without it. *)
  mutable i : int;
  unicode : bool;
  mutable depth : int;
  mutable group_count : int;
      (** [group_count] and [has_names] come from a scan of the whole pattern before parsing, because the meaning of
          [\1] and [\k] depends on groups that may follow them. *)
  mutable has_names : bool;
  mutable groups : int;
  names : (string, named_group list) Hashtbl.t;
  mutable named_refs : node list;
  mutable path : branch list;  (** The alternatives that enclose the current position, innermost first. *)
  mutable disjunctions : int;
  mutable icase : bool;
      (** [icase], [multiline] and [dot_all] are the modifiers in force, which only a group such as [(?i:...)] changes.
      *)
  mutable multiline : bool;
  mutable dot_all : bool;
  mutable needs_backtracking : bool;
      (** Records that the pattern has a construct only the backtracking matcher can run. *)
}
(** A parser reads a pattern by recursive descent. With [unicode] set it applies the grammar of the u flag. Without it,
    it applies the Annex B grammar of patterns with no flag. *)

exception Parse_error of string * int
(** The reason a pattern is not valid, and the offset in characters at which it was found. *)

let fail p message = raise (Parse_error (message, p.i))

(* Counts the capturing groups of the pattern and notes whether any is named. *)
let scan_groups p =
  let in_class = ref false in
  let s = p.p in
  let n = Array.length s in
  let k = ref 0 in
  while !k < n do
    let c = s.(!k) in
    if c = 0x5C (* \ *) then incr k
    else if c = 0x5B (* [ *) then in_class := true
    else if c = 0x5D (* ] *) then in_class := false
    else if c = 0x28 (* ( *) && not !in_class then
      if !k + 1 >= n || s.(!k + 1) <> 0x3F (* ? *) then p.group_count <- p.group_count + 1
      else if !k + 3 < n && s.(!k + 2) = 0x3C (* < *) && s.(!k + 3) <> 0x3D (* = *) && s.(!k + 3) <> 0x21 (* ! *) then begin
        p.group_count <- p.group_count + 1;
        p.has_names <- true
      end;
    incr k
  done

let more p = p.i < Array.length p.p
let peek p = if p.i < Array.length p.p then p.p.(p.i) else -1

let eat p c =
  if p.i < Array.length p.p && p.p.(p.i) = Char.code c then begin
    p.i <- p.i + 1;
    true
  end
  else false

(* The character as a [char] for a match over the ASCII characters of the syntax, or NUL for any other. *)
let ascii c = if c > 0 && c < 128 then Char.chr c else '\000'

(* Returns the node for one literal character. Inside a case-insensitive group that is the class of the characters
   equivalent to it. *)
let char_node p c =
  let plain = { empty_node with kind = Char; r = c } in
  if p.icase then
    let set = Charset.fold_closure (Charset.make [| c; c |] false) p.unicode in
    if Array.length set.ranges <> 2 || set.ranges.(0) <> set.ranges.(1) then { empty_node with kind = Set; set }
    else plain
  else plain

(* Returns the node for a class. Inside a case-insensitive group the class also takes every character equivalent to
   one of its members. *)
let set_node p set = { empty_node with kind = Set; set = (if p.icase then Charset.fold_closure set p.unicode else set) }
let is_digit c = c >= 0x30 && c <= 0x39

(* Reports whether the text at the current '{' reads as {n}, {n,} or {n,m}. *)
let looks_like_quantifier p =
  let s = p.p in
  let n = Array.length s in
  let k = ref (p.i + 1) in
  let start = !k in
  while !k < n && is_digit s.(!k) do
    incr k
  done;
  if !k = start || !k >= n then false
  else if s.(!k) = 0x7D (* } *) then true
  else if s.(!k) <> 0x2C (* , *) then false
  else begin
    incr k;
    while !k < n && is_digit s.(!k) do
      incr k
    done;
    !k < n && s.(!k) = 0x7D
  end

(* Reads a run of decimal digits, saturating at max_count. *)
let number p =
  let start = p.i in
  let v = ref 0 in
  while p.i < Array.length p.p && is_digit p.p.(p.i) do
    if !v < max_count then begin
      v := (!v * 10) + (p.p.(p.i) - 0x30);
      if !v > max_count then v := max_count
    end;
    p.i <- p.i + 1
  done;
  if start = p.i then fail p "expected a number";
  !v

(* Reads a quantifier, if one is there, as its min, its max and whether it is lazy. *)
let quantifier p =
  let bounds =
    match ascii (peek p) with
    | '*' ->
        p.i <- p.i + 1;
        Some (0, unbounded)
    | '+' ->
        p.i <- p.i + 1;
        Some (1, unbounded)
    | '?' ->
        p.i <- p.i + 1;
        Some (0, 1)
    | '{' when looks_like_quantifier p ->
        p.i <- p.i + 1;
        let min = number p in
        let max = if eat p ',' then if peek p = 0x7D then unbounded else number p else min in
        if not (eat p '}') then fail p "malformed quantifier";
        if max <> unbounded && max < min then fail p "numbers out of order in quantifier";
        Some (min, max)
    | _ -> None
  in
  match bounds with
  | None -> None
  | Some (min, max) ->
      let lazy_ = eat p '?' in
      Some (min, max, lazy_)

let close p = if not (eat p ')') then fail p "unterminated group"

(* Reports whether two positions lie in different alternatives of some disjunction. Each is the alternatives that
   enclose it, outermost first. *)
let separate_alternatives a b =
  let rec walk i =
    if i < Array.length a && i < Array.length b && fst a.(i) = fst b.(i) then snd a.(i) <> snd b.(i) || walk (i + 1)
    else false
  in
  walk 0

(* The alternatives that enclose the current position, outermost first. *)
let current_path p = Array.of_list (List.rev_map (fun branch -> (branch.disjunction, branch.alternative)) p.path)

let hex_value c =
  if c >= 0x30 && c <= 0x39 then c - 0x30
  else if c >= 0x61 && c <= 0x66 then c - 0x61 + 10
  else if c >= 0x41 && c <= 0x46 then c - 0x41 + 10
  else -1

(* Reads exactly n hexadecimal digits. It returns -1, consuming nothing, when they are not there. *)
let hex p n =
  if p.i + n > Array.length p.p then -1
  else
    let rec read k v =
      if k = n then begin
        p.i <- p.i + n;
        v
      end
      else
        let d = hex_value p.p.(p.i + k) in
        if d < 0 then -1 else read (k + 1) ((v * 16) + d)
    in
    read 0 0

(* Reads what follows a \u and returns the code point, or -1, consuming nothing, when it is not a well-formed escape.
   With unicode_mode set, as it is with the u flag and always in a group name, it accepts the \u{...} form and reads a
   surrogate pair written as two escapes as one code point. Without it each escape is one code unit. *)
let unicode_escape p unicode_mode =
  let start = p.i in
  if unicode_mode && eat p '{' then begin
    let digits = p.i in
    let v = ref 0 in
    while more p && hex_value (peek p) >= 0 do
      if !v <= Charset.max_rune then v := (!v * 16) + hex_value (peek p);
      p.i <- p.i + 1
    done;
    if digits = p.i || (not (eat p '}')) || !v > Charset.max_rune then begin
      p.i <- start;
      -1
    end
    else !v
  end
  else
    let u = hex p 4 in
    if u < 0 then -1
    else if
      unicode_mode && u >= 0xD800 && u <= 0xDBFF
      && p.i + 6 <= Array.length p.p
      && p.p.(p.i) = 0x5C (* \ *)
      && p.p.(p.i + 1) = 0x75 (* u *)
    then begin
      let save = p.i in
      p.i <- p.i + 2;
      let low = hex p 4 in
      if low >= 0xDC00 && low <= 0xDFFF then 0x10000 + ((u - 0xD800) lsl 10) + (low - 0xDC00)
      else begin
        p.i <- save;
        u
      end
    end
    else u

let id_start = match Ucd.binary "ID_Start" with Some table -> table | None -> assert false
let id_continue = match Ucd.binary "ID_Continue" with Some table -> table | None -> assert false

(* Reads a group name up to and including its '>'. A character of the name may be written as a \u escape. *)
let group_name p =
  let name = Buffer.create 16 in
  let count = ref 0 in
  while not (eat p '>') do
    if not (more p) then fail p "invalid group name";
    let c = peek p in
    p.i <- p.i + 1;
    let c =
      if c = 0x5C (* \ *) then begin
        if not (eat p 'u') then fail p "invalid group name";
        let c = unicode_escape p true in
        if c < 0 then fail p "invalid group name";
        c
      end
      else if c >= 0xD800 && c <= 0xDBFF && peek p >= 0xDC00 && peek p <= 0xDFFF then begin
        (* Without the u flag the pattern is code units, and a name still takes a surrogate pair as one character. *)
        let low = peek p in
        p.i <- p.i + 1;
        0x10000 + ((c - 0xD800) lsl 10) + (low - 0xDC00)
      end
      else c
    in
    if !count = 0 then
      begin if not (c = 0x24 (* $ *) || c = 0x5F (* _ *) || Ucd.contains id_start c) then fail p "invalid group name"
      end
    else if not (c = 0x24 || c = 0x200C || c = 0x200D || Ucd.contains id_continue c) then fail p "invalid group name";
    Utf8.add_rune name c;
    incr count
  done;
  if !count = 0 then fail p "invalid group name";
  Buffer.contents name

(* Reads an Annex B octal escape of up to three digits with a value below 256. A digit that cannot start one (8 or 9)
   stands for itself. *)
let legacy_octal p =
  let v = ref 0 and k = ref 0 in
  while !k < 3 && more p && peek p >= 0x30 && peek p <= 0x37 && (!v * 8) + (peek p - 0x30) <= 0o377 do
    v := (!v * 8) + (peek p - 0x30);
    p.i <- p.i + 1;
    incr k
  done;
  if !k = 0 then begin
    let d = peek p in
    p.i <- p.i + 1;
    d
  end
  else !v

(* Reads a class escape such as \d or \p{...} whose letter is c. It returns [None], consuming nothing, when c does not
   start one. *)
let class_escape p c =
  let take set =
    p.i <- p.i + 1;
    Some set
  in
  match ascii c with
  | 'd' -> take Charset.digit_set
  | 'D' -> take Charset.not_digit_set
  | 'w' -> take (if p.unicode && p.icase then Charset.fold_word_set else Charset.word_set)
  | 'W' -> take (if p.unicode && p.icase then Charset.not_fold_word_set else Charset.not_word_set)
  | 's' -> take Charset.space_set
  | 'S' -> take Charset.not_space_set
  | ('p' | 'P') when p.unicode -> (
      (* Without the u flag \p is the letter p. *)
      p.i <- p.i + 1;
      if not (eat p '{') then fail p "invalid property name";
      let start = p.i in
      while more p && peek p <> 0x7D (* } *) do
        p.i <- p.i + 1
      done;
      let expr = Buffer.create 32 in
      for k = start to p.i - 1 do
        Utf8.add_rune expr p.p.(k)
      done;
      if not (eat p '}') then fail p "invalid property name";
      match Charset.property_set (Buffer.contents expr) with
      | None -> fail p "invalid property name"
      | Some set -> if c = 0x50 (* P *) then Some (Charset.make set.ranges true) else Some set)
  | _ -> None

let is_ascii_letter c = (c >= 0x61 && c <= 0x7A) || (c >= 0x41 && c <= 0x5A)

(* Reports whether c is one of the characters the u flag lets an identity escape name. *)
let syntax_character c =
  match ascii c with
  | '^' | '$' | '\\' | '.' | '*' | '+' | '?' | '(' | ')' | '[' | ']' | '{' | '}' | '|' | '/' -> true
  | _ -> false

(* Reads the escape after a backslash, in or out of a class, and returns the code point it names. *)
let character_escape p in_class =
  let c = peek p in
  p.i <- p.i + 1;
  match ascii c with
  | 'f' -> 0x0C
  | 'n' -> 0x0A
  | 'r' -> 0x0D
  | 't' -> 0x09
  | 'v' -> 0x0B
  | 'c' ->
      let l = peek p in
      if is_ascii_letter l then begin
        p.i <- p.i + 1;
        l mod 32
      end
      else if (not p.unicode) && in_class && (is_digit l || l = 0x5F (* _ *)) then begin
        p.i <- p.i + 1;
        l mod 32
      end
      else begin
        if p.unicode then fail p "invalid control escape";
        (* Annex B reads a \c that is not a control escape as a backslash followed by the letter c. *)
        p.i <- p.i - 1;
        0x5C
      end
  | '0' ->
      if more p && is_digit (peek p) then begin
        if p.unicode then fail p "invalid decimal escape";
        p.i <- p.i - 1;
        legacy_octal p
      end
      else 0
  | 'x' ->
      let h = hex p 2 in
      if h < 0 then begin
        if p.unicode then fail p "invalid \\x escape";
        0x78
      end
      else h
  | 'u' ->
      let u = unicode_escape p p.unicode in
      if u < 0 then begin
        if p.unicode then fail p "invalid \\u escape";
        0x75
      end
      else u
  | _ ->
      if c >= 0x31 && c <= 0x39 && (not p.unicode) && in_class then begin
        p.i <- p.i - 1;
        legacy_octal p
      end
      else begin
        (* Identity escapes. The u flag allows only the syntax characters and '/', and '-' in a class. Annex B allows
           any character, except k in a pattern with a named group. Outside a class atom_escape reads \k before this
           is reached, so here the \k of such a pattern is in a class, where it is not a reference either. *)
        if p.unicode && (not (syntax_character c)) && not (in_class && c = 0x2D (* - *)) then fail p "invalid escape";
        if c = 0x6B (* k *) && p.has_names then fail p "invalid escape";
        c
      end

(* One member of a class: a code point, or a set for a class escape. *)
type class_atom = Point of int | Class of Charset.t

(* Reads one member of a class. *)
let class_atom p =
  let c = peek p in
  p.i <- p.i + 1;
  if c <> 0x5C (* \ *) then Point c
  else begin
    if not (more p) then fail p "\\ at end of pattern";
    let e = peek p in
    if e = 0x62 (* b *) then begin
      p.i <- p.i + 1;
      Point 0x08
    end
    else match class_escape p e with Some set -> Class set | None -> Point (character_escape p true)
  end

let class_atom_pairs = function Point c -> [| c; c |] | Class set -> set.Charset.ranges

(* Reads a character class from its '[' to its ']'. *)
let class_ p =
  p.i <- p.i + 1;
  let negated = eat p '^' in
  let pairs = ref [] in
  let add more_pairs = pairs := more_pairs :: !pairs in
  let finished = ref false in
  while not !finished do
    if not (more p) then fail p "unterminated character class";
    if eat p ']' then finished := true
    else begin
      let first = class_atom p in
      if peek p = 0x2D (* - *) && p.i + 1 < Array.length p.p && p.p.(p.i + 1) <> 0x5D (* ] *) then begin
        p.i <- p.i + 1;
        let second = class_atom p in
        match (first, second) with
        | Point first, Point second ->
            if second < first then fail p "range out of order in character class";
            add [| first; second |]
        | _ ->
            if p.unicode then fail p "invalid character class range";
            (* Annex B reads the '-' as itself when a class escape is at either end. *)
            add (class_atom_pairs first);
            add [| 0x2D; 0x2D |];
            add (class_atom_pairs second)
      end
      else add (class_atom_pairs first)
    end
  done;
  (* Inside a case-insensitive group a character matches the class when it is equivalent to a member, and a negated
     class excludes exactly those characters. *)
  let set = Charset.make (Array.concat (List.rev !pairs)) false in
  let set = if p.icase then Charset.fold_closure set p.unicode else set in
  let set = if negated then Charset.make set.ranges true else set in
  { empty_node with kind = Set; set }

let rec disjunction p =
  p.depth <- p.depth + 1;
  if p.depth > max_nesting then fail p "groups nested too deeply";
  p.disjunctions <- p.disjunctions + 1;
  let branch = { disjunction = p.disjunctions; alternative = 0 } in
  let outer = p.path in
  p.path <- branch :: outer;
  let first = alternative p in
  let result =
    if peek p <> 0x7C (* | *) then first
    else begin
      let subs = ref [ first ] in
      while eat p '|' do
        branch.alternative <- branch.alternative + 1;
        subs := alternative p :: !subs
      done;
      { empty_node with kind = Alt; subs = Array.of_list (List.rev !subs) }
    end
  in
  p.path <- outer;
  p.depth <- p.depth - 1;
  result

and alternative p =
  let terms = ref [] in
  while more p && peek p <> 0x7C (* | *) && peek p <> 0x29 (* ) *) do
    terms := term p :: !terms
  done;
  match !terms with
  | [] -> { empty_node with kind = Empty }
  | [ one ] -> one
  | terms -> { empty_node with kind = Cat; subs = Array.of_list (List.rev terms) }

and term p =
  let c = peek p in
  let atom, quantifiable =
    match ascii c with
    | '^' ->
        p.i <- p.i + 1;
        if p.multiline then begin
          p.needs_backtracking <- true;
          ({ empty_node with kind = Line_start }, false)
        end
        else ({ empty_node with kind = Bol }, false)
    | '$' ->
        p.i <- p.i + 1;
        if p.multiline then begin
          p.needs_backtracking <- true;
          ({ empty_node with kind = Line_end }, false)
        end
        else ({ empty_node with kind = Eol }, false)
    | '(' -> group p
    | '.' ->
        p.i <- p.i + 1;
        ({ empty_node with kind = Set; set = (if p.dot_all then Charset.any_set else Charset.dot_set) }, true)
    | '[' -> (class_ p, true)
    | '\\' -> atom_escape p
    | '*' | '+' | '?' -> fail p "nothing to repeat"
    | '{' ->
        if p.unicode || looks_like_quantifier p then fail p "nothing to repeat";
        p.i <- p.i + 1;
        (char_node p 0x7B, true)
    | ']' | '}' ->
        if p.unicode then fail p "lone bracket";
        p.i <- p.i + 1;
        (char_node p c, true)
    | _ ->
        p.i <- p.i + 1;
        (char_node p c, true)
  in
  match quantifier p with
  | None -> atom
  | Some (min, max, lazy_) ->
      if not quantifiable then fail p "nothing to repeat";
      { empty_node with kind = Repeat; subs = [| atom |]; min; max; lazy_ }

(* Reads a group or a lookaround. It also reports whether a quantifier may follow. *)
and group p =
  p.i <- p.i + 1;
  if not (eat p '?') then (capture p "", true)
  else if eat p ':' then begin
    let body = disjunction p in
    close p;
    ({ empty_node with kind = Group; subs = [| body |] }, true)
  end
  else
    let c = peek p in
    if c = 0x69 (* i *) || c = 0x6D (* m *) || c = 0x73 (* s *) || c = 0x2D (* - *) then (modifier_group p, true)
    else if eat p '=' || eat p '!' then begin
      let negative =
        p.p.(p.i - 1) = 0x21
        (* ! *)
      in
      let body = disjunction p in
      close p;
      p.needs_backtracking <- true;
      (* Annex B lets a quantifier follow a lookahead. The u flag does not. *)
      ({ empty_node with kind = Look; subs = [| body |]; negative }, not p.unicode)
    end
    else if eat p '<' then
      if eat p '=' || eat p '!' then begin
        let negative = p.p.(p.i - 1) = 0x21 in
        let body = disjunction p in
        close p;
        p.needs_backtracking <- true;
        ({ empty_node with kind = Look; subs = [| body |]; negative; behind = true }, false)
      end
      else begin
        let name = group_name p in
        (* Two groups may share a name only when they are in separate alternatives of one disjunction, so that no
           match can go through both. *)
        let here = current_path p in
        List.iter
          (fun other -> if not (separate_alternatives other.group_path here) then fail p "duplicate group name")
          (match Hashtbl.find_opt p.names name with Some named -> named | None -> []);
        (capture p name, true)
      end
    else fail p "invalid group"

(* Reads a group that turns modifiers on or off for its body, such as (?i:...) or (?s-i:...). The position is after
   the "(?". *)
and modifier_group p =
  let icase = p.icase and multiline = p.multiline and dot_all = p.dot_all in
  let seen = [| false; false; false |] in
  let removing = ref false and count = ref 0 in
  while not (eat p ':') do
    let c = peek p in
    p.i <- p.i + 1;
    if c = 0x2D (* - *) then begin
      if !removing then fail p "invalid group modifier";
      removing := true
    end
    else begin
      let flag =
        match ascii c with
        | 'i' ->
            p.icase <- not !removing;
            0
        | 'm' ->
            p.multiline <- not !removing;
            1
        | 's' ->
            p.dot_all <- not !removing;
            2
        | _ -> fail p "invalid group modifier"
      in
      if seen.(flag) then fail p "repeated group modifier";
      seen.(flag) <- true;
      incr count
    end
  done;
  if !count = 0 then fail p "invalid group modifier";
  let body = disjunction p in
  close p;
  p.icase <- icase;
  p.multiline <- multiline;
  p.dot_all <- dot_all;
  { empty_node with kind = Group; subs = [| body |] }

and capture p name =
  p.groups <- p.groups + 1;
  let number = p.groups in
  if name <> "" then
    Hashtbl.replace p.names name
      ((match Hashtbl.find_opt p.names name with Some named -> named | None -> [])
      @ [ { number; group_path = current_path p } ]);
  let body = disjunction p in
  close p;
  { empty_node with kind = Group; group = number; subs = [| body |] }

(* Reads an escape outside a class. It also reports whether a quantifier may follow. *)
and atom_escape p =
  p.i <- p.i + 1;
  if not (more p) then fail p "\\ at end of pattern";
  let c = peek p in
  if c = 0x62 (* b *) || c = 0x42 (* B *) then begin
    p.i <- p.i + 1;
    (* With the u flag, a case-insensitive word boundary counts two more characters as word characters, which the
       automaton does not. *)
    let fold = p.unicode && p.icase in
    if fold then p.needs_backtracking <- true;
    ({ empty_node with kind = (if c = 0x42 then Not_word_boundary else Word_boundary); fold }, false)
  end
  else if c = 0x6B (* k *) then
    (* Annex B reads \k as the letter k in a pattern with no named group. *)
    if p.unicode || p.has_names then begin
      p.i <- p.i + 1;
      if not (eat p '<') then fail p "invalid named reference";
      let reference = { empty_node with kind = Backref; name = group_name p; fold = p.icase } in
      p.named_refs <- reference :: p.named_refs;
      p.needs_backtracking <- true;
      (reference, true)
    end
    else begin
      p.i <- p.i + 1;
      (char_node p 0x6B, true)
    end
  else if c >= 0x31 && c <= 0x39 then begin
    let start = p.i in
    let n = number p in
    if n <= p.group_count then begin
      p.needs_backtracking <- true;
      ({ empty_node with kind = Backref; groups = [| n |]; fold = p.icase }, true)
    end
    else begin
      if p.unicode then fail p "reference to a group that does not exist";
      (* Annex B reads it as an octal escape, or as the digit itself. *)
      p.i <- start;
      (char_node p (legacy_octal p), true)
    end
  end
  else
    match class_escape p c with
    | Some set -> (set_node p set, true)
    | None -> (char_node p (character_escape p false), true)

(* Writes the pattern for a message: in quotes, with the characters that would break a line or the quotes escaped. *)
let quote pattern =
  let out = Buffer.create (String.length pattern + 2) in
  Buffer.add_char out '"';
  String.iter
    (fun c ->
      if c = '"' || c = '\\' then begin
        Buffer.add_char out '\\';
        Buffer.add_char out c
      end
      else if Char.code c < 0x20 || Char.code c = 0x7F then
        Buffer.add_string out (Printf.sprintf "\\x%02x" (Char.code c))
      else Buffer.add_char out c)
    pattern;
  Buffer.add_char out '"';
  Buffer.contents out

(** [parse pattern unicode] reads the pattern with the grammar of the u flag, or with the Annex B grammar. It returns
    the root of the tree and the parser, or the reason the pattern is not valid under that grammar.

    Without the u flag ECMA-262 reads a pattern, and matches a text, a UTF-16 code unit at a time, so the pattern is
    read as code units. A character beyond the Basic Multilingual Plane is then two atoms, a quantifier after it repeats
    the second, and in a class it is two members, so a range between two such characters runs from a low surrogate to a
    high one and is an error. *)
let parse pattern unicode =
  let p =
    {
      p = Utf8.runes pattern (not unicode);
      i = 0;
      unicode;
      depth = 0;
      group_count = 0;
      has_names = false;
      groups = 0;
      names = Hashtbl.create 8;
      named_refs = [];
      path = [];
      disjunctions = 0;
      icase = false;
      multiline = false;
      dot_all = false;
      needs_backtracking = false;
    }
  in
  try
    scan_groups p;
    let root = disjunction p in
    if p.i < Array.length p.p then
      if p.p.(p.i) = 0x29 (* ) *) then fail p "unmatched ')'" else fail p "unexpected character";
    List.iter
      (fun reference ->
        match Hashtbl.find_opt p.names reference.name with
        | None -> fail p "reference to an unknown group name"
        | Some named -> reference.groups <- Array.of_list (List.map (fun g -> g.number) named))
      p.named_refs;
    Ok (root, p)
  with Parse_error (message, position) ->
    Error (Printf.sprintf "ecmaregex: %s at offset %d in %s" message position (quote pattern))
