const http = require('node:http');
const send = (res, status, value) => { res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(value === undefined ? '' : JSON.stringify(value)); };
http.createServer(async (req, res) => {
  const path = req.url.split('?')[0];
  if (req.method === 'POST' && path === '/v1/auth/token') {
    let body = req.body;
    if (!body) { let text = ''; for await (const chunk of req) text += chunk; try { body = JSON.parse(text); } catch { return send(res, 400, { title: 'Invalid JSON', status: 400 }); } }
    if (typeof body === 'string') { try { body = JSON.parse(body); } catch { return send(res, 400, {}); } }
    const name = body.identifiant;
    if (!name || !body.motDePasse || body.identifiant.trim() !== body.identifiant) return send(res, 400, { title: 'Validation failed', errors: { Identifiant: ['Required'] } });
    if (name === 'validation') return send(res, 400, { title: 'Validation failed', errors: { Identifiant: ['private server detail'] } });
    if (name === 'server') return send(res, 503, { title: 'Database unavailable', status: 503, detail: 'private database diagnostic' });
    if (name === 'forbidden') return send(res, 403, { status: 403 });
    if (name === 'empty') return send(res, 204);
    if (name === 'malformed') { res.writeHead(200, { 'Content-Type': 'text/html' }); return res.end('<html>Proxy error</html>'); }
    if (name === 'null') return send(res, 200, null);
    if (name === 'slow') { await new Promise(resolve => setTimeout(resolve, 500)); }
    if (body.motDePasse !== 'fictif') return send(res, 401, { title: 'Invalid credentials', status: 401 });
    return send(res, 200, { accessToken: `simulation-only-${name}`, tokenType: 'Bearer', expiresAt: '2099-01-01T00:00:00Z', societeKey: body.societeKey || 'S01' });
  }
  if (req.method === 'GET' && path === '/v1/auth/moi') {
    const bearer = req.headers.authorization || '';
    if (!bearer.startsWith('Bearer simulation-only-') || bearer.endsWith('identity-unauthorized')) return send(res, 401, { status: 401 });
    if (bearer.endsWith('identity-empty')) return send(res, 204);
    return send(res, 200, { societeKey: 'S01', agenceKey: 'AG01', userKey: 'SIM-USER', userDisplayName: 'Compte de simulation', estAdmin: false, typesDossierAutorises: [] });
  }
  return send(res, 501, { title: 'Outside authentication simulation scope' });
}).listen(process.env.PORT || 4901, '127.0.0.1');
