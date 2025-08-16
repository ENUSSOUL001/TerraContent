const fetch = require('node-fetch');

exports.handler = async (event) => {
  const { run_id } = event.queryStringParameters;
  const GITHUB_TOKEN = process.env.GITHUB_PAT;
  const GITHUB_USER = 'ENUSSOUL001';
  const GITHUB_REPO = 'TerraContent';

  if (!run_id) {
    return { statusCode: 400, body: 'Missing run_id parameter.' };
  }

  const url = `https://api.github.com/repos/${GITHUB_USER}/${GITHUB_REPO}/actions/runs/${run_id}`;

  try {
    const response = await fetch(url, {
      headers: {
        'Authorization': `token ${GITHUB_TOKEN}`,
        'Accept': 'application/vnd.github.v3+json',
      },
    });

    if (!response.ok) {
      throw new Error(`GitHub API error: ${response.status}`);
    }

    const data = await response.json();
    const artifactsUrl = data.artifacts_url;
    
    let artifactInfo = null;
    if(data.status === 'completed' && data.conclusion === 'success') {
        const artifactsResponse = await fetch(artifactsUrl, {
             headers: { 'Authorization': `token ${GITHUB_TOKEN}`, 'Accept': 'application/vnd.github.v3+json' },
        });
        const artifactsData = await artifactsResponse.json();
        const worldArtifact = artifactsData.artifacts.find(art => art.name === 'generated-world-files');
        if (worldArtifact) {
            artifactInfo = { id: worldArtifact.id, expired: worldArtifact.expired };
        }
    }

    return {
      statusCode: 200,
      body: JSON.stringify({ 
        status: data.status, 
        conclusion: data.conclusion,
        artifact: artifactInfo
      }),
    };
  } catch (error) {
    return { statusCode: 500, body: JSON.stringify({ message: error.message }) };
  }
};
