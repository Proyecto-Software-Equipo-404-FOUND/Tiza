// Reutiliza Newman/lockfile QA05 y el entorno Postman existente. Sólo loopback.
import newman from '../identity-negative/node_modules/newman/index.js';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { randomUUID } from 'node:crypto';
import assert from 'node:assert/strict';
const read = relative => JSON.parse(readFileSync(new URL(relative, import.meta.url), 'utf8'));
const url = process.env.QA_EVALUACIONES_URL || 'http://127.0.0.1:18543';
assert.ok(['127.0.0.1', 'localhost'].includes(new URL(url).hostname), 'Sólo APIs locales de prueba');
assert.ok(process.env.QA_INTERNAL_API_KEY, 'Falta QA_INTERNAL_API_KEY');
const environment = read('../postman/identidad.local.postman_environment.json');
const values = { evaluacionesUrl: url, internalApiKey: process.env.QA_INTERNAL_API_KEY,
  tenantA: '11111111-1111-1111-1111-111111111111', tenantB: '22222222-2222-2222-2222-222222222222',
  cohorteA: '33333333-3333-3333-3333-333333333333', cohorteB: '44444444-4444-4444-4444-444444444444',
  alumnoA: '11111111-0000-0000-0000-000000000003', alumnoB: '22222222-0000-0000-0000-000000000003',
  preguntaId: randomUUID(), opcionId: randomUUID(), salaA: randomUUID(), salaB: randomUUID(), loteA: randomUUID(), loteB: randomUUID(),
  tokenA: process.env.QA_TOKEN_A || '', tokenB: process.env.QA_TOKEN_B || '' };
environment.values.push(...Object.entries(values).map(([key, value]) => ({ key, value, enabled: true })));
const visibility = process.env.QA_TEST_VISIBILITY === 'true';
if (visibility) assert.ok(values.tokenA && values.tokenB, 'Visibilidad requiere JWT reales A/B');
const folders = ['S2-QA-01 Evaluaciones y lotes'];
if (visibility) folders.push('S2-QA-01 Visibilidad de entregas (requiere S2-EV-03)');
const summary = await new Promise((resolve, reject) => newman.run({
  collection: read('../postman/PEI-Cursos.postman_collection.json'), environment, folder: folders,
  reporters: [], timeoutRequest: 15000, timeout: 120000,
}, (error, result) => error ? reject(error) : resolve(result)));
// Nunca exportar environment ni los reports crudos: contienen la clave interna/JWT.
const evidence = { timestamp: new Date().toISOString(), visibility: visibility ? 'ejecutada' : 'bloqueada: S2-EV-03 fuera de main',
  requests: summary.run.executions.map(x => ({ name: x.item.name, status: x.response?.code,
    assertions: (x.assertions || []).map(a => ({ name: a.assertion, passed: !a.error })) })),
  failures: summary.run.failures.length, assertions: summary.run.stats.assertions.total };
mkdirSync(new URL('results/', import.meta.url), { recursive: true });
writeFileSync(new URL('results/newman.json', import.meta.url), JSON.stringify(evidence, null, 2));
assert.equal(evidence.requests.length, visibility ? 18 : 12);
assert.ok(evidence.requests.every(x => x.assertions.length > 0));
assert.equal(evidence.failures, 0, 'Newman falló; ver evidencia sanitizada');
console.log(`PASS Newman: ${evidence.requests.length} requests, ${evidence.assertions} aserciones. Visibilidad: ${evidence.visibility}`);
