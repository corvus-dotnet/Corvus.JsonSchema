(* URI handling for schema identification and reference resolution (RFC 3986 section 5). Reference resolution is
   implemented directly so that opaque bases (urn:, tag:) resolve the same way in every port of the evaluator. A port
   of uri.go of the Go module. *)

type parts = {
  scheme : string;
  authority : string;
  path : string;
  query : string;
  has_scheme : bool;
  has_authority : bool;
  has_query : bool;
}

let is_ascii_letter b = Char.code b lor 0x20 >= Char.code 'a' && Char.code b lor 0x20 <= Char.code 'z'
let is_ascii_digit b = b >= '0' && b <= '9'

(* scheme_end is the index of the ':' ending a URI scheme, or -1 if the text does not start with one. *)
let scheme_end s =
  if s = "" || not (is_ascii_letter s.[0]) then -1
  else begin
    let result = ref (-1) in
    let i = ref 1 in
    let scanning = ref true in
    while !scanning && !i < String.length s do
      let b = s.[!i] in
      if b = ':' then begin
        result := !i;
        scanning := false
      end
      else if not (is_ascii_letter b || is_ascii_digit b || b = '+' || b = '-' || b = '.') then scanning := false;
      incr i
    done;
    !result
  end

let after s i = String.sub s i (String.length s - i)

(* The index of the first '/' or '?' of s, or its length. *)
let index_slash_or_query s =
  let i = ref 0 in
  while !i < String.length s && s.[!i] <> '/' && s.[!i] <> '?' do
    incr i
  done;
  !i

(* parse splits a URI into scheme, authority, path and query (the fragment must already be removed). *)
let parse uri =
  let colon = scheme_end uri in
  let scheme, has_scheme, rest =
    if colon >= 0 then (String.sub uri 0 colon, true, after uri (colon + 1)) else ("", false, uri)
  in
  let authority, has_authority, rest =
    if String.starts_with ~prefix:"//" rest then begin
      let a = after rest 2 in
      let stop = index_slash_or_query a in
      (String.sub a 0 stop, true, after a stop)
    end
    else ("", false, rest)
  in
  match String.index_opt rest '?' with
  | Some q ->
      {
        scheme;
        authority;
        path = String.sub rest 0 q;
        query = after rest (q + 1);
        has_scheme;
        has_authority;
        has_query = true;
      }
  | None -> { scheme; authority; path = rest; query = ""; has_scheme; has_authority; has_query = false }

(* ascii_lower returns s with the letters A to Z in lower case and everything else as it is. RFC 3986 and RFC 3987
   make a scheme and a host case-insensitive for those letters only. *)
let ascii_lower s = String.lowercase_ascii s

(* has_scheme reports whether the reference starts with a URI scheme. *)
let has_scheme reference = scheme_end reference >= 0

(* split_fragment splits a reference at its first '#'. *)
let split_fragment reference =
  match String.index_opt reference '#' with
  | Some i -> (String.sub reference 0 i, after reference (i + 1))
  | None -> (reference, "")

let to_string p =
  let s = Buffer.create 64 in
  if p.has_scheme then begin
    Buffer.add_string s p.scheme;
    Buffer.add_char s ':'
  end;
  if p.has_authority then begin
    Buffer.add_string s "//";
    Buffer.add_string s p.authority
  end;
  Buffer.add_string s p.path;
  if p.has_query then begin
    Buffer.add_char s '?';
    Buffer.add_string s p.query
  end;
  Buffer.contents s

let remove_dot_segments path =
  if not (String.contains path '.') then path
  else begin
    let input = Array.of_list (String.split_on_char '/' path) in
    let last = Array.length input - 1 in
    (* The output segments, last first. *)
    let out = ref [] in
    let count = ref 0 in
    let add seg =
      out := seg :: !out;
      incr count
    in
    Array.iteri
      (fun i seg ->
        match seg with
        | "." -> if i = last then add ""
        | ".." ->
            (match !out with
            | top :: rest when !count > 1 || (!count = 1 && top <> "") ->
                out := rest;
                decr count
            | _ -> ());
            if i = last then add ""
        | _ -> add seg)
      input;
    String.concat "/" (List.rev !out)
  end

let merge_paths base ref_path =
  if base.has_authority && base.path = "" then "/" ^ ref_path
  else
    match String.rindex_opt base.path '/' with Some i -> String.sub base.path 0 (i + 1) ^ ref_path | None -> ref_path

let trim_suffix s suffix =
  if String.ends_with ~suffix s then String.sub s 0 (String.length s - String.length suffix) else s

let normalize_parts p =
  let p = if p.has_scheme then { p with scheme = ascii_lower p.scheme } else p in
  let p = { p with path = remove_dot_segments p.path } in
  let p =
    if p.has_authority then begin
      let a = ascii_lower p.authority in
      let a = match p.scheme with "http" -> trim_suffix a ":80" | "https" -> trim_suffix a ":443" | _ -> a in
      { p with authority = a; path = (if p.path = "" then "/" else p.path) }
    end
    else p
  in
  to_string p

(* normalize normalises an absolute URI (dropping its fragment) so that equivalent spellings compare equal. *)
let normalize uri =
  let u, _ = split_fragment uri in
  if not (has_scheme u) then u else normalize_parts (parse u)

