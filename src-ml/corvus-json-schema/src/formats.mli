(** Format assertions, applied only when format is asserted (the format-assertion vocabulary or the assert format
    option). They match the C# evaluator's format checks.

    A text is a range of a [Bytes.t], given by an offset and a length, that holds valid UTF-8. No check copies the text
    or writes to it. A byte that is not well-formed UTF-8 reads as U+FFFD.

    {1 Allocation}

    {!check_string} allocates nothing for any format but these: [regex], [idn-hostname], [idn-email], and [hostname]
    when a label starts with ["xn--"] (and so [email] when a label of its domain does). Every table the checks read is
    built when the module is initialised and never written to afterwards, so the checks are safe to call from any number
    of threads and domains at once. *)

type kind = int
(** The format a dialect recognises for a format value. The values are those of the Go source's [formatKind], in the
    same order. *)

val unknown : kind
(** A format the dialect does not know, which asserts nothing. *)

val date : kind
(** [date]. *)

val time : kind
(** [time]. *)

val date_time : kind
(** [date-time]. *)

val duration : kind
(** [duration]. *)

val uuid : kind
(** [uuid]. *)

val ipv4 : kind
(** [ipv4]. *)

val ipv6 : kind
(** [ipv6]. *)

val hostname : kind
(** [hostname]. *)

val idn_hostname : kind
(** [idn-hostname]. *)

val email : kind
(** [email]. *)

val idn_email : kind
(** [idn-email]. *)

val uri : kind
(** [uri]. *)

val uri_reference : kind
(** [uri-reference]. *)

val iri : kind
(** [iri]. *)

val iri_reference : kind
(** [iri-reference]. *)

val uri_template : kind
(** [uri-template]. *)

val json_pointer : kind
(** [json-pointer]. *)

val relative_json_pointer : kind
(** [relative-json-pointer]. *)

val regex : kind
(** [regex]. It is the last of the string formats. *)

val byte : kind
(** [byte], the first of the numeric formats (a Corvus extension). *)

val uint16 : kind
(** [uint16]. *)

val uint32 : kind
(** [uint32]. *)

val uint64 : kind
(** [uint64]. *)

val uint128 : kind
(** [uint128]. *)

val sbyte : kind
(** [sbyte]. *)

val int16 : kind
(** [int16]. *)

val int32 : kind
(** [int32]. *)

val int64 : kind
(** [int64]. *)

val int128 : kind
(** [int128]. *)

val half : kind
(** [half]. *)

val single : kind
(** [single], which a schema also names [float]. *)

val double : kind
(** [double]. *)

val decimal : kind
(** [decimal], the last kind. *)

val kind_of : string -> int -> kind
(** [kind_of format dialect] is the format a dialect recognises for a format value, or {!unknown}. [dialect] is the rank
    of the dialect in specification order: 0 for draft 4, 1 for draft 6, 2 for draft 7, 3 for 2019-09 and 4 for 2020-12.
*)

val is_numeric : kind -> bool
(** [is_numeric kind] reports whether the format asserts something of a number and nothing of a string. *)

val name : kind -> string
(** [name kind] is the canonical name, for messages.

    @raise Invalid_argument if [kind] is not one of the kinds. *)

