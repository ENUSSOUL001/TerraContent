const fetch = require('node-fetch');
const JSZip = require('jszip');

exports.handler = async (event) => {
  const { run_id, artifact_id } = event.queryStringParameters;
  if (!run_id || !artifact_id) {
    return { statusCode: 400, body: 'Missing run_id or artifact_id.' };
  }

  const GITHUB_TOKEN = process.env.GITHUB_PAT;
  const GITHUB_USER = 'ENUSSOUL001';
  const GITHUB_REPO = 'TerraContent';
  
  const artifactUrl = `https://api.github.com/repos/${GITHUB_USER}/${GITHUB_REPO}/actions/artifacts/${artifact_id}/zip`;

  try {
    const response = await fetch(artifactUrl, {
      headers: { 'Authorization': `token ${GITHUB_TOKEN}` },
    });
    if (!response.ok) throw new Error(`Failed to fetch artifact zip. Status: ${response.status}`);

    const buffer = await response.buffer();
    const zip = await JSZip.loadAsync(buffer);
    
    const files = {};
    for (const filename in zip.files) {
      if (!zip.files[filename].dir) {
        const fileBuffer = await zip.files[filename].async('nodebuffer');
        files[filename] = fileBuffer.toString('base64');
      }
    }
    
    return {
      statusCode: 200,
      body: JSON.stringify(files),
    };

  } catch (error) {
    return { statusCode: 500, body: JSON.stringify({ message: error.message }) };
  }
};
