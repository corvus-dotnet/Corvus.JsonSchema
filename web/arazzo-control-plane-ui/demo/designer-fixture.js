// A realistic Arazzo 1.1 document for the workflow-designer demo and the graph-projection tests.
//
// Deliberately exercises every §6.2 projection shape: OpenAPI operation steps, an AsyncAPI channel
// step (receive + correlationId + timeout), a sub-workflow step (workflowId binding), local
// onSuccess/onFailure actions (goto + retry + end), a reusable failure-action reference into
// `components`, workflow-level failureActions (the inherited-defaults layer), and typed
// inputs/outputs with runtime expressions.

export const designerFixture = {
  arazzo: '1.1.0',
  info: {
    title: 'Order processing',
    version: '1.0.0',
    description: 'Validate, authorize, confirm and capture an order payment.',
  },
  sourceDescriptions: [
    { name: 'payments', url: './specs/payments.openapi.json', type: 'openapi' },
    { name: 'order-events', url: './specs/order-events.asyncapi.json', type: 'asyncapi' },
  ],
  workflows: [
    {
      workflowId: 'place-order',
      summary: 'The happy path: validate, authorize, await confirmation, capture.',
      inputs: {
        type: 'object',
        required: ['orderId', 'amount'],
        properties: {
          orderId: { type: 'string' },
          amount: { type: 'number' },
          customerEmail: { type: 'string', format: 'email' },
        },
      },
      // The inherited-defaults layer: any step without a matching local failure action falls back here.
      failureActions: [
        { name: 'give-up', type: 'end' },
      ],
      steps: [
        {
          stepId: 'validate-order',
          description: 'Check the order is well-formed and in stock.',
          operationId: 'validateOrder',
          parameters: [
            { name: 'orderId', in: 'path', value: '$inputs.orderId' },
            // A COMPLEX parameter: a deepObject query value edited structurally in the inspector.
            { name: 'checks', in: 'query', value: { inventory: true, fraud: true, priceTolerance: 0.05 } },
          ],
          successCriteria: [
            { condition: '$statusCode == 200' },
          ],
          outputs: { validated: '$response.body#/validated' },
        },
        {
          stepId: 'authorize-payment',
          description: 'Authorize the card for the order amount.',
          operationId: 'authorizePayment',
          parameters: [
            { name: 'Idempotency-Key', in: 'header', value: '$inputs.orderId' },
          ],
          requestBody: {
            contentType: 'application/json',
            payload: { orderId: '$inputs.orderId', amount: '$inputs.amount' },
          },
          successCriteria: [
            { condition: '$statusCode == 201' },
            { context: '$response.body', type: 'jsonpath', condition: '$[?@.status == "authorized"]' },
          ],
          onFailure: [
            {
              name: 'retry-throttled',
              type: 'retry',
              retryAfter: 5,
              retryLimit: 3,
              criteria: [{ condition: '$statusCode == 429' }],
            },
            {
              name: 'manual-review-on-decline',
              type: 'goto',
              stepId: 'manual-review',
              criteria: [{ condition: '$statusCode == 402' }],
            },
            // A reusable failure action shared through the document's components library.
            { reference: '$components.failureActions.escalate' },
          ],
          outputs: { authorizationId: '$response.body#/authorizationId' },
        },
        {
          stepId: 'await-confirmation',
          description: 'Wait for the confirmation event correlated to this order.',
          channelPath: '/channels/orderConfirmations',
          action: 'receive',
          correlationId: '$inputs.orderId',
          timeout: 300000,
          successCriteria: [
            { context: '$message.payload', type: 'jsonpath', condition: '$[?@.confirmed == true]' },
          ],
        },
        {
          stepId: 'capture-payment',
          description: 'Capture the authorized amount.',
          operationId: 'capturePayment',
          parameters: [
            { name: 'authorizationId', in: 'path', value: '$steps.authorize-payment.outputs.authorizationId' },
          ],
          successCriteria: [
            { condition: '$statusCode == 200' },
          ],
          onSuccess: [
            { name: 'done', type: 'end' },
          ],
          outputs: { receiptId: '$response.body#/receiptId' },
        },
        {
          stepId: 'manual-review',
          description: 'Declined or escalated orders queue for a human.',
          operationId: 'createReviewTask',
          successCriteria: [
            { condition: '$statusCode == 201' },
          ],
          onSuccess: [
            { name: 'halt-after-review', type: 'end' },
          ],
        },
      ],
      outputs: {
        receiptId: '$steps.capture-payment.outputs.receiptId',
      },
    },
    {
      workflowId: 'order-with-compensation',
      summary: 'Runs place-order as a sub-workflow and refunds if it ends badly.',
      steps: [
        {
          stepId: 'run-order',
          description: 'The whole place-order workflow as one step.',
          workflowId: 'place-order',
          onSuccess: [
            { name: 'ordered', type: 'end' },
          ],
          onFailure: [
            { name: 'compensate', type: 'goto', stepId: 'refund-payment' },
          ],
        },
        {
          stepId: 'refund-payment',
          operationId: 'refundPayment',
          successCriteria: [
            { condition: '$statusCode == 200' },
          ],
          onSuccess: [
            { name: 'compensated', type: 'end' },
          ],
        },
      ],
    },
  ],
  components: {
    failureActions: {
      escalate: {
        name: 'escalate',
        type: 'goto',
        stepId: 'manual-review',
        criteria: [{ condition: '$statusCode == 500' }],
      },
    },
  },
};

