//! Sidecar-side artifact verification (P1-10, ADR 0063).
//!
//! The runner's deploy path verifies a native artifact's attestation before it bakes the initrd, but the sidecar
//! is the process that actually boots the image, and under the mutual-distrust model (ADR 0065) it does not take
//! the runner's word for it. Every evolve carries the control plane's signed attestation for the binary inside the
//! staged initrd, and the sidecar checks it against its own trust store: the detached signature verifies under a
//! trusted key, the attestation parses, and the digest it names is the digest of the one regular file in the
//! newc CPIO. Nothing boots otherwise.
//!
//! The scheme is the executor-package scheme the runner already uses: the signature document is
//! `{"algorithm", "keyId", "value"}` with `value` base64, the algorithms are `ecdsa-p256-sha256`,
//! `ecdsa-p384-sha384` (IEEE P1363 fixed-size signatures) and `rsa-pss-sha256`, and the trusted keys are
//! `-----BEGIN PUBLIC KEY-----` (SubjectPublicKeyInfo) PEM, keyed by the key id a signature names. Trusting more
//! than one key id rolls a signing key over without a flag day.
//!
//! What the sidecar cannot check is the attestation's `packageHash`: it has no package, so the binding of a
//! binary to a catalog version stays on the runner's deploy path. The sidecar's guarantee is that the binary it
//! boots is one a trusted key attested.

use std::collections::HashMap;

use base64::Engine;
use ring::signature::{self, UnparsedPublicKey};
use serde::Deserialize;

/// ECDSA over P-256 with SHA-256, IEEE P1363 fixed-size signature.
pub const ALGORITHM_ECDSA_P256_SHA256: &str = "ecdsa-p256-sha256";
/// ECDSA over P-384 with SHA-384, IEEE P1363 fixed-size signature.
pub const ALGORITHM_ECDSA_P384_SHA384: &str = "ecdsa-p384-sha384";
/// RSASSA-PSS with SHA-256 and a salt the length of the digest.
pub const ALGORITHM_RSA_PSS_SHA256: &str = "rsa-pss-sha256";

/// The attestation format version this sidecar understands.
const ATTESTATION_FORMAT_VERSION: u32 = 1;

const PEM_BEGIN: &str = "-----BEGIN PUBLIC KEY-----";
const PEM_END: &str = "-----END PUBLIC KEY-----";

// DER-encoded object identifiers (content bytes) of the key algorithms the trust store accepts.
const OID_EC_PUBLIC_KEY: &[u8] = &[0x2a, 0x86, 0x48, 0xce, 0x3d, 0x02, 0x01];
const OID_PRIME256V1: &[u8] = &[0x2a, 0x86, 0x48, 0xce, 0x3d, 0x03, 0x01, 0x07];
const OID_SECP384R1: &[u8] = &[0x2b, 0x81, 0x04, 0x00, 0x22];
const OID_RSA_ENCRYPTION: &[u8] = &[0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01];

const DER_SEQUENCE: u8 = 0x30;
const DER_OID: u8 = 0x06;
const DER_BIT_STRING: u8 = 0x03;

/// The newc header length and magic, and the mode bits that mark a regular file.
const NEWC_HEADER_LEN: usize = 110;
const NEWC_MAGIC: &[u8] = b"070701";
const NEWC_TRAILER: &[u8] = b"TRAILER!!!";
const MODE_TYPE_MASK: usize = 0o170000;
const MODE_REGULAR_FILE: usize = 0o100000;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum KeyKind {
    P256,
    P384,
    Rsa,
}

impl KeyKind {
    fn name(self) -> &'static str {
        match self {
            KeyKind::P256 => "ECDSA P-256",
            KeyKind::P384 => "ECDSA P-384",
            KeyKind::Rsa => "RSA",
        }
    }
}

/// A trusted public key in the form ring verifies with: the uncompressed point for an EC key, the PKCS#1
/// `RSAPublicKey` for an RSA key.
#[derive(Clone, Debug)]
struct TrustedKey {
    kind: KeyKind,
    material: Vec<u8>,
}

