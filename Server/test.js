// Protocol test for the relay: node test.js
process.env.PORT = '0';
const WebSocket = require('ws');
const { server, rooms } = require('./server');

let failures = 0;
function check(cond, what) { if (!cond) { failures++; console.log('FAIL:', what); } else console.log('ok  :', what); }

function client(port) {
  const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
  const inbox = [];
  const waiters = [];
  ws.on('message', (raw) => {
    const m = JSON.parse(raw.toString());
    const w = waiters.findIndex((x) => x.t === m.t);
    if (w >= 0) waiters.splice(w, 1)[0].resolve(m); else inbox.push(m);
  });
  ws.next = (t) => new Promise((resolve, reject) => {
    const i = inbox.findIndex((m) => m.t === t);
    if (i >= 0) return resolve(inbox.splice(i, 1)[0]);
    const waiter = { t, resolve };
    waiters.push(waiter);
    setTimeout(() => {
      const i = waiters.indexOf(waiter);
      if (i >= 0) waiters.splice(i, 1);          // a timed-out wait must not swallow a later message
      reject(new Error('timeout waiting for ' + t));
    }, 2000);
  });
  ws.sendJson = (o) => ws.send(JSON.stringify(o));
  return new Promise((r) => ws.on('open', () => r(ws)));
}

(async () => {
  await new Promise((r) => server.listen(0, '127.0.0.1', r));
  const port = server.address().port;

  const host = await client(port);
  host.sendJson({ t: 'host' });
  const hosted = await host.next('hosted');
  check(/^[A-Z]{4}$/.test(hosted.code), 'host gets a 4-letter code (' + hosted.code + ')');

  const bad = await client(port);
  bad.sendJson({ t: 'join', code: 'ZZZZ', name: 'x' });
  check((await bad.next('error')).reason.includes('No game'), 'unknown code is refused');
  bad.close();

  const a = await client(port), b = await client(port);
  a.sendJson({ t: 'join', code: hosted.code.toLowerCase(), name: 'Ann' });
  b.sendJson({ t: 'join', code: hosted.code, name: 'Bo' });
  const ja = await a.next('joined'), jb = await b.next('joined');
  check(ja.id !== jb.id, 'peers get different ids');
  const pj = [await host.next('peer_joined'), await host.next('peer_joined')];
  check(pj.map((p) => p.name).sort().join() === 'Ann,Bo', 'host is told who joined');

  a.sendJson({ t: 'msg', data: 'hello host' });
  const got = await host.next('msg');
  check(got.data === 'hello host' && got.from === ja.id, 'peer message reaches host with sender id');

  host.sendJson({ t: 'to', id: jb.id, data: 'only bo' });
  check((await b.next('msg')).data === 'only bo', 'host can message one peer');
  host.sendJson({ t: 'all', data: 'everyone' });
  check((await a.next('msg')).data === 'everyone' && (await b.next('msg')).data === 'everyone', 'host can message everyone');

  // A peer cannot impersonate the host.
  a.sendJson({ t: 'all', data: 'fake' });
  a.sendJson({ t: 'to', id: jb.id, data: 'fake' });
  await new Promise((r) => setTimeout(r, 150));
  check(!(await b.next('msg').then(() => true, () => false)), 'peers cannot broadcast as the host');

  // A phone that floods the server is throttled, and the host still gets a normal message afterwards.
  await new Promise((r) => setTimeout(r, 1100));
  for (let i = 0; i < 500; i++) b.sendJson({ t: 'msg', data: 'spam' + i });
  await new Promise((r) => setTimeout(r, 400));
  let spam = 0;
  while (await host.next('msg').then(() => true, () => false)) spam++;
  check(spam > 0 && spam <= 61, 'flooding phone is throttled (' + spam + ' of 500 delivered)');
  await new Promise((r) => setTimeout(r, 1100));
  b.sendJson({ t: 'msg', data: 'after the flood' });
  check((await host.next('msg')).data === 'after the flood', 'the phone works again after the flood');

  a.close();
  check((await host.next('peer_left')).id === ja.id, 'host is told when a peer leaves');

  host.close();
  check(!!(await b.next('closed')), 'peers are told when the host leaves');
  await new Promise((r) => setTimeout(r, 100));
  check(rooms.size === 0, 'room is removed');

  console.log(failures === 0 ? 'RELAY TESTS PASSED' : failures + ' FAILURES');
  process.exit(failures === 0 ? 0 : 1);
})().catch((e) => { console.log('ERROR', e.message); process.exit(1); });
