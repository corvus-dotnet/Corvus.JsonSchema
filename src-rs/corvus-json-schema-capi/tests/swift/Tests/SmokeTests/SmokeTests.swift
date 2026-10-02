import Smoke
import XCTest

final class SmokeTests: XCTestCase {
    func testValidates() {
        XCTAssertEqual(isValid(schema: #"{"type": "integer"}"#, json: "3"), true)
        XCTAssertEqual(isValid(schema: #"{"type": "integer"}"#, json: #""3""#), false)
        XCTAssertNil(isValid(schema: #"{"type": "integer"}"#, json: "{"))
    }

    func testVersion() {
        XCTAssertFalse(libraryVersion.isEmpty)
    }
}