/// The sidecar's trust store: the attestation-signing public keys it accepts, by key id.
#[derive(Clone, Debug, Default)]
pub struct TrustStore {
    keys: HashMap<String, TrustedKey>,
}

/// The detached signature document over the attestation's exact bytes.
#[derive(Clone, Debug, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SignatureDocument {
    pub algorithm: String,
    pub key_id: String,
    /// The raw signature bytes, base64.
    pub value: String,
}

/// The fields of the attestation the sidecar acts on (`NativeArtifactAttestation` on the control plane).
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AttestationDocument {
    format_version: u32,
    package_hash: String,
    rid: String,
    native_digest: String,
}

/// What a verified evolve established about the staged image.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct VerifiedImage {
    /// The `sha256:<hex>` digest of the guest binary, as attested and as measured.
    pub native_digest: String,
    /// The runtime identifier the attestation names.
    pub rid: String,
    /// The catalog version content hash the attestation names (not checkable here; logged for the trail).
    pub package_hash: String,
    /// The trusted key id the signature verified under.
    pub key_id: String,
}

impl TrustStore {
    /// An empty store. A sidecar refuses to start with one.
    pub fn new() -> Self {
        Self::default()
    }

    /// Whether the store trusts no key at all.
    pub fn is_empty(&self) -> bool {
        self.keys.is_empty()
    }

    /// Adds a SubjectPublicKeyInfo PEM public key (`-----BEGIN PUBLIC KEY-----`) under `key_id`. An EC key
    /// must be on P-256 or P-384; anything else, a private key included, is refused.
    pub fn add_pem(&mut self, key_id: &str, pem: &str) -> anyhow::Result<()> {
        if key_id.trim().is_empty() {
            anyhow::bail!("a trusted key needs a non-empty key id");
        }

        let spki = decode_pem(pem).map_err(|reason| anyhow::anyhow!("trusted key '{key_id}': {reason}"))?;
        let key = parse_spki(&spki).map_err(|reason| anyhow::anyhow!("trusted key '{key_id}': {reason}"))?;
        self.keys.insert(key_id.to_string(), key);
        Ok(())
    }

    /// Verifies a detached signature over `message` against the trusted key the signature names. Every failure
    /// is the same refusal to a caller; the reason is for the sidecar's own log line.
    pub fn verify(&self, message: &[u8], signature: &SignatureDocument) -> Result<(), String> {
        let Some(key) = self.keys.get(&signature.key_id) else {
            return Err(format!("the signature names key id '{}', which this sidecar does not trust", signature.key_id));
        };

        let algorithm: &'static dyn signature::VerificationAlgorithm = match (signature.algorithm.as_str(), key.kind) {
            (ALGORITHM_ECDSA_P256_SHA256, KeyKind::P256) => &signature::ECDSA_P256_SHA256_FIXED,
            (ALGORITHM_ECDSA_P384_SHA384, KeyKind::P384) => &signature::ECDSA_P384_SHA384_FIXED,
            (ALGORITHM_RSA_PSS_SHA256, KeyKind::Rsa) => &signature::RSA_PSS_2048_8192_SHA256,
            (ALGORITHM_ECDSA_P256_SHA256 | ALGORITHM_ECDSA_P384_SHA384 | ALGORITHM_RSA_PSS_SHA256, kind) => {
                return Err(format!(
                    "the signature algorithm '{}' cannot be verified with key id '{}', which is an {} key",
                    signature.algorithm,
                    signature.key_id,
                    kind.name()
                ));
            }
            (other, _) => return Err(format!("the signature algorithm '{other}' is not one this sidecar understands")),
        };

        let value = base64::engine::general_purpose::STANDARD
            .decode(&signature.value)
            .map_err(|error| format!("the signature value is not base64: {error}"))?;

        UnparsedPublicKey::new(algorithm, &key.material)
            .verify(message, &value)
            .map_err(|_| format!("the signature does not verify under key id '{}'", signature.key_id))
    }
}

