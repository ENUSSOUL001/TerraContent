document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('generation-form');
    const submitButton = document.getElementById('submit-button');
    const jobsContainer = document.getElementById('jobs-container');
    const sizeSelect = document.getElementById('size');
    const customSizeContainer = document.getElementById('custom-size-container');
    const widthInput = document.getElementById('width');
    const heightInput = document.getElementById('height');
    const presetSelect = document.getElementById('preset-select');
    const presetNameInput = document.getElementById('preset-name');
    const savePresetBtn = document.getElementById('save-preset-btn');
    const loadPresetBtn = document.getElementById('load-preset-btn');
    const deletePresetBtn = document.getElementById('delete-preset-btn');
    const modal = document.getElementById('results-modal');
    const modalCloseBtn = document.getElementById('modal-close-btn');
    const modalTitle = document.getElementById('modal-title');
    const modalBody = document.getElementById('modal-body');

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
            if (jobs.some(job => job.runId && !job.isComplete)) {
                startPolling();
            }
        }
    }

    function getPresets() {
        return JSON.parse(localStorage.getItem('terra-presets')) || [];
    }

    function savePresets(presets) {
        localStorage.setItem('terra-presets', JSON.stringify(presets));
    }

    function populatePresetDropdown() {
        const presets = getPresets();
        presetSelect.innerHTML = '<option value="">-- Select a preset --</option>';
        presets.forEach(preset => {
            const option = document.createElement('option');
            option.value = preset.name;
            option.textContent = preset.name;
            presetSelect.appendChild(option);
        });
    }

    function getFormValues() {
        const values = {};
        const formData = new FormData(form);
        for (const [key, value] of formData.entries()) {
            const element = form.elements[key];
            if (element.type === 'checkbox') {
                values[key] = element.checked;
            } else if (element.type === 'radio') {
                if(element.checked) values[key] = value;
            } else {
                values[key] = value;
            }
        }
        return values;
    }
    
    function applyPreset(preset) {
        Object.keys(preset.options).forEach(key => {
            const element = form.elements[key];
            if (element) {
                if (element.type === 'checkbox') {
                    element.checked = preset.options[key];
                } else {
                    element.value = preset.options[key];
                }
            }
        });
        toggleCustomSize();
    }

    savePresetBtn.addEventListener('click', () => {
        const name = presetNameInput.value.trim();
        if (!name) {
            alert('Please enter a name for the preset.');
            return;
        }
        const presets = getPresets();
        if (presets.some(p => p.name === name)) {
            if (!confirm(`A preset named "${name}" already exists. Overwrite it?`)) {
                return;
            }
        }
        const newPresets = presets.filter(p => p.name !== name);
        newPresets.push({ name: name, options: getFormValues() });
        savePresets(newPresets);
        populatePresetDropdown();
        presetNameInput.value = '';
        presetSelect.value = name;
    });

    loadPresetBtn.addEventListener('click', () => {
        const presetName = presetSelect.value;
        if (!presetName) return;
        const presets = getPresets();
        const preset = presets.find(p => p.name === presetName);
        if (preset) {
            applyPreset(preset);
        }
    });

    deletePresetBtn.addEventListener('click', () => {
        const presetName = presetSelect.value;
        if (!presetName) return;
        if (confirm(`Are you sure you want to delete the preset "${presetName}"?`)) {
            let presets = getPresets();
            presets = presets.filter(p => p.name !== presetName);
            savePresets(presets);
            populatePresetDropdown();
        }
    });

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
        const options = getOptionsFromFormForGeneration();

        const newJobs = [];
        for (let i = 0; i < runCount; i++) {
            const job = {
                id: `job_${Date.now()}_${i}`,
                options: options,
                status: 'Submitting...',
                runId: null,
                artifactId: null,
                isError: false,
                isComplete: false,
                timestamp: new Date().toISOString()
            };
            newJobs.push(job);
        }
        
        jobs = [...newJobs, ...jobs].slice(0, 50);
        renderAllJobs();
        saveJobs();

        for (const job of newJobs) {
            try {
                const response = await fetch('/api/get-run-id', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ options: job.options })
                });
                if (!response.ok) {
                    const errorData = await response.json();
                    throw new Error(errorData.message || `API Error: ${response.statusText}`);
                }
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
    
    function getOptionsFromFormForGeneration() {
        const formData = new FormData(form);
        const getCheckboxValue = (name) => formData.has(name) ? 'true' : 'false';
        const options = {};
        for (const [key, value] of formData.entries()) {
            const element = form.elements[key];
            if (element && element.type === 'checkbox') {
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
        delete options['preset-name'];
        delete options['preset-select'];
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
                            updateJobState(job.id, { status: 'Completed', artifactId: data.artifact.id, isComplete: true });
                        } else {
                            updateJobState(job.id, { status: `Failed: ${data.conclusion || 'Unknown Reason'}`, isError: true, isComplete: true });
                        }
                    } else {
                        updateJobState(job.id, { status: `In Progress (${data.status})...` });
                    }
                })
                .catch(err => {
                    console.error('Polling error for runId', job.runId, err);
                });
        });
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
        jobElement.dataset.jobId = job.id;

        if (job.isComplete && !job.isError) {
            jobElement.classList.add('is-success', 'is-clickable');
        }
        if(job.isError) {
            jobElement.classList.add('is-error');
        }

        const mapStatus = job.options.map === 'true' 
            ? '<p>Map Preview: <span class="map-status">Available in results.</span></p>' 
            : '<p>Map Preview: <span class="map-status">Disabled</span></p>';
        
        let resultsHTML = '';
        if (job.isComplete && !job.isError) {
            resultsHTML = `<p>Click to view results</p>`;
        }
        
        const statusClass = job.isError ? 'is-error' : (job.isComplete ? 'is-success' : '');

        jobElement.innerHTML = `
            <h3>${job.options.name} (Seed: ${job.options.seed})</h3>
            <p>Status: <span class="status-text ${statusClass}">${job.status}</span></p>
            ${mapStatus}
            <div class="job-results">${resultsHTML}</div>
            <details>
                <summary>Show Config</summary>
                <pre>${JSON.stringify(job.options, null, 2)}</pre>
            </details>
        `;
        return jobElement;
    }

    async function showResults(job) {
        modal.classList.remove('hidden');
        modalTitle.textContent = `Results for: ${job.options.name}`;
        modalBody.innerHTML = '<p>Loading results...</p>';

        try {
            const res = await fetch(`/api/get-artifact-contents?run_id=${job.runId}&artifact_id=${job.artifactId}`);
            if (!res.ok) throw new Error(`Failed to load artifact content: ${res.statusText}`);
            const files = await res.json();
            
            let mapHTML = '<p>Map preview was not generated for this world.</p>';
            const pngFile = Object.keys(files).find(name => name.endsWith('.png'));
            if (pngFile) {
                mapHTML = `<img src="data:image/png;base64,${files[pngFile]}" alt="World Map Preview" class="modal-map-preview">`;
            }

            let fileListHTML = '<ul class="modal-file-list">';
            for(const filename in files) {
                const fileType = filename.endsWith('.wld') ? 'application/octet-stream' : 'image/png';
                fileListHTML += `
                    <li>
                        <span>${filename}</span>
                        <a href="data:${fileType};base64,${files[filename]}" download="${filename}" class="download-button">Download</a>
                    </li>
                `;
            }
            fileListHTML += '</ul>';

            modalBody.innerHTML = mapHTML + fileListHTML;

        } catch (error) {
            modalBody.innerHTML = `<p class="status-text is-error">Error loading results: ${error.message}</p>`;
        }
    }

    jobsContainer.addEventListener('click', (event) => {
        const card = event.target.closest('.job-card.is-clickable');
        if (card) {
            const jobId = card.dataset.jobId;
            const job = jobs.find(j => j.id === jobId);
            if (job) {
                showResults(job);
            }
        }
    });

    modalCloseBtn.addEventListener('click', () => modal.classList.add('hidden'));
    modal.addEventListener('click', (event) => {
        if (event.target === modal) {
            modal.classList.add('hidden');
        }
    });

    loadJobs();
    populatePresetDropdown();
});
