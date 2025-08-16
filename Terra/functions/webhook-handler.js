const { getStore } = require('@netlify/blobs');
const crypto = require('crypto');
const fetch = require('node-fetch');

exports.handler = async (event) => {
  const secret = process.env.GITHUB_WEBHOOK_SECRET;
  const signature = event.headers['x-hub-signature-256'];
  
  const hmac = crypto.createHmac('sha256', secret);
  const digest = `sha256=${hmac.update(event.body).digest('hex')}`;

  if (!signature || !crypto.timingSafeEqual(Buffer.from(signature), Buffer.from(digest))) {
    return { statusCode: 401, body: 'Unauthorized' };
  }

  const payload = JSON.parse(event.body);

  if (payload.action === 'completed' && payload.workflow_run) {
    const run = payload.workflow_run;
    const runId = run.id.toString();
    const statusStore = getStore('job-statuses');
    
    let artifactInfo = null;
    if (run.conclusion === 'success' && run.artifacts_url) {
      try {
        const artifactsResponse = await fetch(run.artifacts_url, {
          headers: { 'Authorization': `token ${process.env.GITHUB_PAT}`, 'Accept': 'application/vnd.github.v3+json' },
        });
        if(artifactsResponse.ok) {
          const artifactsData = await artifactsResponse.json();
          const worldArtifact = artifactsData.artifacts.find(art => art.name === 'generated-world-files');
          if (worldArtifact) {
              artifactInfo = { id: worldArtifact.id, expired: worldArtifact.expired };
          }
        }
      } catch (e) {
        console.error("Failed to fetch artifact details.", e);
      }
    }

    const statusUpdate = {
      status: run.status,
      conclusion: run.conclusion,
      artifact: artifactInfo,
      timestamp: new Date().toISOString()
    };
    
    await statusStore.setJSON(runId, statusUpdate);
  }

  return { statusCode: 200, body: 'OK' };
};
