const volSlider = document.getElementById('volSlider');
const volLabel = document.getElementById('volLabel');
const loudnessBtn = document.getElementById('loudnessBtn');
const levelingBtn = document.getElementById('levelingBtn');
const crossfeedBtn = document.getElementById('crossfeedBtn');
const presetSelect = document.getElementById('presetSelect');
const refreshBtn = document.getElementById('refreshBtn');
const logDiv = document.getElementById('log');
const opticalUsbBtn = document.getElementById('opticalUsbBtn');
const opticalStatus = document.getElementById('opticalStatus');
const inputBtn = document.getElementById('inputBtn');

let isProcessRunning = false;

async function updateOpticalUsbButton() {
    const res = await sendCommand('is_running');
    isProcessRunning = (res === 'True');
    
    if (opticalStatus) {
        opticalStatus.textContent = isProcessRunning ? 'ON' : 'OFF';
        opticalStatus.style.color = isProcessRunning ? '#28a745' : '#007bff';
    }

    if (opticalUsbBtn) {
        opticalUsbBtn.style.background = isProcessRunning ? '#28a745' : '#007bff';
    }
    
    // Auto-poll while running
    if (isProcessRunning) {
        setTimeout(updateOpticalUsbButton, 5000);
    }
}

opticalUsbBtn.onclick = async () => {
    if (isProcessRunning) {
        await sendCommand('kill_str');
    } else {
        await sendCommand('run_str');
    }
    await updateOpticalUsbButton();
};

inputBtn.onclick = async () => {
    const current = inputBtn.textContent;
    const next = current === 'USB' ? 'spdif' : 'usb';
    const res = await sendCommand(`set_input ${next}`);
    if (res === 'OK') {
        inputBtn.textContent = next.toUpperCase();
        inputBtn.style.background = next === 'spdif' ? '#0056b3' : '#007bff';
    }
};

async function sendCommand(cmd) {
    try {
        const response = await fetch('/api/command', {
            method: 'POST',
            body: cmd
        });
        const text = await response.text();
        addLog(`Sent: ${cmd} | Received: ${text}`);
        return text;
    } catch (err) {
        addLog(`Error: ${err}`);
        return null;
    }
}

function addLog(msg) {
    const entry = document.createElement('div');
    entry.textContent = `[${new Date().toLocaleTimeString()}] ${msg}`;
    logDiv.appendChild(entry);
    logDiv.scrollTop = logDiv.scrollHeight;
}

volSlider.oninput = () => volLabel.textContent = volSlider.value;
volSlider.onchange = async () => {
    await sendCommand(`set_vol ${volSlider.value}`);
};

let isLoudness = false;
loudnessBtn.onclick = async () => {
    isLoudness = !isLoudness;
    const res = await sendCommand(`set_loudness ${isLoudness ? 1 : 0}`);
    if (res === 'OK') {
        loudnessBtn.textContent = `Loudness: ${isLoudness ? 'ON' : 'OFF'}`;
    } else {
        isLoudness = !isLoudness;
    }
};

let isLeveling = false;
levelingBtn.onclick = async () => {
    isLeveling = !isLeveling;
    const res = await sendCommand(`set_leveling ${isLeveling ? 1 : 0}`);
    if (res === 'OK') {
        levelingBtn.textContent = `Leveling: ${isLeveling ? 'ON' : 'OFF'}`;
    } else {
        isLeveling = !isLeveling;
    }
};

let isCrossfeed = false;
crossfeedBtn.onclick = async () => {
    isCrossfeed = !isCrossfeed;
    const res = await sendCommand(`set_crossfeed ${isCrossfeed ? 1 : 0}`);
    if (res === 'OK') {
        crossfeedBtn.textContent = `Crossfeed: ${isCrossfeed ? 'ON' : 'OFF'}`;
    } else {
        isCrossfeed = !isCrossfeed;
    }
};

presetSelect.onchange = async () => {
    const slot = presetSelect.value;
    if (slot === '-1') return;
    const res = await sendCommand(`set_preset ${slot}`);
    if (res !== 'OK') {
        addLog('Failed to set preset');
        await refreshPresets();
    }
};

async function refreshPresets() {
    const presetsStr = await sendCommand('get_presets');
    if (!presetsStr || presetsStr === 'Error' || presetsStr === 'Not connected') {
        presetSelect.innerHTML = '<option value="-1">No Presets Found</option>';
        return;
    }

    const resParts = presetsStr.split('|');
    const activeSlot = resParts[0];
    const listStr = resParts[1];
    presetSelect.innerHTML = '';
    
    if (listStr) {
        const items = listStr.split(',');
        for (const item of items) {
            const itemParts = item.split(':');
            const slot = itemParts[0];
            const name = itemParts[1];
            const opt = document.createElement('option');
            opt.value = slot;
            opt.textContent = name;
            if (slot === activeSlot) opt.selected = true;
            presetSelect.appendChild(opt);
        }
    } else {
        presetSelect.innerHTML = '<option value="-1">No Presets Found</option>';
    }
}

async function refresh() {
    await refreshPresets();

    const allStatus = await sendCommand('get_all');
    if (allStatus && allStatus !== 'Error' && allStatus !== 'Not connected') {
        const parts = allStatus.split(';');
        const statusMap = {};
        parts.forEach(p => {
            const pair = p.split('=');
            if (pair.length === 2) statusMap[pair[0]] = pair[1];
        });

        if (statusMap.preset && !isNaN(parseInt(statusMap.preset))) {
            presetSelect.value = statusMap.preset;
        }

        if (statusMap.vol && !isNaN(parseFloat(statusMap.vol))) {
            volSlider.value = statusMap.vol;
            volLabel.textContent = statusMap.vol;
        }

        if (statusMap.loudness) {
            isLoudness = statusMap.loudness === '1';
            loudnessBtn.textContent = `Loudness: ${isLoudness ? 'ON' : 'OFF'}`;
        }

        if (statusMap.leveling) {
            isLeveling = statusMap.leveling === '1';
            levelingBtn.textContent = `Leveling: ${isLeveling ? 'ON' : 'OFF'}`;
        }

        if (statusMap.crossfeed) {
            isCrossfeed = statusMap.crossfeed === '1';
            crossfeedBtn.textContent = `Crossfeed: ${isCrossfeed ? 'ON' : 'OFF'}`;
        }

        if (statusMap.samplerate) {
            document.getElementById('srText').textContent = statusMap.samplerate + ' Hz';
        }

        if (statusMap.input) {
            const inputSource = statusMap.input;
            if (inputSource === 'Usb' || inputSource === 'Spdif') {
                inputBtn.textContent = inputSource.toUpperCase();
                inputBtn.style.background = inputSource.toLowerCase() === 'spdif' ? '#0056b3' : '#007bff';
            }
        }
    }

    document.getElementById('idText').textContent = await sendCommand('get_deviceid');
    await updateOpticalUsbButton();
}

refreshBtn.onclick = refresh;
window.onload = refresh;