// The seed source documents the demo attaches to the fixture working copy — the REAL operation
// surfaces (raw JSON Schema) that the operation browser lists and the step inspector's templates
// consume, matching the workflows' operationId/channelPath bindings above.
export const paymentsOpenApi = {
  openapi: '3.1.0',
  info: { title: 'Payments', version: '1.0.0' },
  components: {
    schemas: {
      Card: { type: 'object', properties: { number: { type: 'string' }, cvv: { type: 'string' } } },
    },
  },
  paths: {
    '/orders/validate': {
      post: {
        operationId: 'validateOrder',
        summary: 'Validate an order before payment',
        parameters: [
          {
            name: 'checks',
            in: 'query',
            style: 'deepObject',
            description: 'Which validations to run.',
            schema: {
              type: 'object',
              properties: {
                inventory: { type: 'boolean' },
                fraud: { type: 'boolean' },
                priceTolerance: { type: 'number' },
              },
            },
          },
        ],
        responses: { 200: { description: 'valid' }, 400: { description: 'invalid' }, default: { description: 'unexpected' } },
      },
    },
    '/payments/authorize': {
      post: {
        operationId: 'authorizePayment',
        summary: 'Authorize a card payment',
        parameters: [
          { name: 'Idempotency-Key', in: 'header', required: true, description: 'Retries reuse the same key.', schema: { type: 'string' } },
        ],
        requestBody: {
          content: {
            'application/json': {
              schema: {
                type: 'object',
                properties: {
                  orderId: { type: 'string' },
                  amount: { type: 'number' },
                  card: { $ref: '#/components/schemas/Card' },
                  capture: { type: 'boolean', default: false },
                },
              },
            },
          },
        },
        responses: {
          201: { description: 'authorized', content: { 'application/json': { schema: { type: 'object', properties: { status: { type: 'string' }, authorizationId: { type: 'string' } } } } } },
          402: { description: 'declined' },
          429: { description: 'rate limited' },
          '5XX': { description: 'gateway trouble' },
        },
      },
    },
    '/payments/{authorizationId}/capture': {
      parameters: [{ name: 'authorizationId', in: 'path', required: true, schema: { type: 'string' } }],
      post: {
        operationId: 'capturePayment',
        summary: 'Capture an authorized payment',
        responses: { 200: { description: 'captured' }, 404: { description: 'unknown authorization' } },
      },
    },
    '/reviews': {
      post: { operationId: 'createReviewTask', summary: 'Queue a manual review', responses: { 201: { description: 'queued' } } },
    },
    '/payments/{authorizationId}/refund': {
      parameters: [{ name: 'authorizationId', in: 'path', required: true, schema: { type: 'string' } }],
      post: {
        operationId: 'refundPayment',
        summary: 'Refund a captured payment',
        responses: { 200: { description: 'refunded' }, 404: { description: 'unknown authorization' }, default: { description: 'unexpected' } },
      },
    },
  },
};

export const orderEventsAsyncApi = {
  asyncapi: '2.6.0',
  info: { title: 'Order events', version: '1.0.0' },
  channels: {
    '/channels/orderConfirmations': {
      publish: {
        operationId: 'onOrderConfirmation',
        summary: 'Confirmations arrive here',
        message: {
          payload: {
            type: 'object',
            properties: { confirmed: { type: 'boolean' }, at: { type: 'string', format: 'date-time' } },
          },
        },
      },
    },
  },
};
