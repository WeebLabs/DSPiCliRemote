const volSlider = document.getElementById('volSlider');
const volLabel = document.getElementById('volLabel');
const loudnessBtn = document.getElementById('loudnessBtn');
const levelingBtn = document.getElementById('levelingBtn');
const crossfeedBtn = document.getElementById('crossfeedBtn');
const presetSelect = document.getElementById('presetSelect');
const refreshBtn = document.getElementById('refreshBtn');
const logDiv = document.getElementById('log');

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

    const activePreset = await sendCommand('get_activepreset');
    if (activePreset && !isNaN(parseInt(activePreset))) {
        presetSelect.value = activePreset;
    }

    const vol = await sendCommand('get_vol');
    if (!isNaN(parseFloat(vol))) {
        volSlider.value = vol;
        volLabel.textContent = vol;
    }

    const loudness = await sendCommand('get_loudness');
    isLoudness = loudness.toLowerCase() === 'true';
    loudnessBtn.textContent = `Loudness: ${isLoudness ? 'ON' : 'OFF'}`;

    const leveling = await sendCommand('get_leveling');
    isLeveling = leveling.toLowerCase() === 'true';
    levelingBtn.textContent = `Leveling: ${isLeveling ? 'ON' : 'OFF'}`;

    const crossfeed = await sendCommand('get_crossfeed');
    isCrossfeed = crossfeed.toLowerCase() === 'true';
    crossfeedBtn.textContent = `Crossfeed: ${isCrossfeed ? 'ON' : 'OFF'}`;

    document.getElementById('srText').textContent = await sendCommand('get_samplerate') + ' Hz';
    document.getElementById('idText').textContent = await sendCommand('get_deviceid');
}

refreshBtn.onclick = refresh;
window.onload = refresh;
