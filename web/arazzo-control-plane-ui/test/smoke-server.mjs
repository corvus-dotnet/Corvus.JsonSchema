// A tiny static file server for the Playwright smoke tests. Portable across OSes (pure Node, the
// project's own runtime) — it replaces `python3 -m http.server`, which needed committed `ui/`
// symlinks that do not survive a Windows checkout (git materializes them as plain text files).
//
// It serves the web project root, and additionally resolves a leading `/ui/` prefix to the root:
// demo/designer.html imports assets via absolute `/ui/...` paths (for the live host's `/designer`
// route, where the UI is mounted under `/ui`), so the smoke server must resolve `/ui/...` to the
// same files it serves at the root.
import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { join, extname, normalize, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

// web/arazzo-control-plane-ui, with any trailing separator stripped so `ROOT + sep` is a single boundary.
const ROOT = normalize(fileURLToPath(new URL('..', import.meta.url))).replace(/[\\/]+$/, '');
const PORT = Number(process.env.SMOKE_PORT ?? 8138);

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.wasm': 'application/wasm',
  '.map': 'application/json; charset=utf-8',
  '.ico': 'image/x-icon',
  '.png': 'image/png',
  '.woff2': 'font/woff2',
};

const server = createServer(async (req, res) => {
  try {
    let path = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    if (path === '/ui' || path.startsWith('/ui/')) path = path.slice(3) || '/'; // /ui/demo/x -> /demo/x
    if (path.endsWith('/')) path += 'index.html';

    // Resolve strictly within ROOT (reject traversal), OS-independently.
    const abs = normalize(join(ROOT, '.' + path));
    if (abs !== ROOT && !abs.startsWith(ROOT + sep)) { res.writeHead(403).end('Forbidden'); return; }

    const info = await stat(abs).catch(() => null);
    if (!info?.isFile()) { res.writeHead(404).end('File not found'); return; }

    res.writeHead(200, { 'Content-Type': MIME[extname(abs).toLowerCase()] ?? 'application/octet-stream' });
    res.end(await readFile(abs));
  } catch {
    res.writeHead(500).end('Server error');
  }
});

server.listen(PORT, () => console.log(`smoke static server on http://localhost:${PORT} (root ${ROOT})`));
