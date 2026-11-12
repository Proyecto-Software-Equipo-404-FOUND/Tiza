import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { randomUUID, randomBytes } from 'node:crypto';
import { createRequire } from 'node:module';
import path from 'node:path';
const require = createRequire(new URL('../../frontend/package.json', import.meta.url));
const { HubConnectionBuilder, LogLevel } = require('@microsoft/signalr');

export async function runScenarios({ root, env, password, container, command, launch, processes, secrets, evidence, waitFor }) {
  const key = randomBytes(32).toString('hex'); secrets.push(key);
  Object.assign(env, {
    ConnectionStrings__Default: `Host=127.0.0.1;Port=5541;Database=pei_evaluaciones_s2live;Username=s2live;Password=${password}`,
    ConnectionStrings__DefaultConnection: `Host=127.0.0.1;Port=5541;Database=pei_sala_s2live;Username=s2live;Password=${password}`,
    ConnectionStrings__Redis: '', ServiciosInternos__ApiKey: key, Evaluaciones__BaseUrl: 'http://127.0.0.1:18643',
    CorreccionIa__Habilitada: 'false', QA_INTERNAL_API_KEY: key, QA_EVALUACIONES_URL: 'http://127.0.0.1:18643', QA_TEST_VISIBILITY: 'true',
  });
  for (const db of ['pei_evaluaciones_s2live', 'pei_sala_s2live']) {
    await command('docker', ['exec', container, 'createdb', '-U', 's2live', db], `Crear ${db}`);
  }
  await command('dotnet', ['ef', 'database', 'update', '--project', 'services/evaluaciones/src/PEI.Evaluaciones.Infrastructure',
    '--startup-project', 'services/evaluaciones/src/PEI.Evaluaciones.Api', '--no-build'], 'Migrar Evaluaciones');
  launch('evaluaciones', 'Evaluaciones', 18643); launch('sala-en-vivo', 'SalaEnVivo', 18644);
  for (const port of [18643, 18644]) await waitFor(async () => (await fetch(`http://127.0.0.1:${port}/swagger/v1/swagger.json`, { signal: AbortSignal.timeout(2000) })).ok, `API ${port}`);
  const gatewayProject = path.join(root, 'gateway/src/PEI.Gateway');
  const gatewayEnv = { ...env, ASPNETCORE_URLS: 'http://127.0.0.1:8080' };
  for (const [service, port] of [['identidad', 18641], ['cursos', 18642], ['evaluaciones', 18643], ['sala', 18644]]) {
    gatewayEnv[`ReverseProxy__Clusters__${service}-cluster__Destinations__destination1__Address`] = `http://127.0.0.1:${port}`;
  }
  const gateway = spawn('dotnet', [path.join(gatewayProject, 'bin/Debug/net8.0/PEI.Gateway.dll')],
    { cwd: gatewayProject, env: gatewayEnv, windowsHide: true, stdio: 'ignore' });
  processes.push(gateway);
  await waitFor(async () => (await fetch('http://127.0.0.1:8080', { signal: AbortSignal.timeout(2000) })).ok, 'gateway');

  async function api(route, { method = 'GET', body, token, tenant, expected = 200 } = {}) {
    const response = await fetch(`http://127.0.0.1:8080${route}`, { method,
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(tenant ? { 'X-Institucion-Id': tenant } : {}) },
      ...(body ? { body: JSON.stringify(body) } : {}), signal: AbortSignal.timeout(20000) });
    assert.equal(response.status, expected, `${method} ${route}: HTTP inesperado`);
    return response.status === 204 ? null : response.json();
  }
  async function login(email) {
    const session = await api('/api/v1/identidad/auth/login', { method: 'POST', body: { email, password } });
    secrets.push(session.accessToken, session.refreshToken); return session.accessToken;
  }
  const docenteA = await login('docente@a.test'); const docenteB = await login('docente@b.test');
  const alumno = await login('alumno1@a.test');
  const tenant = '11111111-1111-1111-1111-111111111111';
  Object.assign(env, { QA_TOKEN_A: docenteA, QA_TOKEN_B: docenteB, QA_DOCENTE_PASSWORD: password, QA_DOCENTE_EMAIL: 'docente@a.test', QA_CYPRESS_FLOW: 'permisos' });
  console.log(await command('node', ['tests/s2-qa01/run-newman.mjs'], 'Newman con JWT emitidos por Identidad'));
  evidence.newman = { requests: 18, visibility: 'A/B y acceso cruzado con JWT reales' };
  console.log(await command('node', ['frontend/cypress/run-ui.mjs', '--live'], 'Cypress contra gateway y servicios reales', {}, 300000));
  evidence.cypress = { mode: 'live', flow: 'administrador crea; docente matricula', passed: true };
  // Prueba explícita del segundo permiso que bloquea el recorrido pedido en #150.
  await api('/api/v1/cursos/cccccccc-0000-0000-0000-000000000001/cohortes', { method: 'POST', token: docenteA,
    expected: 403, body: { nombre: 'S2-QA-01 prohibida', periodo: '2026-2C', docenteId: '11111111-0000-0000-0000-000000000002' } });
  evidence.blockedAcceptance = ['#150 pide que Docente cree curso/cohorte; las políticas actuales devuelven 403 (SoloAdministrador).'];

  const cursos = await api('/api/v1/cursos', { token: docenteA });
  const curso = cursos.items.find(x => x.nombre === 'S2-QA-01 Curso reproducible'); assert.ok(curso);
  const cohortes = await api(`/api/v1/cursos/${curso.id}/cohortes`, { token: docenteA });
  const cohorte = cohortes.find(x => x.nombre === 'S2-QA-01 Cohorte'); assert.ok(cohorte);
  const matriculas = await api(`/api/v1/cohortes/${cohorte.id}/matriculas`, { token: docenteA });
  assert.equal(matriculas.filter(x => x.alumnoId === '11111111-0000-0000-0000-000000000003').length, 1);
  evidence.matriculaPersistida = true;
  const pregunta = randomUUID(); const opcion = randomUUID();
  const evaluacion = await api('/api/v1/evaluaciones', { method: 'POST', token: docenteA, tenant, expected: 201,
    body: { titulo: 'S2-QA-01 cierre real', tipo: 'Examen', preguntasJson: { version: 1, preguntas: [{ id: pregunta, enunciado: '1 + 1', tipo: 'OpcionMultiple', puntajeMaximo: 1,
      opciones: [{ id: opcion, texto: '2', esCorrecta: true }, { id: randomUUID(), texto: '3', esCorrecta: false }] }] } } });
  await api(`/api/v1/evaluaciones/${evaluacion.id}/publicar`, { method: 'POST', token: docenteA, tenant, body: { versionEsperada: evaluacion.version, cohorteId: cohorte.id } });
  const sala = await api('/api/v1/salas', { method: 'POST', token: docenteA, expected: 201, body: { evaluacionId: evaluacion.id, capacidadMaxima: 10 } });
  const connect = token => new HubConnectionBuilder().withUrl('http://127.0.0.1:8080/hubs/sala', { accessTokenFactory: () => token, timeout: 15000 }).configureLogging(LogLevel.None).build();
  const teacher = connect(docenteA); const student = connect(alumno);
  let finalizada;
  student.on('salaFinalizada', event => { finalizada = event; });
  // Consumir los eventos de control evita warnings; las aserciones consultan estado real.
  for (const hub of [teacher, student]) for (const event of ['estadoSala', 'alumnosConectados', 'salaIniciada', 'preguntaActual', 'alumnoIngreso', 'respuestaRegistrada']) hub.on(event, () => {});
  try {
    await teacher.start(); await teacher.invoke('UnirseASalaDocente', sala.id);
    await student.start(); await student.invoke('UnirseASala', sala.codigoPIN);
    await teacher.invoke('IniciarSala');
    const before = await student.invoke('ObtenerEstadoSala');
    assert.equal(before.preguntaActual.preguntaId, pregunta);
    const payload = { preguntaId: pregunta, respuesta: opcion, idempotencyKey: randomUUID(), secuencia: 1 };
    const first = await student.invoke('EnviarRespuesta', payload);
    const retry = await student.invoke('EnviarRespuesta', payload);
    assert.equal(first.estado, 'Aceptada'); assert.equal(retry.estado, 'Duplicada');
    // .NET emite 7 decimales; PostgreSQL persiste microsegundos (6).
    const persistedTime = value => value.replace(/(\.\d{6})\d+Z$/, '$1Z');
    assert.equal(persistedTime(retry.recibidaUtc), persistedTime(first.recibidaUtc));
    const sql = async (db, query) => command('docker', ['exec', container, 'psql', '-U', 's2live', '-d', db, '-Atc', query]);
    assert.equal(await sql('pei_sala_s2live', `SELECT count(*) FROM "RespuestasSala" WHERE "SalaId"='${sala.id}'`), '1');
    evidence.signalr = { acceptedThenDuplicate: true, persistedResponses: 1 };
    await assert.rejects(student.invoke('FinalizarSala', null), /NO_AUTORIZADO|docente/i);
    await teacher.invoke('FinalizarSala', null);
    await waitFor(async () => !!finalizada, 'evento salaFinalizada');
    assert.equal(finalizada.salaId, sala.id);
    const entregas = await api(`/api/v1/evaluaciones/${evaluacion.id}/entregas`, { token: docenteA });
    assert.equal(entregas.length, 1); assert.equal(entregas[0].alumnoId, '11111111-0000-0000-0000-000000000003');
    await teacher.invoke('FinalizarSala', null);
    const again = await api(`/api/v1/evaluaciones/${evaluacion.id}/entregas`, { token: docenteA });
    assert.deepEqual(again, entregas);
    assert.equal(await sql('pei_evaluaciones_s2live', `SELECT count(*) FROM "EntregaExamen" WHERE "SalaId"='${sala.id}'`), '1');
    const rejected = await student.invoke('EnviarRespuesta', { ...payload, idempotencyKey: randomUUID(), secuencia: 2 });
    assert.equal(rejected.estado, 'Rechazada'); assert.equal(rejected.error.codigo, 'SALA_FINALIZADA');
    evidence.cierre = { event: true, persistedDeliveries: 1, repeatedCloseNoDuplicates: true, rejectsNewAnswers: true };
    console.log('PASS Sala real: idempotencia persistida, cierre, evento y entrega única a Evaluaciones');
  } finally { await student.stop(); await teacher.stop(); }
}