val message : kind -> string
(** [message kind] is the message for a string format failure (the C# evaluator's text), or [""]. *)

val check_string : kind -> Bytes.t -> int -> int -> bool -> bool
(** [check_string kind text off len legacy_hostname] asserts a string format of the [len] bytes of [text] from [off].
    [legacy_hostname] selects the RFC 1123 host name rules of draft 4 and 6. It is [true] for {!unknown} and for a
    numeric format. See the note on allocation above.

    @raise Invalid_argument if [off] and [len] do not name a range of [text]. *)

val kind_validator : kind -> bool -> (Bytes.t -> int -> int -> bool) option
(** [kind_validator kind legacy_hostname] is the check {!check_string} makes for a string format, as a function to keep.
    It is [None] for {!unknown} and for the numeric formats, which assert nothing of a string. *)

val validator : string -> (Bytes.t -> int -> int -> bool) option
(** [validator name] is the built-in check for a format name, or [None] when the name is not a known format. It is the
    format as the latest dialect reads it. The numeric formats assert nothing of a string and have no check. *)

(** {1 The checks themselves}

    Each reports whether the [len] bytes of [text] from [off] are valid for its format: [is_date text off len]. They are
    internal, for {!check_string} and the tests. *)

val is_date : Bytes.t -> int -> int -> bool
(** An RFC 3339 full-date. *)

val is_time : Bytes.t -> int -> int -> bool
(** An RFC 3339 full-time. A leap second is valid only at 23:59:60 UTC. *)

val is_date_time : Bytes.t -> int -> int -> bool
(** An RFC 3339 date-time. *)

val is_duration : Bytes.t -> int -> int -> bool
(** An ISO 8601 duration: P then weeks, or date parts in order then an optional time, or a time. *)

val is_uuid : Bytes.t -> int -> int -> bool
(** An RFC 4122 UUID. *)

val is_ipv4 : Bytes.t -> int -> int -> bool
(** A dotted-quad IPv4 address. *)

val is_ipv6 : Bytes.t -> int -> int -> bool
(** An RFC 4291 IPv6 address. *)

val is_legacy_hostname : Bytes.t -> int -> int -> bool
(** An RFC 1123 host name (draft 4 and 6): no IDNA rules for ["--"] or A-labels. *)

val is_hostname : Bytes.t -> int -> int -> bool
(** A host name whose A-labels are valid. It allocates when a label starts with ["xn--"]. *)

val is_idn_hostname : Bytes.t -> int -> int -> bool
(** An internationalized host name. It allocates. *)

val is_email : Bytes.t -> int -> int -> bool
(** An e-mail address. It allocates when a label of its domain starts with ["xn--"]. *)

val is_idn_email : Bytes.t -> int -> int -> bool
(** An internationalized e-mail address. It allocates. *)

val is_uri : Bytes.t -> int -> int -> bool
(** An RFC 3986 URI. *)

val is_uri_reference : Bytes.t -> int -> int -> bool
(** An RFC 3986 URI or relative reference. *)

val is_iri : Bytes.t -> int -> int -> bool
(** An RFC 3987 IRI. *)

val is_iri_reference : Bytes.t -> int -> int -> bool
(** An RFC 3987 IRI or relative reference. *)

val is_uri_template : Bytes.t -> int -> int -> bool
(** An RFC 6570 URI template. *)

val is_json_pointer : Bytes.t -> int -> int -> bool
(** An RFC 6901 JSON pointer. *)

val is_relative_json_pointer : Bytes.t -> int -> int -> bool
(** A relative JSON pointer. *)

val is_regex : Bytes.t -> int -> int -> bool
(** An ECMA-262 pattern that is valid with the u flag. It allocates. *)

(** {1 Punycode}

    Internal, for the tests. *)

val punycode_encode_string : string -> string
(** [punycode_encode_string label] is the RFC 3492 encoding of a label (UTF-8), without the ["xn--"] prefix. *)

val punycode_decode_string : string -> string option
(** [punycode_decode_string encoded] is the label (UTF-8) that RFC 3492 text decodes to, or [None] when the text is not
    Punycode. The text has no ["xn--"] prefix. *)

(** {1 Content}

    The contentEncoding check of draft 7, which eval.go holds in the Go source and the formats unit holds in the Object
    Pascal port. *)

val base64_decoded_capacity : int -> int
(** [base64_decoded_capacity len] is the most bytes that [len] bytes of base64 decode to. *)

val base64_decode : Bytes.t -> int -> int -> Bytes.t -> int
(** [base64_decode text off len output] decodes padded standard base64 into [output] from its start, giving the number
    of bytes decoded, or -1 when the text is not base64. It accepts no line breaks. It does not allocate.

    @raise Invalid_argument
      if [output] is shorter than {!base64_decoded_capacity}[ len], or [off] and [len] do not name a range of [text]. *)
