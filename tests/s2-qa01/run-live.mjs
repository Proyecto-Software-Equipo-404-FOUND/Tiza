// Infraestructura descartable reutilizada de tests/tenant-isolation/run.mjs (QA04).
// Entorno exclusivamente descartable. Nunca acepta una conexión ni URL externa.
import { spawn } from 'node:child_process';
import { randomBytes, randomUUID } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:net';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { runScenarios } from './live-scenarios.mjs';


const root = fileURLToPath(new URL('../../', import.meta.url));
const output = path.join(root, 'tests/s2-qa01/results');
mkdirSync(output, { recursive: true });
const runId = randomUUID();
const container = `tiza-s2live-${runId}`;
const docker = process.env.DOCKER_EXE || 'docker';
const password = `S2Qa01!${randomBytes(24).toString('hex')}`;
const signingKey = randomBytes(48).toString('hex');
const issuer = `s2live-${runId}`;
const connection = `Host=127.0.0.1;Port=5541;Database=pei_identidad_tests;Username=s2live;Password=${password};SearchPath=s2live_seed;Pooling=false`;
const coursesConnection = `Host=127.0.0.1;Port=5541;Database=pei_cursos_s2live;Username=s2live;Password=${password};Pooling=false`;
const env = { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development',
  Jwt__SecretKey: signingKey, Jwt__Issuer: issuer, Jwt__Audience: 's2live', Platform__Enabled: 'false',
  ConnectionStrings__IdentidadDb: connection, ConnectionStrings__CursosDb: coursesConnection,
  PEI_IDENTIDAD_TEST_CONNECTION: connection, POSTGRES_PASSWORD: password };
const secrets = [password, signingKey, connection, coursesConnection];
const redact = value => secrets.reduce((text, secret) => text.split(secret).join('[REDACTADO]'), String(value))
  .replace(/eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/g, '[JWT REDACTADO]');
const processes = [];
let created = false;
const evidence = { runId, timestamp: new Date().toISOString(), baseCommit: '', environment: '.NET 8; Node 20; PostgreSQL 16; APIs loopback; datos CU06/ID08',
  requests: [], regression: [], collections: [], completed: false };

