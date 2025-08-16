exports.handler = async (event) => {
  console.log('--- WEBHOOK HANDLER INVOCATION ---');
  console.log('Headers Received:', JSON.stringify(event.headers, null, 2));
  
  const secret = process.env.GITHUB_WEBHOOK_SECRET;
  console.log('Secret Loaded from Environment:', secret ? `A secret of length ${secret.length} was loaded.` : 'SECRET IS UNDEFINED OR EMPTY!');
  
  console.log('Type of event.body:', typeof event.body);
  console.log('Content of event.body (first 500 chars):', event.body.substring(0, 500));

  if (secret && typeof event.body === 'string') {
    const crypto = require('crypto');
    const hmac = crypto.createHmac('sha256', secret);
    const calculatedDigest = `sha256=${hmac.update(event.body).digest('hex')}`;
    
    console.log('Signature Received from GitHub:', event.headers['x-hub-signature-256']);
    console.log('Signature Calculated by Us:', calculatedDigest);

    if (event.headers['x-hub-signature-256'] === calculatedDigest) {
      console.log('SUCCESS: Signatures match!');
    } else {
      console.log('ERROR: SIGNATURES DO NOT MATCH!');
    }
  } else {
    console.log('Skipping signature calculation because secret is missing or body is not a string.');
  }

  console.log('--- END OF INVOCATION ---');

  return {
    statusCode: 200,
    body: 'Debug log generated. Check Netlify function logs.',
  };
};