/// Verifies the staged initrd against the attestation and signature an evolve carries. Order matters for what
/// the refusal reveals: the signature is checked before the attestation is read, so an unsigned document is
/// never parsed, and the digest is compared last.
pub fn verify_staged_image(
    trust: &TrustStore,
    initrd: &[u8],
    attestation_base64: Option<&str>,
    signature: Option<&SignatureDocument>,
) -> Result<VerifiedImage, String> {
    let (Some(attestation_base64), Some(signature)) = (attestation_base64, signature) else {
        return Err("the configuration carries no attestation and signature for the staged initrd".to_string());
    };

    let attestation_utf8 = base64::engine::general_purpose::STANDARD
        .decode(attestation_base64)
        .map_err(|error| format!("the attestation is not base64: {error}"))?;

    trust.verify(&attestation_utf8, signature)?;

    let document: AttestationDocument =
        serde_json::from_slice(&attestation_utf8).map_err(|error| format!("the attestation is malformed: {error}"))?;
    if document.format_version != ATTESTATION_FORMAT_VERSION {
        return Err(format!(
            "the attestation format version {} is not the {} this sidecar understands",
            document.format_version, ATTESTATION_FORMAT_VERSION
        ));
    }

    let guest_binary = guest_binary_of(initrd)?;
    let measured = sha256_digest(guest_binary);
    if !constant_time_eq(measured.as_bytes(), document.native_digest.as_bytes()) {
        return Err(format!(
            "the guest binary in the staged initrd digests to {measured} but the attestation names {}",
            document.native_digest
        ));
    }

    Ok(VerifiedImage {
        native_digest: measured,
        rid: document.rid,
        package_hash: document.package_hash,
        key_id: signature.key_id.clone(),
    })
}

/// The one regular file in a newc CPIO archive: the guest binary the deployer baked. An archive with no
/// regular file, more than one, or any structural fault is refused.
pub fn guest_binary_of(initrd: &[u8]) -> Result<&[u8], String> {
    let mut offset = 0usize;
    let mut found: Option<&[u8]> = None;
    loop {
        let header = initrd
            .get(offset..offset + NEWC_HEADER_LEN)
            .ok_or_else(|| format!("the initrd is not a complete newc cpio archive: truncated header at offset {offset}"))?;
        if &header[..NEWC_MAGIC.len()] != NEWC_MAGIC {
            return Err(format!("the initrd is not a newc cpio archive: bad magic at offset {offset}"));
        }

        let field = |index: usize| -> Result<usize, String> {
            let start = NEWC_MAGIC.len() + index * 8;
            let text = std::str::from_utf8(&header[start..start + 8]).map_err(|_| "non-ASCII header field".to_string())?;
            usize::from_str_radix(text, 16).map_err(|_| format!("the newc header field {index} at offset {offset} is not hex"))
        };
        let mode = field(1)?;
        let file_size = field(6)?;
        let name_size = field(11)?;

        let name_start = offset + NEWC_HEADER_LEN;
        let name = initrd
            .get(name_start..name_start + name_size)
            .ok_or_else(|| format!("the initrd is truncated in the entry name at offset {name_start}"))?;
        let name = name.strip_suffix(&[0u8]).unwrap_or(name);

        let data_start = align4(name_start + name_size);
        let data = initrd
            .get(data_start..data_start + file_size)
            .ok_or_else(|| format!("the initrd is truncated in the entry data at offset {data_start}"))?;

        if name == NEWC_TRAILER {
            break;
        }

        if mode & MODE_TYPE_MASK == MODE_REGULAR_FILE {
            if found.is_some() {
                return Err("the initrd carries more than one regular file; a guest image carries exactly the guest binary".to_string());
            }
            found = Some(data);
        }

        offset = align4(data_start + file_size);
    }

    found.ok_or_else(|| "the initrd carries no regular file to attest".to_string())
}

/// The `sha256:<hex>` digest the attestation's `nativeDigest` carries.
pub fn sha256_digest(bytes: &[u8]) -> String {
    let digest = ring::digest::digest(&ring::digest::SHA256, bytes);
    let mut text = String::with_capacity(7 + 64);
    text.push_str("sha256:");
    for byte in digest.as_ref() {
        text.push_str(&format!("{byte:02x}"));
    }
    text
}

fn align4(offset: usize) -> usize {
    (offset + 3) & !3
}

