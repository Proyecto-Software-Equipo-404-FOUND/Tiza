import { spawn, execFileSync } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
import newman from 'newman';

const root = fileURLToPath(new URL('../../', import.meta.url));
const output = path.join(root, 'tests/qa05/results');
const hostProject = 'services/identidad/tests/PEI.Identidad.Qa02Host';
const hostDll = path.join(root, hostProject, 'bin/Debug/net8.0/PEI.Identidad.Qa02Host.dll');
const baseUrl = 'http://127.0.0.1:18502';
const folderName = '0. Smoke de autenticación automática (opt-in; crea y revoca sesión)';
const collection = JSON.parse(readFileSync(path.join(root, 'tests/postman/identidad.postman_collection.json'), 'utf8'));
const folder = collection.item.find(item => item.name === folderName);
const result = { date: new Date().toISOString(), commit: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(), folder: folderName, fixture: 'Host QA02 + TestServer; persistencia efímera en memoria', completed: false };
mkdirSync(output, { recursive: true });
let host;

async function command(args, label) {
  console.log(label);
  await new Promise((resolve, reject) => {
    const child = spawn('dotnet', args, { cwd: root, windowsHide: true, stdio: ['ignore', 'ignore', 'pipe'] });
    let diagnostic = '';
    child.stderr.on('data', chunk => { diagnostic += chunk.toString(); });
    child.on('error', reject);
    child.on('exit', code => code === 0 ? resolve() : reject(new Error(`${label}: dotnet terminó con código ${code}. ${diagnostic.slice(-2000)}`)));
  });
}

async function run(targetCollection, environment, selectedFolder = folderName) {
  return await new Promise((resolve, reject) => newman.run({
    collection: targetCollection,
    ...(selectedFolder ? { folder: selectedFolder } : {}),
    environment: { values: Object.entries(environment).map(([key, value]) => ({ key, value: String(value), enabled: true })) },
    reporters: [], timeoutRequest: 30000, timeoutScript: 10000, timeout: 120000
  }, (error, summary) => error ? reject(error) : resolve(summary)));
}

async function waitReady() {
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    if (host.exitCode !== null) throw new Error(`Host de prueba terminó al iniciar (${host.exitCode})`);
    try {
      const response = await fetch(`${baseUrl}/__qa02/audit`, { signal: AbortSignal.timeout(1000) });
      if (response.ok) return await response.json();
    } catch { /* el host puede estar terminando de iniciar */ }
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error('El host QA05 no respondió al endpoint de readiness en 30 segundos.');
}

