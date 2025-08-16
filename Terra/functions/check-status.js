const { getStore } = require('@netlify/blobs');
const fetch = require('node-fetch');

exports.handler = async (event) => {
  const { run_id } = event.queryStringParameters;
  if (!run_id) {
    return { statusCode: 400, body: 'Missing run_id parameter.' };
  }

  const statusStore = getStore('job-statuses');
  const cachedStatus = await statusStore.get(run_id, { type: 'json' });

  if (cachedStatus) {
    return { statusCode: 200, body: JSON.stringify(cachedStatus) };
  }
  
  const GITHUB_TOKEN = process.env.GITHUB_PAT;
  const GITHUB_USER = 'ENUSSOUL001';
  const GITHUB_REPO = 'TerraContent';
  const url = `https://api.github.com/repos/${GITHUB_USER}/${GITHUB_REPO}/actions/runs/${run_id}`;
  
  try {
    const response = await fetch(url, {
      headers: { 'Authorization': `token ${GITHUB_TOKEN}`, 'Accept': 'application/vnd.github.v3+json' }
    });
    if (!response.ok) throw new Error(`GitHub API error: ${response.status}`);
    const data = await response.json();
    
    return {
      statusCode: 200,
      body: JSON.stringify({ status: data.status, conclusion: data.conclusion, artifact: null })
    };
  } catch (error) {
    return { statusCode: 500, body: JSON.stringify({ message: error.message }) };
  }
};