fn constant_time_eq(a: &[u8], b: &[u8]) -> bool {
    a.len() == b.len() && a.iter().zip(b).fold(0u8, |acc, (x, y)| acc | (x ^ y)) == 0
}

/// Decodes a `-----BEGIN PUBLIC KEY-----` PEM block to its DER. Any other PEM label is refused so a private key
/// or a bare `RSA PUBLIC KEY` handed to the sidecar by mistake is a start-up error, not a silent trust.
fn decode_pem(pem: &str) -> Result<Vec<u8>, String> {
    let text = pem.trim();
    let body = text
        .strip_prefix(PEM_BEGIN)
        .and_then(|rest| rest.strip_suffix(PEM_END))
        .ok_or_else(|| format!("expected a '{PEM_BEGIN}' PEM block (a SubjectPublicKeyInfo public key)"))?;
    let compact: String = body.chars().filter(|c| !c.is_ascii_whitespace()).collect();
    base64::engine::general_purpose::STANDARD
        .decode(compact)
        .map_err(|error| format!("the PEM body is not base64: {error}"))
}

/// Parses a SubjectPublicKeyInfo: `SEQUENCE { SEQUENCE { OID algorithm, parameters }, BIT STRING key }`.
fn parse_spki(der: &[u8]) -> Result<TrustedKey, String> {
    let (spki, rest) = der_element(der, DER_SEQUENCE)?;
    if !rest.is_empty() {
        return Err("trailing bytes after the SubjectPublicKeyInfo".to_string());
    }

    let (algorithm, after_algorithm) = der_element(spki, DER_SEQUENCE)?;
    let (key_bits, after_key) = der_element(after_algorithm, DER_BIT_STRING)?;
    if !after_key.is_empty() {
        return Err("trailing bytes after the public key".to_string());
    }

    let (oid, parameters) = der_element(algorithm, DER_OID)?;
    let material = match key_bits.split_first() {
        Some((0, material)) => material,
        _ => return Err("the public key BIT STRING has unused bits".to_string()),
    };

    let kind = if oid == OID_EC_PUBLIC_KEY {
        let (curve, rest) = der_element(parameters, DER_OID)?;
        if !rest.is_empty() {
            return Err("trailing bytes after the EC curve parameters".to_string());
        }
        let (kind, point_len) = if curve == OID_PRIME256V1 {
            (KeyKind::P256, 65)
        } else if curve == OID_SECP384R1 {
            (KeyKind::P384, 97)
        } else {
            return Err("the EC key is not on P-256 or P-384".to_string());
        };
        if material.len() != point_len || material[0] != 0x04 {
            return Err(format!("the {} public key is not an uncompressed point", kind.name()));
        }
        kind
    } else if oid == OID_RSA_ENCRYPTION {
        // ring takes the PKCS#1 RSAPublicKey the BIT STRING wraps, verbatim; it checks the modulus size itself.
        let (_, rest) = der_element(material, DER_SEQUENCE)?;
        if !rest.is_empty() {
            return Err("trailing bytes after the RSAPublicKey".to_string());
        }
        KeyKind::Rsa
    } else {
        return Err("the key algorithm is neither EC (id-ecPublicKey) nor RSA (rsaEncryption)".to_string());
    };

    Ok(TrustedKey { kind, material: material.to_vec() })
}

/// One DER element of the expected tag: returns its content and whatever follows it.
fn der_element(input: &[u8], expected_tag: u8) -> Result<(&[u8], &[u8]), String> {
    let (&tag, rest) = input.split_first().ok_or_else(|| "unexpected end of DER".to_string())?;
    if tag != expected_tag {
        return Err(format!("expected DER tag 0x{expected_tag:02x}, found 0x{tag:02x}"));
    }

    let (&first, rest) = rest.split_first().ok_or_else(|| "unexpected end of DER length".to_string())?;
    let (length, rest) = if first < 0x80 {
        (first as usize, rest)
    } else {
        let count = (first & 0x7f) as usize;
        if count == 0 || count > 4 || rest.len() < count {
            return Err("unsupported DER length encoding".to_string());
        }
        let length = rest[..count].iter().fold(0usize, |acc, &b| (acc << 8) | b as usize);
        (length, &rest[count..])
    };

    if rest.len() < length {
        return Err("DER element runs past the end of the input".to_string());
    }

    Ok(rest.split_at(length))
}

