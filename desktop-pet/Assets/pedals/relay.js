import {characterState} from './PedalContact.js';
import {PlayablePedal} from './PlayablePedal.js';
import {PedalSerial} from './PedalSerial.js';

const get = id => document.getElementById(id);
const write = (node, text) => { if (node.textContent !== text) node.textContent = text; };
const names = ['左脚', '右脚'];
const waiting = '已连接桌宠，等待左右踏板就绪。';
const token = new URLSearchParams(location.hash.slice(1)).get('pet');
const connection = get('link');
const socket = token && /^[a-f0-9]{48}$/.test(token)
  ? new WebSocket(`ws://127.0.0.1:5180/pedals/?token=${token}`) : null;
let sentAt = -Infinity;
let lastContacts = '';
if (socket) {
  write(connection, '正在连接桌宠…');
  socket.addEventListener('open', () => write(connection, waiting));
  socket.addEventListener('message', ({data}) => {
    if (data === lastContacts) write(connection, data.includes('?') ? waiting : '桌宠正在接收左右脚状态。');
  });
  socket.addEventListener('close', () => write(connection, '连接已断开，请从桌宠右键菜单重新打开接收页。'));
}

get('pedals').innerHTML = names.map((name, side) => `
  <section class="pedal" aria-labelledby="title-${side}">
    <h2 id="title-${side}">${name}</h2>
    <strong id="contact-${side}" class="contact">未连接</strong>
    <div class="actions">
      <button id="connect-${side}" type="button" aria-label="连接${name} USB">连接 USB</button>
      <button id="disconnect-${side}" type="button" disabled>断开</button>
    </div>
    <div class="actions calibrate">
      <button id="empty-${side}" type="button" disabled>记录离板</button>
      <button id="loaded-${side}" type="button" disabled>记录踩住</button>
      <button id="extra-empty-${side}" type="button" disabled>补录空板角度</button>
    </div>
    <p id="info-${side}" class="info" role="status"></p>
    <p id="reading-${side}" class="reading"></p>
    <details>
      <summary>方向与下踩灵敏度</summary>
      <label><input id="horizontal-${side}" type="checkbox">反转左右</label>
      <label><input id="vertical-${side}" type="checkbox">反转前后</label>
      <label>下踩灵敏度 <input id="force-${side}" type="range" min="0" max="2" value="1"></label>
    </details>
  </section>`).join('');

const feet = names.map((_, side) => {
  const pedal = new PlayablePedal(`desk-away-pedal-play-v1-${side}`);
  const element = id => get(`${id}-${side}`);
  let stomps = 0;
  const serial = new PedalSerial(packet => {
    pedal.update(packet);
    const stomp = pedal.takeStomp();
    if (stomp) stomps++;
    publish(stomp);
  });
  const connect = element('connect');
  const disconnect = element('disconnect');
  const buttons = ['empty', 'loaded', 'extra-empty'].map(pose => {
    const button = element(pose);
    button.onclick = () => pedal.begin(pose);
    return {pose, button};
  });
  connect.onclick = () => { pedal.disconnect(); void serial.connect(); refresh(); };
  disconnect.onclick = () => { void serial.disconnect(); };
  element('horizontal').onchange = ({target}) => { pedal.invertSide = target.checked; };
  element('vertical').onchange = ({target}) => { pedal.invertForward = target.checked; };
  element('force').oninput = ({target}) => { pedal.stompThreshold = [.2, .14, .09][target.valueAsNumber]; };
  return {
    serial,
    get state() { return serial.fresh ? pedal.contact.state : 'unknown'; },
    get values() {
      const state = this.state;
      return [state === 'loaded' ? '1' : state === 'empty' ? '0' : '?',
        state === 'loaded' ? pedal.x.toFixed(4) : '0', state === 'loaded' ? pedal.y.toFixed(4) : '0', stomps];
    },
    render() {
      const contact = pedal.contact;
      const state = this.state;
      const fresh = serial.fresh;
      if (!fresh && contact.state !== 'unknown') { contact.pause(); pedal.clearMotion(); }
      connect.disabled = !serial.supported || serial.port !== null || serial.busy;
      disconnect.disabled = !serial.port;
      for (const {button, pose} of buttons) {
        button.disabled = !fresh || !!contact.capturing || (pose === 'loaded' && !contact.hasEmpty)
          || (pose === 'extra-empty' && contact.separation === null);
      }
      element('contact').dataset.contact = state;
      write(element('contact'), state === 'loaded' ? '踩住' : state === 'empty' ? '离板'
        : fresh ? contact.calibrationOverlap ? '无法区分' : '待确认' : serial.port ? '等待数据' : '未连接');
      write(element('info'), !serial.supported ? '请用电脑上的 Chrome 或 Edge 打开本页。' : fresh
        ? contact.message || '把脚移开，记录离板姿势。'
        : `${pedal.saved ? '有已保存的校准 · ' : ''}${serial.status}`);
      const angle = contact.emptyAngle === null ? '' : ` · 距空板 ${contact.emptyAngle.toFixed(1)}°`;
      write(element('reading'), !fresh ? '' : contact.capturing ? `采样 ${Math.round(contact.progress * 100)}%`
        : contact.separation === null ? '' : `左右 ${pedal.x.toFixed(2)} · 前后 ${pedal.y.toFixed(2)} · 下踩 ${stomps} 次${angle}`);
    },
  };
});

function publish(stomp = false) {
  if (socket?.readyState !== WebSocket.OPEN || socket.bufferedAmount > 1024) return;
  const [left, right] = feet.map(foot => foot.values);
  const now = performance.now();
  const contacts = left[0] + right[0];
  if (!stomp && contacts === lastContacts && now - sentAt < 40) return;
  socket.send(['M1', ...left, ...right].join(','));
  if (contacts.includes('?')) write(connection, waiting);
  lastContacts = contacts;
  sentAt = now;
}

const states = {waiting: '等待双脚就绪', apart: '全身散架', whole: '完整站立',
  'lean-left': '向左偏斜 · 局部散开', 'lean-right': '向右偏斜 · 局部散开'};
function refresh() {
  for (const foot of feet) foot.render();
  const state = characterState(...feet.map(foot => foot.state));
  get('result').dataset.state = state;
  write(get('state'), states[state]);
  publish();
}
refresh();
setInterval(refresh, 100);
window.addEventListener('pagehide', () => {
  socket?.close();
  for (const foot of feet) void foot.serial.disconnect();
});
