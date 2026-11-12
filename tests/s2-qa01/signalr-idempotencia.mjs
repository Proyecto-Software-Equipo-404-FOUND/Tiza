// Newman no invoca métodos SignalR: se usa el cliente oficial, sin inventar un REST.
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
const require = createRequire(new URL('../../frontend/package.json', import.meta.url));
const { HubConnectionBuilder, LogLevel } = require('@microsoft/signalr');
const url = process.env.QA_HUB_URL || 'http://127.0.0.1:5104/hubs/sala';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(url).hostname), 'Sólo entorno local de prueba');
assert.ok(process.env.QA_ALUMNO_TOKEN && process.env.QA_SALA_PIN, 'Faltan QA_ALUMNO_TOKEN y QA_SALA_PIN de sala activa de prueba');
const hub = new HubConnectionBuilder().withUrl(url, { accessTokenFactory: () => process.env.QA_ALUMNO_TOKEN,
  timeout: 15000 }).configureLogging(LogLevel.None).build();
try {
  await hub.start();
  await hub.invoke('UnirseASala', Number(process.env.QA_SALA_PIN));
  const before = await hub.invoke('ObtenerEstadoSala');
  assert.equal(before.estado, 'EnCurso');
  assert.ok(before.preguntaActual?.preguntaId, 'La sala debe tener una pregunta vigente');
  const preguntaId = before.preguntaActual.preguntaId;
  assert.ok(!before.preguntasRespondidas.includes(preguntaId), 'El alumno debe no haber respondido esta pregunta');
  const payload = { preguntaId, respuesta: process.env.QA_RESPUESTA || 'Respuesta QA', idempotencyKey: randomUUID(), secuencia: before.ultimaSecuencia + 1 };
  const first = await hub.invoke('EnviarRespuesta', payload);
  const second = await hub.invoke('EnviarRespuesta', payload);
  assert.equal(first.estado, 'Aceptada');
  assert.equal(second.estado, 'Duplicada');
  assert.equal(second.idempotencyKey, payload.idempotencyKey);
  assert.equal(second.recibidaUtc, first.recibidaUtc);
  const after = await hub.invoke('ObtenerEstadoSala');
  assert.equal(after.ultimaSecuencia, payload.secuencia);
  assert.equal(after.preguntasRespondidas.filter(id => id === preguntaId).length, 1);
  assert.equal(after.preguntasRespondidas.length, before.preguntasRespondidas.length + 1);
  console.log('PASS SignalR: Aceptada → Duplicada; una sola pregunta respondida y secuencia sin duplicar');
} finally {
  await hub.stop();
}
