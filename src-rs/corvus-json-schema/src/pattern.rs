//! `pattern`/`patternProperties` matching with ECMA-262 semantics (the `u` flag), as JSON Schema specifies.
//!
//! Patterns compile once per process (a pattern is immutable, so identical patterns share one matcher).

use std::collections::HashMap;
use std::fmt;
use std::sync::{Arc, LazyLock, Mutex};

/// A compiled `pattern`.
pub(crate) struct Pattern {
    pub source: String,
    matcher: Matcher,
}

enum Matcher {
    /// Matches every string (`""`, `.*` without anchors, and the like).
    Everything,
    Regex(regress::Regex),
}

impl fmt::Debug for Pattern {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "Pattern({:?})", self.source)
    }
}

impl Pattern {
    #[inline]
    pub fn is_match(&self, s: &str) -> bool {
        match &self.matcher {
            Matcher::Everything => true,
            Matcher::Regex(re) => re.find(s).is_some(),
        }
    }
}

fn compile_regex(pattern: &str) -> Option<regress::Regex> {
    regress::Regex::with_flags(pattern, "u").or_else(|_| regress::Regex::new(pattern)).ok()
}

static CACHE: LazyLock<Mutex<HashMap<String, Arc<Pattern>>>> = LazyLock::new(|| Mutex::new(HashMap::new()));

/// Compiles (or fetches from the process-wide cache) a pattern; `None` when it is not a valid ECMA-262 regex.
pub(crate) fn compile(pattern: &str) -> Option<Arc<Pattern>> {
    if let Some(p) = CACHE.lock().unwrap().get(pattern) {
        return Some(p.clone());
    }
    // Unanchored (or start-anchored) `.*` finds an empty match in any string; `^.*$` does not (`.` stops at a line
    // terminator), so it is not listed.
    let matcher = if matches!(pattern, "" | ".*" | "^.*" | ".*$" | "[\\s\\S]*" | "^[\\s\\S]*" | "^[\\s\\S]*$") {
        Matcher::Everything
    } else {
        Matcher::Regex(compile_regex(pattern)?)
    };
    let p = Arc::new(Pattern { source: pattern.to_string(), matcher });
    CACHE.lock().unwrap().insert(pattern.to_string(), p.clone());
    Some(p)
}

/// The `regex` format: a valid ECMA-262 regular expression (with the `u` flag).
pub(crate) fn is_valid_ecma_regex(s: &str) -> bool {
    regress::Regex::with_flags(s, "u").is_ok()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn match_everything_shortcuts_agree_with_the_regex() {
        for p in ["", ".*", "^.*", ".*$", "[\\s\\S]*", "^[\\s\\S]*", "^[\\s\\S]*$", "^.*$"] {
            let re = compile_regex(p).unwrap();
            let pattern = compile(p).unwrap();
            for s in ["", "abc", "a\nb", "\n", "é😀"] {
                assert_eq!(pattern.is_match(s), re.find(s).is_some(), "{p:?} on {s:?}");
            }
        }
    }
}
