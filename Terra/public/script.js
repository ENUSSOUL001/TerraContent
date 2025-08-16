document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('generation-form');
    const submitButton = document.getElementById('submit-button');
    const jobsContainer = document.getElementById('jobs-container');
    const sizeSelect = document.getElementById('size');
    const customSizeContainer = document.getElementById('custom-size-container');
    const widthInput = document.getElementById('width');
    const heightInput = document.getElementById('height');

    let jobs = [];
    let isSubmitting = false;
    let pollingInterval;

    const sizeMap = {
        "Small": { width: 4200, height: 1200 },
        "Medium": { width: 6400, height: 1800 },
        "Large": { width: 8400, height: 2400 }
    };

    function saveJobs() {
        localStorage.setItem('terra-jobs', JSON.stringify(jobs));
    }

    function loadJobs() {
        const savedJobs = localStorage.getItem('terra-jobs');
        if (savedJobs) {
            jobs = JSON.parse(savedJobs);
            renderAllJobs();
            if (jobs.some(job => job.status !== 'Completed' && job.runId)) {
                startPolling();
            }
        }
    }

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

        const newJobs = [];
        for (let i = 0; i < runCount; i++) {
            const job = {
                id: `job_${Date.now()}_${i}`,
                options: options,
                status: 'Submitting...',
                runId: null,
                artifactId: null,
                downloadUrl: null,
                isError: false,
                isComplete: false,
                timestamp: new Date().toISOString()
            };
            newJobs.push(job);
        }
        
        jobs = [...newJobs, ...jobs];
        renderAllJobs();
        saveJobs();

        for (const job of newJobs) {
            try {
                const response = await fetch('/api/get-run-id', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ options: job.options })
                });
                if (!response.ok) throw new Error(`API Error: ${response.statusText}`);
                const { run_id } = await response.json();
                updateJobState(job.id, { runId: run_id, status: 'Queued on GitHub' });
            } catch (error) {
                updateJobState(job.id, { status: `Error: ${error.message}`, isError: true, isComplete: true });
            }
        }

        startPolling();
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
    
    function startPolling() {
        if (pollingInterval) clearInterval(pollingInterval);
        pollingInterval = setInterval(pollAllJobs, 15000);
        pollAllJobs(); 
    }

    function pollAllJobs() {
        const activeJobs = jobs.filter(job => job.runId && !job.isComplete);
        if (activeJobs.length === 0) {
            clearInterval(pollingInterval);
            pollingInterval = null;
            return;
        }
        
        activeJobs.forEach(job => {
            fetch(`/api/check-status?run_id=${job.runId}`)
                .then(res => res.json())
                .then(data => {
                    if (data.status === 'completed') {
                        if (data.conclusion === 'success' && data.artifact) {
                            updateJobState(job.id, { status: 'Success! Fetching artifact...', artifactId: data.artifact.id });
                            fetchArtifact(job.id);
                        } else {
                            updateJobState(job.id, { status: `Failed: ${data.conclusion || 'Unknown Reason'}`, isError: true, isComplete: true });
                        }
                    } else {
                        updateJobState(job.id, { status: `In Progress (${data.status})...` });
                    }
                })
                .catch(err => {
                    console.error('Polling error:', err);
                });
        });
    }

    async function fetchArtifact(jobId) {
        const job = jobs.find(j => j.id === jobId);
        if (!job || !job.runId || !job.artifactId) return;

        try {
            const res = await fetch(`/api/get-artifact?run_id=${job.runId}&artifact_id=${job.artifactId}`);
            if (!res.ok) throw new Error('Could not fetch artifact link.');
            const { downloadUrl } = await res.json();
            updateJobState(jobId, { downloadUrl: downloadUrl, status: 'Completed', isComplete: true });
        } catch(error) {
            updateJobState(jobId, { status: 'Error fetching artifact.', isError: true, isComplete: true });
        }
    }
    
    function updateJobState(jobId, updates) {
        const jobIndex = jobs.findIndex(j => j.id === jobId);
        if (jobIndex > -1) {
            jobs[jobIndex] = { ...jobs[jobIndex], ...updates };
            saveJobs();
            renderSingleJob(jobs[jobIndex]);
        }
    }

    function renderAllJobs() {
        jobsContainer.innerHTML = '';
        if (jobs.length === 0) {
            jobsContainer.innerHTML = '<p>Jobs will appear here once you start a generation.</p>';
            return;
        }
        jobs.forEach(job => {
            const jobElement = createJobElement(job);
            jobsContainer.appendChild(jobElement);
        });
    }

    function renderSingleJob(job) {
        const existingElement = document.getElementById(job.id);
        const newElement = createJobElement(job);
        if (existingElement) {
            existingElement.replaceWith(newElement);
        } else {
            jobsContainer.prepend(newElement);
        }
    }

    function createJobElement(job) {
        const jobElement = document.createElement('div');
        jobElement.className = 'job-card';
        jobElement.id = job.id;
        jobElement.dataset.runId = job.runId;
        if(job.isComplete) jobElement.dataset.completed = "true";

        const mapStatus = job.options.map === 'true' 
            ? '<p>Map Preview: <span class="map-status">Will be available on download.</span></p>' 
            : '<p>Map Preview: <span class="map-status">Disabled</span></p>';
        
        let resultsHTML = '';
        if (job.downloadUrl) {
            resultsHTML = `<a href="${job.downloadUrl}" class="download-button" target="_blank" rel="noopener noreferrer">Download World Files (.zip)</a>`;
        }

        jobElement.innerHTML = `
            <h3>${job.options.name} (Seed: ${job.options.seed})</h3>
            <p>Status: <span class="status-text ${job.isError ? 'error' : ''}">${job.status}</span></p>
            ${mapStatus}
            <div class="job-results">${resultsHTML}</div>
            <details>
                <summary>Show Config</summary>
                <pre>${JSON.stringify(job.options, null, 2)}</pre>
            </details>
        `;
        return jobElement;
    }

    loadJobs();
});