#[cfg(test)]
pub(crate) mod testing {
    //! Test-only signing: a fresh ECDSA key pair, its SPKI PEM for a trust store, and a signature document
    //! over any message, mirroring what the control plane's signer produces.

    use base64::Engine;
    use ring::rand::SystemRandom;
    use ring::signature::{EcdsaKeyPair, KeyPair, ECDSA_P256_SHA256_FIXED_SIGNING, ECDSA_P384_SHA384_FIXED_SIGNING};

    use super::{SignatureDocument, ALGORITHM_ECDSA_P256_SHA256, ALGORITHM_ECDSA_P384_SHA384};

    // The SubjectPublicKeyInfo prefix in front of an uncompressed point for each curve.
    const SPKI_P256_PREFIX: &[u8] = &[
        0x30, 0x59, 0x30, 0x13, 0x06, 0x07, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x02, 0x01, 0x06, 0x08, 0x2a, 0x86, 0x48, 0xce, 0x3d,
        0x03, 0x01, 0x07, 0x03, 0x42, 0x00,
    ];
    const SPKI_P384_PREFIX: &[u8] = &[
        0x30, 0x76, 0x30, 0x10, 0x06, 0x07, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x02, 0x01, 0x06, 0x05, 0x2b, 0x81, 0x04, 0x00, 0x22,
        0x03, 0x62, 0x00,
    ];

    pub(crate) struct TestSigner {
        key_pair: EcdsaKeyPair,
        rng: SystemRandom,
        prefix: &'static [u8],
        algorithm: &'static str,
        pub(crate) key_id: String,
    }

    impl TestSigner {
        pub(crate) fn p256(key_id: &str) -> Self {
            Self::generate(&ECDSA_P256_SHA256_FIXED_SIGNING, SPKI_P256_PREFIX, ALGORITHM_ECDSA_P256_SHA256, key_id)
        }

        pub(crate) fn p384(key_id: &str) -> Self {
            Self::generate(&ECDSA_P384_SHA384_FIXED_SIGNING, SPKI_P384_PREFIX, ALGORITHM_ECDSA_P384_SHA384, key_id)
        }

        fn generate(
            signing: &'static ring::signature::EcdsaSigningAlgorithm,
            prefix: &'static [u8],
            algorithm: &'static str,
            key_id: &str,
        ) -> Self {
            let rng = SystemRandom::new();
            let pkcs8 = EcdsaKeyPair::generate_pkcs8(signing, &rng).expect("generate key");
            let key_pair = EcdsaKeyPair::from_pkcs8(signing, pkcs8.as_ref(), &rng).expect("load key");
            Self { key_pair, rng, prefix, algorithm, key_id: key_id.to_string() }
        }

        pub(crate) fn public_key_pem(&self) -> String {
            let mut spki = self.prefix.to_vec();
            spki.extend_from_slice(self.key_pair.public_key().as_ref());
            let body = base64::engine::general_purpose::STANDARD.encode(spki);
            let lines: Vec<&str> = body.as_bytes().chunks(64).map(|chunk| std::str::from_utf8(chunk).unwrap()).collect();
            format!("-----BEGIN PUBLIC KEY-----\n{}\n-----END PUBLIC KEY-----\n", lines.join("\n"))
        }

        pub(crate) fn sign(&self, message: &[u8]) -> SignatureDocument {
            let signature = self.key_pair.sign(&self.rng, message).expect("sign");
            SignatureDocument {
                algorithm: self.algorithm.to_string(),
                key_id: self.key_id.clone(),
                value: base64::engine::general_purpose::STANDARD.encode(signature.as_ref()),
            }
        }
    }

    /// A newc CPIO in the deployer's shape: `.`, `bin`, `bin/guest` (the binary), trailer.
    pub(crate) fn initrd_around(guest_binary: &[u8]) -> Vec<u8> {
        let mut archive = Vec::new();
        newc_entry(&mut archive, 1, ".", 0o040755, &[]);
        newc_entry(&mut archive, 2, "bin", 0o040755, &[]);
        newc_entry(&mut archive, 3, "bin/guest", 0o100755, guest_binary);
        newc_entry(&mut archive, 0, "TRAILER!!!", 0, &[]);
        archive
    }

