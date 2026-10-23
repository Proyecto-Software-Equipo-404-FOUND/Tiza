import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import Ajv2020 from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';
import SwaggerParser from '@apidevtools/swagger-parser';

const root = fileURLToPath(new URL('../../', import.meta.url));
const read = relative => JSON.parse(readFileSync(path.join(root, relative), 'utf8'));
for (const name of readdirSync(path.join(root, 'contracts/openapi')).filter(x => x.endsWith('.json'))) {
  test(`OpenAPI válido: ${name}`, async () => {
    // No descargar referencias remotas de contratos no confiables.
    await SwaggerParser.validate(read(`contracts/openapi/${name}`), { resolve: { external: false } });
  });
}
const ajv = new Ajv2020({ allErrors: true, strict: false });
addFormats(ajv);
const schema = read('contracts/schemas/preguntas-v1.schema.json');
const validate = ajv.compile(schema);
const id = '00000000-0000-4000-8000-000000000101';
const pregunta = { id, enunciado: 'Explicar una relación.', tipo: 'Abierta', puntajeMaximo: 10,
  rubrica: { criterios: [{ descripcion: 'Claridad', puntaje: 10 }] } };
const base = { version: 1, preguntas: [pregunta] };
const clone = () => structuredClone(base);
const cases = [
  ['abierta con rúbrica', true, x => x],
  ['opción múltiple', true, x => { x.preguntas[0].tipo = 'OpcionMultiple'; x.preguntas[0].opciones = [{ id, texto: 'A', esCorrecta: true }]; }],
  ['sin version', false, x => { delete x.version; }],
  ['sin preguntas', false, x => { delete x.preguntas; }],
  ['version textual', false, x => { x.version = '1'; }],
  ['preguntas no array', false, x => { x.preguntas = {}; }],
  ['sin id', false, x => { delete x.preguntas[0].id; }],
  ['UUID inválido', false, x => { x.preguntas[0].id = 'incorrecto'; }],
  ['tipo desconocido', false, x => { x.preguntas[0].tipo = 'VerdaderoFalso'; }],
  ['puntaje negativo', false, x => { x.preguntas[0].puntajeMaximo = -1; }],
  ['puntaje textual', false, x => { x.preguntas[0].puntajeMaximo = '10'; }],
  ['sin enunciado', false, x => { delete x.preguntas[0].enunciado; }],
  ['opción sin respuesta', false, x => { x.preguntas[0].opciones = [{ id, texto: 'A' }]; }],
  ['respuesta no booleana', false, x => { x.preguntas[0].opciones = [{ id, texto: 'A', esCorrecta: 'true' }]; }],
  ['UUID de opción inválido', false, x => { x.preguntas[0].opciones = [{ id: 'x', texto: 'A', esCorrecta: true }]; }],
  ['rúbrica sin descripción', false, x => { delete x.preguntas[0].rubrica.criterios[0].descripcion; }],
  ['rúbrica puntaje textual', false, x => { x.preguntas[0].rubrica.criterios[0].puntaje = '10'; }],
];
for (const [name, expected, mutate] of cases) test(`Schema: ${name}`, () => {
  const value = clone(); mutate(value);
  assert.equal(validate(value), expected, JSON.stringify(validate.errors));
});

// Caracterización explícita, NO aprobación funcional: estas permisividades son hallazgos QA06-H04.
for (const [name, mutate] of [
  ['version 99', x => { x.version = 99; }],
  ['lista vacía', x => { x.preguntas = []; }],
  ['ids duplicados', x => { x.preguntas.push(structuredClone(x.preguntas[0])); }],
  ['opción múltiple sin opciones', x => { x.preguntas[0].tipo = 'OpcionMultiple'; }],
  ['abierta sin rúbrica', x => { delete x.preguntas[0].rubrica; }],
  ['enunciado vacío', x => { x.preguntas[0].enunciado = ''; }],
  ['criterio negativo', x => { x.preguntas[0].rubrica.criterios[0].puntaje = -2; }],
]) test(`Brecha conocida QA06-H04 (no aceptación): ${name}`, () => {
  const value = clone(); mutate(value); assert.equal(validate(value), true);
});
