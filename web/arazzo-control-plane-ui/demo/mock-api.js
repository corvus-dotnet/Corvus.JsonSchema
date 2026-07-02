// In-memory mock of the Arazzo Control Plane API for the demo (and for tests).
//
// Returns a `fetch`-compatible function implementing all six operations with RFC 9457 errors and keyset
// pagination, so the kit is fully explorable with no server:
//
//   import { createMockControlPlane } from './mock-api.js';
//   import { ArazzoControlPlaneClient } from '../src/arazzo-client.js';
//   const mock = createMockControlPlane();
//   const client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });

import { unpackWorkflowPackage } from '../src/workflow-package.js';

const TERMINAL = new Set(['Completed', 'Cancelled']);
let etagSeq = 1000;

function nextEtag() {
  return `"etag-${++etagSeq}"`;
}

function iso(offsetMs) {
  return new Date(Date.now() + offsetMs).toISOString();
}

export function seedRuns() {
  const min = 60000;
  const hr = 60 * min;
  const day = 24 * hr;
  return [
    {
      id: 'run-7f3a9c21', workflowId: 'adopt-pet-v1', status: 'Faulted', cursor: 1,
      createdAt: iso(-3 * hr), updatedAt: iso(-2 * min), etag: nextEtag(),
      fault: { stepId: 'reservePayment', attempt: 3, error: 'HttpRequestException: 502 from payments (upstream)', at: iso(-2 * min) },
      _errorType: 'HttpRequestException',
      correlationId: '7f3a9c21d4e54a1b9c0d1e2f3a4b5c6d', environment: 'production', tags: ['tenant-42', 'priority'],
    },
    {
      // Faulted at submitAdoption — skip/state-patch outputs include nested 'fee' & 'adopter' objects and a 'documents' array.
      id: 'run-b2c3d4e5', workflowId: 'adopt-pet-v1', status: 'Faulted', cursor: 2,
      createdAt: iso(-5 * hr), updatedAt: iso(-7 * min), etag: nextEtag(),
      fault: { stepId: 'submitAdoption', attempt: 2, error: 'ValidationException: adopter.email missing from shelter record', at: iso(-7 * min) },
      _errorType: 'ValidationException',
      correlationId: 'b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7', tags: ['tenant-42'],
    },
    {
      // Faulted at flagDiscrepancies — outputs are an array of typed objects (account/delta/currency/severity/contact).
      id: 'run-c9d8e7f6', workflowId: 'nightly-reconcile-v3', status: 'Faulted', cursor: 3,
      createdAt: iso(-6 * hr), updatedAt: iso(-12 * min), etag: nextEtag(),
      fault: { stepId: 'flagDiscrepancies', attempt: 1, error: 'TimeoutException: ledger service did not respond within 30s', at: iso(-12 * min) },
      _errorType: 'TimeoutException',
      correlationId: 'c9d8e7f6a5b4c3d2e1f0a9b8c7d6e5f4', environment: 'production', tags: ['prod', 'billing'],
    },
    {
      // Faulted at verifyIdentity — outputs include a nested 'applicant' object, an enum 'method', and a 'flags' enum array.
      id: 'run-aa11bb22', workflowId: 'onboard-customer-v1', status: 'Faulted', cursor: 1,
      createdAt: iso(-100 * min), updatedAt: iso(-15 * min), etag: nextEtag(),
      fault: { stepId: 'verifyIdentity', attempt: 4, error: 'KycProviderException: document image unreadable', at: iso(-15 * min) },
      _errorType: 'KycProviderException',
      correlationId: 'aa11bb22cc33dd44ee55ff6677889900', environment: 'staging', tags: ['tenant-7', 'kyc'],
    },
    {
      // Faulted at provisionResources — outputs are an array of resource objects (kind/region/endpoint enums + uri).
      id: 'run-dd44ee55', workflowId: 'onboard-customer-v1', status: 'Faulted', cursor: 2,
      createdAt: iso(-70 * min), updatedAt: iso(-3 * min), etag: nextEtag(),
      fault: { stepId: 'provisionResources', attempt: 2, error: 'QuotaExceededException: region eu-west-1 database quota reached', at: iso(-3 * min) },
      _errorType: 'QuotaExceededException',
      correlationId: 'dd44ee55ff66aa77bb88cc99dd00ee11', tags: ['tenant-7'],
    },
    {
      id: 'run-1b88de40', workflowId: 'adopt-pet-v1', status: 'Suspended', cursor: 4,
      createdAt: iso(-90 * min), updatedAt: iso(-30 * min), etag: nextEtag(),
      wait: { kind: 'Timer', dueAt: iso(45 * min) },
      correlationId: '1b88de40a1b2c3d4e5f60718293a4b5c', tags: ['tenant-42'],
    },
    {
      id: 'run-9c0142ab', workflowId: 'onboard-customer-v1', status: 'Suspended', cursor: 1,
      createdAt: iso(-5 * hr), updatedAt: iso(-4 * hr), etag: nextEtag(),
      wait: { kind: 'Message', channel: 'kyc.results', correlationId: 'cust-55021' },
      correlationId: '9c0142ab5d6e7f80911a2b3c4d5e6f70', tags: ['tenant-7'],
    },
    {
      id: 'run-33aa71f9', workflowId: 'onboard-customer-v1', status: 'Running', cursor: 3,
      createdAt: iso(-8 * min), updatedAt: iso(-10000), etag: nextEtag(),
      correlationId: '33aa71f9b5c6d7e8f90a1b2c3d4e5f60', tags: ['tenant-7'],
    },
    {
      id: 'run-0a5512cd', workflowId: 'adopt-pet-v1', status: 'Completed', cursor: 6,
      createdAt: iso(-2 * day), updatedAt: iso(-2 * day + 5 * min), etag: nextEtag(),
      correlationId: '0a5512cd6e7f8a9b0c1d2e3f4a5b6c7d', tags: ['tenant-42'],
    },
    {
      id: 'run-44b0e7e2', workflowId: 'nightly-reconcile-v2', status: 'Completed', cursor: 9,
      createdAt: iso(-9 * day), updatedAt: iso(-9 * day + 2 * min), etag: nextEtag(),
      correlationId: '44b0e7e2c3d4e5f6a7b8c9d0e1f20314',
    },
    {
      id: 'run-6610ffac', workflowId: 'onboard-customer-v1', status: 'Cancelled', cursor: 2,
      createdAt: iso(-40 * day), updatedAt: iso(-40 * day + min), etag: nextEtag(),
      correlationId: '6610ffac1726354455647382910a0b0c',
    },
    {
      id: 'run-2d77b410', workflowId: 'nightly-reconcile-v3', status: 'Pending', cursor: 0,
      createdAt: iso(-20000), updatedAt: iso(-20000), etag: nextEtag(),
      correlationId: '2d77b410aabbccddeeff00112233445566',
    },
  ];
}

// Reach demo (§14.2): each demo workflow is seeded with a REAL, non-internal security tag ({ key, value }, matching the
// CatalogSecurityTag contract) — not a computed fiction. A catalog version stores its own tags (settable at add,
// re-taggable via PATCH), a run inherits its workflow's, and a reach-scoped persona (e.g. the payments team) reads only
// rows whose tags its reach admits.
const WORKFLOW_SECURITY_TAGS = {
  'adopt-pet': [{ key: 'domain', value: 'pets' }],
  'nightly-reconcile': [{ key: 'domain', value: 'payments' }],
  'onboard-customer': [{ key: 'domain', value: 'identity' }],
};
const baseWorkflowOf = (workflowId) => (workflowId || '').replace(/-v\d+$/, '');
const securityTagsForBase = (baseWorkflowId) => WORKFLOW_SECURITY_TAGS[baseWorkflowId] ?? [{ key: 'domain', value: 'other' }];
// The reserved internal-tag prefix (§14.2) — deployment-owned; user-supplied tags using it are rejected (400).
const RESERVED_TAG_PREFIX = 'sys:';
const usesReservedTag = (tags) => (tags || []).some((t) => (t.key || '').startsWith(RESERVED_TAG_PREFIX));

function toSummary(run) {
  return {
    id: run.id,
    workflowId: run.workflowId,
    status: run.status,
    createdAt: run.createdAt,
    updatedAt: run.updatedAt,
    dueAt: run.wait?.kind === 'Timer' ? run.wait.dueAt : null,
    awaitingChannel: run.wait?.kind === 'Message' ? run.wait.channel : null,
    awaitingCorrelationId: run.wait?.kind === 'Message' ? (run.wait.correlationId ?? null) : null,
    errorType: run.status === 'Faulted' ? (run._errorType ?? 'Error') : null,
    correlationId: run.correlationId ?? null,
    environment: run.environment ?? null,
    securityTags: securityTagsForBase(baseWorkflowOf(run.workflowId)),
    tags: run.tags ?? [],
  };
}

function toDetail(run) {
  return {
    id: run.id,
    workflowId: run.workflowId,
    status: run.status,
    cursor: run.cursor,
    createdAt: run.createdAt,
    wait: run.wait ?? null,
    fault: run.fault ?? null,
    etag: run.etag,
    correlationId: run.correlationId ?? null,
    environment: run.environment ?? null,
    securityTags: securityTagsForBase(baseWorkflowOf(run.workflowId)),
    tags: run.tags ?? [],
  };
}

const STEP_SETS = {
  'adopt-pet': ['findPet', 'reservePayment', 'submitAdoption', 'confirmAdoption'],
  'nightly-reconcile': ['loadLedger', 'fetchTransactions', 'matchEntries', 'flagDiscrepancies', 'postCorrections', 'publishReport'],
  'onboard-customer': ['createAccount', 'verifyIdentity', 'provisionResources', 'sendWelcome'],
};

// Typed outputs per step (a TypeDescriptor each) — the precomputed metadata the typed patch builder reads.
// Several steps carry deliberately rich shapes (nested objects, arrays of objects, enums, and a spread of
// string/number formats + constraints) so the typed patch builder can be exercised across all its controls.
const STEP_OUTPUTS = {
  'adopt-pet': {
    findPet: { petId: { type: 'integer', format: 'int64' }, available: { type: 'boolean' } },
    reservePayment: {
      paymentId: { type: 'string', format: 'uuid' },
      amount: { type: 'number', minimum: 0 },
      status: { type: 'string', enum: ['pending', 'settled', 'failed'] },
    },
    submitAdoption: {
      adoptionId: { type: 'string', format: 'uuid' },
      confirmedAt: { type: 'string', format: 'date-time' },
      fee: {
        type: 'object', description: 'The adoption fee charged.',
        properties: {
          amount: { type: 'number', minimum: 0, multipleOf: 0.01 },
          currency: { type: 'string', enum: ['GBP', 'USD', 'EUR'] },
        },
        required: ['amount', 'currency'],
      },
      adopter: {
        type: 'object', description: 'The adopting party.',
        properties: {
          name: { type: 'string', maxLength: 80 },
          email: { type: 'string', format: 'email' },
          phone: { type: 'string', pattern: '^[+0-9 ()-]{7,}$', description: 'Digits, spaces and + ( ) - only.' },
        },
        required: ['name', 'email'],
      },
      documents: { type: 'array', description: 'Signed paperwork.', items: { type: 'string', format: 'uri' } },
    },
  },
  'nightly-reconcile': {
    fetchTransactions: { count: { type: 'integer' }, cursor: { type: 'string' } },
    matchEntries: { matched: { type: 'integer' }, unmatched: { type: 'integer' } },
    flagDiscrepancies: {
      discrepancies: {
        type: 'array', description: 'Accounts whose ledger and bank balances disagree.',
        items: {
          type: 'object',
          properties: {
            account: { type: 'string', pattern: '^[0-9]{8}$', description: 'Eight-digit account number.' },
            delta: { type: 'number', description: 'Signed difference (bank − ledger).' },
            currency: { type: 'string', enum: ['GBP', 'USD', 'EUR'] },
            severity: { type: 'string', enum: ['info', 'warning', 'critical'] },
            firstSeen: { type: 'string', format: 'date' },
            contact: {
              type: 'object',
              properties: {
                email: { type: 'string', format: 'email' },
                runbook: { type: 'string', format: 'uri' },
              },
            },
          },
          required: ['account', 'delta'],
        },
      },
      totalDelta: { type: 'number' },
      reportUrl: { type: 'string', format: 'uri' },
      // A tuple (prefixItems) — drives the patch builder's fixed positional slots.
      range: {
        type: 'array', description: 'The [from, to] transaction sequence numbers scanned.',
        prefixItems: [
          { type: 'integer', minimum: 0, title: 'from' },
          { type: 'integer', minimum: 0, title: 'to' },
        ],
      },
    },
  },
  'onboard-customer': {
    verifyIdentity: {
      verified: { type: 'boolean' },
      score: { type: 'number', minimum: 0, maximum: 1, description: 'Match confidence (0–1).' },
      method: { type: 'string', enum: ['document', 'biometric', 'knowledge-based'] },
      reviewedAt: { type: 'string', format: 'date-time' },
      applicant: {
        type: 'object', description: 'The resolved identity.',
        properties: {
          fullName: { type: 'string', maxLength: 120 },
          dateOfBirth: { type: 'string', format: 'date' },
          email: { type: 'string', format: 'idn-email' },
          country: { type: 'string', pattern: '^[A-Z]{2}$', description: 'ISO 3166-1 alpha-2.' },
        },
        required: ['fullName'],
      },
      flags: { type: 'array', description: 'Screening hits, if any.', items: { type: 'string', enum: ['pep', 'sanctions', 'adverse-media'] } },
      // A polymorphic union (oneOf with a discriminator) — drives the patch builder's variant picker.
      evidence: {
        type: 'union', description: 'The evidence that established the identity.', discriminator: 'kind',
        variants: [
          {
            type: 'object', title: 'Document',
            properties: {
              kind: { type: 'string', const: 'document' },
              documentType: { type: 'string', enum: ['passport', 'driving-licence', 'national-id'] },
              documentNumber: { type: 'string', maxLength: 40 },
              expiry: { type: 'string', format: 'date' },
            },
            required: ['kind', 'documentType', 'documentNumber'],
          },
          {
            type: 'object', title: 'Biometric',
            properties: {
              kind: { type: 'string', const: 'biometric' },
              modality: { type: 'string', enum: ['face', 'fingerprint', 'voice'] },
              confidence: { type: 'number', minimum: 0, maximum: 1 },
            },
            required: ['kind', 'modality', 'confidence'],
          },
          {
            type: 'object', title: 'Knowledge-based',
            properties: {
              kind: { type: 'string', const: 'knowledge-based' },
              questionsPassed: { type: 'integer', minimum: 0, maximum: 5 },
            },
            required: ['kind', 'questionsPassed'],
          },
        ],
      },
    },
    provisionResources: {
      accountUrl: { type: 'string', format: 'uri' },
      quotaGb: { type: 'integer', minimum: 1, maximum: 1024 },
      resources: {
        type: 'array', description: 'The provisioned resources.',
        items: {
          type: 'object',
          properties: {
            kind: { type: 'string', enum: ['database', 'bucket', 'queue'] },
            name: { type: 'string' },
            region: { type: 'string', enum: ['eu-west-1', 'us-east-1', 'ap-southeast-2'] },
            endpoint: { type: 'string', format: 'uri' },
          },
          required: ['kind', 'name'],
        },
      },
      // A free-form map (additionalProperties) — drives the patch builder's key/value editor.
      tags: { type: 'object', description: 'Arbitrary resource tags (key → value).', additionalProperties: { type: 'string' } },
    },
  },
};

function schemasFor(v) {
  const base = v.baseWorkflowId;
  const stepIds = STEP_SETS[base] || [];
  const stepOutputs = STEP_OUTPUTS[base] || {};
  return {
    formatVersion: 1,
    workflows: {
      [v.workflowId]: {
        inputs: (v._workflow?.workflows?.[0]?.inputs) || { type: 'object', properties: {} },
        steps: Object.fromEntries(stepIds.map((stepId) => [stepId, { outputs: stepOutputs[stepId] || {} }])),
      },
    },
  };
}