    pub(crate) fn newc_entry(archive: &mut Vec<u8>, inode: u32, name: &str, mode: u32, data: &[u8]) {
        let name_size = name.len() + 1;
        let fields = [
            inode,
            mode,
            0,
            0,
            if mode & 0o170000 == 0o040000 { 2 } else { 1 },
            0,
            data.len() as u32,
            0,
            0,
            0,
            0,
            name_size as u32,
            0,
        ];
        archive.extend_from_slice(b"070701");
        for field in fields {
            archive.extend_from_slice(format!("{field:08x}").as_bytes());
        }
        archive.extend_from_slice(name.as_bytes());
        archive.push(0);
        while archive.len() % 4 != 0 {
            archive.push(0);
        }
        archive.extend_from_slice(data);
        while archive.len() % 4 != 0 {
            archive.push(0);
        }
    }

    /// The attestation the control plane would sign for `guest_binary`, in its exact (alphabetical-key) form.
    pub(crate) fn attestation_for(guest_binary: &[u8]) -> Vec<u8> {
        format!(
            r#"{{"engineVersion":"1.0.0","formatVersion":1,"nativeDigest":"{}","packageHash":"sha256:package","rid":"linux-musl-x64"}}"#,
            super::sha256_digest(guest_binary)
        )
        .into_bytes()
    }
}

#[cfg(test)]
mod tests {
    use base64::Engine;

    use super::testing::{attestation_for, initrd_around, newc_entry, TestSigner};
    use super::*;

    const GUEST: &[u8] = b"\x7fELF-fake-guest-binary";

    const RSA_PUBLIC_KEY_PEM: &str = "-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAiV0nSYgL5hgasU3qFh52
KW7O0ms+GVohh4HVYel7kybQoNob8lSPYRUD5mSLZVTZ5ToRVyocUzror7kDgPcR
/JQR3xicJ/E6rFc8Jj8px/E2OkMUle7hpHNBddm5PP9hzVMKJrZaWEu67Vd2PX9K
goWDnDLkd0tBiaAP2wlKUhwdpnBPTHuSTlTLMOxIb0a88+QKyTRzm1VMcSBMS9AL
luhLz2FQmPWs+QtXSdOb5vWbBHrwnBn4Lo4XVvDU6mZPNHzSPPQxhIFVfnUhHK8k
XhY9G7TNPsKommi7r6bGbEs2tMAuNLvkK36/DYxWovx1mMX5s/jrMFQFSOTLeC98
6wIDAQAB
-----END PUBLIC KEY-----";

    // Produced with `openssl dgst -sha256 -sigopt rsa_padding_mode:pss -sigopt rsa_pss_saltlen:32` over the message
    // below, under the private half of the key above: the shape .NET's RSASignaturePadding.Pss and the cloud KMS
    // PS256 modes produce.
    const RSA_MESSAGE: &[u8] = br#"{"engineVersion":"1.0.0","formatVersion":1,"nativeDigest":"sha256:0000000000000000000000000000000000000000000000000000000000000000","packageHash":"sha256:fixture","rid":"linux-musl-x64"}"#;
    const RSA_SIGNATURE_BASE64: &str = "Uw2l4DEOJk6FWK4XS86xEmf5uOj9oIBA9tzDMwlvbfXC7Q6OG8ZWjktS3VmXP2AcrwYL68zZbZeDU0oIJpOAAE8mEMg8zXkpdzjtN5pumFfhvu6bNVHHlPO0v57zyPCabKPqTlr/tV9o4AxiGqqVWBsq9J1CwSmHMz2R1GsSkvErCCUUZjAGny7Ixc/Oj51mAYmW2BWS7G/CEgNM1FbJJKABzaSJ8+klKEcb8MnvoKDQgsMqg8Chvm1seki3wAgc6rO5Mizs9sGv3ifLGbg4TKAiHMgZ/JJcrOtA6c2LOH8zCHdKm5N1uaR1VBYbxv3YuQdqE8/0w3inzlbMtzj1mQ==";

