import { spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createServer } from 'node:net';
import cypress from 'cypress';
const root = fileURLToPath(new URL('../', import.meta.url));
await new Promise((resolve, reject) => {
  const server = createServer(); server.once('error', reject);
  server.listen(3000, '127.0.0.1', () => server.close(resolve));
});
process.env.QA_CYPRESS_MODE = 'ui';
process.env.QA_DOCENTE_PASSWORD = randomBytes(24).toString('hex');
const vite = spawn(process.execPath, ['node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--strictPort'], {
  cwd: root, windowsHide: true, env: { ...process.env, VITE_USE_MOCKS: 'true', VITE_API_URL: '' }, stdio: 'ignore',
});
try {
  let ready = false;
  for (let i = 0; i < 60; i++) {
    if (vite.exitCode !== null) throw new Error('Vite terminó antes de iniciar');
    try { ready = (await fetch('http://127.0.0.1:3000', { signal: AbortSignal.timeout(1000) })).ok; } catch { /* arranque */ }
    if (ready) break;
    await new Promise(r => setTimeout(r, 500));
  }
  if (!ready) throw new Error('Vite no inició en 30 segundos');
  const result = await cypress.run({ project: root, browser: 'electron', headless: true });
  if (result.status === 'failed' || result.totalFailed || result.totalTests !== 1) process.exitCode = 1;
} finally {
  vite.kill();
  delete process.env.QA_DOCENTE_PASSWORD;
}