try {
  assert.ok(folder && folder.item.length === 3, 'No se encontró la carpeta smoke QA-01 completa.');
  await new Promise((resolve, reject) => {
    const probe = createServer();
    probe.once('error', () => reject(new Error('Puerto 18502 ocupado; no se detuvo ningún proceso ajeno.')));
    probe.listen(18502, '127.0.0.1', () => probe.close(resolve));
  });
  await command(['build', 'services/identidad/PEI.Identidad.sln', '--nologo', '-v', 'quiet', '-m:1'], 'Compilar solución de Identidad');
  await command(['build', hostProject, '--nologo', '-v', 'quiet', '-m:1'], 'Compilar host aislado de pruebas');
  await command(['test', 'services/identidad/tests/PEI.Identidad.UnitTests', '--no-build', '--logger', 'trx;LogFileName=qa05.trx', '--results-directory', output, '--nologo', '-v', 'quiet', '-m:1'], 'Ejecutar pruebas xUnit de Identidad');
  const trxPath = path.join(output, 'qa05.trx');
  const trx = readFileSync(trxPath, 'utf8');
  const counters = trx.match(/<Counters ([^>]+)\/>/);
  assert.ok(counters, 'No se pudo leer el resultado xUnit TRX.');
  result.xunit = Object.fromEntries([...counters[1].matchAll(/(\w+)="(\d+)"/g)].map(match => [match[1], Number(match[2])]));
  assert.equal(result.xunit.notExecuted, 0, 'Hay tests xUnit omitidos.');
  assert.equal(result.xunit.failed, 0, 'Fallaron tests xUnit.');
  assert.ok(result.xunit.passed > 0, 'No se ejecutaron pruebas xUnit.');

  let pending = '';
  const fixtureVariables = await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('El fixture no entregó las credenciales efímeras.')), 30000);
    host = spawn('dotnet', [hostDll], { cwd: root, windowsHide: true, stdio: ['ignore', 'pipe', 'ignore'] });
    host.on('error', error => { clearTimeout(timer); reject(error); });
    host.on('exit', code => { clearTimeout(timer); reject(new Error(`El fixture terminó antes de entregar credenciales (${code}).`)); });
    host.stdout.on('data', chunk => {
      pending += chunk.toString();
      const lines = pending.split('\n'); pending = lines.pop();
      for (const line of lines) {
        try {
          const values = JSON.parse(line);
          if (values.qa02AdminEmail && values.qa02Password) { clearTimeout(timer); resolve(values); }
        } catch { /* stdout es IPC privado y puede contener secretos: nunca imprimirlo. */ }
      }
    });
  });
  const before = await waitReady();
  const environment = {
    baseUrl,
    institutionalEmail: fixtureVariables.qa02AdminEmail,
    institutionalPassword: fixtureVariables.qa02Password,
    allowAuthentication: 'true'
  };
  const smoke = await run(collection, environment);
  const executions = smoke.run.executions;
  const expected = [
    { name: 'Login y guardar sesión para el smoke test', method: 'POST', status: 200 },
    { name: 'Leer endpoint de Administrador con el token obtenido automáticamente', method: 'GET', status: 200 },
    { name: 'Cerrar la sesión creada por este flujo', method: 'POST', status: 204 }
  ];
  assert.equal(smoke.run.stats.requests.total, expected.length, 'La carpeta smoke no ejecutó sus tres requests.');
  assert.equal(executions.length, expected.length, 'Newman no reportó las tres ejecuciones esperadas.');
  result.requests = executions.map((execution, index) => {
    const wanted = expected[index];
    const assertions = (execution.assertions ?? []).map(assertion => ({ name: assertion.assertion, passed: !assertion.error }));
    assert.equal(execution.item.name, wanted.name, `Orden o request inesperado en posición ${index + 1}.`);
    assert.equal(execution.request.method, wanted.method, `Método HTTP inesperado para ${wanted.name}.`);
    assert.equal(execution.response?.code, wanted.status, `Status HTTP incorrecto para ${wanted.name}.`);
    assert.ok(assertions.length > 0 && assertions.every(assertion => assertion.passed), `Aserción Newman fallida en ${wanted.name}.`);
    return { name: wanted.name, method: wanted.method, status: execution.response.code, assertions };
  });
  assert.equal(smoke.run.failures.length, 0, 'Newman reportó fallos en el smoke test.');
  const after = await (await fetch(`${baseUrl}/__qa02/audit`)).json();
  assert.deepEqual(after, before, 'El smoke modificó datos de usuarios o Plataforma.');
  result.audit = { before, after, unchanged: true };
  result.newman = { requests: smoke.run.stats.requests.total, assertions: smoke.run.stats.assertions.total, failedAssertions: smoke.run.stats.assertions.failed, failures: smoke.run.failures.length };
  assert.equal(result.newman.assertions, 6, 'La colección ejecutó una cantidad inesperada de aserciones.');
  assert.equal(result.newman.failedAssertions, 0);

  // Comprueba que una credencial errónea realmente hace fallar Newman; solo ejecuta el login.
  const negativeCollection = structuredClone(collection);
  const negativeFolder = negativeCollection.item.find(item => item.name === folderName);
  negativeFolder.item = [negativeFolder.item[0]];
  const wrongPassword = fixtureVariables.qa02WrongPassword;
  const negative = await run(negativeCollection, { ...environment, institutionalPassword: wrongPassword });
  assert.equal(negative.run.stats.requests.total, 1, 'La comprobación negativa no emitió exactamente un login.');
  assert.equal(negative.run.executions[0]?.response?.code, 401, 'La credencial negativa no fue rechazada con HTTP 401.');
  assert.ok(negative.run.failures.length > 0, 'Newman no detectó como fallo la respuesta HTTP 401.');
  result.negativeCredentialCheck = { response: 401, NewmanDetectedFailure: true, assertions: negative.run.stats.assertions.total, failures: negative.run.failures.length };
  result.completed = true;
  console.log(`PASS: Newman ejecutó ${result.newman.requests} requests y ${result.newman.assertions} aserciones; credencial inválida detectada como fallo; ${result.xunit.passed} tests xUnit.`);
} catch (error) {
  result.error = error.message;
  console.error(error.message);
  process.exitCode = 1;
} finally {
  if (host && host.exitCode === null) {
    host.kill();
    await new Promise(resolve => { host.once('exit', resolve); setTimeout(resolve, 5000); });
  }
  writeFileSync(path.join(output, 'summary.json'), `${JSON.stringify(result, null, 2)}\n`);
}