    fn store_with(signer: &TestSigner) -> TrustStore {
        let mut store = TrustStore::new();
        store.add_pem(&signer.key_id, &signer.public_key_pem()).expect("trust the test key");
        store
    }

    fn encoded(bytes: &[u8]) -> String {
        base64::engine::general_purpose::STANDARD.encode(bytes)
    }

    #[test]
    fn a_p256_signed_attestation_over_the_staged_binary_verifies() {
        let signer = TestSigner::p256("release-2026");
        let attestation = attestation_for(GUEST);
        let signature = signer.sign(&attestation);

        let verified = verify_staged_image(&store_with(&signer), &initrd_around(GUEST), Some(&encoded(&attestation)), Some(&signature))
            .expect("verifies");

        assert_eq!(verified.native_digest, sha256_digest(GUEST));
        assert_eq!(verified.rid, "linux-musl-x64");
        assert_eq!(verified.package_hash, "sha256:package");
        assert_eq!(verified.key_id, "release-2026");
    }

    #[test]
    fn a_p384_signed_attestation_verifies_too() {
        let signer = TestSigner::p384("release-2027");
        let attestation = attestation_for(GUEST);
        let signature = signer.sign(&attestation);

        verify_staged_image(&store_with(&signer), &initrd_around(GUEST), Some(&encoded(&attestation)), Some(&signature)).expect("verifies");
    }

    #[test]
    fn an_rsa_pss_signature_from_openssl_verifies_and_a_flipped_bit_does_not() {
        let mut store = TrustStore::new();
        store.add_pem("rsa-2026", RSA_PUBLIC_KEY_PEM).expect("trust the RSA key");
        let signature = SignatureDocument {
            algorithm: ALGORITHM_RSA_PSS_SHA256.to_string(),
            key_id: "rsa-2026".to_string(),
            value: RSA_SIGNATURE_BASE64.to_string(),
        };

        store.verify(RSA_MESSAGE, &signature).expect("the fixture verifies");

        let mut tampered = RSA_MESSAGE.to_vec();
        tampered[10] ^= 0x01;
        assert!(store.verify(&tampered, &signature).unwrap_err().contains("does not verify"));
    }

    #[test]
    fn a_binary_that_does_not_match_the_attestation_is_refused() {
        let signer = TestSigner::p256("release-2026");
        let attestation = attestation_for(GUEST);
        let signature = signer.sign(&attestation);

        let error = verify_staged_image(&store_with(&signer), &initrd_around(b"\x7fELF-swapped"), Some(&encoded(&attestation)), Some(&signature))
            .unwrap_err();

        assert!(error.contains("digests to") && error.contains("but the attestation names"), "{error}");
    }

    #[test]
    fn a_tampered_attestation_and_an_untrusted_key_and_a_mismatched_algorithm_are_refused() {
        let signer = TestSigner::p256("release-2026");
        let store = store_with(&signer);
        let attestation = attestation_for(GUEST);
        let signature = signer.sign(&attestation);
        let initrd = initrd_around(GUEST);

        let mut tampered = attestation.clone();
        tampered[5] ^= 0x01;
        let error = verify_staged_image(&store, &initrd, Some(&encoded(&tampered)), Some(&signature)).unwrap_err();
        assert!(error.contains("does not verify under key id 'release-2026'"), "{error}");

        let other = TestSigner::p256("release-2026");
        let forged = other.sign(&attestation);
        let error = verify_staged_image(&store, &initrd, Some(&encoded(&attestation)), Some(&forged)).unwrap_err();
        assert!(error.contains("does not verify under key id 'release-2026'"), "{error}");

        let unknown = SignatureDocument { key_id: "nobody".to_string(), ..signature.clone() };
        let error = verify_staged_image(&store, &initrd, Some(&encoded(&attestation)), Some(&unknown)).unwrap_err();
        assert!(error.contains("key id 'nobody', which this sidecar does not trust"), "{error}");

        let wrong_algorithm = SignatureDocument { algorithm: ALGORITHM_RSA_PSS_SHA256.to_string(), ..signature.clone() };
        let error = verify_staged_image(&store, &initrd, Some(&encoded(&attestation)), Some(&wrong_algorithm)).unwrap_err();
        assert!(error.contains("cannot be verified with key id 'release-2026', which is an ECDSA P-256 key"), "{error}");

        let unknown_algorithm = SignatureDocument { algorithm: "ed25519".to_string(), ..signature.clone() };
        let error = verify_staged_image(&store, &initrd, Some(&encoded(&attestation)), Some(&unknown_algorithm)).unwrap_err();
        assert!(error.contains("'ed25519' is not one this sidecar understands"), "{error}");

        let error = verify_staged_image(&store, &initrd, None, Some(&signature)).unwrap_err();
        assert!(error.contains("carries no attestation and signature"), "{error}");
    }

