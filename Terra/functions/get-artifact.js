const fetch = require('node-fetch');

exports.handler = async (event) => {
  const { run_id, artifact_id } = event.queryStringParameters;
  const GITHUB_TOKEN = process.env.GITHUB_PAT;
  const GITHUB_USER = 'ENUSSOUL001';
  const GITHUB_REPO = 'TerraContent';
  
  if (!run_id || !artifact_id) {
    return { statusCode: 400, body: 'Missing run_id or artifact_id.' };
  }

  const url = `https://api.github.com/repos/${GITHUB_USER}/${GITHUB_REPO}/actions/runs/${run_id}/artifacts/${artifact_id}/zip`;
  
  try {
    const response = await fetch(url, {
      headers: { 'Authorization': `token ${GITHUB_TOKEN}` },
      redirect: 'manual' 
    });

    const downloadUrl = response.headers.get('location');
    if (response.status === 302 && downloadUrl) {
      return { statusCode: 200, body: JSON.stringify({ downloadUrl }) };
    } else {
      throw new Error('Could not retrieve artifact download URL.');
    }
  } catch (error) {
    return { statusCode: 500, body: JSON.stringify({ message: error.message }) };
  }
};
