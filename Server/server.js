// King's Court relay server.
// 1. Serves the WebGL build (so phones just open http://<this PC>:8000).
// 2. Relays messages between the host phone and the other phones by 4-letter join code at /ws.
// It has NO game logic: the host phone runs the rules and decides what is legal.
//
//   node server.js [path-to-webgl-build]      (default: ../Builds/WebGL, port 8000 or $PORT)

const http = require('http');
const fs = require('fs');
const path = require('path');
const { WebSocketServer } = require('ws');

const PORT = Number(process.env.PORT || 8000);
const ROOT = path.resolve(process.argv[2] || path.join(__dirname, '..', 'Builds', 'WebGL'));
const MAX_PEERS = 11;               // host + 11 = more than the 10 seats a game needs
const LETTERS = 'ABCDEFGHJKLMNPQRSTUVWXYZ';

const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.wasm': 'application/wasm',
  '.data': 'application/octet-stream', '.png': 'image/png', '.ico': 'image/x-icon',
  '.css': 'text/css', '.json': 'application/json', '.svg': 'image/svg+xml', '.txt': 'text/plain',
};

const rooms = new Map();            // code -> { host: ws, peers: Map<id, ws>, nextId }

function log(...a) { console.log(new Date().toISOString().slice(11, 19), ...a); }

// The best guess at an address phones on the same Wi-Fi can actually reach. Link-local (169.254.x.x,
// assigned when a real address never came through, e.g. no DHCP) is a last resort, not a real LAN
// address, so a normal private address is always preferred over one when both are present.
function lanAddresses() {
  const nets = require('os').networkInterfaces();
  const all = [];
  for (const list of Object.values(nets))
    for (const n of list) if (n.family === 'IPv4' && !n.internal) all.push(n.address);
  const real = all.filter((a) => !a.startsWith('169.254.'));
  return real.length > 0 ? real : all;
}

function newCode() {
  for (let tries = 0; tries < 1000; tries++) {
    let c = '';
    for (let i = 0; i < 4; i++) c += LETTERS[Math.floor(Math.random() * LETTERS.length)];
    if (!rooms.has(c)) return c;
  }
  throw new Error('no free room codes');
}

function send(ws, obj) {
  if (ws && ws.readyState === 1) ws.send(JSON.stringify(obj));
}

// ---------------------------------------------------------------- static files

const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://x');
  if (url.pathname === '/health') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    return res.end(JSON.stringify({ ok: true, rooms: rooms.size }));
  }
  let rel = decodeURIComponent(url.pathname);
  if (rel === '/' || rel === '') rel = '/index.html';
  const file = path.normalize(path.join(ROOT, rel));
  if (!file.startsWith(ROOT)) { res.writeHead(403); return res.end('forbidden'); }
  fs.stat(file, (err, st) => {
    if (err || !st.isFile()) { res.writeHead(404); return res.end('not found'); }
    res.writeHead(200, {
      'Content-Type': MIME[path.extname(file).toLowerCase()] || 'application/octet-stream',
      'Content-Length': st.size,
      'Cache-Control': 'no-store',
    });
    if (req.method === 'HEAD') return res.end();
    fs.createReadStream(file).pipe(res);
  });
});

// ---------------------------------------------------------------- relay

const wss = new WebSocketServer({ server, path: '/ws', maxPayload: 512 * 1024 });

wss.on('connection', (ws) => {
  ws.alive = true;
  ws.on('pong', () => { ws.alive = true; });

  ws.on('message', (raw) => {
    // A phone answers a question a few times a second at most. Anything far above that is a bug or abuse:
    // drop it so one phone cannot bury the host.
    const now = Date.now();
    if (!ws.win || now - ws.win.start >= 1000) ws.win = { start: now, n: 0 };
    if (++ws.win.n > 60) return;

    let m;
    try { m = JSON.parse(raw.toString()); } catch (e) { return; }
    if (!m || typeof m.t !== 'string') return;

    switch (m.t) {
      case 'host': {
        if (ws.room) return send(ws, { t: 'error', reason: 'Already in a game.' });
        const code = newCode();
        rooms.set(code, { host: ws, peers: new Map(), nextId: 1 });
        ws.room = code; ws.role = 'host';
        const addrs = lanAddresses();
        const lan = addrs.length > 0 ? `${addrs[0]}:${PORT}` : '';
        send(ws, { t: 'hosted', code, lan });
        log('room', code, 'opened');
        break;
      }
      case 'join': {
        if (ws.room) return send(ws, { t: 'error', reason: 'Already in a game.' });
        const code = String(m.code || '').toUpperCase().trim();
        const room = rooms.get(code);
        if (!room) return send(ws, { t: 'error', reason: 'No game with that code.' });
        if (room.peers.size >= MAX_PEERS) return send(ws, { t: 'error', reason: 'That game is full.' });
        const id = room.nextId++;
        room.peers.set(id, ws);
        ws.room = code; ws.role = 'peer'; ws.id = id;
        send(ws, { t: 'joined', id });
        send(room.host, { t: 'peer_joined', id, name: String(m.name || '').slice(0, 24) });
        log('room', code, 'peer', id, 'joined (' + room.peers.size + ')');
        break;
      }
      case 'msg': {   // peer -> host
        const room = rooms.get(ws.room);
        if (!room || ws.role !== 'peer') return;
        send(room.host, { t: 'msg', from: ws.id, data: String(m.data) });
        break;
      }
      case 'to': {    // host -> one peer
        const room = rooms.get(ws.room);
        if (!room || ws.role !== 'host') return;
        send(room.peers.get(m.id), { t: 'msg', data: String(m.data) });
        break;
      }
      case 'all': {   // host -> every peer
        const room = rooms.get(ws.room);
        if (!room || ws.role !== 'host') return;
        for (const p of room.peers.values()) send(p, { t: 'msg', data: String(m.data) });
        break;
      }
    }
  });

  ws.on('close', () => {
    const room = rooms.get(ws.room);
    if (!room) return;
    if (ws.role === 'host') {
      for (const p of room.peers.values()) { send(p, { t: 'closed' }); p.close(); }
      rooms.delete(ws.room);
      log('room', ws.room, 'closed (host left)');
    } else if (ws.role === 'peer') {
      room.peers.delete(ws.id);
      send(room.host, { t: 'peer_left', id: ws.id });
      log('room', ws.room, 'peer', ws.id, 'left');
    }
  });
});

// Drop dead connections (a phone that lost signal without closing).
setInterval(() => {
  for (const ws of wss.clients) {
    if (!ws.alive) { ws.terminate(); continue; }
    ws.alive = false;
    ws.ping();
  }
}, 20000);

if (require.main === module) {
  server.listen(PORT, '0.0.0.0', () => {
    log(`King's Court server on port ${PORT}, serving ${ROOT}`);
    for (const addr of lanAddresses()) log(`  phones on this Wi-Fi open  http://${addr}:${PORT}`);
  });
}

module.exports = { server, wss, rooms };
