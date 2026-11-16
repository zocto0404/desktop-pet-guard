export function crc16(text) {
    let crc = 0xffff;
    for (let i = 0; i < text.length; i++) {
        crc ^= text.charCodeAt(i) << 8;
        for (let bit = 0; bit < 8; bit++)
            crc = (crc & 0x8000) ? ((crc << 1) ^ 0x1021) & 0xffff : (crc << 1) & 0xffff;
    }
    return crc;
}
export function encodePacket(p) {
    const body = ['DA1', p.boot, p.seq, p.tUs, ...p.acc.map(v => v.toFixed(5)), ...p.gyro.map(v => v.toFixed(4))].join(',');
    return body + '*' + crc16(body).toString(16).toUpperCase().padStart(4, '0') + '\n';
}
export function parsePacket(line, legacySignedClock = false) {
    line = line.replace(/\r$/, '');
    if (line.length > 256 || !/^DA1,[\x20-\x7e]+\*[0-9a-fA-F]{4}$/.test(line))
        return null;
    const star = line.lastIndexOf('*'), body = line.slice(0, star);
    if (crc16(body) !== parseInt(line.slice(star + 1), 16))
        return null;
    const f = body.split(',');
    if (f.length !== 10 || !/^[0-9a-f]{8}$/i.test(f[1]))
        return null;
    if (!f.slice(2).every(v => /^-?\d+(?:\.\d+)?$/.test(v)))
        return null;
    const nums = f.slice(2).map(Number), [seq, tUs, ...raw] = nums;
    const minTime = legacySignedClock ? -0x80000000 : 0;
    if (!nums.every(Number.isFinite) || !Number.isInteger(seq) || seq < 0 || seq > 0xffffffff || !Number.isSafeInteger(tUs) || tUs < minTime)
        return null;
    if (raw.slice(0, 3).some(v => Math.abs(v) > 16) || raw.slice(3).some(v => Math.abs(v) > 2200) || Math.hypot(...raw.slice(0, 3)) < .05)
        return null;
    return { boot: f[1].toLowerCase(), seq, tUs: tUs < 0 ? tUs + 0x100000000 : tUs, acc: raw.slice(0, 3), gyro: raw.slice(3) };
}
