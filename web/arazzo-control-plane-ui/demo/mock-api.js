// In-memory mock of the Arazzo Control Plane API for the demo (and for tests).
//
// Returns a `fetch`-compatible function implementing all six operations with RFC 9457 errors and keyset
// pagination, so the kit is fully explorable with no server:
//
//   import { createMockControlPlane } from './mock-api.js';
//   import { ArazzoControlPlaneClient } from '../src/arazzo-client.js';
//   const mock = createMockControlPlane();
//   const client = new ArazzoControlPlaneClient({ baseUrl: 'https://mock/arazzo/v1', fetch: mock.fetch });

const TERMINAL = new Set(['Completed', 'Cancelled']);
let etagSeq = 1000;

function nextEtag() {
  return `"etag-${++etagSeq}"`;
}

function iso(offsetMs) {
  return new Date(Date.now() + offsetMs).toISOString();
}

function seedRuns() {
  const min = 60000;
  const hr = 60 * min;
  const day = 24 * hr;
  return [
    {
      id: 'run-7f3a9c21', workflowId: 'adopt-pet-v1', status: 'Faulted', cursor: 1,
      createdAt: iso(-3 * hr), updatedAt: iso(-2 * min), etag: nextEtag(),
      fault: { stepId: 'reservePayment', attempt: 3, error: 'HttpRequestException: 502 from payments (upstream)', at: iso(-2 * min) },
      _errorType: 'HttpRequestException',
      correlationId: '7f3a9c21d4e54a1b9c0d1e2f3a4b5c6d', tags: ['tenant-42', 'priority'],
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
      correlationId: 'c9d8e7f6a5b4c3d2e1f0a9b8c7d6e5f4', tags: ['prod', 'billing'],
    },
    {
      // Faulted at verifyIdentity — outputs include a nested 'applicant' object, an enum 'method', and a 'flags' enum array.
      id: 'run-aa11bb22', workflowId: 'onboard-customer-v1', status: 'Faulted', cursor: 1,
      createdAt: iso(-100 * min), updatedAt: iso(-15 * min), etag: nextEtag(),
      fault: { stepId: 'verifyIdentity', attempt: 4, error: 'KycProviderException: document image unreadable', at: iso(-15 * min) },
      _errorType: 'KycProviderException',
      correlationId: 'aa11bb22cc33dd44ee55ff6677889900', tags: ['tenant-7', 'kyc'],
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

function workflowDoc(workflowId, title, description) {
  const base = workflowId.replace(/-v\d+$/, '');
  const stepIds = STEP_SETS[base] || ['start', 'process', 'finish'];
  return {
    arazzo: '1.1.0',
    info: { title, description },
    sourceDescriptions: [
      { name: 'petstore', url: './petstore.json', type: 'openapi' },
      { name: 'events', url: './events.json', type: 'asyncapi' },
    ],
    workflows: [{ workflowId, steps: stepIds.map((stepId) => ({ stepId, operationId: stepId })) }],
  };
}

function seedCatalog() {
  const hr = 60 * 60000;
  const day = 24 * hr;
  const sources = {
    petstore: { openapi: '3.1.0', info: { title: 'Petstore', version: '1.0.0' } },
    events: { asyncapi: '3.0.0', info: { title: 'Events', version: '1.0.0' } },
  };
  const sourceRefs = [{ name: 'petstore', type: 'openapi' }, { name: 'events', type: 'asyncapi' }];
  const teamA = { name: 'Reconciliation Team', email: 'reconcile@example.com', team: 'Platform', url: 'https://runbooks.example.com/nightly-reconcile' };
  const teamB = { name: 'Onboarding Team', email: 'onboarding@example.com' };
  const v = (base, n, status, title, owner, tags, ageDays, extra = {}) => ({
    baseWorkflowId: base, versionNumber: n, workflowId: `${base}-v${n}`,
    title, description: `${title} — versioned workflow.`, status, tags, owner, sources: sourceRefs,
    hash: `${base}${n}`.padEnd(64, '0'),
    createdBy: 'alice@example.com', createdAt: iso(-ageDays * day),
    _workflow: workflowDoc(`${base}-v${n}`, title, `${title} — versioned workflow.`),
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
    usageGrants: extra.usageGrants ?? [],
    description: extra.description,
    expiresAt: extra.expiresAt,
    rotatedAt: extra.rotatedAt,
    createdBy: 'alice@example.com', createdAt: iso(-30 * day), etag: nextEtag(),
  });
  return [
    b('petstore', 'production', 'apiKey', [{ name: 'value', ref: 'keyvault://petstore-key#3' }], { config: [{ key: 'parameterName', value: 'X-Api-Key' }], expiresAt: iso(20 * day), description: 'Petstore API key.' }),
    b('billing', 'production', 'oauth2ClientCredentials', [{ name: 'clientSecret', ref: 'vault://kv/billing#secret' }], { config: [{ key: 'tokenUrl', value: 'https://idp.example.com/oauth/token' }, { key: 'clientId', value: 'billing-client' }], usageGrants: [{ dimension: 'workflow', value: 'nightly-reconcile' }], expiresAt: iso(3 * day) }),
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
    usageGrants: b.usageGrants ?? [],
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

// ---- workflow administration (§15) — deployment-mapped {dimension,value} identities ------------

function seedAdministrators() {
  // Keyed by baseWorkflowId; each administrator is a {dimension, value} grant.
  return {
    'nightly-reconcile': [{ dimension: 'tenant', value: 'platform' }],
    'onboard-customer': [{ dimension: 'tenant', value: 'platform' }, { dimension: 'tenant', value: 'growth' }],
  };
}

function sameIdentity(a, b) {
  return a.dimension === b.dimension && a.value === b.value;
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
export function createMockControlPlane(options = {}) {
  const runs = options.seed ? structuredClone(options.seed) : seedRuns();
  const catalog = options.catalogSeed ? structuredClone(options.catalogSeed) : seedCatalog();
  const credentials = options.credentialsSeed ? structuredClone(options.credentialsSeed) : seedCredentials();
  const administrators = options.administratorsSeed ? structuredClone(options.administratorsSeed) : seedAdministrators();
  const accessRequests = options.accessRequestsSeed ? structuredClone(options.accessRequestsSeed) : seedAccessRequests();
  const latency = options.latencyMs ?? 250;
  const find = (id) => runs.find((r) => r.id === id);
  const findVersion = (base, n) => catalog.find((v) => v.baseWorkflowId === base && v.versionNumber === Number(n));
  const findCredential = (s, e) => credentials.find((c) => c.sourceName === s && c.environment === e);

  async function handle(url, init = {}) {
    const method = (init.method || 'GET').toUpperCase();
    const u = new URL(url, 'https://mock');
    const path = u.pathname;
    const isForm = typeof FormData !== 'undefined' && init.body instanceof FormData;
    const body = init.body && !isForm ? JSON.parse(init.body) : undefined;

    const catalogResponse = await handleCatalog(path, method, u.searchParams, body, isForm ? init.body : null);
    if (catalogResponse) return catalogResponse;

    const credentialsResponse = handleCredentials(path, method, u.searchParams, body);
    if (credentialsResponse) return credentialsResponse;

    const administratorsResponse = handleAdministrators(path, method, body);
    if (administratorsResponse) return administratorsResponse;

    const accessRequestsResponse = handleAccessRequests(path, method, u.searchParams, body);
    if (accessRequestsResponse) return accessRequestsResponse;

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

      if (!action && method === 'GET') return json(toDetail(run));
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
      if (!sub && method === 'GET') return json(toCatalogSummary(v));
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
    const q = (params.get('q') || '').toLowerCase();
    if (q && !v.title.toLowerCase().includes(q) && !(v.description || '').toLowerCase().includes(q)) return false;
    const owner = (params.get('owner') || '').toLowerCase();
    if (owner && !v.owner.name.toLowerCase().includes(owner) && !v.owner.email.toLowerCase().includes(owner)) return false;
    const wantTags = params.getAll('tag').filter(Boolean);
    if (wantTags.length > 0 && !wantTags.every((t) => (v.tags ?? []).includes(t))) return false;
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

  function searchCatalog(params) {
    return pageCatalog(catalog.filter((v) => matchesCatalog(v, params)), params);
  }

  function listCatalogVersions(base, params) {
    return pageCatalog(catalog.filter((v) => v.baseWorkflowId === base), params);
  }

  function updateVersion(v, patch) {
    if (patch?.owner) v.owner = patch.owner;
    if (Array.isArray(patch?.tags)) v.tags = patch.tags;
    if (patch?.status && patch.status !== v.status) {
      const newlyObsolete = patch.status === 'Obsolete';
      v.status = patch.status;
      v.obsoletedBy = newlyObsolete ? 'demo' : null;
      v.obsoletedAt = newlyObsolete ? iso(0) : null;
    }
    v.lastUpdatedBy = 'demo';
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
      const entries = await readZip(await pkg.arrayBuffer());
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
    const v = {
      baseWorkflowId: base, versionNumber, workflowId,
      title, description, status: 'Active',
      tags: form.getAll('tags'), owner, sources: sourceRefs, hash: `${base}${versionNumber}`.padEnd(64, '0'),
      createdBy: 'demo', createdAt: iso(0),
      _workflow: workflowDoc,
      _sources: sources,
    };
    catalog.push(v);
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
    if (findCredential(body.sourceName, body.environment)) {
      return problem(409, 'Credential already exists', `A binding for '${body.sourceName}@${body.environment}' already exists.`);
    }
    const b = {
      id: `cred-${body.sourceName}-${body.environment}`,
      sourceName: body.sourceName, environment: body.environment, authKind: body.authKind,
      secretRefs: body.secretRefs, config: body.config ?? [], managementTags: body.managementTags ?? [],
      usageGrants: body.usageGrants ?? [], description: body.description,
      expiresAt: body.expiresAt, rotatedAt: body.rotatedAt,
      createdBy: 'demo', createdAt: iso(0), etag: nextEtag(),
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
    b.lastUpdatedBy = 'demo';
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

    const memberMatch = path.match(/^\/administrators\/([^/]+)\/members\/([^/]+)\/([^/]+)$/);
    if (memberMatch && method === 'DELETE') {
      return removeAdministrator(decodeURIComponent(memberMatch[1]), decodeURIComponent(memberMatch[2]), decodeURIComponent(memberMatch[3]));
    }
    const membersMatch = path.match(/^\/administrators\/([^/]+)\/members$/);
    if (membersMatch && method === 'POST') {
      return addAdministrator(decodeURIComponent(membersMatch[1]), body);
    }
    const baseMatch = path.match(/^\/administrators\/([^/]+)$/);
    if (baseMatch) {
      const base = decodeURIComponent(baseMatch[1]);
      if (method === 'GET') return json({ administrators: administrators[base] ?? [] });
      if (method === 'PUT') return transferAdministration(base, body);
      return problem(405, 'Method not allowed');
    }
    return null;
  }

  function addAdministrator(base, identity) {
    if (!identity?.dimension || !identity?.value) return problem(400, 'Invalid administrator identity', 'An identity of { dimension, value } is required.');
    const set = administrators[base] ?? (administrators[base] = []);
    if (!set.some((a) => sameIdentity(a, identity))) set.push({ dimension: identity.dimension, value: identity.value });
    return json({ administrators: set });
  }

  function removeAdministrator(base, dimension, value) {
    const set = administrators[base] ?? [];
    const i = set.findIndex((a) => sameIdentity(a, { dimension, value }));
    if (i >= 0) {
      if (set.length === 1) return problem(409, 'Cannot remove the last administrator', 'A workflow must always have at least one administrator.');
      set.splice(i, 1);
    }
    return json({ administrators: set });
  }

  function transferAdministration(base, body) {
    if (!Array.isArray(body?.administrators) || body.administrators.length === 0) {
      return problem(400, 'Invalid administrator set', 'At least one administrator is required.');
    }
    const deduped = [];
    for (const a of body.administrators) {
      if (a?.dimension && a?.value && !deduped.some((d) => sameIdentity(d, a))) deduped.push({ dimension: a.dimension, value: a.value });
    }
    administrators[base] = deduped;
    return json({ administrators: deduped });
  }

  // ---- access requests (§16.5) ------------------------------------------------------------------
  // The mock is identity-less, so "my requests" (no baseWorkflowId) returns the whole list and the workflow
  // queue (with baseWorkflowId) filters to that workflow; the requester-only / administrator-only 403s are
  // exercised by the server/CLI tests and by component tests with a fake client. State transitions ARE modelled
  // (a non-pending request conflicts, etc.) so the UI's optimistic flows and conflict banners are exercised.

  function handleAccessRequests(fullPath, method, params, body) {
    const idx = fullPath.indexOf('/accessRequests');
    if (idx < 0) return null;
    const path = fullPath.slice(idx);

    if (/^\/accessRequests\/?$/.test(path)) {
      if (method === 'GET') return listAccessRequests(params);
      if (method === 'POST') return submitAccessRequest(body);
      return problem(405, 'Method not allowed');
    }
    const actionMatch = path.match(/^\/accessRequests\/([^/]+)\/(approve|approve-as-eligible|deny|withdraw|revoke)$/);
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

  function listAccessRequests(params) {
    const status = params.get('status');
    const base = params.get('baseWorkflowId');
    let rows = [...accessRequests].sort((a, b) => Date.parse(a.createdAt) - Date.parse(b.createdAt));
    if (base) rows = rows.filter((r) => r.baseWorkflowId === base);
    if (status) rows = rows.filter((r) => r.status === status);
    return json({ accessRequests: rows });
  }

  function submitAccessRequest(body) {
    if (!body?.baseWorkflowId || !Array.isArray(body.requestedScopes) || body.requestedScopes.length === 0) {
      return problem(400, 'Invalid access request', 'A baseWorkflowId and at least one requestedScope are required.');
    }
    const r = {
      id: `req-${++etagSeq}`, baseWorkflowId: body.baseWorkflowId, requestedScopes: body.requestedScopes,
      subjectClaimType: 'preferred_username', subjectClaimValue: 'demo-user', requesterLabel: 'demo-user',
      reason: body.reason, requestedDurationSeconds: body.requestedDurationSeconds,
      status: 'Pending', createdBy: 'demo-user', createdAt: iso(0), etag: nextEtag(),
    };
    accessRequests.push(r);
    return json(r, 201);
  }

  function decideAccessRequest(id, action, body) {
    const r = accessRequests.find((x) => x.id === id);
    if (!r) return notFoundAccessRequest(id);
    const reason = body?.reason;
    const requirePending = (verb) => r.status === 'Pending' ? null
      : problem(409, 'Invalid access-request state', `Request '${id}' is ${r.status}; only a pending request can be ${verb}.`);

    if (action === 'approve' || action === 'approve-as-eligible') {
      const conflict = requirePending('approved');
      if (conflict) return conflict;
      const eligible = action === 'approve-as-eligible';
      r.status = eligible ? 'Eligible' : 'Approved';
      r.decidedBy = 'boss'; r.decidedAt = iso(0); r.decisionReason = reason;
      r.grantedBindingId = `bind-${++etagSeq}`;
      const windowSeconds = eligible ? body?.eligibilityWindowSeconds : (r.requestedDurationSeconds ?? 8 * 3600);
      r.grantedUntil = windowSeconds ? iso(windowSeconds * 1000) : undefined;
    } else if (action === 'deny') {
      const conflict = requirePending('denied');
      if (conflict) return conflict;
      r.status = 'Denied'; r.decidedBy = 'boss'; r.decidedAt = iso(0); r.decisionReason = reason;
    } else if (action === 'withdraw') {
      const conflict = requirePending('withdrawn');
      if (conflict) return conflict;
      r.status = 'Withdrawn'; r.decidedAt = iso(0); r.decisionReason = reason;
    } else if (action === 'revoke') {
      if (r.status !== 'Approved') {
        return problem(409, 'Invalid access-request state', `Request '${id}' is ${r.status}; only an approved grant can be revoked.`);
      }
      r.status = 'Revoked'; r.decidedBy = 'boss'; r.decidedAt = iso(0); r.decisionReason = reason; r.grantedUntil = undefined;
    }
    r.etag = nextEtag();
    return json(r);
  }

  function notFoundAccessRequest(id) {
    return problem(404, 'Access request not found', `No access request with id '${id}'.`);
  }

  return {
    runs,
    catalog,
    credentials,
    administrators,
    accessRequests,
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
async function readZip(arrayBuffer) {
  const dv = new DataView(arrayBuffer);
  const bytes = new Uint8Array(arrayBuffer);
  const dec = new TextDecoder();
  let eocd = -1;
  for (let i = dv.byteLength - 22; i >= 0; i--) {
    if (dv.getUint32(i, true) === 0x06054b50) { eocd = i; break; }
  }
  if (eocd < 0) return null;
  const count = dv.getUint16(eocd + 10, true);
  let p = dv.getUint32(eocd + 16, true);
  const out = new Map();
  for (let n = 0; n < count; n++) {
    if (dv.getUint32(p, true) !== 0x02014b50) break;
    const method = dv.getUint16(p + 10, true);
    const compSize = dv.getUint32(p + 20, true);
    const nameLen = dv.getUint16(p + 28, true);
    const extraLen = dv.getUint16(p + 30, true);
    const commentLen = dv.getUint16(p + 32, true);
    const localOff = dv.getUint32(p + 42, true);
    const name = dec.decode(bytes.subarray(p + 46, p + 46 + nameLen));
    const lhNameLen = dv.getUint16(localOff + 26, true);
    const lhExtraLen = dv.getUint16(localOff + 28, true);
    const dataStart = localOff + 30 + lhNameLen + lhExtraLen;
    const comp = bytes.subarray(dataStart, dataStart + compSize);
    if (method === 0) {
      out.set(name, dec.decode(comp));
    } else {
      const stream = new Response(comp).body.pipeThrough(new DecompressionStream('deflate-raw'));
      out.set(name, dec.decode(await new Response(stream).arrayBuffer()));
    }
    p += 46 + nameLen + extraLen + commentLen;
  }
  return out;
}

function btoaSafe(s) { return typeof btoa === 'function' ? btoa(s) : Buffer.from(s).toString('base64'); }
function atobSafe(s) { if (!s) return ''; return typeof atob === 'function' ? atob(s) : Buffer.from(s, 'base64').toString(); }
