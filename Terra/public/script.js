:root {
    --bg-color: #151e33;
    --surface-color: #1f3053;
    --primary-color: #87adff;
    --text-color: #e0e0e0;
    --subtitle-color: #a0b1c0;
    --border-color: #38538c;
    --shadow-color-1: #1f3053;
    --shadow-color-2: #111;
    --inset-shadow-color: #5c8bee;
    --button-bg: #4162a8;
    --font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
    --transition-speed: 0.3s;
}
body {
    font-family: var(--font-family);
    background-color: var(--bg-color);
    color: var(--text-color);
    margin: 0;
    padding: 1rem;
    line-height: 1.6;
    transition: background-color var(--transition-speed), color var(--transition-speed);
}
.header-content { max-width: 960px; margin: 0 auto 2rem; padding: 0 1rem; }
.title-group { text-align: center; margin-bottom: 0.5rem; }
.title-group h1 { line-height: 1.1; margin: 0; font-size: clamp(2.5rem, 6vw, 3.5rem); color: var(--primary-color); }
.title-group .subtitle { display: block; font-size: clamp(1rem, 2vw, 1.2rem); color: var(--subtitle-color); }
.attribution { text-align: center; margin: 1rem 0; }
main { max-width: 960px; margin: 0 auto; padding: 0 1rem; }
a { color: var(--primary-color); text-decoration: none; }
a:hover { text-decoration: underline; }
form { display: flex; flex-direction: column; gap: 1.5rem; }
fieldset {
    border: 1px solid var(--border-color);
    border-radius: 8px;
    padding: 1.5rem;
    background-color: var(--surface-color);
    box-shadow: inset 0 1px 4px rgba(0,0,0,0.2);
    opacity: 0;
    transform: translateY(20px);
    animation: fadeInUp 0.5s ease-out forwards;
}
fieldset:nth-child(1) { animation-delay: 0.1s; }
fieldset:nth-child(2) { animation-delay: 0.2s; }
fieldset:nth-child(3) { animation-delay: 0.3s; }
fieldset:nth-child(4) { animation-delay: 0.4s; }
fieldset:nth-child(5) { animation-delay: 0.5s; }
@keyframes fadeInUp { to { opacity: 1; transform: translateY(0); } }
legend { padding: 0 0.5rem; font-weight: bold; color: var(--primary-color); }
.form-group { display: flex; flex-direction: column; gap: 0.5rem; margin-bottom: 1rem; }
label { font-weight: 500; display: flex; align-items: center; }
small { color: var(--subtitle-color); font-size: 0.875rem; }
input[type="text"], input[type="number"], select {
    width: 100%;
    padding: 0.75rem;
    background-color: var(--bg-color);
    border: 1px solid var(--border-color);
    border-radius: 4px;
    color: var(--text-color);
    font-size: 1rem;
    box-sizing: border-box;
    transition: border-color var(--transition-speed), box-shadow var(--transition-speed);
}
input:focus, select:focus {
    outline: none;
    border-color: var(--primary-color);
    box-shadow: 0 0 0 3px color-mix(in srgb, var(--primary-color) 25%, transparent);
}
input[type="checkbox"] {
    width: 1.2em;
    height: 1.2em;
    margin-right: 0.5rem;
    accent-color: var(--primary-color);
}
.form-grid-2 { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1.5rem; }
.form-grid-responsive { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1.5rem; }
#submit-button {
    background: var(--button-bg);
    border-radius: 4px;
    border-top: 1px solid #38538c;
    border-right: 1px solid #1f2d4d;
    border-bottom: 1px solid #151e33;
    border-left: 1px solid #1f2d4d;
    box-shadow: inset 0 1px 10px 1px var(--inset-shadow-color), 0px 1px 0 var(--border-color), 0 6px 0px var(--shadow-color-1), 0 8px 4px 1px var(--shadow-color-2);
    color: #fff;
    font: bold 20px "helvetica neue", helvetica, arial, sans-serif;
    line-height: 1;
    padding: 10px 0 12px 0;
    text-align: center;
    text-shadow: 0px -1px 1px #1e2d4d;
    width: 100%;
    max-width: 300px;
    margin: 1rem auto 0;
    cursor: pointer;
    user-select: none;
    -webkit-user-select: none;
    transition: all 0.1s ease-in-out;
}
#submit-button:hover {
    box-shadow: inset 0 0px 20px 1px var(--primary-color), 0px 1px 0 var(--border-color), 0 6px 0px var(--shadow-color-1), 0 8px 4px 1px var(--shadow-color-2);
}
#submit-button:active {
    box-shadow: inset 0 1px 10px 1px var(--inset-shadow-color), 0 1px 0 var(--border-color), 0 2px 0 var(--shadow-color-1), 0 4px 3px 0 var(--shadow-color-2);
    transform: translateY(4px);
}
#submit-button:disabled {
    cursor: not-allowed;
    opacity: 0.7;
    transform: translateY(2px);
    box-shadow: inset 0 1px 10px 1px var(--inset-shadow-color), 0 1px 0 var(--border-color), 0 4px 0 var(--shadow-color-1), 0 6px 3px 0 var(--shadow-color-2);
}
#status-section { margin-top: 3rem; }
#jobs-container { display: flex; flex-direction: column; gap: 1rem; }
.job-card {
    background-color: var(--surface-color);
    border-left: 5px solid var(--primary-color);
    padding: 1rem 1.5rem;
    border-radius: 4px;
    opacity: 0;
    transform: scale(0.95);
    animation: zoomIn 0.4s ease-out forwards;
}
@keyframes zoomIn { to { opacity: 1; transform: scale(1); } }
.job-card h3 { margin: 0 0 0.5rem 0; }
.job-card p { margin: 0; }
.job-card .status-text { font-weight: bold; }
details { margin-top: 1rem; }
summary { cursor: pointer; color: var(--primary-color); }
.job-card pre {
    background-color: var(--bg-color);
    padding: 0.5rem;
    border-radius: 4px;
    white-space: pre-wrap;
    word-break: break-all;
    max-height: 200px;
    overflow-y: auto;
    margin-top: 0.5rem;
    font-size: 0.8rem;
}
.hidden { display: none !important; }
@media (min-width: 1400px) {
  .header-content, main { max-width: 1200px; }
}
