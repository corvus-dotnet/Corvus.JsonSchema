# The cases of the unified JSON-Schema-Test-Suite that the format checks got wrong (the descriptions are the suite's).
@testset "unified suite formats" begin
    b(s) = CorvusJsonSchema.Bytes(Vector{UInt8}(codeunits(s)))
    cases = [
        (:email, "\"test\\\u00a9\"@iana.org", false, "a non-ASCII character in a quoted pair is not valid"),
        (:email, "\"\\a\"@iana.org", true, "a quoted pair in the local part is valid"),
        (:idn_email, "\u0085@example.com", true, "a C1 control in the local part is valid"),
        (:idn_email, "\uffff@example.com", true, "a noncharacter in the local part is valid"),
        (:idn_hostname, "a\u200bb", true, "a zero width space is ignored by the mapping"),
        (:idn_hostname, repeat("\uff41", 63), true, "a label of 63 fullwidth characters is valid after mapping"),
        (:idn_hostname, "\uff58\uff4e--nxasmq6b", true, "an ACE prefix produced by the mapping step is decoded as an A-label"),
        (:uri_template, "a'b", true, "an apostrophe in a literal is valid"),
        (:uri_template, "a%", false, "a literal with a lone percent sign is invalid"),
        (:uri_template, "a%4", false, "a literal with an incomplete percent-encoded triplet is invalid"),
        (:uri_template, "a%GG", false, "a literal with non-hex percent encoding is invalid"),
        (:uri_template, "a\u007fb", false, "a delete character in a literal is invalid"),
    ]
    for (format, text, expected, description) in cases
        got = format === :email ? CorvusJsonSchema.is_email(b(text), false) :
              format === :idn_email ? CorvusJsonSchema.is_email(b(text), true) :
              format === :idn_hostname ? CorvusJsonSchema.is_idn_hostname(b(text)) :
              CorvusJsonSchema.is_uri_template(b(text))
        @test got == expected || error("$format: $description")
    end
    @test CorvusJsonSchema.UTS46_UNICODE_VERSION == "17.0.0"
    @test String(collect(CorvusJsonSchema.uts46_map(b("a\u200bb")))) == "ab"
end
