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
