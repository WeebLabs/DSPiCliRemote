const cliInput = document.getElementById('cliInput');
const sendBtn = document.getElementById('sendBtn');
const cliOutput = document.getElementById('cliOutput');
const backBtn = document.getElementById('backBtn');

let logLines = [];

async function sendCommand(cmd) {
    if (!cmd.trim()) return;
    
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
    }
};

backBtn.onclick = () => {
    window.location.href = '/';
};
