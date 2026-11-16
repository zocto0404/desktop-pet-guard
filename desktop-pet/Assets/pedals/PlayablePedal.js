import { PedalContact } from './PedalContact.js';
const validVector = (v) => Array.isArray(v) && v.length === 3 && v.every(value => Number.isFinite(value));
function validRecording(value) {
    const record = value;
    return !!record && ['empty', 'loaded', 'extra-empty'].includes(record.pose) &&
        Array.isArray(record.samples) && record.samples.length >= 20 && record.samples.length <= 240 &&
        record.samples.every((sample, index, samples) => sample && Number.isSafeInteger(sample.tUs) &&
            sample.tUs >= 0 && sample.tUs <= 1300000 && (!index || sample.tUs > samples[index - 1].tUs) &&
            validVector(sample.acc) && validVector(sample.gyro));
}
const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const difference = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const length = (v) => Math.hypot(...v);
const normalize = (v) => { const n = length(v) || 1; return [v[0] / n, v[1] / n, v[2] / n]; };
const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const average = (samples) => samples.reduce((sum, s) => [sum[0] + s.acc[0] / samples.length, sum[1] + s.acc[1] / samples.length, sum[2] + s.acc[2] / samples.length], [0, 0, 0]);
const move = (v) => Math.sign(v) * Math.min(1, Math.max(0, (Math.abs(v) - .023) / .16));
export class PlayablePedal {
    storageKey;
    contact = new PedalContact();
    x = 0;
    y = 0;
    strike = 0;
    stompThreshold = .14;
    invertSide = false;
    invertForward = false;
    saved = false;
    boot;
    recordings = [];
    previousCount = 0;
    samples = [];
    axes;
    baseline = 1;
    lastTime;
    lastStomp = -1;
    pendingStomp = false;
    armed = true;
    constructor(storageKey) {
        this.storageKey = storageKey;
        try {
            const data = JSON.parse(localStorage.getItem(storageKey) ?? 'null');
            if (data?.version === 1 && Array.isArray(data.recordings) && data.recordings.length <= 9 &&
                data.recordings[0]?.pose === 'empty' && data.recordings[1]?.pose === 'loaded' &&
                data.recordings.slice(2).every((record) => record?.pose === 'extra-empty') &&
                data.recordings.every(validRecording))
                this.recordings = data.recordings;
            this.saved = this.recordings.some(record => record.pose === 'loaded');
        }
        catch { /* Calibration is still available without browser storage. */ }
    }
    begin(pose) {
        this.previousCount = this.contact.emptyPoseCount;
        this.samples = [];
        this.contact.begin(pose);
    }
    disconnect() {
        this.boot = undefined;
        this.contact = new PedalContact();
        this.clearMotion();
    }
    clearMotion() {
        this.x = this.y = this.strike = 0;
        this.lastTime = undefined;
        this.pendingStomp = false;
        this.lastStomp = -1;
        this.armed = true;
    }
    takeStomp() {
        const value = this.pendingStomp;
        this.pendingStomp = false;
        return value;
    }
    update(packet) {
        if (this.boot !== packet.boot) {
            this.boot = packet.boot;
            this.restore(packet.boot);
            this.clearMotion();
        }
        const capturing = this.contact.capturing;
        if (capturing) {
            if (this.samples.length && packet.tUs - this.samples.at(-1).tUs > 250000)
                this.samples = [];
            const { tUs, acc, gyro } = packet;
            this.samples.push({ tUs, acc, gyro });
            while (this.samples.length > 240 || tUs - this.samples[0].tUs > 1300000)
                this.samples.shift();
        }
        this.contact.update(packet);
        if (capturing && !this.contact.capturing)
            this.finishRecording(capturing, packet.boot);
        this.updateMotion(packet);
    }
    restore(boot) {
        const empty = this.recordings.find(record => record.pose === 'empty');
        const loaded = this.recordings.find(record => record.pose === 'loaded');
        this.contact = new PedalContact();
        this.axes = undefined;
        if (!empty || !loaded)
            return;
        try {
            for (const record of this.recordings)
                this.applyRecording(this.contact, record, boot);
            if (this.contact.separation === null)
                throw new Error('Calibration is incomplete');
            this.setAxes(empty, loaded);
            this.contact.pause();
            this.contact.message = '已恢复这只脚的校准。';
        }
        catch {
            this.contact = new PedalContact();
            this.saved = false;
        }
    }
    applyRecording(detector, record, boot) {
        detector.begin(record.pose);
        record.samples.forEach((sample, seq) => detector.update({ ...sample, seq, boot }));
    }
    finishRecording(pose, boot) {
        const done = pose === 'empty' ? this.contact.hasEmpty :
            pose === 'loaded' ? this.contact.separation !== null : this.contact.emptyPoseCount > this.previousCount;
        if (!done)
            return;
        // Capture completed with a verified still window. Retain its time series for reuse.
        const first = this.samples[0].tUs;
        const record = { pose, samples: this.samples.map(sample => ({ ...sample, tUs: sample.tUs - first })) };
        const previousEmpty = this.recordings.find(item => item.pose === 'empty');
        const extras = this.recordings.filter(item => item.pose === 'extra-empty');
        if (pose === 'empty') {
            this.recordings = [record];
            this.axes = undefined;
        }
        else if (pose === 'loaded' && previousEmpty) {
            this.recordings = [previousEmpty, record, ...extras];
            this.setAxes(previousEmpty, record);
        }
        else if (pose === 'extra-empty') {
            this.recordings.push(record);
        }
        this.saved = false;
        if (this.contact.separation !== null) {
            const restored = new PedalContact();
            this.recordings.forEach(item => this.applyRecording(restored, item, boot));
            if (restored.separation === null || restored.emptyPoseCount !== this.contact.emptyPoseCount)
                return;
            try {
                localStorage.setItem(this.storageKey, JSON.stringify({ version: 1, recordings: this.recordings }));
                this.saved = true;
            }
            catch { /* Browser storage unavailable. */ }
        }
    }
    setAxes(empty, loaded) {
        const resting = normalize(average(empty.samples));
        const neutral = average(loaded.samples);
        const up = normalize(neutral);
        const forward = normalize(difference(up, resting));
        const side = normalize(cross(up, forward));
        this.axes = { up, forward: normalize(cross(side, up)), side };
        this.baseline = length(neutral);
    }
    updateMotion(packet) {
        const time = packet.tUs / 1e6;
        const dt = Math.max(0, Math.min(.15, time - (this.lastTime ?? time - .01)));
        this.lastTime = time;
        const axes = this.axes;
        if (!axes || this.contact.state !== 'loaded' || this.contact.capturing) {
            this.x = this.y = this.strike = 0;
            this.pendingStomp = false;
            return;
        }
        const gravity = length(packet.acc);
        const blend = 1 - Math.exp(-dt / .09);
        if (Math.abs(gravity - this.baseline) < .14) {
            const direction = normalize(packet.acc);
            this.x += (move(dot(direction, axes.side)) * (this.invertSide ? -1 : 1) - this.x) * blend;
            this.y += (move(dot(direction, axes.forward)) * (this.invertForward ? -1 : 1) - this.y) * blend;
        }
        this.baseline += (gravity - this.baseline) * (1 - Math.exp(-dt / .85));
        this.strike = Math.max(0, gravity - this.baseline);
        if (this.strike < this.stompThreshold * .4)
            this.armed = true;
        if (this.armed && this.strike >= this.stompThreshold && time - this.lastStomp > .32) {
            this.pendingStomp = true;
            this.armed = false;
            this.lastStomp = time;
        }
    }
}
