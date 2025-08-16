const fetch = require('node-fetch');

exports.handler = async (event) => {
  if (event.httpMethod !== 'POST') {
    return { statusCode: 405, body: 'Method Not Allowed' };
  }

  const { options } = JSON.parse(event.body);
  const GITHUB_TOKEN = process.env.GITHUB_PAT;
  const GITHUB_USER = 'ENUSSOUL001';
  const GITHUB_REPO = 'TerraContent';
  const WORKFLOW_FILE = 'terra.yml';

  const dispatchUrl = `https://api.github.com/repos/${GITHUB_USER}/${GITHUB_REPO}/actions/workflows/${WORKFLOW_FILE}/dispatches`;
  const runsUrl = `https://api.github.com/repos/${GITHUB_USER}/${GITHUB_REPO}/actions/workflows/${WORKFLOW_FILE}/runs?branch=main&per_page=1`;

  try {
    const dispatchResponse = await fetch(dispatchUrl, {
      method: 'POST',
      headers: {
        'Authorization': `token ${GITHUB_TOKEN}`,
        'Accept': 'application/vnd.github.v3+json',
      },
      body: JSON.stringify({ ref: 'main', inputs: { json_config: JSON.stringify(options) } }),
    });

    if (dispatchResponse.status !== 204) {
      throw new Error(`Failed to dispatch workflow. Status: ${dispatchResponse.status}`);
    }

    await new Promise(resolve => setTimeout(resolve, 5000));

    const runsResponse = await fetch(runsUrl, {
      headers: {
        'Authorization': `token ${GITHUB_TOKEN}`,
        'Accept': 'application/vnd.github.v3+json',
      }
    });
    
    if (!runsResponse.ok) {
        throw new Error(`Failed to fetch workflow runs. Status: ${runsResponse.status}`);
    }

    const runsData = await runsResponse.json();
    if (runsData.workflow_runs.length === 0) {
        throw new Error('No workflow runs found after dispatch.');
    }
    
    const latestRun = runsData.workflow_runs[0];
    return {
      statusCode: 200,
      body: JSON.stringify({ run_id: latestRun.id }),
    };

  } catch (error) {
    return {
      statusCode: 500,
      body: JSON.stringify({ message: error.message }),
    };
  }
};