(* resolve resolves a reference (without fragment) against a base URI, returning the normalised absolute URI. *)
let resolve base_uri reference =
  if reference = "" then base_uri
  else begin
    let r = parse reference in
    if r.has_scheme then normalize_parts r
    else if base_uri = "" then reference
    else begin
      let b = parse base_uri in
      let t =
        if r.has_authority then
          {
            scheme = b.scheme;
            has_scheme = b.has_scheme;
            authority = r.authority;
            has_authority = true;
            path = remove_dot_segments r.path;
            query = r.query;
            has_query = r.has_query;
          }
        else begin
          let path, query, has_query =
            if r.path = "" then if r.has_query then (b.path, r.query, true) else (b.path, b.query, b.has_query)
            else if String.starts_with ~prefix:"/" r.path then (remove_dot_segments r.path, r.query, r.has_query)
            else (remove_dot_segments (merge_paths b r.path), r.query, r.has_query)
          in
          {
            scheme = b.scheme;
            has_scheme = b.has_scheme;
            authority = b.authority;
            has_authority = b.has_authority;
            path;
            query;
            has_query;
          }
        end
      in
      if b.has_scheme then normalize_parts t else to_string t
    end
  end

let hex_value c =
  if c >= '0' && c <= '9' then Char.code c - 48
  else if c >= 'a' && c <= 'f' then Char.code c - 87
  else if c >= 'A' && c <= 'F' then Char.code c - 55
  else -1

(* decode_fragment percent-decodes a fragment (invalid escapes leave the text unchanged). *)
let decode_fragment fragment =
  if not (String.contains fragment '%') then fragment
  else begin
    let n = String.length fragment in
    let out = Buffer.create n in
    let valid = ref true in
    let i = ref 0 in
    while !valid && !i < n do
      if fragment.[!i] = '%' then
        if !i + 2 >= n then valid := false
        else begin
          let h = hex_value fragment.[!i + 1] and l = hex_value fragment.[!i + 2] in
          if h < 0 || l < 0 then valid := false
          else begin
            Buffer.add_char out (Char.chr ((h * 16) + l));
            i := !i + 3
          end
        end
      else begin
        Buffer.add_char out fragment.[!i];
        incr i
      end
    done;
    if (not !valid) || not (Document.utf8_valid (Buffer.to_bytes out) 0 (Buffer.length out)) then fragment
    else Buffer.contents out
  end

(* replace_all replaces every occurrence of a character by a string. *)
let replace_char s c by =
  if not (String.contains s c) then s
  else begin
    let out = Buffer.create (String.length s + 8) in
    String.iter (fun x -> if x = c then Buffer.add_string out by else Buffer.add_char out x) s;
    Buffer.contents out
  end

(* escape_pointer_token escapes a JSON pointer token (~ as ~0, / as ~1). *)
let escape_pointer_token token =
  if not (String.contains token '~' || String.contains token '/') then token
  else replace_char (replace_char token '~' "~0") '/' "~1"

(* unescape_pointer_token is the name a JSON pointer token stands for (~1 as /, then ~0 as ~). *)
let unescape_pointer_token raw =
  if not (String.contains raw '~') then raw
  else begin
    let replace s a b =
      (* Replaces each "~a" by b, left to right without overlap. *)
      let out = Buffer.create (String.length s) in
      let i = ref 0 in
      let n = String.length s in
      while !i < n do
        if s.[!i] = '~' && !i + 1 < n && s.[!i + 1] = a then begin
          Buffer.add_char out b;
          i := !i + 2
        end
        else begin
          Buffer.add_char out s.[!i];
          incr i
        end
      done;
      Buffer.contents out
    in
    replace (replace raw '1' '/') '0' '~'
  end

let is_array_index token =
  token = "0"
  || token <> ""
     && token.[0] <> '0'
     &&
     let ok = ref true in
     String.iter (fun c -> if not (is_ascii_digit c) then ok := false) token;
     !ok

(* resolve_pointer resolves an RFC 6901 JSON pointer against a value of a document, returning the value and its
   normalised pointer, or -1. *)
let resolve_pointer d root pointer =
  if pointer = "" then (root, "")
  else if pointer.[0] <> '/' then (-1, "")
  else begin
    let current = ref root in
    let path = Buffer.create (String.length pointer) in
    List.iter
      (fun raw ->
        if !current >= 0 then begin
          let token = unescape_pointer_token raw in
          let kind = Document.kind d !current in
          if kind = Document.kind_array then begin
            (* More than 18 digits cannot index an array, and would not fit an integer. *)
            let i = if is_array_index token && String.length token <= 18 then int_of_string token else -1 in
            if i < 0 || i >= Document.count d !current then current := -1
            else begin
              current := Document.first d !current + i;
              Buffer.add_char path '/';
              Buffer.add_string path token
            end
          end
          else if kind = Document.kind_object then begin
            current := Document.property d !current token;
            if !current >= 0 then begin
              Buffer.add_char path '/';
              Buffer.add_string path (escape_pointer_token token)
            end
          end
          else current := -1
        end)
      (String.split_on_char '/' (after pointer 1));
    if !current < 0 then (-1, "") else (!current, Buffer.contents path)
  end
