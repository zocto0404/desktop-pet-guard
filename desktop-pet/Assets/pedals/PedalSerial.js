import {parsePacket} from './MotionProtocol.js';

export class PedalSerial {
  constructor(onPacket) { this.onPacket = onPacket; }
  port = null;
  reader = null;
  task = null;
  busy = false;
  status = '未连接';
  lastArrival = -Infinity;

  get supported() { return isSecureContext && !!navigator.serial; }
  get fresh() { return this.port !== null && performance.now() - this.lastArrival < 500; }

  async connect() {
    if (!this.supported || this.port || this.busy) return;
    this.busy = true;
    try {
      const port = await navigator.serial.requestPort();
      await port.open({baudRate: 115200});
      this.port = port;
      this.status = '串口已打开，等待 DA1 数据。';
      this.task = this.read(port);
    } catch (error) {
      this.status = error.name === 'NotFoundError' ? '未选择串口。' : `连接失败。确认串口已在其他页面断开。${error.message}`;
    } finally {
      this.busy = false;
    }
  }

  async read(port) {
    const decoder = new TextDecoder();
    let line = '', discard = false, previous;
    try {
      this.reader = port.readable.getReader();
      while (true) {
        const {value, done} = await this.reader.read();
        if (done) break;
        for (const char of decoder.decode(value, {stream: true})) {
          if (char !== '\n') {
            if (!discard) {
              line += char;
              if (line.length > 256) { discard = true; line = ''; }
            }
            continue;
          }
          const packet = discard ? null : parsePacket(line);
          if (packet) {
            const step = (packet.seq - (previous?.seq ?? 0)) >>> 0;
            if (previous?.boot !== packet.boot || (step > 0 && step < 0x80000000 && packet.tUs > previous.tUs)) {
              previous = packet;
              this.lastArrival = performance.now();
              this.onPacket(packet);
            }
          } else if (line.startsWith('# MPU6050') || line.startsWith('# IMU')) {
            this.status = '串口已打开，请检查传感器供电及 SDA / SCL。';
          }
          line = '';
          discard = false;
        }
      }
    } catch {
      // A USB removal can reject a pending read and an attempted close.
    } finally {
      this.reader?.releaseLock();
      this.reader = null;
      try { await port.close(); } catch { /* Already unplugged. */ }
      this.port = null;
      this.lastArrival = -Infinity;
      this.status = 'USB 已断开。';
    }
  }

  async disconnect() {
    try { await this.reader?.cancel(); } catch { /* Already unplugged. */ }
    await this.task;
  }
}
