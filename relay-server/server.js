// TAMSDESK relay server
// A thin WebSocket relay that lets two TAMSDESK clients find each other over the
// internet by ID and then forwards messages between them. It never sees the
// session password (the host validates that) and it does not store any traffic.
//
// Run on a VPS:  PORT=8080 node server.js
// Put it behind TLS (nginx / caddy) so clients connect with wss://

const WebSocket = require('ws');
const http = require('http');

const PORT = process.env.PORT || 8080;

// id -> host socket
const hosts = new Map();

const server = http.createServer((req, res) => {
  // simple health check
  res.writeHead(200, { 'Content-Type': 'text/plain' });
  res.end('TAMSDESK relay OK\n');
});

const wss = new WebSocket.Server({ server });

function sendJson(ws, obj) {
  try { ws.send(JSON.stringify(obj)); } catch (e) { /* ignore */ }
}

wss.on('connection', (ws) => {
  ws.role = null;          // 'host' | 'controller'
  ws.hostId = null;
  ws.peer = null;          // the socket we are linked to once a session starts
  ws._wantController = null;
  ws._wantHost = null;

  ws.on('message', (data, isBinary) => {
    // Binary = a screen frame (host -> controller). Forward verbatim to peer.
    if (isBinary) {
      if (ws.peer && ws.peer.readyState === WebSocket.OPEN) {
        ws.peer.send(data, { binary: true });
      }
      return;
    }

    let msg;
    try { msg = JSON.parse(data.toString()); } catch (e) { return; }

    switch (msg.type) {
      case 'register-host': {
        ws.role = 'host';
        ws.hostId = String(msg.id);
        hosts.set(ws.hostId, ws);
        sendJson(ws, { type: 'registered', id: ws.hostId });
        break;
      }

      case 'connect': {
        // controller -> relay: I want to reach host <target> with <password>
        const host = hosts.get(String(msg.target));
        if (!host || host.readyState !== WebSocket.OPEN) {
          sendJson(ws, { type: 'error', message: 'host-offline' });
          return;
        }
        if (host.peer) {
          sendJson(ws, { type: 'error', message: 'host-busy' });
          return;
        }
        ws.role = 'controller';
        ws._wantHost = host;
        host._wantController = ws;
        // Ask the host to approve. Host validates the password locally.
        sendJson(host, { type: 'connect-request', password: msg.password });
        break;
      }

      case 'accept': {
        // host approved the pending controller
        const c = ws._wantController;
        if (c && c.readyState === WebSocket.OPEN) {
          ws.peer = c;
          c.peer = ws;
          sendJson(c, { type: 'accepted' });
          sendJson(ws, { type: 'session-start' });
        }
        ws._wantController = null;
        break;
      }

      case 'reject': {
        const c = ws._wantController;
        if (c && c.readyState === WebSocket.OPEN) {
          sendJson(c, { type: 'rejected', reason: msg.reason || 'denied' });
        }
        ws._wantController = null;
        break;
      }

      default: {
        // Any other control message (input events, clipboard, resolution, ...)
        // is forwarded to the linked peer unchanged.
        if (ws.peer && ws.peer.readyState === WebSocket.OPEN) {
          sendJson(ws.peer, msg);
        }
      }
    }
  });

  ws.on('close', () => {
    if (ws.role === 'host' && ws.hostId) hosts.delete(ws.hostId);
    if (ws.peer && ws.peer.readyState === WebSocket.OPEN) {
      sendJson(ws.peer, { type: 'peer-disconnected' });
      ws.peer.peer = null;
    }
  });

  ws.on('error', () => { /* ignore, close will fire */ });
});

server.listen(PORT, () => {
  console.log('TAMSDESK relay listening on port ' + PORT);
});
