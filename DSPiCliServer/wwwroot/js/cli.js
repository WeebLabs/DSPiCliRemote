const cliInput = document.getElementById('cliInput');
const sendBtn = document.getElementById('sendBtn');
const cliOutput = document.getElementById('cliOutput');
const backBtn = document.getElementById('backBtn');

let logLines = [];
let history = [];
let historyIndex = -1;

async function sendCommand(cmd) {
    if (!cmd.trim()) return;
    
    // Add to local history
    if (history.length === 0 || history[history.length - 1] !== cmd) {
        history.push(cmd);
        if (history.length > 20) history.shift();
    }
    historyIndex = -1;

    addLog(`> ${cmd}`);
    try {
        const response = await fetch('/api/command', {
            method: 'POST',
            body: cmd
        });
        const text = await response.text();
        addLog(text);
    } catch (err) {
        addLog(`Error: ${err}`);
    }
}

function addLog(msg) {
    logLines.push(msg);
    if (logLines.length > 50) {
        logLines.shift();
    }
    
    cliOutput.innerHTML = logLines.map(line => `<div>${line}</div>`).join('');
    cliOutput.scrollTop = cliOutput.scrollHeight;
}

sendBtn.onclick = () => {
    const cmd = cliInput.value;
    cliInput.value = '';
    sendCommand(cmd);
};

cliInput.onkeydown = (e) => {
    if (e.key === 'Enter') {
        sendBtn.click();
    } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        if (history.length > 0) {
            if (historyIndex === -1) historyIndex = history.length - 1;
            else if (historyIndex > 0) historyIndex--;
            cliInput.value = history[historyIndex];
        }
    } else if (e.key === 'ArrowDown') {
        e.preventDefault();
        if (history.length > 0) {
            if (historyIndex !== -1 && historyIndex < history.length - 1) {
                historyIndex++;
                cliInput.value = history[historyIndex];
            } else {
                historyIndex = -1;
                cliInput.value = '';
            }
        }
    }
};

backBtn.onclick = () => {
    window.location.href = '/';
};