// A small stand-in for the server's true JSON Schema validation. The real server resolves the actual schema
// from the package and runs Corvus.Text.Json.Validator; the mock validates against the precomputed descriptor
// metadata (inputs + step outputs) so the demo behaves end to end without a server.
function validateValue(v, body) {
  const target = body?.target || {};
  const value = body?.value;
  const wfId = target.workflowId || v.workflowId;
  const meta = schemasFor(v).workflows[wfId];
  let descriptor = null;
  if (target.kind === 'inputs') {
    descriptor = meta?.inputs;
  } else if (target.kind === 'stepOutputs' && target.stepId) {
    const outputs = meta?.steps?.[target.stepId]?.outputs;
    if (outputs) descriptor = { type: 'object', properties: outputs };
  }
  // requestBody/responseBody aren't in the mock's precomputed metadata — treat as unconstrained.
  if (!descriptor) return { valid: true, errors: [] };
  const errors = [];
  validateNode(descriptor, value, '', errors);
  return { valid: errors.length === 0, errors };
}

function validateNode(d, value, path, errors) {
  if (!d || typeof d !== 'object') return;
  const add = (message) => errors.push({ instancePath: path || '/', message });

  if (Array.isArray(d.variants)) {
    // Validate against the variant the value claims to be (via the discriminator), or the closest match,
    // so the operator sees that variant's actual field errors rather than a blanket "no match".
    const variant = pickVariant(d, value);
    if (variant) validateNode(variant, value, path, errors);
    else if (d.variants.length) add('does not match any allowed type');
    return;
  }
  if (value === undefined || value === null) return; // presence handled by the parent's `required`
  if (d.const !== undefined) {
    if (value !== d.const) add(`must be ${JSON.stringify(d.const)}`);
    return;
  }
  if (Array.isArray(d.enum)) {
    if (!d.enum.some((e) => e === value)) add(`must be one of: ${d.enum.join(', ')}`);
    return;
  }
  switch (d.type) {
    case 'object': {
      if (typeof value !== 'object' || Array.isArray(value)) { add('must be an object'); return; }
      const props = d.properties || {};
      for (const name of (Array.isArray(d.required) ? d.required : [])) {
        if (value[name] === undefined) errors.push({ instancePath: `${path}/${name}`, message: `"${name}" is required` });
      }
      for (const [name, child] of Object.entries(props)) {
        if (value[name] !== undefined) validateNode(child, value[name], `${path}/${name}`, errors);
      }
      // Free-form map: keys not covered by `properties` are validated against `additionalProperties`.
      if (d.additionalProperties && typeof d.additionalProperties === 'object') {
        for (const name of Object.keys(value)) {
          if (!(name in props)) validateNode(d.additionalProperties, value[name], `${path}/${name}`, errors);
        }
      }
      break;
    }
    case 'array': {
      if (!Array.isArray(value)) { add('must be an array'); return; }
      if (Array.isArray(d.prefixItems)) {
        // Tuple: each position has its own schema; any tail items fall back to `items`.
        value.forEach((item, i) => {
          const schema = d.prefixItems[i] ?? d.items;
          if (schema) validateNode(schema, item, `${path}/${i}`, errors);
        });
      } else if (d.items) {
        value.forEach((item, i) => validateNode(d.items, item, `${path}/${i}`, errors));
      }
      break;
    }
    case 'integer':
      if (typeof value !== 'number' || !Number.isInteger(value)) { add('must be an integer'); return; }
      checkNumber(d, value, add);
      break;
    case 'number':
      if (typeof value !== 'number') { add('must be a number'); return; }
      checkNumber(d, value, add);
      break;
    case 'boolean':
      if (typeof value !== 'boolean') add('must be a boolean');
      break;
    case 'string':
      if (typeof value !== 'string') { add('must be a string'); return; }
      if (d.minLength != null && value.length < d.minLength) add(`must be at least ${d.minLength} characters`);
      if (d.maxLength != null && value.length > d.maxLength) add(`must be at most ${d.maxLength} characters`);
      if (d.pattern && !new RegExp(d.pattern).test(value)) add(`must match ${d.pattern}`);
      break;
    default:
      break;
  }
}

function checkNumber(d, value, add) {
  if (d.minimum != null && value < d.minimum) add(`must be >= ${d.minimum}`);
  if (d.maximum != null && value > d.maximum) add(`must be <= ${d.maximum}`);
  if (d.multipleOf != null && d.multipleOf > 0 && Math.abs(value % d.multipleOf) > 1e-9) add(`must be a multiple of ${d.multipleOf}`);
}

/** The number of validation errors a node would produce for a value (used to rank union variants). */
function validateCount(d, value) {
  const errors = [];
  validateNode(d, value, '', errors);
  return errors.length;
}

/**
 * Choose the union variant to validate `value` against: the one named by the discriminator when it matches,
 * else the closest fit (fewest errors). Returns null only when there are no variants.
 */
function pickVariant(d, value) {
  const variants = d.variants || [];
  if (d.discriminator && value && typeof value === 'object') {
    const tag = value[d.discriminator];
    const byTag = variants.find((v) => v?.properties?.[d.discriminator]?.const === tag);
    if (byTag) return byTag;
  }
  let best = null;
  let bestCount = Infinity;
  for (const v of variants) {
    const count = validateCount(v, value);
    if (count === 0) return v;
    if (count < bestCount) { best = v; bestCount = count; }
  }
  return best;
}

function workflowDoc(workflowId, title, description, sourceRefs) {
  const base = workflowId.replace(/-v\d+$/, '');
  const stepIds = STEP_SETS[base] || ['start', 'process', 'finish'];
  return {
    arazzo: '1.1.0',
    info: { title, description },
    sourceDescriptions: (sourceRefs ?? []).map((s) => ({ name: s.name, url: `./${s.name}.json`, type: s.type })),
    workflows: [{ workflowId, steps: stepIds.map((stepId) => ({ stepId, operationId: stepId })) }],
  };
}

// The OpenAPI/AsyncAPI documents the demo's sources resolve to. Real security schemes so the credential dialog can
// DERIVE the auth kind + config from the source document (petstore: an OpenAPI apiKey in a header; events: an
// AsyncAPI oauth2 client-credentials flow). These back BOTH the per-version embedded sources and the §7.6 registry.
const SOURCE_DOCS = {
  petstore: {
    openapi: '3.1.0', info: { title: 'Petstore', version: '1.0.0' },
    servers: [{ url: 'https://petstore.example.com/v1' }],
    components: { securitySchemes: { apiKey: { type: 'apiKey', name: 'X-Api-Key', in: 'header' } } },
  },
  events: {
    asyncapi: '3.0.0', info: { title: 'Events', version: '1.0.0' },
    servers: { production: { host: 'events.example.com', protocol: 'kafka' } },
    components: { securitySchemes: { tokenAuth: { type: 'oauth2', flows: { clientCredentials: { tokenUrl: 'https://idp.example.com/oauth/token', scopes: { 'events:read': 'Read events' } } } } } },
  },
  billing: {
    openapi: '3.1.0', info: { title: 'Billing', version: '1.0.0' },
    servers: [{ url: 'https://billing.example.com' }],
    components: { securitySchemes: { clientCreds: { type: 'oauth2', flows: { clientCredentials: { tokenUrl: 'https://idp.example.com/oauth/token', scopes: { 'ledger:read': 'Read the ledger' } } } } } },
  },
};
const SOURCE_TYPES = { petstore: 'openapi', events: 'asyncapi', billing: 'openapi' };

// ---- source registry (§7.6) — first-class, reach-scoped registered sources, referenced by name ------------------
// A source is registered once ({ name, type, document } + per-environment credentials); workflows reference it by
// name. Add-workflow resolves an already-registered source (no re-upload) and registers genuinely new ones.
function seedSources() {
  const day = 24 * 60 * 60000;
  return Object.keys(SOURCE_TYPES).map((name) => ({
    name, type: SOURCE_TYPES[name], document: structuredClone(SOURCE_DOCS[name]),
    displayName: SOURCE_DOCS[name].info?.title || name, managementTags: [],
    createdBy: 'alice@example.com', createdAt: iso(-40 * day), etag: nextEtag(),
  }));
}

function seedCatalog() {
  const hr = 60 * 60000;
  const day = 24 * hr;
  const sources = SOURCE_DOCS;
  // Each workflow references only the sources it actually uses (design §7.6/§7.7). A version is "ready" in an environment
  // only where EVERY source it references has a credential there, so these refs are chosen to line up with seedCredentials:
  // adopt-pet→petstore (credentialed in production), nightly-reconcile→billing (production), onboard-customer→events
  // (staging). That makes each workflow promotable in exactly one environment and gives the readiness gate something to bite.
  const refsByBase = {
    'adopt-pet': [{ name: 'petstore', type: 'openapi' }],
    'nightly-reconcile': [{ name: 'billing', type: 'openapi' }],
    'onboard-customer': [{ name: 'events', type: 'asyncapi' }],
  };
  const refsFor = (base) => refsByBase[base] ?? [{ name: 'petstore', type: 'openapi' }];
  const teamA = { name: 'Reconciliation Team', email: 'reconcile@example.com', team: 'Platform', url: 'https://runbooks.example.com/nightly-reconcile' };
  const teamB = { name: 'Onboarding Team', email: 'onboarding@example.com' };
  const v = (base, n, status, title, owner, tags, ageDays, extra = {}) => ({
    baseWorkflowId: base, versionNumber: n, workflowId: `${base}-v${n}`,
    title, description: `${title} — versioned workflow.`, status, tags, owner, sources: refsFor(base),
    securityTags: securityTagsForBase(base),
    hash: `${base}${n}`.padEnd(64, '0'),
    createdBy: 'alice@example.com', createdAt: iso(-ageDays * day),
    _workflow: workflowDoc(`${base}-v${n}`, title, `${title} — versioned workflow.`, refsFor(base)),
    _sources: sources,
    ...extra,
  });
  return [
    v('nightly-reconcile', 1, 'Obsolete', 'Nightly Reconcile', teamA, ['prod', 'billing'], 30, { obsoletedBy: 'alice@example.com', obsoletedAt: iso(-10 * day), lastUpdatedBy: 'alice@example.com', lastUpdatedAt: iso(-10 * day) }),
    v('nightly-reconcile', 2, 'Active', 'Nightly Reconcile', teamA, ['prod', 'billing'], 10),
    v('nightly-reconcile', 3, 'Active', 'Nightly Reconcile', teamA, ['prod', 'billing', 'beta'], 1),
    v('adopt-pet', 1, 'Active', 'Adopt a Pet', teamB, ['prod'], 5),
    v('onboard-customer', 1, 'Active', 'Onboard Customer', teamB, ['prod', 'kyc'], 7),
  ];
}

function toCatalogSummary(v) {
  return {
    baseWorkflowId: v.baseWorkflowId,
    versionNumber: v.versionNumber,
    workflowId: v.workflowId,
    title: v.title,
    description: v.description,
    status: v.status,
    tags: v.tags ?? [],
    owner: v.owner,
    sources: v.sources ?? [],
    hash: v.hash,
    createdBy: v.createdBy,
    createdAt: v.createdAt,
    lastUpdatedBy: v.lastUpdatedBy ?? undefined,
    lastUpdatedAt: v.lastUpdatedAt ?? undefined,
    obsoletedBy: v.obsoletedBy ?? undefined,
    obsoletedAt: v.obsoletedAt ?? undefined,
    securityTags: v.securityTags ?? securityTagsForBase(v.baseWorkflowId),
  };
}

// ---- source credentials (§13) — references + non-secret metadata only -------------------------

const SECRET_REF = /^(keyvault|awssm|vault|env|file):\/\/.+/;
const EXPIRING_WINDOW_MS = 7 * 24 * 60 * 60000;

function isSecretRef(ref) {
  return typeof ref === 'string' && SECRET_REF.test(ref);
}

function credentialStatus(expiresAt) {
  if (!expiresAt) return 'valid';
  const ms = Date.parse(expiresAt) - Date.now();
  if (Number.isNaN(ms)) return 'valid';
  if (ms <= 0) return 'expired';
  if (ms <= EXPIRING_WINDOW_MS) return 'expiringSoon';
  return 'valid';
}

function seedCredentials() {
  const day = 24 * 60 * 60000;
  const b = (sourceName, environment, authKind, secretRefs, extra = {}) => ({
    id: `cred-${sourceName}-${environment}`,
    sourceName, environment, authKind, secretRefs,
    config: extra.config ?? [],
    managementTags: extra.managementTags ?? [],
    usageGrantee: extra.usageGrantee,
    description: extra.description,
    expiresAt: extra.expiresAt,
    rotatedAt: extra.rotatedAt,
    createdBy: 'alice@example.com', createdAt: iso(-30 * day), etag: nextEtag(),
  });
  return [
    b('petstore', 'production', 'apiKey', [{ name: 'value', ref: 'keyvault://petstore-key#3' }], { config: [{ key: 'parameterName', value: 'X-Api-Key' }], expiresAt: iso(20 * day), description: 'Petstore API key.' }),
    b('billing', 'production', 'oauth2ClientCredentials', [{ name: 'clientSecret', ref: 'vault://kv/billing#secret' }], { config: [{ key: 'tokenUrl', value: 'https://idp.example.com/oauth/token' }, { key: 'clientId', value: 'billing-client' }], usageGrantee: { identity: [{ dimension: 'workflow', value: 'nightly-reconcile' }], kind: 'workflow', label: 'nightly-reconcile' }, expiresAt: iso(3 * day) }),
    b('legacy', 'production', 'basic', [{ name: 'password', ref: 'env://LEGACY_PW' }], { config: [{ key: 'username', value: 'svc-legacy' }], expiresAt: iso(-2 * day) }),
    b('events', 'staging', 'bearer', [{ name: 'value', ref: 'awssm://events-token' }], {}),
  ];
}

function toCredentialSummary(b) {
  return {
    id: b.id,
    sourceName: b.sourceName,
    environment: b.environment,
    authKind: b.authKind,
    secretRefs: b.secretRefs,
    config: b.config ?? [],
    managementTags: b.managementTags ?? [],
    usageGrantee: b.usageGrantee ?? undefined,
    description: b.description ?? undefined,
    expiresAt: b.expiresAt ?? undefined,
    rotatedAt: b.rotatedAt ?? undefined,
    credentialStatus: credentialStatus(b.expiresAt),
    createdBy: b.createdBy,
    createdAt: b.createdAt,
    lastUpdatedBy: b.lastUpdatedBy ?? undefined,
    lastUpdatedAt: b.lastUpdatedAt ?? undefined,
    etag: b.etag,
  };
}

// ---- deployment environments (§7.7) — first-class, governed, reach-scoped resources -----------
// The environments the seeded credentials reference (production, staging), so the readiness gate and the
// promotion dialog's environment picker have a real set to work over.

function seedEnvironments() {
  const day = 24 * 60 * 60000;
  const e = (name, displayName, description) => ({
    name, displayName, description, managementTags: [],
    createdBy: 'alice@example.com', createdAt: iso(-40 * day), etag: nextEtag(),
  });
  return [
    e('production', 'Production', 'The live production environment.'),
    e('staging', 'Staging', 'Pre-production staging environment.'),
  ];
}

// ---- workflow administration (§15) — deployment-mapped {dimension,value} identities ------------

function seedAdministrators() {
  // Keyed by baseWorkflowId; each administrator is a resolved identity grant
  // { digest, identity: [{dimension,value}], kind?, label? } — the digest is the stable removal key.
  // alice@ops (the platform admin persona) administers every seeded workflow; omar@ops additionally administers
  // onboard-customer — so the approver inbox differs per persona (§15). The team grants remain as extra admins.
  return {
    'adopt-pet': [adminGrant([{ dimension: 'sys:sub', value: 'alice@ops' }], 'person', 'alice@ops')],
    'nightly-reconcile': [
      adminGrant([{ dimension: 'tenant', value: 'platform' }], 'team', 'Platform'),
      adminGrant([{ dimension: 'sys:sub', value: 'alice@ops' }], 'person', 'alice@ops'),
    ],
    'onboard-customer': [
      adminGrant([{ dimension: 'tenant', value: 'platform' }], 'team', 'Platform'),
      adminGrant([{ dimension: 'tenant', value: 'growth' }], 'team', 'Growth'),
      adminGrant([{ dimension: 'sys:sub', value: 'alice@ops' }], 'person', 'alice@ops'),
      adminGrant([{ dimension: 'sys:sub', value: 'omar@ops' }], 'person', 'omar@ops'),
    ],
  };
}

