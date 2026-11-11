// Infraestructura descartable, siguiendo el runner QA04. Nunca usa bases de desarrollo.
import { spawn } from 'node:child_process';
import { randomBytes, randomUUID } from 'node:crypto';
import { createServer } from 'node:net';
import { fileURLToPath } from 'node:url';
import { readFileSync, writeFileSync } from 'node:fs';
import assert from 'node:assert/strict';
const root = fileURLToPath(new URL('../../', import.meta.url));
const runId = randomUUID();
const container = `tiza-s2qa01-${runId}`;
const password = randomBytes(24).toString('hex');
const key = randomBytes(32).toString('hex');
const connection = `Host=127.0.0.1;Port=5543;Database=tiza_s2qa01;Username=postgres;Password=${password}`;
const env = { ...process.env, POSTGRES_PASSWORD: password, ConnectionStrings__Default: connection,
  ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://127.0.0.1:18543',
  ServiciosInternos__ApiKey: key, QA_INTERNAL_API_KEY: key, QA_EVALUACIONES_URL: 'http://127.0.0.1:18543' };
const redact = text => [password, key, connection].reduce((s, secret) => s.replaceAll(secret, '[REDACTADO]'), text);
async function command(exe, args, timeout = 300000) {
  return new Promise((resolve, reject) => {
    const child = spawn(exe, args, { cwd: root, env, windowsHide: true });
    let log = '';
    const timer = setTimeout(() => { child.kill(); reject(new Error(`Timeout: ${exe} ${args[0]}`)); }, timeout);
    child.stdout.on('data', x => { log += x; }); child.stderr.on('data', x => { log += x; });
    child.on('error', e => { clearTimeout(timer); reject(e); });
    child.on('close', code => { clearTimeout(timer); code === 0 ? resolve(log.trim()) : reject(new Error(redact(log).slice(-4000))); });
  });
}
async function ready(action, description) {
  for (let i = 0; i < 60; i++) {
    try { if (await action()) return; } catch { /* arranque */ }
    await new Promise(r => setTimeout(r, 1000));
  }
  throw new Error(`Timeout: ${description}`);
}
let created = false;
let api;
try {
  for (const port of [5543, 18543]) await new Promise((resolve, reject) => {
    const server = createServer(); server.once('error', reject);
    server.listen(port, '127.0.0.1', () => server.close(resolve));
  });
  console.log('Comprobar Docker y compilar Evaluaciones');
  await command('docker', ['info', '--format', '{{.ServerVersion}}'], 30000);
  await command('dotnet', ['build', 'services/evaluaciones/PEI.Evaluaciones.sln', '-v', 'quiet']);
  await command('docker', ['run', '-d', '--name', container, '--label', `tiza.s2qa01=${runId}`,
    '-p', '127.0.0.1:5543:5432', '-e', 'POSTGRES_PASSWORD', '-e', 'POSTGRES_DB=tiza_s2qa01', 'postgres:16-alpine']);
  created = true;
  await ready(async () => { await command('docker', ['exec', container, 'pg_isready', '-h', '127.0.0.1', '-U', 'postgres']); return true; }, 'PostgreSQL');
  console.log('Migrar base exclusiva de esta ejecución');
  await command('dotnet', ['ef', 'database', 'update', '--project', 'services/evaluaciones/src/PEI.Evaluaciones.Infrastructure',
    '--startup-project', 'services/evaluaciones/src/PEI.Evaluaciones.Api', '--no-build']);
  api = spawn('dotnet', ['services/evaluaciones/src/PEI.Evaluaciones.Api/bin/Debug/net8.0/PEI.Evaluaciones.Api.dll'],
    { cwd: root, env, windowsHide: true, stdio: 'ignore' });
  api.on('error', () => {});
  await ready(async () => (await fetch('http://127.0.0.1:18543/swagger/v1/swagger.json', { signal: AbortSignal.timeout(1000) })).ok, 'API Evaluaciones');
  console.log(await command('node', ['tests/s2-qa01/run-newman.mjs']));
  // Segunda evidencia independiente de la respuesta HTTP: exactamente una fila por alumno/sala.
  const count = await command('docker', ['exec', container, 'psql', '-U', 'postgres', '-d', 'tiza_s2qa01', '-Atc',
    `SELECT count(*) FROM "EntregaExamen" e JOIN "Evaluacion" v ON v."Id"=e."EvaluacionId" WHERE v."Titulo" IN ('S2-QA-01 A','S2-QA-01 B')`]);
  assert.equal(count, '2', 'El reenvío debe conservar exactamente dos entregas (una por institución)');
  const resultFile = new URL('results/newman.json', import.meta.url);
  const evidence = JSON.parse(readFileSync(resultFile, 'utf8'));
  evidence.persistedDeliveries = Number(count);
  evidence.persistenceVerified = true;
  writeFileSync(resultFile, JSON.stringify(evidence, null, 2));
  console.log('PASS PostgreSQL: dos entregas totales tras reenviar ambos lotes');
} finally {
  if (api && api.exitCode === null) {
    api.kill(); await new Promise(r => { api.once('exit', r); setTimeout(r, 5000); });
  }
  if (created) {
    const label = await command('docker', ['inspect', '--format', '{{index .Config.Labels "tiza.s2qa01"}}', container]);
    assert.equal(label, runId, 'No se limpia un contenedor ajeno');
    await command('docker', ['rm', '-f', '-v', container]);
  }
}
