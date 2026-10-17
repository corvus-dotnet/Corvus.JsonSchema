(* A small JSON reader for the tests: the oracle and the files of the JSON Schema Test Suite. It reads well-formed JSON
   and does not check what it reads beyond what it needs to find its way. *)

type t = Null | Bool of bool | Number of float | String of string | Array of t list | Object of (string * t) list

exception Error of string

let add_rune buffer r =
  (* A surrogate alone is U+FFFD, as encoding/json of Go reads it. *)
  let r = if (r >= 0xD800 && r <= 0xDFFF) || r > 0x10FFFF then 0xFFFD else r in
  Buffer.add_utf_8_uchar buffer (Uchar.of_int r)

let parse (data : string) =
  let at = ref 0 in
  let n = String.length data in
  let fail what = raise (Error (Printf.sprintf "%s at byte %d" what !at)) in
  let space () =
    while !at < n && match data.[!at] with ' ' | '\t' | '\n' | '\r' -> true | _ -> false do
      incr at
    done
  in
  let hex4 from =
    let v = ref 0 in
    for i = from to from + 3 do
      let c = data.[i] in
      let d =
        match c with
        | '0' .. '9' -> Char.code c - 48
        | 'a' .. 'f' -> Char.code c - 87
        | 'A' .. 'F' -> Char.code c - 55
        | _ -> fail "a \\u escape"
      in
      v := (!v * 16) + d
    done;
    !v
  in
  let read_string () =
    (* The position is at the opening quote. *)
    incr at;
    let out = Buffer.create 32 in
    while data.[!at] <> '"' do
      let c = data.[!at] in
      incr at;
      if c <> '\\' then Buffer.add_char out c
      else begin
        let e = data.[!at] in
        incr at;
        match e with
        | 'n' -> Buffer.add_char out '\n'
        | 'r' -> Buffer.add_char out '\r'
        | 't' -> Buffer.add_char out '\t'
        | 'b' -> Buffer.add_char out '\b'
        | 'f' -> Buffer.add_char out '\012'
        | 'u' ->
            let r = hex4 !at in
            at := !at + 4;
            (* A surrogate pair is one code point. *)
            if r >= 0xD800 && r <= 0xDBFF && !at + 6 <= n && data.[!at] = '\\' && data.[!at + 1] = 'u' then begin
              let low = hex4 (!at + 2) in
              if low >= 0xDC00 && low <= 0xDFFF then begin
                at := !at + 6;
                add_rune out (0x10000 + ((r - 0xD800) lsl 10) + (low - 0xDC00))
              end
              else add_rune out r
            end
            else add_rune out r
        | other -> Buffer.add_char out other
      end
    done;
    incr at;
    Buffer.contents out
  in
  let rec value () =
    space ();
    if !at >= n then fail "the end of the text";
    match data.[!at] with
    | '{' ->
        incr at;
        space ();
        if data.[!at] = '}' then begin
          incr at;
          Object []
        end
        else begin
          let members = ref [] in
          let more = ref true in
          while !more do
            space ();
            let name = read_string () in
            space ();
            if data.[!at] <> ':' then fail "no colon";
            incr at;
            let v = value () in
            members := (name, v) :: !members;
            space ();
            if data.[!at] = ',' then incr at
            else if data.[!at] = '}' then begin
              incr at;
              more := false
            end
            else fail "an object"
          done;
          Object (List.rev !members)
        end
    | '[' ->
        incr at;
        space ();
        if data.[!at] = ']' then begin
          incr at;
          Array []
        end
        else begin
          let items = ref [] in
          let more = ref true in
          while !more do
            let v = value () in
            items := v :: !items;
            space ();
            if data.[!at] = ',' then incr at
            else if data.[!at] = ']' then begin
              incr at;
              more := false
            end
            else fail "an array"
          done;
          Array (List.rev !items)
        end
    | '"' -> String (read_string ())
    | 't' ->
        at := !at + 4;
        Bool true
    | 'f' ->
        at := !at + 5;
        Bool false
    | 'n' ->
        at := !at + 4;
        Null
    | _ ->
        let start = !at in
        while !at < n && match data.[!at] with '0' .. '9' | '-' | '+' | '.' | 'e' | 'E' -> true | _ -> false do
          incr at
        done;
        if !at = start then fail "a value";
        Number (float_of_string (String.sub data start (!at - start)))
  in
  let v = value () in
  space ();
  if !at <> n then fail "text after the value";
  v

let read_file path = In_channel.with_open_bin path In_channel.input_all
let load path = parse (read_file path)
let member name = function Object members -> List.assoc_opt name members | _ -> None
let text name v = match member name v with Some (String s) -> s | _ -> raise (Error ("no string " ^ name))
let flag name v = match member name v with Some (Bool b) -> b | _ -> false
let items name v = match member name v with Some (Array a) -> a | _ -> raise (Error ("no array " ^ name))