// Environment administration (§7.7) — the same resolved-identity set as the workflow administrator set (§15), keyed by
// environment name. alice@ops administers both seeded environments; omar@ops additionally administers staging — so the
// promotion/runner-authorization inboxes differ per persona, alongside the platform team.
function seedEnvironmentAdministrators() {
  return {
    production: [
      adminGrant([{ dimension: 'sys:sub', value: 'alice@ops' }], 'person', 'alice@ops'),
      adminGrant([{ dimension: 'tenant', value: 'platform' }], 'team', 'Platform'),
    ],
    staging: [
      adminGrant([{ dimension: 'sys:sub', value: 'alice@ops' }], 'person', 'alice@ops'),
      adminGrant([{ dimension: 'sys:sub', value: 'omar@ops' }], 'person', 'omar@ops'),
    ],
  };
}

// The well-known grantee kind a single-grant dimension is inferred as (mirrors the server's TryGranteeKindForDimension);
// a custom dimension names no well-known kind.
function kindForDimension(dimension) {
  return { sub: 'person', tenant: 'team', role: 'role', workflow: 'workflow' }[dimension];
}

// A stable, order-independent opaque digest of an identity's {dimension,value} grants. NOT the server's SHA-256 — the
// mock is a standalone backend, and a caller only ever feeds back the digest the list/add response handed it — but it is
// stable, set-independent and 64 hex chars, so the picker/remove round-trip behaves like the real one.
function digestOf(identity) {
  const canonical = identity.map((g) => `${g.dimension}=${g.value}`).sort().join('\u0000');
  let out = '';
  for (let seed = 0; seed < 8; seed++) {
    let h = (0x811c9dc5 ^ Math.imul(seed, 0x9e3779b1)) >>> 0;
    for (let i = 0; i < canonical.length; i++) {
      h = Math.imul(h ^ canonical.charCodeAt(i), 0x01000193) >>> 0;
    }
    out += h.toString(16).padStart(8, '0');
  }
  return out;
}

// Builds an administrator grant from its resolved identity, stamping the stable digest and the optional kind/label.
export function adminGrant(identity, kind, label) {
  const normalized = identity.map((g) => ({ dimension: g.dimension, value: g.value }));
  const grant = { digest: digestOf(normalized), identity: normalized };
  if (kind) grant.kind = kind;
  if (label) grant.label = label;
  return grant;
}

function seedGrantees() {
  // Resolvable grantees as the server's GET /identity/grantees returns them: a well-known kind, a value/label,
  // the exact sys: identity as a {dimension,value} array, where it was resolved (observed/directory), and whether
  // that identity is the principal's *complete* stamped identity. The picker resolves these — no hand-typed tuples.
  return [
    { kind: 'person', value: 'u-1042', label: 'Ada Lovelace', identity: [{ dimension: 'sys:iss', value: 'https://idp.example.com' }, { dimension: 'sys:sub', value: 'u-1042' }], source: 'directory', complete: true },
    { kind: 'person', value: 'u-2099', label: 'Grace Hopper', identity: [{ dimension: 'sys:sub', value: 'u-2099' }], source: 'observed', complete: false },
    { kind: 'team', value: 'payments', label: 'Payments', identity: [{ dimension: 'team', value: 'payments' }], source: 'directory', complete: true },
    { kind: 'role', value: 'sre', label: 'Site Reliability', identity: [{ dimension: 'role', value: 'sre' }], source: 'directory', complete: true },
    { kind: 'workflow', value: 'onboard-customer', label: 'onboard-customer', identity: [{ dimension: 'workflow', value: 'onboard-customer' }], source: 'observed', complete: true },
    { kind: 'tenant', value: 'platform', label: 'platform', identity: [{ dimension: 'tenant', value: 'platform' }], source: 'observed', complete: true },
  ];
}

function seedAccessRequests() {
  const hr = 60 * 60 * 1000;
  return [
    {
      id: 'req-2001', baseWorkflowId: 'onboard-customer', requestedScopes: ['runs:write'],
      subjectClaimType: 'preferred_username', subjectClaimValue: 'alice', requesterLabel: 'alice',
      reason: 'On-call: need to retry a faulted onboarding.', requestedDurationSeconds: 4 * 3600,
      status: 'Pending', createdBy: 'alice', createdAt: iso(-2 * hr), etag: nextEtag(),
    },
    {
      id: 'req-2002', baseWorkflowId: 'nightly-reconcile', requestedScopes: ['runs:write'],
      subjectClaimType: 'preferred_username', subjectClaimValue: 'bob', requesterLabel: 'bob',
      reason: 'Re-run the overnight reconcile after the ledger fix.',
      status: 'Approved', createdBy: 'bob', createdAt: iso(-26 * hr),
      decidedBy: 'boss', decidedAt: iso(-25 * hr), grantedBindingId: 'bind-9001', grantedUntil: iso(6 * hr),
      etag: nextEtag(),
    },
    {
      id: 'req-2003', baseWorkflowId: 'onboard-customer', requestedScopes: ['runs:write'],
      subjectClaimType: 'preferred_username', subjectClaimValue: 'carol', requesterLabel: 'carol',
      reason: 'Investigating a stuck run.', status: 'Denied', createdBy: 'carol', createdAt: iso(-50 * hr),
      decidedBy: 'boss', decidedAt: iso(-49 * hr), decisionReason: 'Use the shared service account for read-only triage.',
      etag: nextEtag(),
    },
    {
      // A §17.3 "view" grant — the least-privilege catalog:read access an auditor requests to see one workflow without
      // joining its domain or operating it; pending so the approver inbox demonstrates approving a view grant.
      id: 'req-2004', baseWorkflowId: 'nightly-reconcile', requestedScopes: ['catalog:read'],
      subjectClaimType: 'preferred_username', subjectClaimValue: 'dave', requesterLabel: 'dave',
      reason: 'Auditor: read-only view of the reconcile workflow for the quarterly review.',
      status: 'Pending', createdBy: 'dave', createdAt: iso(-1 * hr), etag: nextEtag(),
    },
  ];
}

// The availability MATRIX (§7.8) — which (workflow version) is made available in which environment. Seeded so the
// catalog detail's "Available in" shows something; approving a promotion request adds to it.
function seedAvailabilityEntries() {
  const day = 24 * 60 * 60000;
  const e = (baseWorkflowId, versionNumber, environment) => ({
    baseWorkflowId, versionNumber, environment,
    createdBy: 'alice@example.com', createdAt: iso(-5 * day), etag: nextEtag(),
  });
  return [
    e('adopt-pet', 1, 'production'),       // petstore (shared) is credentialed in production → ready + made available
    e('nightly-reconcile', 3, 'production'), // billing (scoped to nightly-reconcile) is credentialed in production
  ];
}

function seedAvailabilityRequests() {
  const hr = 60 * 60 * 1000;
  // Cross-environment "promotion" requests (§7.8) that line up with the source/credential matrix so the readiness gate is
  // visible. scope=mine → the demo user's own (areq-3004); scope=queue → the approver inbox (all, the demo user administers
  // every environment in the mock); environment → that environment's queue. Readiness (a version is promotable only where
  // each source it references is credentialed) is enforced on approve:
  //   • onboard-customer→production (areq-3001) is NOT ready — its 'events' source has no production credential → Approve 409.
  //   • adopt-pet→production (areq-3002) IS ready — 'petstore' is credentialed in production → Approve makes it available.
  //   • onboard-customer→staging (areq-3003) IS ready — 'events' is credentialed in staging.
  return [
    {
      id: 'areq-3001', baseWorkflowId: 'onboard-customer', versionNumber: 1, environment: 'production',
      reason: 'Promote onboarding to production for the new tenant launch.',
      status: 'Pending', subjectClaimType: 'preferred_username', subjectClaimValue: 'alice', requesterLabel: 'alice',
      createdBy: 'alice', createdAt: iso(-3 * hr), etag: nextEtag(),
    },
    {
      id: 'areq-3002', baseWorkflowId: 'adopt-pet', versionNumber: 1, environment: 'production',
      reason: 'Adopt-a-pet is signed off — make it available in production.',
      status: 'Pending', subjectClaimType: 'preferred_username', subjectClaimValue: 'bob', requesterLabel: 'bob',
      createdBy: 'bob', createdAt: iso(-5 * hr), etag: nextEtag(),
    },
    {
      id: 'areq-3003', baseWorkflowId: 'onboard-customer', versionNumber: 1, environment: 'staging',
      reason: 'Need onboarding in staging for the integration test.',
      status: 'Pending', subjectClaimType: 'preferred_username', subjectClaimValue: 'carol', requesterLabel: 'carol',
      createdBy: 'carol', createdAt: iso(-8 * hr), etag: nextEtag(),
    },
    {
      id: 'areq-3004', baseWorkflowId: 'nightly-reconcile', versionNumber: 2, environment: 'production',
      reason: 'Roll back to v2 while we investigate.',
      status: 'Denied', subjectClaimType: 'preferred_username', subjectClaimValue: 'omar@ops', requesterLabel: 'omar@ops',
      createdBy: 'omar@ops', createdAt: iso(-50 * hr), decidedBy: 'alice@ops', decidedAt: iso(-49 * hr),
      decisionReason: 'v3 supersedes v2; promote v3 instead.', etag: nextEtag(),
    },
    {
      // A resolved (Approved) request OWNED BY the default administrator persona (alice@ops), so her "My requests" view is
      // non-empty. It is not Pending, so it never appears in the approver inbox (which defaults to Pending) — the seeded
      // inbox stays at three pending requests.
      id: 'areq-3005', baseWorkflowId: 'nightly-reconcile', versionNumber: 3, environment: 'production',
      reason: 'Promote the new reconcile to production.',
      status: 'Approved', subjectClaimType: 'preferred_username', subjectClaimValue: 'alice@ops', requesterLabel: 'alice@ops',
      createdBy: 'alice@ops', createdAt: iso(-30 * hr), decidedBy: 'alice@ops', decidedAt: iso(-29 * hr), etag: nextEtag(),
    },
  ];
}

function seedSecurityRules() {
  const hr = 60 * 60 * 1000;
  const day = 24 * hr;
  // The reusable reach vocabulary (§14.2): named expressions a binding scopes read/write/purge with. The bootstrap
  // archetypes (tenant isolation, ABAC superset/intersect) plus a couple authored via the template-first builder.
  return [
    { name: 'tenant-scoped', expression: 'tenant == $claim.tenant', description: "Tenant isolation: the row's tenant label must match the principal's tenant claim.", createdBy: 'system', createdAt: iso(-40 * day), etag: nextEtag() },
    { name: 'abac-superset', expression: '$claims.superset', description: 'ABAC clearance: the principal must hold every label the row carries.', createdBy: 'system', createdAt: iso(-40 * day), etag: nextEtag() },
    { name: 'shares-a-label', expression: '$claims.intersects', description: 'The principal shares at least one label with the row.', createdBy: 'system', createdAt: iso(-40 * day), etag: nextEtag() },
    { name: 'reach-payments', expression: "domain == 'payments'", description: 'Rows in the payments domain.', createdBy: 'alice@example.com', createdAt: iso(-9 * day), lastUpdatedBy: 'alice@example.com', lastUpdatedAt: iso(-2 * day), etag: nextEtag() },
    { name: 'reach-core-tenants', expression: "tenant in ('acme', 'globex')", description: 'Rows owned by a core tenant.', createdBy: 'alice@example.com', createdAt: iso(-3 * day), etag: nextEtag() },
  ];
}

function seedSecurityOrderings() {
  // The deployment's ordered tag dimensions (§14.2): each dimension's labels from lowest to highest rank, the ranking
  // the ordered rule templates (classification <= 'confidential', …) use.
  return [
    { dimension: 'classification', labels: ['public', 'internal', 'confidential', 'restricted'] },
  ];
}

function seedSecurityBindings() {
  const hr = 60 * 60 * 1000;
  const day = 24 * hr;
  // Claim → per-verb reach bindings (§14.2): a group/role claim conferring read, and a tenant claim conferring full reach.
  return [
    { id: 'bind-1', claimType: 'team', claimValue: 'payments', read: { ruleNames: ['reach-payments'] }, write: { unrestricted: false }, purge: { unrestricted: false }, order: 0, description: 'Payments team can read payments-domain rows.', createdBy: 'alice@example.com', createdAt: iso(-12 * day), etag: nextEtag() },
    { id: 'bind-2', claimType: 'tenant', claimValue: 'acme', read: { unrestricted: true }, write: { unrestricted: true }, purge: { unrestricted: false }, order: 1, description: 'Acme tenant operators.', createdBy: 'alice@example.com', createdAt: iso(-5 * day), etag: nextEtag() },
  ];
}