    #[test]
    fn an_attestation_of_another_format_version_is_refused_after_its_signature_verifies() {
        let signer = TestSigner::p256("release-2026");
        let attestation = attestation_for(GUEST).replace(b"\"formatVersion\":1", b"\"formatVersion\":2");
        let signature = signer.sign(&attestation);

        let error = verify_staged_image(&store_with(&signer), &initrd_around(GUEST), Some(&encoded(&attestation)), Some(&signature)).unwrap_err();

        assert!(error.contains("format version 2 is not the 1"), "{error}");
    }

    #[test]
    fn the_guest_binary_is_the_one_regular_file_in_the_archive() {
        assert_eq!(guest_binary_of(&initrd_around(GUEST)).unwrap(), GUEST);

        let mut two_files = Vec::new();
        newc_entry(&mut two_files, 1, "bin/guest", 0o100755, GUEST);
        newc_entry(&mut two_files, 2, "bin/other", 0o100755, b"other");
        newc_entry(&mut two_files, 0, "TRAILER!!!", 0, &[]);
        assert!(guest_binary_of(&two_files).unwrap_err().contains("more than one regular file"));

        let mut no_files = Vec::new();
        newc_entry(&mut no_files, 1, ".", 0o040755, &[]);
        newc_entry(&mut no_files, 0, "TRAILER!!!", 0, &[]);
        assert!(guest_binary_of(&no_files).unwrap_err().contains("no regular file"));

        assert!(guest_binary_of(b"070701-fake-initrd").unwrap_err().contains("truncated header"));
        assert!(guest_binary_of(&[b'x'; 120]).unwrap_err().contains("bad magic"));

        let mut no_trailer = Vec::new();
        newc_entry(&mut no_trailer, 1, "bin/guest", 0o100755, GUEST);
        assert!(guest_binary_of(&no_trailer).unwrap_err().contains("truncated header"));
    }

    #[test]
    fn the_trust_store_refuses_keys_that_are_not_spki_public_keys() {
        let mut store = TrustStore::new();
        let pem = TestSigner::p256("k").public_key_pem();

        let private = pem.replace("BEGIN PUBLIC KEY", "BEGIN EC PRIVATE KEY").replace("END PUBLIC KEY", "END EC PRIVATE KEY");
        assert!(store.add_pem("k", &private).unwrap_err().to_string().contains("expected a '-----BEGIN PUBLIC KEY-----' PEM block"));

        let garbage = "-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----";
        assert!(store.add_pem("k", garbage).is_err());

        assert!(store.add_pem(" ", &pem).unwrap_err().to_string().contains("non-empty key id"));
        assert!(store.is_empty());

        store.add_pem("k", &pem).expect("a real key is accepted");
        assert!(!store.is_empty());
    }

    trait ReplaceBytes {
        fn replace(&self, from: &[u8], to: &[u8]) -> Vec<u8>;
    }

    impl ReplaceBytes for Vec<u8> {
        fn replace(&self, from: &[u8], to: &[u8]) -> Vec<u8> {
            let position = self.windows(from.len()).position(|window| window == from).expect("pattern present");
            let mut out = self[..position].to_vec();
            out.extend_from_slice(to);
            out.extend_from_slice(&self[position + from.len()..]);
            out
        }
    }
}
