document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('generation-form');
    const jobsContainer = document.getElementById('jobs-container');
    const sizeSelect = document.getElementById('size');
    const customSizeContainer = document.getElementById('custom-size-container');
    const widthInput = document.getElementById('width');
    const heightInput = document.getElementById('height');
    const themeSwitcher = document.getElementById('theme-switcher');

    const sizeMap = {
        "Small": { width: 4200, height: 1200 },
        "Medium": { width: 6400, height: 1800 },
        "Large": { width: 8400, height: 2400 }
    };

    const applyTheme = (theme) => {
        document.body.dataset.theme = theme;
        localStorage.setItem('terra-theme', theme);
        themeSwitcher.querySelectorAll('.theme-button').forEach(btn => {
            btn.classList.toggle('active', btn.dataset.theme === theme);
        });
    };

    themeSwitcher.addEventListener('click', (e) => {
        if (e.target.matches('.theme-button')) {
            applyTheme(e.target.dataset.theme);
        }
    });

    const savedTheme = localStorage.getItem('terra-theme') || 'default';
    applyTheme(savedTheme);

    const toggleCustomSize = () => {
        if (sizeSelect.value === 'Custom') {
            customSizeContainer.classList.remove('hidden');
        } else {
            customSizeContainer.classList.add('hidden');
            const selectedSize = sizeMap[sizeSelect.value];
            if (selectedSize) {
                widthInput.value = selectedSize.width;
                heightInput.value = selectede.height;
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

        jobsContainer.innerHTML = ''; 
        
        for (let i = 0; i < runCount; i++) {
            addJobToDashboard(`job_${Date.now()}_${i}`, options);
        }
    });

    function addJobToDashboard(jobId, options) {
        const jobElement = document.createElement('div');
        jobElement.className = 'job-card';
        jobElement.id = jobId;
        const mapStatus = options.map === 'true' 
            ? '<p>Map Preview: <span class="map-status">Pending...</span></p>' 
            : '<p>Map Preview: <span class="map-status">Disabled</span></p>';

        jobElement.innerHTML = `
            <h3>${options.name} (Seed: ${options.seed})</h3>
            <p>Status: <span class="status-text">Queued...</span></p>
            ${mapStatus}
            <details>
                <summary>Show Config</summary>
                <pre>${JSON.stringify(options, null, 2)}</pre>
            </details>
        `;
        jobsContainer.appendChild(jobElement);
    }
});
