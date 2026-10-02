import CCorvusJsonSchema

/// Whether `json` is valid against `schema`, through the C library (nil when either is not valid JSON or the schema
/// does not compile).
public func isValid(schema: String, json: String) -> Bool? {
    var validator: OpaquePointer?
    var schema = schema
    let compiled = schema.withUTF8 { cjs_compile(chars($0), $0.count, nil, &validator) }
    guard compiled == cjs_status(CJS_OK), let validator else { return nil }
    defer { cjs_validator_free(validator) }
    var valid = false
    var json = json
    let status = json.withUTF8 { cjs_validator_validate_json(validator, chars($0), $0.count, &valid) }
    return status == cjs_status(CJS_OK) ? valid : nil
}

/// The C library's version, such as "0.1.1".
public var libraryVersion: String {
    let v = cjs_version_string()
    return String(decoding: UnsafeRawBufferPointer(start: v.ptr, count: v.len), as: UTF8.self)
}

private func chars(_ bytes: UnsafeBufferPointer<UInt8>) -> UnsafePointer<CChar>? {
    UnsafeRawPointer(bytes.baseAddress)?.assumingMemoryBound(to: CChar.self)
}
