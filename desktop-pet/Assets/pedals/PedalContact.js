const magnitude = ([x, y, z]) => Math.hypot(x, y, z);
const subtract = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const unit = (v) => {
    const length = magnitude(v);
    return [v[0] / length, v[1] / length, v[2] / length];
};
const degrees = (chord) => 2 * Math.asin(Math.min(1, chord / 2)) * 180 / Math.PI;
const angleBetween = (a, b) => degrees(magnitude(subtract(a, b)));
const releaseHoldSeconds = .4;
const minimumSeparation = (a, b) => Math.max(2.5, 4 * Math.max(a.spread, b.spread));
const releaseRadius = (empty, loaded) => Math.min(angleBetween(empty.up, loaded.up) * .44, Math.max(1.5, 2.2 * empty.spread));
function average(samples, field) {
    const mean = [0, 0, 0];
    for (const sample of samples) {
        sample[field].forEach((value, index) => { mean[index] += value / samples.length; });
    }
    return mean;
}
export class PedalContact {
    state = 'unknown';
    message = '';
    progress = 0;
    emptyAngle = null;
    releaseProgress = 0;
    calibrationOverlap = false;
    boot;
    recording;
    emptyPoses = [];
    loaded;
    candidate = null;
    candidateSince = 0;
    lastTime;
    filteredAcc;
    filteredRotation;
    get emptyPoseCount() { return this.emptyPoses.length; }
    get hasEmpty() { return this.emptyPoseCount > 0; }
    get separation() {
        const loaded = this.loaded;
        return loaded ? Math.min(...this.emptyPoses.map(empty => angleBetween(loaded.up, empty.up))) : null;
    }
    get capturing() { return this.recording?.pose; }
    begin(pose) {
        if ((pose === 'loaded' && !this.hasEmpty) || (pose === 'extra-empty' && !this.loaded))
            return;
        this.pause();
        if (pose === 'empty')
            this.emptyPoses = [];
        if (pose !== 'extra-empty') {
            this.loaded = undefined;
            this.calibrationOverlap = false;
        }
        this.recording = { pose, samples: [] };
        this.message = '保持当前姿势，正在采样';
    }
    pause() {
        this.state = 'unknown';
        this.candidate = null;
        this.lastTime = undefined;
        this.emptyAngle = null;
        this.filteredAcc = undefined;
        this.filteredRotation = undefined;
        this.releaseProgress = 0;
        this.progress = 0;
        if (this.recording)
            this.recording = { pose: this.recording.pose, samples: [] };
    }
    reset() {
        this.boot = undefined;
        this.recording = undefined;
        this.emptyPoses = [];
        this.loaded = undefined;
        this.calibrationOverlap = false;
        this.message = '';
        this.pause();
    }
    update(packet) {
        if (this.boot !== undefined && this.boot !== packet.boot)
            this.reset();
        this.boot = packet.boot;
        const time = packet.tUs / 1e6;
        if (this.lastTime !== undefined && time - this.lastTime > .25)
            this.pause();
        const dt = this.lastTime === undefined ? .02 : time - this.lastTime;
        this.lastTime = time;
        const gravity = magnitude(packet.acc);
        if (this.recording) {
            this.capture(packet, gravity, time);
            return;
        }
        const loaded = this.loaded;
        if (!loaded)
            return;
        const blend = 1 - Math.exp(-dt / .08);
        this.filteredAcc = this.filteredAcc
            ? this.filteredAcc.map((value, index) => value + (packet.acc[index] - value) * blend) : packet.acc;
        const rotation = magnitude(subtract(packet.gyro, this.emptyPoses[0].gyroBias));
        const previousRotation = this.filteredRotation ?? rotation;
        const smoothedRotation = previousRotation + (rotation - previousRotation) * blend;
        this.filteredRotation = smoothedRotation;
        const smoothedGravity = magnitude(this.filteredAcc);
        const up = unit(this.filteredAcc);
        const angles = this.emptyPoses.map(empty => angleBetween(up, empty.up));
        this.emptyAngle = Math.min(...angles);
        // Each empty pose has its own narrow release zone. The space between them
        // can still contain a pressed tilt, so don't use a single expanded zone.
        const matchingPose = this.emptyPoses.findIndex((empty, index) => {
            const radius = releaseRadius(empty, loaded);
            return angles[index] <= radius &&
                Math.abs(smoothedGravity - empty.gravity) <= .08 &&
                smoothedRotation <= 4;
        });
        const awayFromRest = Math.abs(gravity - loaded.gravity) <= .18 &&
            this.emptyPoses.every((empty, index) => angles[index] >= releaseRadius(empty, loaded) + .7);
        const next = matchingPose >= 0 ? matchingPose : awayFromRest ? 'loaded' : null;
        if (next !== this.candidate) {
            this.candidate = next;
            this.candidateSince = time;
        }
        this.releaseProgress = typeof next === 'number' ? Math.min(1, (time - this.candidateSince) / releaseHoldSeconds) : 0;
        if (next === null)
            return;
        const holdTime = next === 'loaded' ? .16 : releaseHoldSeconds;
        if (time - this.candidateSince >= holdTime)
            this.state = next === 'loaded' ? 'loaded' : 'empty';
    }
    capture(packet, gravity, time) {
        const recording = this.recording;
        if (gravity < .65 || gravity > 1.35 || magnitude(packet.gyro) > 12) {
            this.recording = { pose: recording.pose, samples: [] };
            this.progress = 0;
            this.message = '等踏板稳定后继续采样';
            return;
        }
        recording.start ??= time;
        recording.samples.push(packet);
        this.message = '保持当前姿势，正在采样';
        this.progress = Math.min(1, (time - recording.start) / 1.2, recording.samples.length / 20);
        if (this.progress < 1)
            return;
        const mean = average(recording.samples, 'acc');
        const up = unit(mean);
        const spread = Math.max(...recording.samples.map(point => angleBetween(unit(point.acc), up)));
        if (spread > .9) {
            this.recording = { pose: recording.pose, samples: [] };
            this.progress = 0;
            this.message = '踏板有晃动，保持静止后继续采样';
            return;
        }
        this.recording = undefined;
        const sample = { up, spread, gravity: magnitude(mean), gyroBias: average(recording.samples, 'gyro') };
        if (recording.pose === 'empty') {
            this.emptyPoses = [sample];
            this.message = '离板已记录。把脚自然放上去，再记录踩住。';
            return;
        }
        if (recording.pose === 'extra-empty') {
            const loaded = this.loaded;
            if (angleBetween(up, loaded.up) < minimumSeparation(sample, loaded)) {
                this.message = '这个空板角度太接近踩住姿势，无法可靠区分。原来的校准已保留。';
                return;
            }
            if (this.emptyPoses.some(empty => angleBetween(up, empty.up) <= releaseRadius(empty, loaded))) {
                this.message = '这个空板角度已经覆盖，可以直接测试。';
                return;
            }
            if (this.emptyPoseCount >= 8) {
                this.message = '已记录 8 个空板角度。可点「记录离板」重新校准。';
                return;
            }
            this.emptyPoses.push(sample);
            this.message = '已补录空板角度。可以重新踩住，再旋转和抬脚测试。';
            return;
        }
        const conflict = this.emptyPoses.find(empty => angleBetween(up, empty.up) < minimumSeparation(sample, empty));
        if (conflict) {
            const separation = angleBetween(up, conflict.up);
            const minimum = minimumSeparation(sample, conflict);
            this.calibrationOverlap = true;
            this.message = `离板和自然踩住只差 ${separation.toFixed(2)}°，需要至少 ${minimum.toFixed(1)}°。调整踏板后请重新记录两种姿势。`;
            return;
        }
        this.loaded = sample;
        this.message = '可以测试。踏板回到空板姿势并稳住后会显示「离板」。';
    }
}
export function characterState(left, right) {
    if (left === 'unknown' || right === 'unknown')
        return 'waiting';
    if (left === 'loaded')
        return right === 'loaded' ? 'whole' : 'lean-left';
    return right === 'loaded' ? 'lean-right' : 'apart';
}