async function command(executable, args, label, overrides = {}, timeout = 180000) {
  if (label) console.log(label);
  return await new Promise((resolve, reject) => {
    const child = spawn(executable, args, { cwd: root, env: { ...env, ...overrides }, windowsHide: true });
    let log = '';
    const timer = setTimeout(() => { child.kill(); reject(new Error(`${label || executable}: timeout`)); }, timeout);
    child.stdout.on('data', chunk => { log += chunk; });
    child.stderr.on('data', chunk => { log += chunk; });
    child.on('error', error => { clearTimeout(timer); reject(error); });
    child.on('close', code => { clearTimeout(timer); code === 0 ? resolve(log.trim()) : reject(new Error(`${label || executable}: salida ${code}\n${redact(log).slice(-5000)}`)); });
  });
}
async function freePort(port) {
  await new Promise((resolve, reject) => { const server = createServer(); server.on('error', () => reject(new Error(`Puerto ${port} ocupado; no se detuvo ningún servicio ajeno.`))); server.listen(port, '127.0.0.1', () => server.close(resolve)); });
}
const pause = () => new Promise(resolve => setTimeout(resolve, 1000));
async function waitFor(action, description, attempts = 60) {
  for (let i = 0; i < attempts; i++) { try { if (await action()) return; } catch { /* arranque */ } await pause(); }
  throw new Error(`Timeout: ${description}`);
}
function launch(service, name, port) {
  const project = path.join(root, `services/${service}/src/PEI.${name}.Api`);
  const child = spawn('dotnet', [path.join(project, `bin/Debug/net8.0/PEI.${name}.Api.dll`)], {
    cwd: project, env: { ...env, ASPNETCORE_URLS: `http://127.0.0.1:${port}` }, windowsHide: true, stdio: 'ignore'
  });
  child.on('error', () => { /* readiness informa fallo sin volcar configuración */ });
  processes.push(child);
}
async function stopApis() {
  for (const child of processes) {
    if (child.exitCode === null) { child.kill(); await new Promise(resolve => { child.once('exit', resolve); setTimeout(resolve, 5000); }); }
  }
}
async function cleanup() {
  await stopApis();
  if (created) {
    const label = await command(docker, ['inspect', '--format', '{{index .Config.Labels "tiza.s2live.run"}}', container]);
    if (label !== runId) throw new Error('Etiqueta de contenedor no coincide; limpieza cancelada.');
    await command(docker, ['rm', '-f', '-v', container], 'Eliminar sólo el contenedor y volumen descartables de esta ejecución');
    created = false;
  }
}
let stopping = false;
process.on('SIGINT', async () => { if (stopping) return; stopping = true; await cleanup(); process.exit(130); });
try {
  evidence.baseCommit = await command('git', ['rev-parse', 'HEAD']);
  for (const port of [5541, 18641, 18642, 18643, 18644, 8080, 3000]) await freePort(port);
  await command(docker, ['version', '--format', '{{.Server.Version}}'], 'Comprobar Docker');
  await command('dotnet', ['build', 'PEI.sln', '--nologo', '-v', 'quiet', '-m:1'], 'Compilar solución', {}, 600000);
  await command(docker, ['run', '-d', '--name', container, '--label', `tiza.s2live.run=${runId}`, '-p', '127.0.0.1:5541:5432',
    '-e', 'POSTGRES_PASSWORD', '-e', 'POSTGRES_USER=s2live', '-e', 'POSTGRES_DB=pei_identidad_tests', 'postgres:16-alpine'], 'Crear PostgreSQL descartable');
  created = true;
  // La imagen arranca un servidor temporal sólo por socket durante initdb.
  // Esperar TCP evita confundirlo con el servidor definitivo antes de su reinicio.
  await waitFor(async () => { await command(docker, ['exec', container, 'pg_isready', '-h', '127.0.0.1', '-U', 's2live', '-d', 'pei_identidad_tests']); return true; }, 'PostgreSQL');
  await command(docker, ['exec', container, 'psql', '-U', 's2live', '-d', 'pei_identidad_tests', '-v', 'ON_ERROR_STOP=1', '-c', 'CREATE SCHEMA s2live_seed'], 'Crear esquema sintético');
  await command(docker, ['exec', container, 'createdb', '-U', 's2live', 'pei_cursos_s2live'], 'Crear base descartable Cursos');
  for (const [service, name, context] of [['identidad', 'Identidad', 'IdentidadDbContext'], ['identidad', 'Identidad', 'PlataformaDbContext'], ['cursos', 'Cursos', 'CursosDbContext']]) {
    await command('dotnet', ['ef', 'database', 'update', '--project', `services/${service}/src/PEI.${name}.Infrastructure`, '--startup-project', `services/${service}/src/PEI.${name}.Api`, '--context', context, '--no-build'], `Migrar ${context} en destino descartable`);
  }
  const seedEnv = { ID08_ENVIRONMENT: 'Testing', ID08_ENABLED: 'true', ID08_OPERATOR: 'Candela Luna S2QA01', ID08_SCHEMA: 's2live_seed',
    ID08_ALLOWED_DESTINATIONS: JSON.stringify([{ Host: '127.0.0.1', Port: 5541, Database: 'pei_identidad_tests', Schema: 's2live_seed' }]) };
  for (const id of ['08000001-0000-4000-8000-000000000001', '08000002-0000-4000-8000-000000000001', ...['11111111','22222222'].flatMap(prefix => [2,3,4].map(i=>`${prefix}-0000-0000-0000-00000000000${i}`))]) seedEnv[`ID08_PASSWORD_${id.replaceAll('-', '')}`] = password;
  await command('dotnet', ['services/identidad/tools/PEI.Identidad.DevelopmentSeed/bin/Debug/net8.0/PEI.Identidad.DevelopmentSeed.dll'], 'Sembrar ID08 en esquema autorizado', seedEnv);
  launch('identidad', 'Identidad', 18641); launch('cursos', 'Cursos', 18642);
  for (const port of [18641,18642]) await waitFor(async () => (await fetch(`http://127.0.0.1:${port}/swagger/v1/swagger.json`, { signal: AbortSignal.timeout(2000) })).ok, `API ${port}`);
  // Espera el seeder real CU06, sin conceder al test una lista vacía como éxito.
  await waitFor(async () => (await command(docker, ['exec', container, 'psql', '-U', 's2live', '-d', 'pei_cursos_s2live', '-tAc', 'SELECT count(*) FROM "Cursos"'])).trim() === '4', 'semilla Cursos');
  await runScenarios({ root, env, password, container, command, launch, processes, secrets, evidence, waitFor });
  evidence.completed = true;
} catch (error) {
  evidence.error = redact(error.message); console.error(evidence.error); process.exitCode = 1;
} finally {
  try { await cleanup(); evidence.cleanedUp = true; } catch (error) { evidence.cleanupError = redact(error.message); process.exitCode = 1; }
  writeFileSync(path.join(output, 'live.json'), JSON.stringify(evidence, null, 2) + '\n');
}
