document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('generation-form');
    const jobsContainer = document.getElementById('jobs-container');
    const sizeSelect = document.getElementById('size');
    const customSizeContainer = document.getElementById('custom-size-container');
    const widthInput = document.getElementById('width');
    const heightInput = document.getElementById('height');

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

    form.addEventListener('submit', (event) => {
        event.preventDefault();
        
        const formData = new FormData(form);
        const runCount = parseInt(formData.get('run_count'), 10);
        
        const getCheckboxValue = (name) => formData.has(name) ? 'true' : 'false';

        const options = {};
        for (const [key, value] of formData.entries()) {
            if (form.elements[key].type === 'checkbox') {
                 options[key] = getCheckboxValue(key);
            } else {
                 options[key] = value;
            }
        }

        const allCheckboxes = form.querySelectorAll('input[type="checkbox"]');
        allCheckboxes.forEach(cb => {
            if (!formData.has(cb.name)) {
                options[cb.name] = 'false';
            }
        });
        
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

        console.log(`Preparing to generate ${runCount} world(s) with these options:`);
        console.log(options);

        jobsContainer.innerHTML = ''; 
        
        for (let i = 0; i < runCount; i++) {
            addJobToDashboard(`job_${Date.now()}_${i}`, options);
        }
    });

    function addJobToDashboard(jobId, options) {
        const jobElement = document.createElement('div');
        jobElement.className = 'job-card';
        jobElement.id = jobId;
        jobElement.innerHTML = `
            <h3>World: ${options.name} (Seed: ${options.seed})</h3>
            <p>Status: <span class="status-text">Queued...</span></p>
            <pre>${JSON.stringify(options, null, 2)}</pre>
        `;
        jobsContainer.appendChild(jobElement);
    }
});
