import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import SwaggerParser from '@apidevtools/swagger-parser';
const root = fileURLToPath(new URL('../../', import.meta.url));
const read = p => JSON.parse(readFileSync(path.join(root, p), 'utf8'));
const methods = ['get', 'post', 'put', 'delete', 'patch'];
const rows = [];
// Comparación estructural: conserva required, tipos, formatos y restricciones;
// ignora únicamente texto editorial. Cada diferencia requiere juicio humano.
const editorial = new Set(['description', 'title', 'example', 'examples', 'externalDocs']);
function canonical(value) {
  if (Array.isArray(value)) return value.map(canonical);
  if (!value || typeof value !== 'object') return value;
  return Object.fromEntries(Object.keys(value).sort().filter(k => !editorial.has(k)).map(k => [k, canonical(value[k])]));
}
const details = [];
for (const service of ['cursos', 'identidad', 'ia', 'evaluaciones', 'sala-en-vivo']) {
  const live = read(`tests/contracts/results/${service}.json`);
  await SwaggerParser.validate(structuredClone(live), { resolve: { external: false } });
  const frozenPath = `contracts/openapi/${service}-v1.json`;
  const frozen = existsSync(path.join(root, frozenPath)) ? read(frozenPath) : null;
  const liveResolved = await SwaggerParser.dereference(structuredClone(live), { resolve: { external: false } });
  const frozenResolved = frozen ? await SwaggerParser.dereference(structuredClone(frozen), { resolve: { external: false } }) : null;
  for (const route of new Set([...Object.keys(live.paths), ...Object.keys(frozen?.paths ?? {})])) {
    for (const method of methods) {
      const actual = live.paths[route]?.[method], declared = frozen?.paths[route]?.[method];
      if (!actual && !declared) continue;
      const a = liveResolved.paths[route]?.[method], d = frozenResolved?.paths[route]?.[method];
      const differences = [];
      if (a && d) {
        for (const field of ['parameters', 'requestBody', 'responses', 'security']) {
          const expected = canonical(d[field] ?? (field === 'security' ? frozenResolved.security : null));
          const obtained = canonical(a[field] ?? (field === 'security' ? liveResolved.security : null));
          if (JSON.stringify(expected) !== JSON.stringify(obtained)) differences.push({ field, expected, obtained });
        }
      }
      details.push({ service, method: method.toUpperCase(), route, differences,
        implementation: canonical({ parameters: a?.parameters, requestBody: a?.requestBody, responses: a?.responses, security: a?.security ?? live.security }) });
      rows.push({ service, method: method.toUpperCase(), route,
        frozen: declared ? Object.keys(declared.responses) : null,
        swagger: actual ? Object.keys(actual.responses) : null,
        request: actual?.requestBody?.content?.['application/json']?.schema ?? null,
        security: actual?.security ?? live.security ?? [],
        result: !frozen ? 'SIN CONTRATO VERSIONADO' : !declared ? 'NO DOCUMENTADO' : !actual ? 'SIN IMPLEMENTACION' : 'RUTA PRESENTE; REQUIERE REVISION SEMANTICA' });
    }
  }
}
writeFileSync(path.join(root, 'tests/contracts/results/endpoints.json'), JSON.stringify(rows, null, 2) + '\n');
writeFileSync(path.join(root, 'tests/contracts/results/semantic-review.json'), JSON.stringify(details, null, 2) + '\n');
const table = ['| Servicio | Método | Ruta | Respuestas contrato | Respuestas Swagger | Resultado |',
  '|---|---|---|---|---|---|', ...rows.map(r => `| ${r.service} | ${r.method} | \`${r.route}\` | ${r.frozen?.join(', ') ?? '—'} | ${r.swagger?.join(', ') ?? '—'} | ${r.result} |`)];
writeFileSync(path.join(root, 'tests/contracts/results/endpoints.md'), table.join('\n') + '\n');
console.log(`${rows.length} operaciones inventariadas; cinco Swagger válidos. La presencia de rutas no acredita comportamiento HTTP.`);