function json(body, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function problem(status, title, detail) {
  return new Response(
    JSON.stringify({ type: 'about:blank', title, status, detail }),
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

/**
 * @param {{ seed?: object[], latencyMs?: number }} [options]
 * @returns {{ fetch: (url: string, init?: RequestInit) => Promise<Response>, runs: object[] }}
 */
// The runner registry (§5.4): execution hosts that have registered + heartbeat. Read-only — runners self-register and
// refresh `lastSeenAt` out of band; the control plane only observes (so the demo seeds a representative fleet, including
// a STALE one whose heartbeat has lapsed and a host still loading a version, to exercise the health view).
function seedRunners() {
  const min = 60000;
  const r = (runnerId, environment, address, maxConcurrency, transports, hostedVersions, lastSeenAgoMs, startedAgoMs) => ({
    runnerId, environment, address, maxConcurrency, transports, hostedVersions,
    startedAt: iso(-startedAgoMs), lastSeenAt: iso(-lastSeenAgoMs),
  });
  // Each runner serves exactly one environment (design §5.5); a host serving several runs one process per environment.
  return [
    r('runner-eu-1', 'production', 'https://runner-eu-1.svc.internal:8443', 8, ['http', 'amqp'], [
      { baseWorkflowId: 'adopt-pet', versionNumber: 1, hash: 'sha256:adopt-pet-v1', loaded: true },
      { baseWorkflowId: 'nightly-reconcile', versionNumber: 3, hash: 'sha256:nightly-v3', loaded: true },
    ], 18 * 1000, 6 * 60 * min), // healthy: heartbeat 18s ago, up 6h
    r('runner-eu-2', 'staging', 'https://runner-eu-2.svc.internal:8443', 4, ['http'], [
      { baseWorkflowId: 'nightly-reconcile', versionNumber: 3, hash: 'sha256:nightly-v3', loaded: true },
      { baseWorkflowId: 'onboard-customer', versionNumber: 1, hash: 'sha256:onboard-v1', loaded: false }, // still loading
    ], 32 * 1000, 2 * 60 * min),
    r('runner-us-1', 'production', 'https://runner-us-1.svc.internal:8443', 8, ['http', 'kafka'], [
      { baseWorkflowId: 'adopt-pet', versionNumber: 1, hash: 'sha256:adopt-pet-v1', loaded: true },
    ], 4 * min, 12 * 60 * min), // STALE: last heartbeat 4 minutes ago
  ];
}

// The §5.5 runner-authorization roster: which runners may serve an environment. A runner enters Pending on registration
// (it cannot self-assert into an environment — receiving its runs means receiving its credentials) and is dispatchable
// only once an administrator of that environment authorizes it; authorization is revocable. Keyed by (environment,
// runnerId), this lines up with the seeded runner fleet so the approver inbox and per-environment roster show real
// hosts. The demo user administers both environments, so the Pending rows surface as the actionable inbox to-do.
function seedRunnerAuthorizations() {
  const hr = 60 * 60 * 1000;
  const a = (environment, runnerId, status, createdAgoHr, extra = {}) => ({
    environment, runnerId, status, createdBy: runnerId, createdAt: iso(-createdAgoHr * hr), etag: nextEtag(), ...extra,
  });
  return [
    a('production', 'runner-eu-1', 'Authorized', 30, { decidedBy: 'boss', decidedAt: iso(-29 * hr), reason: 'Vetted EU production host.' }),
    a('production', 'runner-us-1', 'Pending', 2),
    a('staging', 'runner-eu-2', 'Pending', 5),
    a('production', 'runner-eu-old', 'Revoked', 200, { decidedBy: 'boss', decidedAt: iso(-50 * hr), reason: 'Decommissioned host.' }),
  ];
}

// Demo personas — distinct identities, so every axis of the model is demonstrable against the real UI: a `subject`
// identity (stamped as the actor on everything the persona writes, and matched by the "my requests" view); capability
// `scopes` (what the components gate their write controls on, and what the mock enforces server-side with 403s); and
// `reach` — the row-security window (§14.2). `reach: 'all'` sees every row; a `{ readDomain }` reach reads only rows
// whose workflow domain matches (the seeded team→reach binding), so a reach-scoped reader sees a strict, NON-DISCLOSING
// subset (out-of-reach rows are absent, not a 403 — unlike a missing scope). ADMINISTRATION is NOT on the persona: it is
// per-resource membership — a persona administers a workflow/environment iff its subject is in that resource's admin set
// (§15/§7.7), so alice@ops (seeded on every resource) administers everything, omar@ops (seeded on onboard-customer +
// staging) administers only those, and vera/pat administer nothing. A real deployment derives all this from the
// principal's token; here the demo selects an identity. `administrator` is the default so existing tests are unchanged.
const READ_ALL_SCOPES = 'runs:read catalog:read credentials:read sources:read environments:read availability:read administrators:read security:read';
export const DEMO_PERSONAS = {
  administrator: {
    label: 'Administrator — alice@ops (administers everything)',
    subject: 'alice@ops',
    scopes: 'runs:read runs:write runs:purge catalog:read catalog:write catalog:purge credentials:read credentials:write sources:read sources:write environments:read environments:write availability:read availability:write administrators:read administrators:write security:read security:write',
    reach: 'all',
  },
  operator: {
    // The gated-elevation persona: NO availability:write, so promoting a version is never a direct "Make available" — the
    // matrix offers "Request promotion" instead, and an administrator (or an environment administrator like omar for
    // staging) approves it. omar@ops administers onboard-customer (workflow) + staging (environment), so he can approve
    // the requests in the environments he administers, but cannot promote directly.
    label: 'Operator — omar@ops (administers onboard-customer + staging)',
    subject: 'omar@ops',
    scopes: 'runs:read runs:write catalog:read credentials:read sources:read environments:read availability:read administrators:read security:read',
    reach: 'all',
  },
  viewer: {
    label: 'Auditor — vera@audit (read-only, full reach, administers nothing)',
    subject: 'vera@audit',
    scopes: READ_ALL_SCOPES,
    reach: 'all',
  },
  'team-reader': {
    // A read-only member of the payments team, reach-scoped to the payments domain (the seeded `team=payments →
    // reach-payments` binding, §14.2). Its read scopes are deliberately NARROWER than the auditor's: it can read the
    // resources it operates (runs/catalog/credentials/sources/environments/availability) but NOT the org-wide security
    // policy (security:read) or the administrator roster (administrators:read) — a team member is not a security auditor.
    // reach then narrows the DOMAIN-TAGGED subset (runs + catalog) to payments; the scope set is what gates whole
    // surfaces (e.g. the Permissions tab). The auditor-vs-team-reader contrast isolates reach; auditor-vs-team-reader on
    // scopes isolates capability. See the demo's tab gating (index.html) for how a missing read scope hides a surface.
    label: 'Payments read-only — pat@payments (reach: domain=payments)',
    subject: 'pat@payments',
    scopes: 'runs:read catalog:read credentials:read sources:read environments:read availability:read',
    reach: { readDomain: 'payments' },
  },
};

export function createMockControlPlane(options = {}) {
  const runs = options.seed ? structuredClone(options.seed) : seedRuns();
  const runners = options.runnersSeed ? structuredClone(options.runnersSeed) : seedRunners();
  const runnerAuthorizations = options.runnerAuthorizationsSeed ? structuredClone(options.runnerAuthorizationsSeed) : seedRunnerAuthorizations();
  const catalog = options.catalogSeed ? structuredClone(options.catalogSeed) : seedCatalog();
  const credentials = options.credentialsSeed ? structuredClone(options.credentialsSeed) : seedCredentials();
  const environments = options.environmentsSeed ? structuredClone(options.environmentsSeed) : seedEnvironments();
  const sourceRegistry = options.sourcesSeed ? structuredClone(options.sourcesSeed) : seedSources();
  const administrators = options.administratorsSeed ? structuredClone(options.administratorsSeed) : seedAdministrators();
  const environmentAdministrators = options.environmentAdministratorsSeed ? structuredClone(options.environmentAdministratorsSeed) : seedEnvironmentAdministrators();
  const grantees = options.granteesSeed ? structuredClone(options.granteesSeed) : seedGrantees();
  const accessRequests = options.accessRequestsSeed ? structuredClone(options.accessRequestsSeed) : seedAccessRequests();
  const availabilityRequests = options.availabilityRequestsSeed ? structuredClone(options.availabilityRequestsSeed) : seedAvailabilityRequests();
  const availabilityEntries = options.availabilityEntriesSeed ? structuredClone(options.availabilityEntriesSeed) : seedAvailabilityEntries();
  const securityRules = options.securityRulesSeed ? structuredClone(options.securityRulesSeed) : seedSecurityRules();
  const securityOrderings = options.securityOrderingsSeed ? structuredClone(options.securityOrderingsSeed) : seedSecurityOrderings();
  const securityBindings = options.securityBindingsSeed ? structuredClone(options.securityBindingsSeed) : seedSecurityBindings();
  let nextBindingId = 1000;
  const latency = options.latencyMs ?? 250;
  const find = (id) => runs.find((r) => r.id === id);
  const findVersion = (base, n) => catalog.find((v) => v.baseWorkflowId === base && v.versionNumber === Number(n));
  const findCredential = (s, e) => credentials.find((c) => c.sourceName === s && c.environment === e);
  // §13 IsUsableBy (demo approximation): an unscoped binding is shared; a usage-scoped one is usable only by the
  // workflow it names. The real backend does a full label-superset over the run's resolved identity.
  const credentialUsableBy = (c, baseWorkflowId) => {
    const id = c.usageGrantee?.identity;
    return !id || id.length === 0 || id.every((t) => t.dimension === 'workflow' && t.value === baseWorkflowId);
  };
  const findUsableCredential = (s, e, wf) => credentials.find((c) => c.sourceName === s && c.environment === e && credentialUsableBy(c, wf));

  // The current persona (default administrator → all scopes + administers everything, so existing callers are unchanged).
  function personaState(name) {
    const p = DEMO_PERSONAS[name] || DEMO_PERSONAS.administrator;
    return { name: DEMO_PERSONAS[name] ? name : 'administrator', subject: p.subject, scopes: new Set(p.scopes.split(/\s+/).filter(Boolean)), reach: p.reach ?? 'all' };
  }

  // The active caller's identity — stamped as the actor on everything this persona writes, and matched by "my requests".
  const actingSubject = () => persona.subject;

  // Administration is per-resource membership (§15/§7.7): the caller administers a workflow/environment iff its subject
  // (sys:sub) is named in that resource's administrator set — the same set the admin panels display. So alice@ops (seeded
  // on every resource) administers all, omar@ops (seeded on onboard-customer + staging) only those, others nothing.
  const adminSetAdmits = (grants) => Array.isArray(grants)
    && grants.some((g) => (g.identity || []).some((t) => t.dimension === 'sys:sub' && t.value === persona.subject));
  const administersWorkflow = (base) => adminSetAdmits(administrators[base]);
  const administersEnvironment = (env) => adminSetAdmits(environmentAdministrators[env]);

  // The caller's read reach (§14.2) over a row's real security tags: 'all' admits everything; a { readDomain } reach
  // admits only rows carrying a `domain=<readDomain>` tag. Out-of-reach rows are omitted from lists and 404 on direct
  // GET (non-disclosing). Reads the row's actual { key, value } securityTags — no computed fiction.
  function reachAdmits(securityTags) {
    const reach = persona.reach;
    if (!reach || reach === 'all' || reach.readDomain === undefined) {
      return true;
    }

    return (securityTags || []).some((t) => t.key === 'domain' && t.value === reach.readDomain);
  }

  let persona = personaState(options.persona || 'administrator');

  // The capability scope a write requires, mirroring the OpenAPI security on each operation (reads are reach-gated in
  // the real backend; the demo gates the capability/write scopes). Access-/availability-request endpoints are auth-only
  // (no capability scope) — they are governed by the administrator check instead. Path carries the /arazzo/v1 prefix.
  function requiredScopeFor(method, path) {
    if (method === 'GET') return null;
    if (/\/catalog\/[^/]+\/versions\/[^/]+\/availability\/[^/]+$/.test(path)) return 'availability:write';
    if (path.includes('/accessRequests') || path.includes('/availabilityRequests')) return null;
    if (path.includes('/catalog')) return method === 'PURGE' || method === 'DELETE' ? 'catalog:purge' : 'catalog:write';
    if (path.includes('/credentials')) return 'credentials:write';
    if (path.includes('/environments')) return 'environments:write'; // incl. env-admin governance (§7.7)
    if (path.includes('/sources')) return 'sources:write';
    if (path.includes('/security')) return 'security:write';
    if (path.includes('/administrators')) return 'administrators:write';
    if (path.includes('/runs')) return method === 'PURGE' || method === 'DELETE' ? 'runs:purge' : 'runs:write';
    return null;
  }

  // The administrator gate (§7.7/§15/§16.5): a governance decision — make a version available, approve/deny a promotion
  // or access request — requires that the caller administers the target. A non-administrator must raise a REQUEST.
  const requireAdministrator = (what, target) => {
    const admits = target && target.workflow !== undefined ? administersWorkflow(target.workflow)
      : target && target.environment !== undefined ? administersEnvironment(target.environment)
      : false;
    const noun = target && target.workflow !== undefined ? 'workflow' : 'environment';
    return admits ? null
      : problem(403, 'Not an administrator', `Only an administrator of this ${noun} can ${what}; request it instead and an administrator will decide.`);
  };

  // The runner registry read (§5.4): one keyset page of execution hosts, ordered by runnerId. Read-only.
  function handleRunners(fullPath, method, params) {
    const idx = fullPath.indexOf('/runners');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);
    if (!/^\/runners\/?$/.test(path)) return null;
    if (method !== 'GET') return problem(405, 'Method not allowed');
    const limit = Math.max(1, Math.min(Number(params.get('limit')) || 50, 200));
    const sorted = [...runners].sort((a, b) => a.runnerId.localeCompare(b.runnerId));
    const offset = Number(params.get('pageToken')) || 0;
    const pageItems = sorted.slice(offset, offset + limit).map((x) => structuredClone(x));
    const nextPageToken = offset + limit < sorted.length ? String(offset + limit) : null;
    return json({ runners: pageItems, nextPageToken });
  }

  // The §5.5 runner-authorization approver inbox: GET /runnerAuthorizations — every authorization across the environments
  // the caller administers, defaulting to Pending (the actionable to-do); with `environment`, that one environment's queue
  // (visible only to an administrator of it). Keyset paged over (environment, runnerId), the order the durable store uses.
  function handleRunnerAuthorizations(fullPath, method, params) {
    const idx = fullPath.indexOf('/runnerAuthorizations');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);
    if (!/^\/runnerAuthorizations\/?$/.test(path)) return null;
    if (method !== 'GET') return problem(405, 'Method not allowed');
    const status = params.get('status') || 'Pending';
    const environment = params.get('environment');
    let rows = runnerAuthorizations.filter((r) => r.status === status);
    if (environment) {
      // A single environment's queue — visible only to an administrator of it (others see an empty queue).
      rows = administersEnvironment(environment) ? rows.filter((r) => r.environment === environment) : [];
    } else {
      // The approver inbox: only authorizations for environments the caller administers (empty if they administer none).
      rows = rows.filter((r) => administersEnvironment(r.environment));
    }
    return pageRunnerAuths(rows, params);
  }

  // Keyset pagination over (environment, runnerId), the same contract the durable store implements: order, seek strictly
  // past the opaque token, take `limit`, emit a nextPageToken.
  function pageRunnerAuths(rows, params) {
    const limit = Math.max(1, Math.min(Number(params.get('limit')) || 50, 200));
    const sorted = [...rows].sort((a, b) => a.environment.localeCompare(b.environment) || a.runnerId.localeCompare(b.runnerId));
    const tok = params.get('pageToken') ? atobSafe(params.get('pageToken')).split(' ') : null;
    const start = tok ? sorted.findIndex((r) => r.environment > tok[0] || (r.environment === tok[0] && r.runnerId > tok[1])) : 0;
    const from = start < 0 ? sorted.length : start;
    const pageItems = sorted.slice(from, from + limit).map((x) => structuredClone(x));
    const more = from + limit < sorted.length;
    const last = pageItems[pageItems.length - 1];
    const nextPageToken = more && last ? btoaSafe(`${last.environment} ${last.runnerId}`) : null;
    return json({ authorizations: pageItems, nextPageToken });
  }

  // Apply an authorize (POST) or revoke (DELETE) decision to a runner's authorization for an environment. Idempotent —
  // deciding to the state it is already in returns it unchanged; an absent (environment, runnerId) is 404 (the runner
  // never registered for it). The caller's administrator gate is checked by the route before this is reached.
  function decideRunnerAuthorization(environment, runnerId, target, body) {
    const r = runnerAuthorizations.find((x) => x.environment === environment && x.runnerId === runnerId);
    if (!r) return problem(404, 'Runner authorization not found', `No runner '${runnerId}' has registered for environment '${environment}'.`);
    if (r.status === target) return json(structuredClone(r));
    r.status = target;
    r.decidedBy = actingSubject();
    r.decidedAt = iso(0);
    if (body?.reason) r.reason = body.reason;
    r.etag = nextEtag();
    return json(structuredClone(r));
  }

  async function handle(url, init = {}) {
    const method = (init.method || 'GET').toUpperCase();
    const u = new URL(url, 'https://mock');
    const path = u.pathname;
    const isForm = typeof FormData !== 'undefined' && init.body instanceof FormData;
    const body = init.body && !isForm ? JSON.parse(init.body) : undefined;

    // Capability-scope enforcement (the server-side gate, mirroring the OpenAPI security): a write the persona's scopes
    // do not admit is a 403 — exactly what the real control plane returns, so the components' scope-hidden controls and
    // the backend agree.
    const requiredScope = requiredScopeFor(method, path);
    if (requiredScope && !persona.scopes.has(requiredScope)) {
      return problem(403, 'Insufficient scope', `This action requires the '${requiredScope}' scope, which the current persona does not hold.`);
    }

    const versionAvailabilityResponse = handleVersionAvailability(path, method, u.searchParams);
    if (versionAvailabilityResponse) return versionAvailabilityResponse;

    const catalogResponse = await handleCatalog(path, method, u.searchParams, body, isForm ? init.body : null);
    if (catalogResponse) return catalogResponse;

    const credentialsResponse = handleCredentials(path, method, u.searchParams, body);
    if (credentialsResponse) return credentialsResponse;

    const environmentsResponse = handleEnvironments(path, method, u.searchParams, body);
    if (environmentsResponse) return environmentsResponse;

    const sourcesResponse = handleSources(path, method, u.searchParams, body);
    if (sourcesResponse) return sourcesResponse;

    const administratorsResponse = handleAdministrators(path, method, body);
    if (administratorsResponse) return administratorsResponse;

    const identityResponse = handleIdentity(path, method, u.searchParams);
    if (identityResponse) return identityResponse;

    const accessRequestsResponse = handleAccessRequests(path, method, u.searchParams, body);
    if (accessRequestsResponse) return accessRequestsResponse;

    const availabilityRequestsResponse = handleAvailabilityRequests(path, method, u.searchParams, body);
    if (availabilityRequestsResponse) return availabilityRequestsResponse;

    const securityOrderingsResponse = handleSecurityOrderings(path, method);
    if (securityOrderingsResponse) return securityOrderingsResponse;

    const securityBindingsResponse = handleSecurityBindings(path, method, u.searchParams, body);
    if (securityBindingsResponse) return securityBindingsResponse;

    const securityRulesResponse = handleSecurityRules(path, method, u.searchParams, body);
    if (securityRulesResponse) return securityRulesResponse;

    const runnerAuthorizationsResponse = handleRunnerAuthorizations(path, method, u.searchParams);
    if (runnerAuthorizationsResponse) return runnerAuthorizationsResponse;

    const runnersResponse = handleRunners(path, method, u.searchParams);
    if (runnersResponse) return runnersResponse;

    // /runs collection
    if (/\/runs\/?$/.test(path)) {
      if (method === 'GET') return listRuns(u.searchParams);
      if (method === 'PURGE') return purgeRuns(u.searchParams);
      return problem(405, 'Method not allowed');
    }

    // /runs/{id}[/action]
    const m = path.match(/\/runs\/([^/]+)(?:\/(resume|cancel))?$/);
    if (m) {
      const id = decodeURIComponent(m[1]);
      const action = m[2];
      const run = find(id);
      if (!run) return problem(404, 'Run not found', `No run with id '${id}'.`);

      if (!action && method === 'GET') {
        // Reach (§14.2): a run outside the caller's reach reads back as not found (non-disclosing).
        if (!reachAdmits(securityTagsForBase(baseWorkflowOf(run.workflowId)))) return problem(404, 'Run not found', `No run with id '${id}'.`);
        return json(toDetail(run));
      }
      if (!action && method === 'DELETE') return deleteRun(run);
      if (action === 'resume' && method === 'POST') return resumeRun(run, body);
      if (action === 'cancel' && method === 'POST') return cancelRun(run, body);
      return problem(405, 'Method not allowed');
    }

    return problem(404, 'Not found', path);
  }

  function listRuns(params) {
    const status = params.get('status');
    const workflowId = params.get('workflowId');
    const limit = Math.max(1, Number(params.get('limit')) || 100);
    const offset = Number(atobSafe(params.get('pageToken'))) || 0;

    let filtered = [...runs].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt));
    if (status) filtered = filtered.filter((r) => r.status === status);
    if (workflowId) filtered = filtered.filter((r) => r.workflowId.includes(workflowId));

    // Time-window filters: createdAfter/updatedAfter inclusive (>=), createdBefore/updatedBefore exclusive (<).
    const createdAfter = parseMs(params.get('createdAfter'));
    const createdBefore = parseMs(params.get('createdBefore'));
    const updatedAfter = parseMs(params.get('updatedAfter'));
    const updatedBefore = parseMs(params.get('updatedBefore'));
    if (createdAfter != null) filtered = filtered.filter((r) => Date.parse(r.createdAt) >= createdAfter);
    if (createdBefore != null) filtered = filtered.filter((r) => Date.parse(r.createdAt) < createdBefore);
    if (updatedAfter != null) filtered = filtered.filter((r) => Date.parse(r.updatedAt) >= updatedAfter);
    if (updatedBefore != null) filtered = filtered.filter((r) => Date.parse(r.updatedAt) < updatedBefore);

    // Tags are AND-matched (a run must carry every requested tag); correlationId is an exact match.
    const wantTags = params.getAll('tag').filter(Boolean);
    if (wantTags.length > 0) filtered = filtered.filter((r) => wantTags.every((t) => (r.tags ?? []).includes(t)));
    const correlationId = params.get('correlationId');
    if (correlationId) filtered = filtered.filter((r) => r.correlationId === correlationId);

    // Reach (§14.2): the caller sees only runs whose workflow domain their read reach admits (non-disclosing).
    filtered = filtered.filter((r) => reachAdmits(securityTagsForBase(baseWorkflowOf(r.workflowId))));

    const slice = filtered.slice(offset, offset + limit);
    const hasMore = offset + limit < filtered.length;
    return json({
      runs: slice.map(toSummary),
      nextPageToken: hasMore ? btoaSafe(String(offset + limit)) : null,
    });
  }

  function resumeRun(run, request) {
    if (run.status !== 'Faulted') {
      return problem(409, 'Run is not faulted', `Run '${run.id}' is ${run.status}; only faulted runs can be resumed.`);
    }
    const mode = request?.mode;
    if (mode === 'Rewind' && typeof request.targetCursor === 'number') run.cursor = request.targetCursor;
    if (mode === 'Skip') run.cursor = (request.targetCursor ?? run.cursor + 1);
    run.fault = null;
    delete run._errorType;
    run.status = 'Running';
    run.updatedAt = iso(0);
    run.etag = nextEtag();
    return json(toDetail(run));
  }

  function cancelRun(run, request) {
    if (TERMINAL.has(run.status)) {
      return problem(409, 'Run already terminal', `Run '${run.id}' is ${run.status} and cannot be cancelled.`);
    }
    run.status = 'Cancelled';
    run.wait = null;
    run._cancelReason = request?.reason;
    run.updatedAt = iso(0);
    run.etag = nextEtag();
    return json(toDetail(run));
  }

  function deleteRun(run) {
    const i = runs.indexOf(run);
    runs.splice(i, 1);
    return new Response(null, { status: 204 });
  }

  function purgeRuns(params) {
    const olderThan = params.get('olderThan');
    const limit = Number(params.get('limit')) || Infinity;
    const cutoff = olderThan ? Date.parse(olderThan) : NaN;
    if (Number.isNaN(cutoff)) return problem(400, 'Invalid olderThan', 'olderThan must be an RFC 3339 timestamp.');
    let purged = 0;
    for (let i = runs.length - 1; i >= 0 && purged < limit; i--) {
      const r = runs[i];
      if (TERMINAL.has(r.status) && Date.parse(r.updatedAt) < cutoff) {
        runs.splice(i, 1);
        purged++;
      }
    }
    return json({ purgedCount: purged });
  }

  async function handleCatalog(fullPath, method, params, body, form) {
    // Tolerate a base-path prefix (e.g. /arazzo/v1) the same way the loose /runs regexes do.
    const idx = fullPath.indexOf('/catalog');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);

    if (/^\/catalog\/?$/.test(path)) {
      if (method === 'GET') return searchCatalog(params);
      if (method === 'POST') return addCatalogVersion(form);
      if (method === 'PURGE') return purgeCatalog();
      return problem(405, 'Method not allowed');
    }

    const versionMatch = path.match(/^\/catalog\/([^/]+)\/versions\/([^/]+)(?:\/(package|workflow|schemas|validate|sources\/[^/]+))?$/);
    if (versionMatch) {
      const base = decodeURIComponent(versionMatch[1]);
      const n = Number(versionMatch[2]);
      const sub = versionMatch[3];
      const v = findVersion(base, n);
      if (!v) return problem(404, 'Version not found', `No version ${n} of workflow '${base}'.`);
      if (!sub && method === 'GET') {
        // Reach (§14.2): a version whose workflow domain the caller cannot read reads back as not found.
        if (!reachAdmits(v.securityTags ?? securityTagsForBase(v.baseWorkflowId))) return problem(404, 'Version not found', `No version ${n} of workflow '${base}'.`);
        return json(toCatalogSummary(v));
      }
      if (!sub && method === 'PATCH') return updateVersion(v, body);
      if (!sub && method === 'DELETE') return deleteVersion(v);
      if (sub === 'package' && method === 'GET') return packageResponse(v);
      if (sub === 'workflow' && method === 'GET') return json(v._workflow);
      if (sub === 'schemas' && method === 'GET') return json(schemasFor(v));
      if (sub === 'validate' && method === 'POST') return json(validateValue(v, body));
      if (sub && sub.startsWith('sources/') && method === 'GET') {
        const name = decodeURIComponent(sub.slice('sources/'.length));
        const doc = v._sources?.[name];
        return doc ? json(doc) : problem(404, 'Source not found', `No source document '${name}'.`);
      }
      return problem(405, 'Method not allowed');
    }

    const listMatch = path.match(/^\/catalog\/([^/]+)\/?$/);
    if (listMatch) {
      if (method === 'GET') return listCatalogVersions(decodeURIComponent(listMatch[1]), params);
      return problem(405, 'Method not allowed');
    }

    return problem(404, 'Not found', path);
  }

  function matchesCatalog(v, params) {
    const base = params.get('baseWorkflowId');
    if (base && v.baseWorkflowId !== base) return false;
    const prefix = (params.get('workflowIdPrefix') || '').toLowerCase();
    if (prefix && !(v.workflowId || '').toLowerCase().startsWith(prefix)) return false;
    const status = params.get('status');
    if (status && v.status !== status) return false;
    // Free-text find: a substring over the title, description, and the workflow id (base + versioned) — so "pet" finds
    // "adopt-pet" and a pasted id like "adopt-pet" matches too, not just an anchored id prefix (workflowIdPrefix).
    const q = (params.get('q') || '').toLowerCase();
    if (q
      && !v.title.toLowerCase().includes(q)
      && !(v.description || '').toLowerCase().includes(q)
      && !(v.workflowId || '').toLowerCase().includes(q)
      && !(v.baseWorkflowId || '').toLowerCase().includes(q)) return false;
    const owner = (params.get('owner') || '').toLowerCase();
    if (owner && !v.owner.name.toLowerCase().includes(owner) && !v.owner.email.toLowerCase().includes(owner)) return false;
    const wantTags = params.getAll('tag').filter(Boolean);
    if (wantTags.length > 0 && !wantTags.every((t) => (v.tags ?? []).includes(t))) return false;
    // Reach (§14.2): a version whose workflow domain the caller cannot read is invisible.
    if (!reachAdmits(v.securityTags ?? securityTagsForBase(v.baseWorkflowId))) return false;
    return true;
  }

  function pageCatalog(filtered, params) {
    filtered.sort((a, b) => a.baseWorkflowId.localeCompare(b.baseWorkflowId) || a.versionNumber - b.versionNumber);
    const limit = Math.max(1, Number(params.get('limit')) || 100);
    const offset = Number(atobSafe(params.get('pageToken'))) || 0;
    const slice = filtered.slice(offset, offset + limit);
    const hasMore = offset + limit < filtered.length;
    return json({ versions: slice.map(toCatalogSummary), nextPageToken: hasMore ? btoaSafe(String(offset + limit)) : null });
  }

  // Representative precedence for distinct-workflow collapse: newest Active, else newest Obsolete, else newest.
  function catalogStatusRank(status) {
    return status === 'Active' ? 0 : status === 'Obsolete' ? 1 : 2;
  }

  function collapseToRepresentatives(filtered) {
    const byBase = new Map();
    for (const v of filtered) {
      const cur = byBase.get(v.baseWorkflowId);
      const better = !cur
        || catalogStatusRank(v.status) < catalogStatusRank(cur.status)
        || (catalogStatusRank(v.status) === catalogStatusRank(cur.status) && v.versionNumber > cur.versionNumber);
      if (better) byBase.set(v.baseWorkflowId, v);
    }
    return [...byBase.values()];
  }

  function searchCatalog(params) {
    const filtered = catalog.filter((v) => matchesCatalog(v, params));
    // distinctWorkflows collapses to one representative version per base workflow (a base appears if any of its
    // versions matched), keyset-paged by base id — the server-side equivalent of the old client-side grouping.
    const rows = params.get('distinctWorkflows') === 'true' ? collapseToRepresentatives(filtered) : filtered;
    return pageCatalog(rows, params);
  }

  function listCatalogVersions(base, params) {
    // Reach (§14.2): a workflow whose domain the caller cannot read has no visible versions (non-disclosing).
    if (!reachAdmits(securityTagsForBase(base))) return pageCatalog([], params);
    return pageCatalog(catalog.filter((v) => v.baseWorkflowId === base), params);
  }

  function updateVersion(v, patch) {
    // Re-tag (§14.2): a present securityTags replaces the version's non-internal labels (the reserved prefix is
    // rejected; the demo carries no internal tags so there is nothing to preserve). Absent leaves them unchanged.
    if (Array.isArray(patch?.securityTags)) {
      if (usesReservedTag(patch.securityTags)) {
        return problem(400, 'Reserved security tag', `A security tag key uses the reserved internal prefix '${RESERVED_TAG_PREFIX}', which is owned by the deployment.`);
      }

      v.securityTags = patch.securityTags;
    }

    if (patch?.owner) v.owner = patch.owner;
    if (Array.isArray(patch?.tags)) v.tags = patch.tags;
    if (patch?.status && patch.status !== v.status) {
      const newlyObsolete = patch.status === 'Obsolete';
      v.status = patch.status;
      v.obsoletedBy = newlyObsolete ? actingSubject() : null;
      v.obsoletedAt = newlyObsolete ? iso(0) : null;
    }
    v.lastUpdatedBy = actingSubject();
    v.lastUpdatedAt = iso(0);
    return json(toCatalogSummary(v));
  }

  function deleteVersion(v) {
    if (runs.some((r) => r.workflowId === v.workflowId)) {
      return problem(409, 'Version is referenced', `Version ${v.versionNumber} of '${v.baseWorkflowId}' cannot be deleted while runs reference it.`);
    }
    catalog.splice(catalog.indexOf(v), 1);
    return new Response(null, { status: 204 });
  }

  function purgeCatalog() {
    let purged = 0;
    for (let i = catalog.length - 1; i >= 0; i--) {
      const v = catalog[i];
      if (v.status === 'Obsolete' && !runs.some((r) => r.workflowId === v.workflowId)) {
        catalog.splice(i, 1);
        purged++;
      }
    }
    return json({ purgedCount: purged });
  }

  async function addCatalogVersion(form) {
    const pkg = form?.get('package');
    if (!pkg || !form.get('owner')) {
      return problem(400, 'Invalid submission', 'A package and an owner are required.');
    }
    let owner;
    try {
      owner = JSON.parse(await form.get('owner').text());
    } catch {
      owner = { name: 'unknown', email: 'unknown' };
    }

    // Read the package the way the server does: pull the base workflow id from the bundled workflow.json,
    // assign the next version for that base, and rewrite the workflow id to "<base>-vN".
    let base = 'uploaded-workflow';
    let title = 'Uploaded workflow';
    let description = null;
    let workflowDoc = { arazzo: '1.1.0', info: { title }, workflows: [{ workflowId: base }] };
    const sources = {};
    const sourceRefs = [];
    try {
      const entries = unpackWorkflowPackage(await pkg.arrayBuffer());
      if (entries?.has('workflow.json')) {
        workflowDoc = JSON.parse(entries.get('workflow.json'));
        const wfId = workflowDoc.workflows?.[0]?.workflowId || '';
        if (/-v\d+$/.test(wfId)) {
          return problem(400, 'Versioned workflow id', 'Submit the bare workflow id without a -vN suffix; the catalog assigns the version.');
        }
        base = wfId || base;
        title = workflowDoc.info?.title || base;
        description = workflowDoc.info?.description ?? null;
        for (const [name, text] of entries) {
          if (name.startsWith('sources/') && name.endsWith('.json')) {
            const sn = name.slice('sources/'.length, -'.json'.length);
            sources[sn] = JSON.parse(text);
            const sd = (workflowDoc.sourceDescriptions || []).find((s) => s.name === sn);
            sourceRefs.push({ name: sn, type: sd?.type || 'openapi' });
          }
        }
      }
    } catch {
      // A non-package upload falls back to the generic placeholder base.
    }

    const versionNumber = (catalog.filter((v) => v.baseWorkflowId === base).reduce((m, v) => Math.max(m, v.versionNumber), 0)) + 1;
    const workflowId = `${base}-v${versionNumber}`;
    if (workflowDoc.workflows?.[0]) workflowDoc.workflows[0].workflowId = workflowId;

    // User-supplied non-internal security tags (§14.2): a single JSON part, mirroring the server's
    // MultipartFormDataSerializer. The reserved internal prefix is rejected (400). Absent → the demo stamps the
    // workflow's seeded tags so a new version stays reach-consistent with its siblings.
    let securityTags = [];
    const securityTagsPart = form.get('securityTags');
    if (securityTagsPart) {
      try {
        securityTags = JSON.parse(await securityTagsPart.text());
      } catch {
        securityTags = [];
      }
    }

    if (usesReservedTag(securityTags)) {
      return problem(400, 'Reserved security tag', `A security tag key uses the reserved internal prefix '${RESERVED_TAG_PREFIX}', which is owned by the deployment.`);
    }

    const v = {
      baseWorkflowId: base, versionNumber, workflowId,
      title, description, status: 'Active',
      tags: form.getAll('tags'), owner, sources: sourceRefs, hash: `${base}${versionNumber}`.padEnd(64, '0'),
      securityTags: securityTags.length ? securityTags : securityTagsForBase(base),
      createdBy: actingSubject(), createdAt: iso(0),
      _workflow: workflowDoc,
      _sources: sources,
    };
    catalog.push(v);
    // The first version of a base id establishes the creator as its first administrator (§15 — "creating it grants
    // the creator administration", mirroring the backend's SecuredWorkflowCatalog). So a freshly-created workflow
    // always has a real administrator; the wizard adds further ones on top.
    if (versionNumber === 1 && !administrators[base]) {
      administrators[base] = [adminGrant([{ dimension: 'sys:sub', value: actingSubject() }], 'person', 'You (creator)')];
    }
    return json(toCatalogSummary(v), 201);
  }

  function packageResponse(v) {
    const bytes = new TextEncoder().encode(JSON.stringify({ manifest: { formatVersion: 1 }, workflow: v._workflow, sources: v._sources }));
    return new Response(bytes, { status: 200, headers: { 'Content-Type': 'application/octet-stream' } });
  }

  // ---- source credentials (§13) -----------------------------------------------------------------

  function handleCredentials(fullPath, method, params, body) {
    const idx = fullPath.indexOf('/credentials');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);

    if (/^\/credentials\/?$/.test(path)) {
      if (method === 'GET') return listCredentialsPage(params);
      if (method === 'POST') return createCredential(body);
      return problem(405, 'Method not allowed');
    }
    const m = path.match(/^\/credentials\/([^/]+)\/([^/]+)$/);
    if (m) {
      const sourceName = decodeURIComponent(m[1]);
      const environment = decodeURIComponent(m[2]);
      const existing = findCredential(sourceName, environment);
      if (method === 'GET') return existing ? json(toCredentialSummary(existing)) : notFoundCredential(sourceName, environment);
      if (method === 'PUT') return existing ? updateCredential(existing, body) : notFoundCredential(sourceName, environment);
      if (method === 'DELETE') return deleteCredential(existing, sourceName, environment);
      return problem(405, 'Method not allowed');
    }
    return null;
  }

  // Keyset pagination over (sourceName, environment) — the same contract the durable stores implement: order, seek
  // strictly past the opaque token, take `limit`, and emit a nextPageToken when more remain.
  function listCredentialsPage(params) {
    const limit = Math.max(1, Number(params.get('limit')) || 100);
    const key = (c) => `${c.sourceName}\u0000${c.environment}`;
    const after = params.get('pageToken') ? atobSafe(params.get('pageToken')) : null;
    const sorted = [...credentials].sort((a, b) => (key(a) < key(b) ? -1 : key(a) > key(b) ? 1 : 0));
    const start = after ? sorted.findIndex((c) => key(c) > after) : 0;
    const from = start < 0 ? sorted.length : start;
    const pageItems = sorted.slice(from, from + limit);
    const more = from + limit < sorted.length;
    const last = pageItems[pageItems.length - 1];
    const nextPageToken = more && last ? btoaSafe(key(last)) : null;
    return json({ credentials: pageItems.map(toCredentialSummary), nextPageToken });
  }

  // A secretRef must be a reference, never inline secret material — the boundary that keeps secrets out.
  function refError(refs) {
    if (!Array.isArray(refs) || refs.length === 0) return 'At least one secretRef is required.';
    for (const r of refs) {
      if (!isSecretRef(r?.ref)) return `'${r?.ref}' is not a well-formed secretRef (scheme://locator[#version]).`;
    }
    return null;
  }

  function createCredential(body) {
    if (!body?.sourceName || !body?.environment || !body?.authKind) {
      return problem(400, 'Invalid credential binding', 'sourceName, environment, and authKind are required.');
    }
    const err = refError(body.secretRefs);
    if (err) return problem(400, 'Invalid credential binding', err);
    // mTLS (§13.1) needs a 'certificate' reference and, being connection-level, cannot be usage-scoped — mirror the
    // server's boundary validation so the demo behaves like the real control plane.
    if (body.authKind === 'mtls') {
      if (!(body.secretRefs || []).some((r) => r.name === 'certificate')) {
        return problem(400, 'Invalid credential binding', "An mTLS credential requires a 'certificate' secret reference.");
      }
      if (body.usageGrantee) {
        return problem(400, 'Invalid credential binding', 'An mTLS credential is connection-level and cannot be usage-scoped.');
      }
    }
    if (findCredential(body.sourceName, body.environment)) {
      return problem(409, 'Credential already exists', `A binding for '${body.sourceName}@${body.environment}' already exists.`);
    }
    const b = {
      id: `cred-${body.sourceName}-${body.environment}`,
      sourceName: body.sourceName, environment: body.environment, authKind: body.authKind,
      secretRefs: body.secretRefs, config: body.config ?? [], managementTags: body.managementTags ?? [],
      usageGrantee: body.usageGrantee, description: body.description,
      expiresAt: body.expiresAt, rotatedAt: body.rotatedAt,
      createdBy: actingSubject(), createdAt: iso(0), etag: nextEtag(),
    };
    credentials.push(b);
    return json(toCredentialSummary(b), 201);
  }

  function updateCredential(b, body) {
    const err = refError(body?.secretRefs);
    if (err) return problem(400, 'Invalid credential binding', err);
    b.authKind = body.authKind ?? b.authKind;
    b.secretRefs = body.secretRefs;
    b.config = body.config ?? [];
    b.description = body.description;
    b.expiresAt = body.expiresAt;
    b.rotatedAt = body.rotatedAt;
    b.lastUpdatedBy = actingSubject();
    b.lastUpdatedAt = iso(0);
    b.etag = nextEtag();
    return json(toCredentialSummary(b));
  }

  function deleteCredential(b, sourceName, environment) {
    if (!b) return notFoundCredential(sourceName, environment);
    credentials.splice(credentials.indexOf(b), 1);
    return new Response(null, { status: 204 });
  }

  function notFoundCredential(sourceName, environment) {
    return problem(404, 'Credential not found', `No source credential binding for '${sourceName}@${environment}' exists.`);
  }

  // ---- workflow administration (§15) ------------------------------------------------------------
  // The mock is identity-less (no auth), so it models the membership-governed mutations as happy-path; the
  // current-administrator 403 is exercised by the server/CLI tests and by component tests with a fake client.

  function handleAdministrators(fullPath, method, body) {
    const idx = fullPath.indexOf('/administrators');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);

    const memberMatch = path.match(/^\/administrators\/([^/]+)\/members\/([^/]+)$/);
    if (memberMatch && method === 'DELETE') {
      return removeAdminFrom(administrators, decodeURIComponent(memberMatch[1]), decodeURIComponent(memberMatch[2]), 'workflow');
    }
    const membersMatch = path.match(/^\/administrators\/([^/]+)\/members$/);
    if (membersMatch && method === 'POST') {
      return addAdminTo(administrators, decodeURIComponent(membersMatch[1]), body);
    }
    const baseMatch = path.match(/^\/administrators\/([^/]+)$/);
    if (baseMatch) {
      const base = decodeURIComponent(baseMatch[1]);
      if (method === 'GET') return json({ administrators: administrators[base] ?? [] });
      if (method === 'PUT') return transferAdminTo(administrators, base, body);
      return problem(405, 'Method not allowed');
    }
    return null;
  }

  // The administrator helpers are subject-agnostic — the same resolved-identity set governs a workflow (§15) and a
  // deployment environment (§7.7). The store + key select which set; `noun` only shapes the last-administrator message.
  function addAdminTo(store, key, member) {
    // Resolved-grantee form (a full identity[] from the picker) or the interim single {dimension,value} grant.
    let identity;
    let kind;
    let label;
    if (Array.isArray(member?.identity) && member.identity.length > 0) {
      identity = member.identity;
      kind = member.kind;
      label = member.label;
    } else if (member?.dimension && member?.value) {
      identity = [{ dimension: member.dimension, value: member.value }];
      kind = kindForDimension(member.dimension);
    } else {
      return problem(400, 'Invalid administrator identity', 'Provide a resolved grantee identity or a single { dimension, value } grant.');
    }

    const grant = adminGrant(identity, kind, label);
    const set = store[key] ?? (store[key] = []);
    if (!set.some((a) => a.digest === grant.digest)) set.push(grant);
    return json({ administrators: set });
  }

  function removeAdminFrom(store, key, digest, noun) {
    const set = store[key] ?? [];
    const i = set.findIndex((a) => a.digest === digest);
    if (i >= 0) {
      if (set.length === 1) return problem(409, 'Cannot remove the last administrator', `A ${noun} must always have at least one administrator.`);
      set.splice(i, 1);
    }
    return json({ administrators: set });
  }

  function transferAdminTo(store, key, body) {
    if (!Array.isArray(body?.administrators) || body.administrators.length === 0) {
      return problem(400, 'Invalid administrator set', 'At least one administrator is required.');
    }
    const deduped = [];
    for (const a of body.administrators) {
      if (a?.dimension && a?.value) {
        const grant = adminGrant([{ dimension: a.dimension, value: a.value }], kindForDimension(a.dimension));
        if (!deduped.some((d) => d.digest === grant.digest)) deduped.push(grant);
      }
    }
    store[key] = deduped;
    return json({ administrators: deduped });
  }

  // ---- security rules (§14.2) — the reusable reach vocabulary -----------------------------------
  // CRUD over the named rule set. The server compiles the expression against the rule grammar; the mock can't run
  // the compiler, so it approximates validation with a balance/non-empty check — enough to exercise the 400 path.

  function handleSecurityOrderings(fullPath, method) {
    if (fullPath.indexOf('/security/orderings') < 0) return null;
    if (method !== 'GET') return problem(405, 'Method not allowed');
    return json({ orderings: securityOrderings.map((o) => structuredClone(o)) });
  }

  // Keyset pagination over (order, id) plus a case-insensitive q over claim type/value/description — the same contract
  // the durable store implements: order, filter, seek strictly past the opaque token, take `limit`, emit a nextPageToken
  // when more remain.
  function listSecurityBindingsPage(params) {
    const limit = Math.max(1, Number(params.get('limit')) || 50);
    const q = (params.get('q') || '').trim().toLowerCase();
    const tok = params.get('pageToken') ? atobSafe(params.get('pageToken')).split('\u0000') : null;
    const afterOrder = tok ? Number(tok[0]) : null;
    const afterId = tok ? tok[1] : null;
    const sorted = [...securityBindings].sort((a, b) => (a.order - b.order) || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
    const matched = q
      ? sorted.filter((b) => `${b.claimType} ${b.claimValue || ''} ${b.description || ''}`.toLowerCase().includes(q))
      : sorted;
    const start = tok ? matched.findIndex((b) => b.order > afterOrder || (b.order === afterOrder && b.id > afterId)) : 0;
    const from = start < 0 ? matched.length : start;
    const pageItems = matched.slice(from, from + limit);
    const more = from + limit < matched.length;
    const last = pageItems[pageItems.length - 1];
    const nextPageToken = more && last ? btoaSafe(`${last.order}\u0000${last.id}`) : null;
    return json({ bindings: pageItems.map((b) => structuredClone(b)), nextPageToken });
  }

  function handleSecurityBindings(fullPath, method, params, body) {
    const idx = fullPath.indexOf('/security/bindings');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);

    if (path === '/security/bindings') {
      if (method === 'GET') return listSecurityBindingsPage(params);
      if (method === 'POST') return createSecurityBinding(body);
      return problem(405, 'Method not allowed');
    }
    const idMatch = path.match(/^\/security\/bindings\/([^/]+)$/);
    if (idMatch) {
      const id = decodeURIComponent(idMatch[1]);
      const binding = securityBindings.find((b) => b.id === id);
      if (method === 'GET') {
        return binding ? json(structuredClone(binding)) : problem(404, 'Binding not found', `No security binding '${id}'.`);
      }
      if (method === 'PUT') {
        if (!binding) return problem(404, 'Binding not found', `No security binding '${id}'.`);
        if (!body?.claimType) return problem(400, 'Invalid binding', 'A claimType is required.');
        Object.assign(binding, normalizeBinding(body), { id, lastUpdatedBy: actingSubject(), lastUpdatedAt: iso(0), etag: nextEtag() });
        return json(structuredClone(binding));
      }
      if (method === 'DELETE') {
        const i = securityBindings.findIndex((b) => b.id === id);
        if (i < 0) return problem(404, 'Binding not found', `No security binding '${id}'.`);
        securityBindings.splice(i, 1);
        return new Response(null, { status: 204 });
      }
      return problem(405, 'Method not allowed');
    }
    return null;
  }

  // The verb grants default to a denied (empty) grant when omitted; the mock cannot resolve the caller's identity, so the
  // server's self-elevation 403 (§16.5.3) is exercised by the server tests, not here.
  function normalizeBinding(body) {
    const verb = (v) => (v && (v.unrestricted || (Array.isArray(v.ruleNames) && v.ruleNames.length > 0)) ? structuredClone(v) : { unrestricted: false });
    const b = {
      claimType: body.claimType,
      read: verb(body.read),
      write: verb(body.write),
      purge: verb(body.purge),
      order: Number.isFinite(body.order) ? body.order : 0,
    };
    if (body.claimValue != null) b.claimValue = body.claimValue;
    if (body.description) b.description = body.description;
    return b;
  }

  function createSecurityBinding(body) {
    if (!body?.claimType) return problem(400, 'Invalid binding', 'A claimType is required.');
    const binding = { id: `bind-${++nextBindingId}`, ...normalizeBinding(body), createdBy: actingSubject(), createdAt: iso(0), etag: nextEtag() };
    securityBindings.push(binding);
    return json(structuredClone(binding), 201);
  }

  // Keyset pagination over `name` plus a case-insensitive q over name/expression — the same contract the durable store
  // implements: order, filter, seek strictly past the opaque token, take `limit`, emit a nextPageToken when more remain.
  function listSecurityRulesPage(params) {
    const limit = Math.max(1, Number(params.get('limit')) || 50);
    const q = (params.get('q') || '').trim().toLowerCase();
    const after = params.get('pageToken') ? atobSafe(params.get('pageToken')) : null;
    const sorted = [...securityRules].sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));
    const matched = q
      ? sorted.filter((r) => r.name.toLowerCase().includes(q) || (r.expression || '').toLowerCase().includes(q))
      : sorted;
    const start = after ? matched.findIndex((r) => r.name > after) : 0;
    const from = start < 0 ? matched.length : start;
    const pageItems = matched.slice(from, from + limit);
    const more = from + limit < matched.length;
    const last = pageItems[pageItems.length - 1];
    const nextPageToken = more && last ? btoaSafe(last.name) : null;
    return json({ rules: pageItems.map((r) => structuredClone(r)), nextPageToken });
  }

  function handleSecurityRules(fullPath, method, params, body) {
    const idx = fullPath.indexOf('/security/rules');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);

    if (path === '/security/rules') {
      if (method === 'GET') return listSecurityRulesPage(params);
      if (method === 'POST') return createSecurityRule(body);
      return problem(405, 'Method not allowed');
    }
    const nameMatch = path.match(/^\/security\/rules\/([^/]+)$/);
    if (nameMatch) {
      const name = decodeURIComponent(nameMatch[1]);
      if (method === 'GET') {
        const rule = securityRules.find((r) => r.name === name);
        return rule ? json(structuredClone(rule)) : problem(404, 'Rule not found', `No security rule named '${name}'.`);
      }
      if (method === 'PUT') return updateSecurityRule(name, body);
      if (method === 'DELETE') return deleteSecurityRule(name);
      return problem(405, 'Method not allowed');
    }
    return null;
  }

  // A best-effort stand-in for the server's grammar compile: non-empty and balanced parentheses.
  function expressionProblem(expression) {
    const expr = (expression || '').trim();
    if (!expr) return problem(400, 'Invalid rule expression', 'The expression must not be empty.');
    let depth = 0;
    for (const ch of expr) {
      if (ch === '(') depth++;
      else if (ch === ')' && --depth < 0) return problem(400, 'Invalid rule expression', 'Unbalanced parentheses in the expression.');
    }
    if (depth !== 0) return problem(400, 'Invalid rule expression', 'Unbalanced parentheses in the expression.');
    return null;
  }

  function createSecurityRule(body) {
    if (!body?.name) return problem(400, 'Invalid rule', 'A rule name is required.');
    const bad = expressionProblem(body.expression);
    if (bad) return bad;
    if (securityRules.some((r) => r.name === body.name)) {
      return problem(409, 'Rule already exists', `A security rule named '${body.name}' already exists.`);
    }
    const rule = {
      name: body.name, expression: body.expression.trim(),
      ...(body.description ? { description: body.description } : {}),
      createdBy: actingSubject(), createdAt: iso(0), etag: nextEtag(),
    };
    securityRules.push(rule);
    return json(structuredClone(rule), 201);
  }

  function updateSecurityRule(name, body) {
    const rule = securityRules.find((r) => r.name === name);
    if (!rule) return problem(404, 'Rule not found', `No security rule named '${name}'.`);
    const bad = expressionProblem(body?.expression);
    if (bad) return bad;
    rule.expression = body.expression.trim();
    if (body.description != null) rule.description = body.description;
    else delete rule.description;
    rule.lastUpdatedBy = actingSubject();
    rule.lastUpdatedAt = iso(0);
    rule.etag = nextEtag();
    return json(structuredClone(rule));
  }

  function deleteSecurityRule(name) {
    const i = securityRules.findIndex((r) => r.name === name);
    if (i < 0) return problem(404, 'Rule not found', `No security rule named '${name}'.`);
    securityRules.splice(i, 1);
    return new Response(null, { status: 204 });
  }

  // ---- availability matrix (§7.8) — where a version is available -------------------------------

  function handleVersionAvailability(fullPath, method, params) {
    // Single-environment make/withdraw (§7.8): PUT/DELETE /catalog/{base}/versions/{n}/availability/{environment}.
    const one = fullPath.match(/\/catalog\/([^/]+)\/versions\/([^/]+)\/availability\/([^/]+)$/);
    if (one) {
      const base = decodeURIComponent(one[1]);
      const versionNumber = Number(one[2]);
      const environment = decodeURIComponent(one[3]);
      if (method === 'PUT') return makeVersionAvailable(base, versionNumber, environment);
      if (method === 'DELETE') return withdrawVersionAvailability(base, versionNumber, environment);
      return problem(405, 'Method not allowed');
    }
    const m = fullPath.match(/\/catalog\/([^/]+)\/versions\/([^/]+)\/availability\/?$/);
    if (!m) return null;
    if (method !== 'GET') return problem(405, 'Method not allowed');
    const base = decodeURIComponent(m[1]);
    const versionNumber = Number(m[2]);
    const entries = availabilityEntries
      .filter((a) => a.baseWorkflowId === base && a.versionNumber === versionNumber)
      .sort((a, b) => a.environment.localeCompare(b.environment));
    const limit = Math.max(1, Math.min(Number(params.get('limit')) || 50, 200));
    const offset = Number(params.get('pageToken')) || 0;
    const pageItems = entries.slice(offset, offset + limit).map((a) => structuredClone(a));
    const nextPageToken = offset + limit < entries.length ? String(offset + limit) : null;
    return json({ availability: pageItems, nextPageToken });
  }

  // Make a version available in an environment (§7.8) — idempotent, readiness-gated (the same gate as the promotion
  // approve path). The mock treats the demo user as an administrator of every environment, so it does not enforce 403.
  function makeVersionAvailable(base, versionNumber, environment) {
    const version = findVersion(base, versionNumber);
    if (!version) return problem(404, 'Workflow version not found', `Version ${versionNumber} of '${base}' does not exist.`);
    if (!findEnvironment(environment)) return notFoundEnvironment(environment);
    const denied = requireAdministrator(`make a version available in '${environment}'`, { environment });
    if (denied) return denied;
    const existing = availabilityEntries.find((a) => a.baseWorkflowId === base && a.versionNumber === versionNumber && a.environment === environment);
    if (existing) return json(structuredClone(existing)); // already available (idempotent → 200)
    const missing = (version.sources ?? []).filter((s) => !findUsableCredential(s.name, environment, base)).map((s) => s.name);
    if (missing.length > 0) {
      return problem(409, 'Environment not ready', `Version ${versionNumber} of '${base}' cannot be made available in '${environment}': no usable credential for ${missing.join(', ')}.`);
    }
    const entry = { baseWorkflowId: base, versionNumber, environment, createdBy: actingSubject(), createdAt: iso(0), etag: nextEtag() };
    availabilityEntries.push(entry);
    return json(structuredClone(entry), 201);
  }

  function withdrawVersionAvailability(base, versionNumber, environment) {
    const denied = requireAdministrator(`withdraw availability in '${environment}'`, { environment });
    if (denied) return denied;
    const i = availabilityEntries.findIndex((a) => a.baseWorkflowId === base && a.versionNumber === versionNumber && a.environment === environment);
    if (i < 0) return problem(404, 'Availability entry not found', `Version ${versionNumber} of '${base}' is not available in '${environment}'.`);
    availabilityEntries.splice(i, 1);
    return new Response(null, { status: 204 });
  }

  // ---- source registry (§7.6) -------------------------------------------------------------------
  // The registry collection (list omits the document) + a single source read (WITH document) + register. The
  // catalog's per-version embedded source (/catalog/.../sources/{n}) is a different endpoint, handled earlier.

  function handleSources(fullPath, method, params, body) {
    const m = fullPath.match(/\/sources(?:\/([^/]+))?\/?$/);
    if (!m) return null;
    if (fullPath.includes('/catalog/')) return null; // a version's embedded source, not the registry
    const name = m[1] ? decodeURIComponent(m[1]) : null;
    if (!name) {
      if (method === 'GET') return listSourcesPage(params);
      if (method === 'POST') return registerSource(body);
      return problem(405, 'Method not allowed');
    }
    if (method === 'GET') return getRegisteredSource(name);
    return problem(405, 'Method not allowed');
  }

  function listSourcesPage(params) {
    const limit = Math.max(1, Math.min(Number(params.get('limit')) || 50, 200));
    const sorted = [...sourceRegistry].sort((a, b) => a.name.localeCompare(b.name));
    const offset = Number(params.get('pageToken')) || 0;
    // The list omits each source's document (returned only on a single read).
    const pageItems = sorted.slice(offset, offset + limit).map(({ document, ...rest }) => structuredClone(rest));
    const nextPageToken = offset + limit < sorted.length ? String(offset + limit) : null;
    return json({ sources: pageItems, nextPageToken });
  }

  function getRegisteredSource(name) {
    const s = sourceRegistry.find((x) => x.name === name);
    if (!s) return problem(404, 'Source not found', `No source named '${name}'.`);
    return json(structuredClone(s)); // includes the document
  }

  function registerSource(body) {
    if (!body || !body.name || !body.type || body.document == null) {
      return problem(400, 'Invalid source', 'Provide a name, type, and document.');
    }
    if (sourceRegistry.some((s) => s.name === body.name)) {
      return problem(409, 'Source exists', `A source named '${body.name}' already exists.`);
    }
    const s = {
      name: body.name, type: body.type, document: body.document,
      displayName: body.displayName || body.name, description: body.description,
      managementTags: body.managementTags ?? [],
      createdBy: actingSubject(), createdAt: iso(0), etag: nextEtag(),
    };
    sourceRegistry.push(s);
    return json(structuredClone(s), 201);
  }

  // ---- deployment environments (§7.7) -----------------------------------------------------------
  // The governed environment registry: the collection (list/create), a single environment (read/update/delete), its
  // administrator set (§15-style resolved identities), and the workflow versions made available in it (§7.8). The
  // collection is ordered by name and keyset-paged with a numeric offset token, like the grantee typeahead. The mock
  // is identity-less and treats the demo user as an administrator of every environment, so it does not enforce the 403
  // the server applies to a non-administrator (consistent with the rest of the mock).

  function handleEnvironments(fullPath, method, params, body) {
    const idx = fullPath.indexOf('/environments');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);

    // /environments/{name}/administrators[...] and /environments/{name}/availability sub-routes.
    const memberMatch = path.match(/^\/environments\/([^/]+)\/administrators\/members\/([^/]+)$/);
    if (memberMatch && method === 'DELETE') {
      const name = decodeURIComponent(memberMatch[1]);
      if (!findEnvironment(name)) return notFoundEnvironment(name);
      return removeAdminFrom(environmentAdministrators, name, decodeURIComponent(memberMatch[2]), 'environment');
    }
    const membersMatch = path.match(/^\/environments\/([^/]+)\/administrators\/members$/);
    if (membersMatch && method === 'POST') {
      const name = decodeURIComponent(membersMatch[1]);
      if (!findEnvironment(name)) return notFoundEnvironment(name);
      return addAdminTo(environmentAdministrators, name, body);
    }
    const adminsMatch = path.match(/^\/environments\/([^/]+)\/administrators$/);
    if (adminsMatch) {
      const name = decodeURIComponent(adminsMatch[1]);
      if (!findEnvironment(name)) return notFoundEnvironment(name);
      if (method === 'GET') return json({ administrators: environmentAdministrators[name] ?? [] });
      if (method === 'PUT') return transferAdminTo(environmentAdministrators, name, body);
      return problem(405, 'Method not allowed');
    }
    const availMatch = path.match(/^\/environments\/([^/]+)\/availability\/?$/);
    if (availMatch) {
      const name = decodeURIComponent(availMatch[1]);
      if (method !== 'GET') return problem(405, 'Method not allowed');
      if (!findEnvironment(name)) return notFoundEnvironment(name);
      return listEnvironmentAvailabilityPage(name, params);
    }

    // /environments/{name}/runners/{runnerId}/authorization — authorize (POST) / revoke (DELETE) a runner (§5.5). Matched
    // here (before the top-level /runners handler) so the env prefix wins; env-admin governed (404 unknown, 403 non-admin).
    const runnerAuthMatch = path.match(/^\/environments\/([^/]+)\/runners\/([^/]+)\/authorization$/);
    if (runnerAuthMatch) {
      const name = decodeURIComponent(runnerAuthMatch[1]);
      const runnerId = decodeURIComponent(runnerAuthMatch[2]);
      if (!findEnvironment(name)) return notFoundEnvironment(name);
      const denied = requireAdministrator(`${method === 'DELETE' ? 'revoke' : 'authorize'} a runner for an environment`, { environment: name });
      if (denied) return denied;
      if (method === 'POST') return decideRunnerAuthorization(name, runnerId, 'Authorized', body);
      if (method === 'DELETE') return decideRunnerAuthorization(name, runnerId, 'Revoked', body);
      return problem(405, 'Method not allowed');
    }
    // /environments/{name}/runners — that environment's runner roster (all statuses unless filtered; env-admin governed).
    const runnerRosterMatch = path.match(/^\/environments\/([^/]+)\/runners\/?$/);
    if (runnerRosterMatch) {
      const name = decodeURIComponent(runnerRosterMatch[1]);
      if (method !== 'GET') return problem(405, 'Method not allowed');
      if (!findEnvironment(name)) return notFoundEnvironment(name);
      const denied = requireAdministrator('list the runner authorizations of an environment', { environment: name });
      if (denied) return denied;
      const status = params.get('status');
      let rows = runnerAuthorizations.filter((r) => r.environment === name);
      if (status) rows = rows.filter((r) => r.status === status);
      return pageRunnerAuths(rows, params);
    }

    // /environments/{name} — single environment read/update/delete.
    const oneMatch = path.match(/^\/environments\/([^/]+)\/?$/);
    if (oneMatch) {
      const name = decodeURIComponent(oneMatch[1]);
      if (method === 'GET') {
        const e = findEnvironment(name);
        return e ? json(structuredClone(e)) : notFoundEnvironment(name);
      }
      if (method === 'PUT') return updateEnvironment(name, body);
      if (method === 'DELETE') return deleteEnvironment(name);
      return problem(405, 'Method not allowed');
    }

    // /environments — collection list/create.
    if (/^\/environments\/?$/.test(path)) {
      if (method === 'GET') return listEnvironmentsPage(params);
      if (method === 'POST') return createEnvironment(body);
      return problem(405, 'Method not allowed');
    }
    return null;
  }

  function findEnvironment(name) {
    return environments.find((e) => e.name === name);
  }

  function notFoundEnvironment(name) {
    return problem(404, 'Environment not found', `No environment named '${name}'.`);
  }

  function listEnvironmentsPage(params) {
    const limit = Math.max(1, Math.min(Number(params.get('limit')) || 50, 200));
    const sorted = [...environments].sort((a, b) => a.name.localeCompare(b.name));
    const offset = Number(params.get('pageToken')) || 0;
    const pageItems = sorted.slice(offset, offset + limit).map((e) => structuredClone(e));
    const nextPageToken = offset + limit < sorted.length ? String(offset + limit) : null;
    return json({ environments: pageItems, nextPageToken });
  }

  function createEnvironment(body) {
    if (!body || !body.name) return problem(400, 'Invalid environment', 'A name is required.');
    if (findEnvironment(body.name)) return problem(409, 'Environment exists', `An environment named '${body.name}' already exists.`);
    const e = {
      name: body.name, displayName: body.displayName || body.name, description: body.description,
      managementTags: body.managementTags ?? [],
      createdBy: actingSubject(), createdAt: iso(0), etag: nextEtag(),
    };
    environments.push(e);
    // Creating an environment grants the creator administration of it (§7.7), mirroring catalog version creation.
    environmentAdministrators[e.name] = [adminGrant([{ dimension: 'sys:sub', value: actingSubject() }], 'person', 'You (creator)')];
    return json(structuredClone(e), 201);
  }

  function updateEnvironment(name, body) {
    const e = findEnvironment(name);
    if (!e) return notFoundEnvironment(name);
    // Only display name and description are mutable; the name, reach scope, and created-* audit fields are immutable.
    if (body?.displayName !== undefined) e.displayName = body.displayName;
    if (body?.description !== undefined) e.description = body.description;
    e.lastUpdatedBy = actingSubject(); e.lastUpdatedAt = iso(0); e.etag = nextEtag();
    return json(structuredClone(e));
  }

  function deleteEnvironment(name) {
    const i = environments.findIndex((e) => e.name === name);
    if (i < 0) return notFoundEnvironment(name);
    environments.splice(i, 1);
    // Cascade the mock's dependent state so nothing references a deleted environment.
    delete environmentAdministrators[name];
    for (let j = availabilityEntries.length - 1; j >= 0; j--) {
      if (availabilityEntries[j].environment === name) availabilityEntries.splice(j, 1);
    }
    return new Response(null, { status: 204 });
  }

  function listEnvironmentAvailabilityPage(name, params) {
    const entries = availabilityEntries
      .filter((a) => a.environment === name)
      .sort((a, b) => a.baseWorkflowId.localeCompare(b.baseWorkflowId) || (a.versionNumber - b.versionNumber));
    const limit = Math.max(1, Math.min(Number(params.get('limit')) || 50, 200));
    const offset = Number(params.get('pageToken')) || 0;
    const pageItems = entries.slice(offset, offset + limit).map((a) => structuredClone(a));
    const nextPageToken = offset + limit < entries.length ? String(offset + limit) : null;
    return json({ availability: pageItems, nextPageToken });
  }

  // ---- identity / grantee resolution (§16.5.4) --------------------------------------------------
  // The mock is identity-less, so the reach-filtering and complete-reporting are pre-baked into the seed; the
  // search models q / kind / source filtering and keyset paging so the picker's typeahead and Load-more work.

  function handleIdentity(fullPath, method, params) {
    const idx = fullPath.indexOf('/identity/');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);
    if (path === '/identity/grantees') {
      if (method !== 'GET') return problem(405, 'Method not allowed');
      return searchGrantees(params);
    }
    return null;
  }

  function searchGrantees(params) {
    const q = (params.get('q') || '').trim().toLowerCase();
    const kind = params.get('kind') || '';
    const source = params.get('source') || '';
    const limit = Math.max(1, Math.min(Number(params.get('limit')) || 20, 100));
    const matches = grantees.filter((g) => {
      if (kind && g.kind !== kind) return false;
      if (source && g.source !== source) return false;
      if (q && !`${g.value} ${g.label || ''}`.toLowerCase().includes(q)) return false;
      return true;
    });
    const offset = Number(params.get('pageToken')) || 0;
    const pageItems = matches.slice(offset, offset + limit).map((g) => structuredClone(g));
    const nextPageToken = offset + limit < matches.length ? String(offset + limit) : null;
    return json({ grantees: pageItems, nextPageToken });
  }

  // ---- access requests (§16.5) ------------------------------------------------------------------
  // Keyed off the active identity (actingSubject) and per-workflow administration, so the panel's views differ per persona:
  //   • scope=mine (or absent, no baseWorkflowId) → the caller's OWN requests (subjectClaimValue === actingSubject()).
  //   • scope=queue → the approver INBOX: only requests for workflows the caller administers (empty if they administer none).
  //   • baseWorkflowId → that one workflow's queue (visible only to an administrator of it).
  // The requester-only / administrator-only 403s are exercised by the server/CLI tests; state transitions ARE
  // modelled (a non-pending request conflicts, etc.) so the UI's optimistic flows and conflict banners are exercised.

  function handleAccessRequests(fullPath, method, params, body) {
    const idx = fullPath.indexOf('/accessRequests');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);

    if (/^\/accessRequests\/?$/.test(path)) {
      if (method === 'GET') return listAccessRequests(params);
      if (method === 'POST') return submitAccessRequest(body);
      return problem(405, 'Method not allowed');
    }
    const actionMatch = path.match(/^\/accessRequests\/([^/]+)\/(approve|approveAsEligible|deny|withdraw|revoke)$/);
    if (actionMatch && method === 'POST') {
      return decideAccessRequest(decodeURIComponent(actionMatch[1]), actionMatch[2], body);
    }
    const idMatch = path.match(/^\/accessRequests\/([^/]+)$/);
    if (idMatch && method === 'GET') {
      const r = accessRequests.find((x) => x.id === decodeURIComponent(idMatch[1]));
      return r ? json(r) : notFoundAccessRequest(decodeURIComponent(idMatch[1]));
    }
    return null;
  }

  // Keyset pagination over (createdAt, id) oldest-first plus the status / base-workflow filters — the same contract the
  // durable store implements: filter, order, seek strictly past the opaque token, take `limit`, emit a nextPageToken.
  function listAccessRequests(params) {
    const status = params.get('status');
    const base = params.get('baseWorkflowId');
    const scope = params.get('scope');
    const limit = Math.max(1, Number(params.get('limit')) || 50);
    let rows = [...accessRequests].sort((a, b) => (Date.parse(a.createdAt) - Date.parse(b.createdAt)) || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
    if (base) {
      // A single workflow's queue — visible only to an administrator of it (others see an empty queue).
      rows = administersWorkflow(base) ? rows.filter((r) => r.baseWorkflowId === base) : [];
    } else if (scope === 'queue') {
      // The approver inbox: only the requests for workflows the caller administers (empty if they administer none).
      rows = rows.filter((r) => administersWorkflow(r.baseWorkflowId));
    } else {
      // The caller's own requests (scope=mine / absent).
      rows = rows.filter((r) => r.subjectClaimValue === actingSubject());
    }
    if (status) rows = rows.filter((r) => r.status === status);
    const tok = params.get('pageToken') ? atobSafe(params.get('pageToken')).split(' ') : null;
    const afterTicks = tok ? Number(tok[0]) : null;
    const afterId = tok ? tok[1] : null;
    const start = tok ? rows.findIndex((r) => { const t = Date.parse(r.createdAt); return t > afterTicks || (t === afterTicks && r.id > afterId); }) : 0;
    const from = start < 0 ? rows.length : start;
    const pageItems = rows.slice(from, from + limit);
    const more = from + limit < rows.length;
    const last = pageItems[pageItems.length - 1];
    const nextPageToken = more && last ? btoaSafe(`${Date.parse(last.createdAt)} ${last.id}`) : null;
    return json({ accessRequests: pageItems, nextPageToken });
  }

  function submitAccessRequest(body) {
    if (!body?.baseWorkflowId || !Array.isArray(body.requestedScopes) || body.requestedScopes.length === 0) {
      return problem(400, 'Invalid access request', 'A baseWorkflowId and at least one requestedScope are required.');
    }
    const r = {
      id: `req-${++etagSeq}`, baseWorkflowId: body.baseWorkflowId, requestedScopes: body.requestedScopes,
      subjectClaimType: 'preferred_username', subjectClaimValue: actingSubject(), requesterLabel: actingSubject(),
      reason: body.reason, requestedDurationSeconds: body.requestedDurationSeconds,
      status: 'Pending', createdBy: actingSubject(), createdAt: iso(0), etag: nextEtag(),
    };
    accessRequests.push(r);
    return json(r, 201);
  }

  function decideAccessRequest(id, action, body) {
    const r = accessRequests.find((x) => x.id === id);
    if (!r) return notFoundAccessRequest(id);
    // approve/approveAsEligible/deny/revoke are workflow-administrator decisions; withdraw is the requester's own.
    if (action !== 'withdraw') {
      const denied = requireAdministrator(`${action} an access request`, { workflow: r.baseWorkflowId });
      if (denied) return denied;
    }
    const reason = body?.reason;
    const requirePending = (verb) => r.status === 'Pending' ? null
      : problem(409, 'Invalid access-request state', `Request '${id}' is ${r.status}; only a pending request can be ${verb}.`);

    if (action === 'approve' || action === 'approveAsEligible') {
      const conflict = requirePending('approved');
      if (conflict) return conflict;
      const eligible = action === 'approveAsEligible';
      r.status = eligible ? 'Eligible' : 'Approved';
      r.decidedBy = actingSubject(); r.decidedAt = iso(0); r.decisionReason = reason;
      r.grantedBindingId = `bind-${++etagSeq}`;
      const windowSeconds = eligible ? body?.eligibilityWindowSeconds : (r.requestedDurationSeconds ?? 8 * 3600);
      r.grantedUntil = windowSeconds ? iso(windowSeconds * 1000) : undefined;
    } else if (action === 'deny') {
      const conflict = requirePending('denied');
      if (conflict) return conflict;
      r.status = 'Denied'; r.decidedBy = actingSubject(); r.decidedAt = iso(0); r.decisionReason = reason;
    } else if (action === 'withdraw') {
      const conflict = requirePending('withdrawn');
      if (conflict) return conflict;
      r.status = 'Withdrawn'; r.decidedAt = iso(0); r.decisionReason = reason;
    } else if (action === 'revoke') {
      if (r.status !== 'Approved') {
        return problem(409, 'Invalid access-request state', `Request '${id}' is ${r.status}; only an approved grant can be revoked.`);
      }
      r.status = 'Revoked'; r.decidedBy = actingSubject(); r.decidedAt = iso(0); r.decisionReason = reason; r.grantedUntil = undefined;
    }
    r.etag = nextEtag();
    return json(r);
  }

  function notFoundAccessRequest(id) {
    return problem(404, 'Access request not found', `No access request with id '${id}'.`);
  }

  // ---- availability ("promotion") requests (§7.8) ----------------------------------------------
  // Mirrors the access-request inbox, parameterised by ENVIRONMENT: a requester asks for a version to be made available;
  // the target environment's administrators approve (making it available) or deny; the requester may withdraw their own.
  //   • scope=mine (or absent, no environment) → the demo user's OWN requests.
  //   • scope=queue → the approver INBOX across all environments (the mock treats the demo user as an administrator).
  //   • environment → that one environment's queue.
  function handleAvailabilityRequests(fullPath, method, params, body) {
    const idx = fullPath.indexOf('/availabilityRequests');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);

    if (/^\/availabilityRequests\/?$/.test(path)) {
      if (method === 'GET') return listAvailabilityRequests(params);
      if (method === 'POST') return submitAvailabilityRequest(body);
      return problem(405, 'Method not allowed');
    }
    const actionMatch = path.match(/^\/availabilityRequests\/([^/]+)\/(approve|deny|withdraw)$/);
    if (actionMatch && method === 'POST') {
      return decideAvailabilityRequest(decodeURIComponent(actionMatch[1]), actionMatch[2], body);
    }
    const idMatch = path.match(/^\/availabilityRequests\/([^/]+)$/);
    if (idMatch && method === 'GET') {
      const r = availabilityRequests.find((x) => x.id === decodeURIComponent(idMatch[1]));
      return r ? json(r) : notFoundAvailabilityRequest(decodeURIComponent(idMatch[1]));
    }
    return null;
  }

  // Keyset pagination over (createdAt, id) oldest-first plus the status / environment filters — the same contract the
  // durable store implements: filter, order, seek strictly past the opaque token, take `limit`, emit a nextPageToken.
  function listAvailabilityRequests(params) {
    const status = params.get('status');
    const environment = params.get('environment');
    const scope = params.get('scope');
    const limit = Math.max(1, Number(params.get('limit')) || 50);
    let rows = [...availabilityRequests].sort((a, b) => (Date.parse(a.createdAt) - Date.parse(b.createdAt)) || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
    if (environment) {
      // A single environment's queue — visible only to an administrator of it (others see an empty queue).
      rows = administersEnvironment(environment) ? rows.filter((r) => r.environment === environment) : [];
    } else if (scope === 'queue') {
      // The approver inbox: only the requests for environments the caller administers (empty if they administer none).
      rows = rows.filter((r) => administersEnvironment(r.environment));
    } else {
      // The caller's own requests (scope=mine / absent).
      rows = rows.filter((r) => r.createdBy === actingSubject());
    }
    if (status) rows = rows.filter((r) => r.status === status);
    const tok = params.get('pageToken') ? atobSafe(params.get('pageToken')).split(' ') : null;
    const afterTicks = tok ? Number(tok[0]) : null;
    const afterId = tok ? tok[1] : null;
    const start = tok ? rows.findIndex((r) => { const t = Date.parse(r.createdAt); return t > afterTicks || (t === afterTicks && r.id > afterId); }) : 0;
    const from = start < 0 ? rows.length : start;
    const pageItems = rows.slice(from, from + limit);
    const more = from + limit < rows.length;
    const last = pageItems[pageItems.length - 1];
    const nextPageToken = more && last ? btoaSafe(`${Date.parse(last.createdAt)} ${last.id}`) : null;
    return json({ availabilityRequests: pageItems, nextPageToken });
  }

  function submitAvailabilityRequest(body) {
    if (!body?.baseWorkflowId || body.versionNumber == null || !body?.environment) {
      return problem(400, 'Invalid availability request', 'A baseWorkflowId, versionNumber, and environment are required.');
    }
    const r = {
      id: `areq-${++etagSeq}`, baseWorkflowId: body.baseWorkflowId, versionNumber: Number(body.versionNumber), environment: body.environment,
      reason: body.reason, status: 'Pending',
      subjectClaimType: 'preferred_username', subjectClaimValue: actingSubject(), requesterLabel: actingSubject(),
      createdBy: actingSubject(), createdAt: iso(0), etag: nextEtag(),
    };
    availabilityRequests.push(r);
    return json(r, 201);
  }

  function decideAvailabilityRequest(id, action, body) {
    const r = availabilityRequests.find((x) => x.id === id);
    if (!r) return notFoundAvailabilityRequest(id);
    // approve/deny are environment-administrator decisions; withdraw is the requester's own.
    if (action !== 'withdraw') {
      const denied = requireAdministrator(`${action} a promotion request`, { environment: r.environment });
      if (denied) return denied;
    }
    const reason = body?.reason;
    const requirePending = (verb) => r.status === 'Pending' ? null
      : problem(409, 'Invalid availability-request state', `Request '${id}' is ${r.status}; only a pending request can be ${verb}.`);

    if (action === 'approve') {
      const conflict = requirePending('approved');
      if (conflict) return conflict;
      // Readiness (§7.7): the version must still exist and every source it references must have a credential in the target
      // environment — the same gate the server applies to a direct make-available. Approving makes the version available.
      const version = findVersion(r.baseWorkflowId, r.versionNumber);
      if (!version) {
        return problem(409, 'Workflow version no longer exists', `Version ${r.versionNumber} of '${r.baseWorkflowId}' no longer exists, so the request cannot be approved.`);
      }
      const missing = (version.sources ?? []).filter((s) => !findUsableCredential(s.name, r.environment, r.baseWorkflowId)).map((s) => s.name);
      if (missing.length > 0) {
        return problem(409, 'Environment not ready', `Version ${r.versionNumber} of '${r.baseWorkflowId}' cannot be made available in '${r.environment}': no usable credential for ${missing.join(', ')}.`);
      }
      r.status = 'Approved'; r.decidedBy = actingSubject(); r.decidedAt = iso(0); r.decisionReason = reason;
      // Approval makes the version available — record it in the matrix (idempotent).
      if (!availabilityEntries.some((a) => a.baseWorkflowId === r.baseWorkflowId && a.versionNumber === r.versionNumber && a.environment === r.environment)) {
        availabilityEntries.push({ baseWorkflowId: r.baseWorkflowId, versionNumber: r.versionNumber, environment: r.environment, createdBy: r.decidedBy, createdAt: iso(0), etag: nextEtag() });
      }
    } else if (action === 'deny') {
      const conflict = requirePending('denied');
      if (conflict) return conflict;
      r.status = 'Denied'; r.decidedBy = actingSubject(); r.decidedAt = iso(0); r.decisionReason = reason;
    } else if (action === 'withdraw') {
      const conflict = requirePending('withdrawn');
      if (conflict) return conflict;
      r.status = 'Withdrawn'; r.decidedAt = iso(0); r.decisionReason = reason;
    }
    r.etag = nextEtag();
    return json(r);
  }

  function notFoundAvailabilityRequest(id) {
    return problem(404, 'Availability request not found', `No availability request with id '${id}'.`);
  }

  return {
    runs,
    catalog,
    credentials,
    environments,
    administrators,
    environmentAdministrators,
    accessRequests,
    availabilityRequests,
    availabilityEntries,
    runners,
    runnerAuthorizations,
    // Switch the acting persona (capability scopes + whether the caller administers governance targets) so the demo can
    // show the gated-elevation model from both sides. Defaults to 'administrator'.
    setPersona: (name) => { persona = personaState(name); return persona.name; },
    get persona() { return persona.name; },
    fetch: async (url, init) => {
      if (latency) await new Promise((r) => setTimeout(r, latency));
      return handle(url, init);
    },
  };
}

function parseMs(value) {
  if (!value) return null;
  const ms = Date.parse(value);
  return Number.isNaN(ms) ? null : ms;
}

// Minimal ZIP reader (central-directory walk) → Map<entryName, utf8 text>. Handles the store method (the
// in-browser packer) and DEFLATE (CLI-built packages, via DecompressionStream). Returns null if not a ZIP.
function btoaSafe(s) { return typeof btoa === 'function' ? btoa(s) : Buffer.from(s).toString('base64'); }
function atobSafe(s) { if (!s) return ''; return typeof atob === 'function' ? atob(s) : Buffer.from(s, 'base64').toString(); }
