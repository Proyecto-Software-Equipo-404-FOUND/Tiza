// Entorno exclusivamente descartable. Nunca acepta una conexión ni URL externa.
import { spawn } from 'node:child_process';
import { randomBytes, randomUUID, createHmac, createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createServer } from 'node:net';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import newman from 'newman';
import sdk from 'postman-collection';

const root = fileURLToPath(new URL('../../', import.meta.url));
const output = path.join(root, 'tests/tenant-isolation/results');
mkdirSync(output, { recursive: true });
const runId = randomUUID();
const container = `tiza-qa04-${runId}`;
const docker = process.env.DOCKER_EXE || 'docker';
const password = `Qa04!${randomBytes(24).toString('hex')}`;
const signingKey = randomBytes(48).toString('hex');
const issuer = `qa04-${runId}`;
const connection = `Host=127.0.0.1;Port=5541;Database=pei_identidad_tests;Username=qa04;Password=${password};SearchPath=qa04_seed;Pooling=false`;
const coursesConnection = `Host=127.0.0.1;Port=5541;Database=pei_cursos_qa04;Username=qa04;Password=${password};Pooling=false`;
const env = { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development',
  Jwt__SecretKey: signingKey, Jwt__Issuer: issuer, Jwt__Audience: 'qa04', Platform__Enabled: 'false',
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
function jwt(claims, key = signingKey) {
  const encode = object => Buffer.from(JSON.stringify(object)).toString('base64url');
  const now = Math.floor(Date.now() / 1000);
  const data = `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({ iss: issuer, aud: 'qa04', sub: '11111111-0000-0000-0000-000000000002', role: 'Docente', nbf: now-5, exp: now+600, ...claims })}`;
  const token = `${data}.${createHmac('sha256', key).update(data).digest('base64url')}`;
  secrets.push(token); return token;
}
async function stopApis() {
  for (const child of processes) {
    if (child.exitCode === null) { child.kill(); await new Promise(resolve => { child.once('exit', resolve); setTimeout(resolve, 5000); }); }
  }
}
async function cleanup() {
  await stopApis();
  if (created) {
    const label = await command(docker, ['inspect', '--format', '{{index .Config.Labels "tiza.qa04.run"}}', container]);
    if (label !== runId) throw new Error('Etiqueta de contenedor no coincide; limpieza cancelada.');
    await command(docker, ['rm', '-f', '-v', container], 'Eliminar sólo el contenedor y volumen descartables de esta ejecución');
    created = false;
  }
}
let stopping = false;
process.on('SIGINT', async () => { if (stopping) return; stopping = true; await cleanup(); process.exit(130); });
try {
  evidence.baseCommit = await command('git', ['rev-parse', 'HEAD']);
  evidence.sourceHashes = Object.fromEntries([
    'services/cursos/src/PEI.Cursos.Infrastructure/Persistence/CursosDbContext.cs',
    'services/cursos/src/PEI.Cursos.Application/UseCases/Cohortes/Commands/CrearCohorteCommandHandler.cs',
    'services/cursos/tests/PEI.Cursos.UnitTests/Infrastructure/TenantQueryTranslationTests.cs',
    'tests/postman/PEI-QA04-Aislamiento.postman_collection.json',
    'tests/tenant-isolation/run.mjs', 'tests/tenant-isolation/package-lock.json'
  ].map(file => [file, createHash('sha256').update(readFileSync(path.join(root,file))).digest('hex')]));
  for (const port of [5541, 18541, 18542]) await freePort(port);
  await command(docker, ['info', '--format', '{{.ServerVersion}}'], 'Comprobar Docker');
  await command('dotnet', ['build', 'PEI.sln', '--nologo', '-v', 'quiet', '-m:1'], 'Compilar solución', {}, 600000);
  await command(docker, ['run', '-d', '--name', container, '--label', `tiza.qa04.run=${runId}`, '-p', '127.0.0.1:5541:5432',
    '-e', 'POSTGRES_PASSWORD', '-e', 'POSTGRES_USER=qa04', '-e', 'POSTGRES_DB=pei_identidad_tests', 'postgres:16-alpine'], 'Crear PostgreSQL descartable');
  created = true;
  // La imagen arranca un servidor temporal sólo por socket durante initdb.
  // Esperar TCP evita confundirlo con el servidor definitivo antes de su reinicio.
  await waitFor(async () => { await command(docker, ['exec', container, 'pg_isready', '-h', '127.0.0.1', '-U', 'qa04', '-d', 'pei_identidad_tests']); return true; }, 'PostgreSQL');
  await command(docker, ['exec', container, 'psql', '-U', 'qa04', '-d', 'pei_identidad_tests', '-v', 'ON_ERROR_STOP=1', '-c', 'CREATE SCHEMA qa04_seed'], 'Crear esquema sintético');
  await command(docker, ['exec', container, 'createdb', '-U', 'qa04', 'pei_cursos_qa04'], 'Crear base descartable Cursos');
  for (const [service, name, context] of [['identidad', 'Identidad', 'IdentidadDbContext'], ['identidad', 'Identidad', 'PlataformaDbContext'], ['cursos', 'Cursos', 'CursosDbContext']]) {
    await command('dotnet', ['ef', 'database', 'update', '--project', `services/${service}/src/PEI.${name}.Infrastructure`, '--startup-project', `services/${service}/src/PEI.${name}.Api`, '--context', context, '--no-build'], `Migrar ${context} en destino descartable`);
  }
  const seedEnv = { ID08_ENVIRONMENT: 'Testing', ID08_ENABLED: 'true', ID08_OPERATOR: 'Candela Luna QA04', ID08_SCHEMA: 'qa04_seed',
    ID08_ALLOWED_DESTINATIONS: JSON.stringify([{ Host: '127.0.0.1', Port: 5541, Database: 'pei_identidad_tests', Schema: 'qa04_seed' }]) };
  for (const id of ['08000001-0000-4000-8000-000000000001', '08000002-0000-4000-8000-000000000001', ...['11111111','22222222'].flatMap(prefix => [2,3,4].map(i=>`${prefix}-0000-0000-0000-00000000000${i}`))]) seedEnv[`ID08_PASSWORD_${id.replaceAll('-', '')}`] = password;
  await command('dotnet', ['services/identidad/tools/PEI.Identidad.DevelopmentSeed/bin/Debug/net8.0/PEI.Identidad.DevelopmentSeed.dll'], 'Sembrar ID08 en esquema autorizado', seedEnv);
  launch('identidad', 'Identidad', 18541); launch('cursos', 'Cursos', 18542);
  for (const port of [18541,18542]) await waitFor(async () => (await fetch(`http://127.0.0.1:${port}/swagger/v1/swagger.json`, { signal: AbortSignal.timeout(2000) })).ok, `API ${port}`);
  // Espera el seeder real CU06, sin conceder al test una lista vacía como éxito.
  await waitFor(async () => (await command(docker, ['exec', container, 'psql', '-U', 'qa04', '-d', 'pei_cursos_qa04', '-tAc', 'SELECT count(*) FROM "Cursos"'])).trim() === '4', 'semilla Cursos');
  const variables = { password, identidadUrl: 'http://127.0.0.1:18541', cursosUrl: 'http://127.0.0.1:18542',
    tenantA: '11111111-1111-1111-1111-111111111111', tenantB: '22222222-2222-2222-2222-222222222222',
    cursoA: 'cccccccc-0000-0000-0000-000000000001', cursoA2: 'cccccccc-0000-0000-0000-000000000003', cursoB: 'cccccccc-0000-0000-0000-000000000002',
    cohorteA: '33333333-3333-3333-3333-333333333333', cohorteB: '44444444-4444-4444-4444-444444444444',
    docenteA: '11111111-0000-0000-0000-000000000002', docenteB: '22222222-0000-0000-0000-000000000002', matriculaB: 'bbbbbbbb-0000-0000-0002-000000000001',
    missingTenantToken: jwt({}), malformedTenantToken: jwt({ institucion_id: 'invalido' }), emptyTenantToken: jwt({ institucion_id: '00000000-0000-0000-0000-000000000000' }),
    invalidToken: jwt({ institucion_id: '11111111-1111-1111-1111-111111111111' }, randomBytes(48).toString('hex')) };
  for (const file of readdirSync(path.join(root, 'tests/postman')).filter(x => x.endsWith('postman_collection.json'))) {
    const data = JSON.parse(readFileSync(path.join(root, 'tests/postman', file), 'utf8'));
    if (data.info?.schema !== 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json') throw new Error(`${file}: versión desconocida`);
    let requests = 0; new sdk.Collection(data).forEachItem(() => requests++);
    if (!requests) throw new Error(`${file}: colección vacía`);
    evidence.collections.push({ file, requests, validation: 'JSON parseado y cargado por Postman SDK; estructura básica. Sólo QA04 ejecutada.' });
  }
  console.log('Ejecutar Newman: sesiones reales A/B y pruebas negativas con clave efímera');
  const summary = await new Promise((resolve,reject) => newman.run({
    collection: JSON.parse(readFileSync(path.join(root, 'tests/postman/PEI-QA04-Aislamiento.postman_collection.json'), 'utf8')),
    envVar: Object.entries(variables).map(([key,value])=>({key,value})), reporters: [], timeoutRequest: 15000, timeoutScript: 10000, timeout: 180000
  }, (error,result)=>error?reject(error):resolve(result)));
  for (const execution of summary.run.executions) {
    const test = execution.item.events.find(e=>e.listen==='test');
    const expected = Number(test.script.exec.join('\n').match(/HTTP esperado (\d+)/)?.[1]);
    const authorization = execution.request.headers.get('Authorization');
    const token = authorization?.replace(/^Bearer /, '');
    let tenant = 'sin JWT', role = 'anónimo';
    if (token) { secrets.push(token); const claims = JSON.parse(Buffer.from(token.split('.')[1], 'base64url')); tenant=claims.institucion_id ?? 'ausente';role=claims.role; }
    const assertions = (execution.assertions??[]).map(a=>({ name:a.assertion, passed:!a.error }));
    evidence.requests.push({ scenario:execution.item.name, method:execution.request.method, endpoint:execution.request.url.getPath(), tenant, role,
      expected, actual:execution.response?.code??null, assertions,
      result:execution.response?.code===expected && assertions.length>0 && assertions.every(a=>a.passed)?'PASS':'FAIL' });
  }
  evidence.newman = { requests:summary.run.stats.requests.total, assertions:summary.run.stats.assertions.total, failedAssertions:summary.run.stats.assertions.failed, failures:summary.run.failures.length };
  writeFileSync(path.join(output,'summary.json'),JSON.stringify(evidence,null,2)+'\n');
  // Reducir memoria antes de levantar los hosts internos de las suites xUnit.
  await stopApis();
  for (const [service, name, kind] of [['cursos','Cursos','UnitTests'],['identidad','Identidad','UnitTests'],['identidad','Identidad','IntegrationTests']]) {
    const project=`services/${service}/tests/PEI.${name}.${kind}`;
    const filename=`${name}-${kind}.trx`;
    let passed = true;
    try { await command('dotnet', ['test',project,'--no-build','--logger',`trx;LogFileName=${filename}`,'--results-directory',output,'--nologo','-v','quiet','-m:1'], `Regresión ${name}.${kind}`, {}, 600000); }
    catch(error) { passed=false;console.error(redact(error.message)); }
    const trx=readFileSync(path.join(output,filename),'utf8');
    const counters=trx.match(/<Counters ([^>]+)\/>/)?.[1];
    if (!counters) throw new Error(`Sin resultados de ${project}`);
    const attrs=Object.fromEntries([...counters.matchAll(/(\w+)="(\d+)"/g)].map(m=>[m[1],Number(m[2])]));
    evidence.regression.push({ project, total:attrs.total, passed:attrs.passed, failed:attrs.failed, skipped:attrs.notExecuted, result:passed && attrs.failed===0 && attrs.notExecuted===0?'PASS':'FAIL' });
    writeFileSync(path.join(output,'summary.json'),JSON.stringify(evidence,null,2)+'\n');
  }
  evidence.completed=true;
  if(summary.run.failures.length || evidence.requests.some(r=>r.result==='FAIL') || evidence.regression.some(r=>r.result==='FAIL')) throw new Error('Fallos detectados; consultar evidencia sanitizada.');
  console.log(`PASS: ${evidence.newman.requests} solicitudes, ${evidence.newman.assertions} aserciones Newman; regresión sin omisiones.`);
} catch(error) {
  evidence.error=redact(error.message);console.error(evidence.error);process.exitCode=1;
} finally {
  try { await cleanup();evidence.cleanedUp=true; } catch(error) { evidence.cleanupError=redact(error.message);process.exitCode=1; }
  writeFileSync(path.join(output,'summary.json'),JSON.stringify(evidence,null,2)+'\n');
  const rows=['# QA-04 — evidencia de ejecución', '', `Fecha: ${evidence.timestamp}. Base: ${evidence.baseCommit}.`, '',
    '| Escenario | Método / endpoint | Rol / tenant | Esperado | Obtenido | Resultado |','|---|---|---|---|---|---|',
    ...evidence.requests.map(r=>`| ${r.scenario} | ${r.method} ${r.endpoint} | ${r.role} / ${r.tenant} | ${r.expected} | ${r.actual} | ${r.result} |`)];
  writeFileSync(path.join(output,'requests.md'),rows.join('\n')+'\n');
}
