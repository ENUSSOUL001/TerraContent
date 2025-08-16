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

  if (!GITHUB_TOKEN) {
    return { statusCode: 500, body: 'GitHub Personal Access Token is not configured.' };
  }
  
  const API_URL = `https://api.github.com/repos/${GITHUB_USER}/${GITHUB_REPO}/actions/workflows/${WORKFLOW_FILE}/dispatches`;

  try {
    const response = await fetch(API_URL, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `token ${GITHUB_TOKEN}`,
        'Accept': 'application/vnd.github.v3+json',
      },
      body: JSON.stringify({
        ref: 'main',
        inputs: {
          json_config: JSON.stringify(options),
        },
      }),
    });

    if (response.status === 204) {
      return {
        statusCode: 200,
        body: JSON.stringify({ message: 'Workflow triggered successfully.' }),
      };
    } else {
      const errorBody = await response.text();
      return {
        statusCode: response.status,
        body: JSON.stringify({ message: `Failed to trigger workflow. Status: ${response.status}. Body: ${errorBody}` }),
      };
    }
  } catch (error) {
    return {
      statusCode: 500,
      body: JSON.stringify({ message: 'An error occurred while contacting the GitHub API.', error: error.message }),
    };
  }
};
