document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('generation-form');
    const submitButton = document.getElementById('submit-button');
    const jobsContainer = document.getElementById('jobs-container');
    const sizeSelect = document.getElementById('size');
    const customSizeContainer = document.getElementById('custom-size-container');
    const widthInput = document.getElementById('width');
    const heightInput = document.getElementById('height');

    let isSubmitting = false;
    let pollingInterval;

    const sizeMap = {
        "Small": { width: 4200, height: 1200 },
        "Medium": { width: 6400, height: 1800 },
        "Large": { width: 8400, height: 2400 }
    };

    const toggleCustomSize = () => {
        if (sizeSelect.value === 'Custom') {
            customSizeContainer.classList.remove('hidden');
        } else {
            customSizeContainer.classList.add('hidden');
            const selectedSize = sizeMap[sizeSelect.value];
            if (selectedSize) {
                widthInput.value = selectedSize.width;
                heightInput.value = selectedSize.height;
            }
        }
    };

    sizeSelect.addEventListener('change', toggleCustomSize);
    toggleCustomSize();

    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        if (isSubmitting) return;

        isSubmitting = true;
        submitButton.disabled = true;
        submitButton.textContent = 'Submitting...';
        
        const formData = new FormData(form);
        const runCount = parseInt(formData.get('run_count'), 10);
        const options = getOptionsFromForm(formData);

        jobsContainer.innerHTML = ''; 
        
        for (let i = 0; i < runCount; i++) {
            const jobId = `job_${Date.now()}_${i}`;
            addJobToDashboard(jobId, options, 'Submitting...');
            
            try {
                const response = await fetch('/api/get-run-id', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ options })
                });
                
                if (!response.ok) throw new Error(`Failed to get Run ID: ${await response.text()}`);
                
                const { run_id } = await response.json();
                const jobElement = document.getElementById(jobId);
                jobElement.dataset.runId = run_id;
                updateJobStatus(jobId, 'Queued on GitHub...');
            } catch (error) {
                updateJobStatus(jobId, `Error: ${error.message}`, true);
            }
        }

        if (!pollingInterval) {
            pollingInterval = setInterval(pollAllJobs, 15000);
        }

        isSubmitting = false;
        submitButton.disabled = false;
        submitButton.textContent = 'Generate';
    });
    
    function getOptionsFromForm(formData) {
        const getCheckboxValue = (name) => formData.has(name) ? 'true' : 'false';
        const options = {};
        for (const [key, value] of formData.entries()) {
            if (form.elements[key] && form.elements[key].type === 'checkbox') {
                 options[key] = getCheckboxValue(key);
            } else {
                 options[key] = value;
            }
        }
        const allCheckboxes = form.querySelectorAll('input[type="checkbox"]');
        allCheckboxes.forEach(cb => { if (!formData.has(cb.name)) { options[cb.name] = 'false'; } });
        if (sizeSelect.value !== 'Custom') {
            const selectedSize = sizeMap[sizeSelect.value];
            options.width = selectedSize.width;
            options.height = selectedSize.height;
        } else {
            options.width = parseInt(widthInput.value, 10);
            options.height = parseInt(heightInput.value, 10);
        }
        delete options.size;
        delete options.run_count;
        return options;
    }
    
    async function pollAllJobs() {
        const activeJobs = document.querySelectorAll('.job-card[data-run-id]:not([data-completed="true"])');
        if (activeJobs.length === 0) {
            clearInterval(pollingInterval);
            pollingInterval = null;
            return;
        }
        
        activeJobs.forEach(job => {
            const runId = job.dataset.runId;
            const jobId = job.id;
            fetch(`/api/check-status?run_id=${runId}`)
                .then(res => res.json())
                .then(data => {
                    if (data.status === 'completed') {
                        job.dataset.completed = "true";
                        if (data.conclusion === 'success') {
                            updateJobStatus(jobId, 'Success! Fetching artifact...', false);
                            fetchArtifact(jobId, runId, data.artifact.id);
                        } else {
                            updateJobStatus(jobId, `Failed: ${data.conclusion}`, true);
                        }
                    } else {
                        updateJobStatus(jobId, `In Progress (${data.status})...`);
                    }
                })
                .catch(err => {
                    console.error('Polling error:', err);
                });
        });
    }

    async function fetchArtifact(jobId, runId, artifactId) {
        try {
            const res = await fetch(`/api/get-artifact?run_id=${runId}&artifact_id=${artifactId}`);
            if (!res.ok) throw new Error('Could not fetch artifact link.');
            const { downloadUrl } = await res.json();
            
            const jobElement = document.getElementById(jobId);
            const resultsContainer = jobElement.querySelector('.job-results');
            resultsContainer.innerHTML = `<a href="${downloadUrl}" class="download-button" target="_blank" rel="noopener noreferrer">Download World Files (.zip)</a>`;

        } catch(error) {
            updateJobStatus(jobId, 'Error fetching artifact.', true);
        }
    }
    
    function updateJobStatus(jobId, text, isError = false) {
        const jobElement = document.getElementById(jobId);
        if (jobElement) {
            const statusElement = jobElement.querySelector('.status-text');
            statusElement.textContent = text;
            statusElement.style.color = isError ? '#dc3545' : 'inherit';
        }
    }

    function addJobToDashboard(jobId, options, initialStatus) {
        const jobElement = document.createElement('div');
        jobElement.className = 'job-card';
        jobElement.id = jobId;
        const mapStatus = options.map === 'true' 
            ? '<p>Map Preview: <span class="map-status">Will be available on download.</span></p>' 
            : '<p>Map Preview: <span class="map-status">Disabled</span></p>';

        jobElement.innerHTML = `
            <h3>${options.name} (Seed: ${options.seed})</h3>
            <p>Status: <span class="status-text">${initialStatus}</span></p>
            ${mapStatus}
            <div class="job-results"></div>
            <details>
                <summary>Show Config</summary>
                <pre>${JSON.stringify(options, null, 2)}</pre>
            </details>
        `;
        jobsContainer.appendChild(jobElement);
    }
});
