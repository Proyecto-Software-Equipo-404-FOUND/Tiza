// Amplía la colección existente sin reemplazar sus carpetas ni su autenticación.
import { readFileSync, writeFileSync } from 'node:fs';
const file = new URL('../postman/PEI-Cursos.postman_collection.json', import.meta.url);
const collection = JSON.parse(readFileSync(file, 'utf8'));
const prefix = 'S2-QA-01';
const event = (listen, code) => ({ listen, script: { type: 'text/javascript', exec: code.split('\n') } });
function request(name, method, path, status, body, assertions = '', headers = {}) {
  return { name, request: { method, auth: { type: 'noauth' },
    header: Object.entries({ 'Content-Type': 'application/json', ...headers }).map(([key, value]) => ({ key, value })),
    url: '{{evaluacionesUrl}}' + path,
    ...(body ? { body: { mode: 'raw', raw: typeof body === 'string' ? body : JSON.stringify(body, null, 2) } } : {}) },
    event: [event('test', `pm.test('HTTP ${status}', () => pm.response.to.have.status(${status}));\n${assertions}`)] };
}
const institutional = tenant => ({ 'X-Institucion-Id': `{{tenant${tenant}}}` });
const internal = { 'X-Api-Key-Interna': '{{internalApiKey}}' };
const items = [];
for (const tenant of ['A', 'B']) {
  const suffix = tenant;
  const evaluation = { titulo: `S2-QA-01 ${tenant}`, tipo: 'Examen', preguntasJson: { version: 1, preguntas: [{
    id: '{{preguntaId}}', enunciado: '¿Cuánto es 1 + 1?', tipo: 'OpcionMultiple', puntajeMaximo: 1,
    opciones: [{ id: '{{opcionId}}', texto: '2', esCorrecta: true },
      { id: '00000000-0000-4000-8000-000000000099', texto: '3', esCorrecta: false }],
  }] } };
  items.push(request(`Crear evaluación ${tenant}`, 'POST', '/api/v1/evaluaciones', 201, evaluation,
    `const j = pm.response.json();
pm.test('Borrador de la institución correcta', () => { pm.expect(j.estado).eq('Borrador'); pm.expect(j.institucionId).eq(pm.environment.get('tenant${tenant}')); });
pm.environment.set('evaluacion${suffix}', j.id); pm.environment.set('version${suffix}', j.version);`, institutional(tenant)));
  items.push(request(`Publicar evaluación ${tenant}`, 'POST', `/api/v1/evaluaciones/{{evaluacion${suffix}}}/publicar`, 200,
    `{"versionEsperada":{{version${suffix}}},"cohorteId":"{{cohorte${tenant}}}"}`,
    `pm.test('Publicada', () => pm.expect(pm.response.json().estado).eq('Publicada'));`, institutional(tenant)));
  const lote = { version: 1, loteId: `{{lote${tenant}}}`, salaId: `{{sala${tenant}}}`, evaluacionId: `{{evaluacion${tenant}}}`,
    institucionId: `{{tenant${tenant}}}`, finalizadaUtc: '2026-11-11T12:00:00Z', motivoCierre: 'Docente',
    entregas: [{ alumnoId: `{{alumno${tenant}}}`, respuestas: [{ preguntaId: '{{preguntaId}}', respuesta: '{{opcionId}}', secuencia: 1, recibidaUtc: '2026-11-11T11:59:00Z' }], alertasFoco: [] }] };
  items.push(request(`Recibir lote ${tenant}`, 'POST', `/interno/salas/{{sala${tenant}}}/entregas`, 200, lote,
    `const j = pm.response.json();
pm.test('Una entrega nueva', () => { pm.expect(j.entregasRecibidas).eq(1); pm.expect(j.entregasNuevas).eq(1); pm.expect(j.entregasYaExistentes).eq(0); pm.expect(j.entregas).to.have.lengthOf(1); pm.expect(j.entregas[0].alumnoId).eq(pm.environment.get('alumno${tenant}')); });
pm.environment.set('entregas${tenant}', JSON.stringify(j.entregas)); pm.environment.set('entrega${tenant}', j.entregas[0].entregaId);`, internal));
  items.push(request(`Reenviar lote ${tenant} idéntico`, 'POST', `/interno/salas/{{sala${tenant}}}/entregas`, 200, lote,
    `const j = pm.response.json();
pm.test('No duplica ni cambia entregas', () => { pm.expect(j.entregasRecibidas).eq(1); pm.expect(j.entregasNuevas).eq(0); pm.expect(j.entregasYaExistentes).eq(1); pm.expect(j.entregas).deep.eq(JSON.parse(pm.environment.get('entregas${tenant}'))); });`, internal));
  items.push(request(`Rechazar lote ${tenant} con institución ajena`, 'POST', `/interno/salas/{{sala${tenant}}}/entregas`, 404,
    { ...lote, institucionId: `{{tenant${tenant === 'A' ? 'B' : 'A'}}}` }, '', internal));
  items.push(request(`Rechazar lote ${tenant} sin clave interna`, 'POST', `/interno/salas/{{sala${tenant}}}/entregas`, 401, lote));
}
const visibility = [];
for (const tenant of ['A', 'B']) {
  const other = tenant === 'A' ? 'B' : 'A';
  const headers = { Authorization: `Bearer {{token${tenant}}}`, ...institutional(tenant) };
  visibility.push(request(`${tenant} ve su entrega (control positivo obligatorio)`, 'GET', `/api/v1/evaluaciones/{{evaluacion${tenant}}}/entregas`, 200, null,
    `const rows = pm.response.json();
pm.test('Sólo la entrega propia', () => { pm.expect(rows).to.be.an('array').with.lengthOf(1); pm.expect(rows[0].alumnoId).eq(pm.environment.get('alumno${tenant}')); });`, headers));
  visibility.push(request(`${tenant} no lista entregas de ${other}`, 'GET', `/api/v1/evaluaciones/{{evaluacion${other}}}/entregas`, 404, null, '', headers));
  visibility.push(request(`${tenant} no lee entrega de ${other} por ID`, 'GET', `/api/v1/evaluaciones/{{evaluacion${other}}}/entregas/{{entrega${other}}}`, 404, null, '', headers));
}
collection.item = collection.item.filter(x => !x.name.startsWith(prefix));
collection.item.push({ name: `${prefix} Evaluaciones y lotes`, item: items },
  { name: `${prefix} Visibilidad de entregas`, item: visibility });
writeFileSync(file, JSON.stringify(collection, null, 2) + '\n');
