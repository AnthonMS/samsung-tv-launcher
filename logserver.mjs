// Dev helper: receives the TV launcher's log lines (and optional JSON dumps) over the LAN,
// so testing doesn't need photos of the on-screen log. Only accepts the TV's address.
//   node logserver.mjs <output-dir>
// Turn on in the launcher: Settings, "Send log to this PC".
// POST /log   text/plain, one or more lines -> appended to launcher.log
// POST /dump?name=x   any body -> saved as dump-x.json

import { createServer } from 'node:http';
import { appendFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';

const TV_IP = '192.168.1.46';
const PORT = 8766;
const out = process.argv[2] || '.';
mkdirSync(out, { recursive: true });

createServer((req, res) => {
  const from = (req.socket.remoteAddress || '').replace('::ffff:', '');
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Headers', '*');
  if (from !== TV_IP) { res.writeHead(403).end(); return; }
  if (req.method === 'OPTIONS') { res.writeHead(204).end(); return; }
  let body = '';
  req.on('data', (c) => { body += c; if (body.length > 5e6) req.destroy(); });
  req.on('end', () => {
    const url = new URL(req.url, 'http://x');
    if (url.pathname === '/log') {
      appendFileSync(join(out, 'launcher.log'), body.endsWith('\n') ? body : body + '\n');
    } else if (url.pathname === '/dump') {
      const name = (url.searchParams.get('name') || 'dump').replace(/[^\w-]/g, '');
      writeFileSync(join(out, `dump-${name}.json`), body);
    }
    res.writeHead(204).end();
  });
}).listen(PORT, '192.168.1.10', () => console.log(`log server on 192.168.1.10:${PORT}, writing to ${out}`));
