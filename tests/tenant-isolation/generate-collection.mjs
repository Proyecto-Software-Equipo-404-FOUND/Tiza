import { writeFileSync } from 'node:fs';
const item = [];
function request(name, method, route, status, token = 'tokenA', body, extra = [], service = 'cursosUrl', headers = []) {
  item.push({ name, request: { method,
    header: [{ key: 'Content-Type', value: 'application/json' }, ...(token ? [{ key: 'Authorization', value: `Bearer {{${token}}}` }] : []), ...headers],
    url: `{{${service}}}${route}`, ...(body ? { body: { mode: 'raw', raw: JSON.stringify(body), options: { raw: { language: 'json' } } } } : {}) },
    event: [{ listen: 'test', script: { type: 'text/javascript', exec: [
      `pm.test('HTTP esperado ${status}', () => pm.response.to.have.status(${status}));`,
      ...extra
    ] } }] });
}
for (const tenant of ['A', 'B']) request(`SETUP Login Docente ${tenant}`, 'POST', '/api/auth/login', 200, null,
  { email: `docente@${tenant.toLowerCase()}.test`, password: '{{password}}' }, [
    `pm.test('Sesión institucional ${tenant}', () => { const b=pm.response.json(); pm.expect(b.tokenType).to.eql('Bearer'); pm.expect(b.accessToken).to.be.a('string').and.not.empty; pm.collectionVariables.set('token${tenant}',b.accessToken); const c=JSON.parse(atob(b.accessToken.split('.')[1].replace(/-/g,'+').replace(/_/g,'/'))); pm.expect(c.institucion_id).to.eql(pm.variables.get('tenant${tenant}')); pm.expect(c.role).to.eql('Docente'); });`
  ], 'identidadUrl');
request('QA04-01 A lista sólo sus cursos', 'GET', '/api/v1/cursos', 200, 'tokenA', null, [
  "pm.test('Exactamente los dos cursos A, ninguno B', () => {const b=pm.response.json(); pm.expect(b.totalItems).to.eql(2); pm.expect(b.items.map(x=>x.id).sort()).to.eql([pm.variables.get('cursoA'),pm.variables.get('cursoA2')].sort()); b.items.forEach(x=>pm.expect(x.institucionId).to.eql(pm.variables.get('tenantA')));});"
]);
request('QA04-02 A no lee curso B', 'GET', '/api/v1/cursos/{{cursoB}}', 404, 'tokenA', null, [
  "pm.test('No filtra atributos B', () => {pm.expect(pm.response.text()).not.to.include('Bases de Datos'); pm.expect(pm.response.text()).not.to.include(pm.variables.get('tenantB'));});"
]);
request('QA04-03 A no lista cohortes B', 'GET', '/api/v1/cursos/{{cursoB}}/cohortes', 404);
request('QA04-04 A no modifica curso B', 'PUT', '/api/v1/cursos/{{cursoB}}', 404, 'tokenA', { nombre: 'Intrusión QA04', area: 'Intrusión' });
request('QA04-05 A no modifica cohorte B', 'PUT', '/api/v1/cursos/{{cursoB}}/cohortes/{{cohorteB}}', 404, 'tokenA', { nombre: 'Intrusión QA04', periodo: '2099', docenteId: '{{docenteA}}' });
request('QA04-06 B no lee curso A', 'GET', '/api/v1/cursos/{{cursoA}}', 404, 'tokenB');
request('QA04-06 B no lista cohortes A', 'GET', '/api/v1/cursos/{{cursoA}}/cohortes', 404, 'tokenB');
request('QA04-07 sin JWT', 'GET', '/api/v1/cursos', 401, null);
request('QA04-08 JWT firma inválida', 'GET', '/api/v1/cursos', 401, 'invalidToken');
for (const token of ['missingTenantToken', 'malformedTenantToken', 'emptyTenantToken']) {
  request(`QA04-09 ${token} lista vacía`, 'GET', '/api/v1/cursos', 200, token, null, [
    "pm.test('Sin contexto no hay registros',()=>{const b=pm.response.json();pm.expect(b.items).to.eql([]);pm.expect(b.totalItems).to.eql(0);});"
  ]);
  request(`QA04-09 ${token} detalle rechazado`, 'GET', '/api/v1/cursos/{{cursoA}}', 404, token);
  request(`QA04-09 ${token} cohorte rechazada`, 'GET', '/api/v1/cohortes/{{cohorteB}}/validacion', 404, token);
  request(`QA04-09 ${token} matrícula rechazada`, 'GET', '/api/v1/cohortes/{{cohorteB}}/matriculas/{{matriculaB}}', 404, token);
  request(`QA04-09 ${token} escritura rechazada`, 'POST', '/api/v1/cursos', 400, token, { nombre: 'No debe existir', area: 'QA04' }, [
    "pm.test('Error de dominio ProblemDetails',()=>{pm.expect(pm.response.json().status).to.eql(400); pm.expect(pm.response.headers.get('Content-Type')).to.include('application/problem+json');});"
  ]);
}
request('QA04-10 Docente A lee su curso', 'GET', '/api/v1/cursos/{{cursoA}}', 200, 'tokenA', null, [
  "pm.test('Curso A íntegro',()=>{const b=pm.response.json();pm.expect(b.id).to.eql(pm.variables.get('cursoA'));pm.expect(b.institucionId).to.eql(pm.variables.get('tenantA'));pm.expect(b.nombre).to.eql('Programación I');});"
]);
request('QA04-10 Docente A lee su cohorte', 'GET', '/api/v1/cursos/{{cursoA}}/cohortes', 200, 'tokenA', null, [
  "pm.test('Cohorte propia',()=>{const b=pm.response.json();pm.expect(b).to.have.lengthOf(1);pm.expect(b[0].id).to.eql(pm.variables.get('cohorteA'));pm.expect(b[0].cursoId).to.eql(pm.variables.get('cursoA'));});"
]);
request('EXTRA A no elimina curso B', 'DELETE', '/api/v1/cursos/{{cursoB}}', 404);
request('EXTRA A no elimina cohorte B', 'DELETE', '/api/v1/cursos/{{cursoB}}/cohortes/{{cohorteB}}', 404);
request('EXTRA A no crea cohorte en B', 'POST', '/api/v1/cursos/{{cursoB}}/cohortes', 404, 'tokenA', { nombre: 'Intrusión QA04', periodo: '2026', docenteId: '{{docenteA}}' });
request('EXTRA A no valida cohorte B', 'GET', '/api/v1/cohortes/{{cohorteB}}/validacion', 404);
request('EXTRA A no lee matrícula B', 'GET', '/api/v1/cohortes/{{cohorteB}}/matriculas/{{matriculaB}}', 404);
request('EXTRA B no modifica curso A', 'PUT', '/api/v1/cursos/{{cursoA}}', 404, 'tokenB', { nombre: 'Intrusión QA04', area: 'Intrusión' });
request('EXTRA B conserva curso después de ataques', 'GET', '/api/v1/cursos/{{cursoB}}', 200, 'tokenB', null, [
  "pm.test('Curso B no fue alterado',()=>{const b=pm.response.json();pm.expect(b.nombre).to.eql('Bases de Datos');pm.expect(b.area).to.eql('Datos');pm.expect(b.activo).to.eql(true);pm.expect(b.institucionId).to.eql(pm.variables.get('tenantB'));});"
]);
request('EXTRA B conserva cohorte después de ataques', 'GET', '/api/v1/cursos/{{cursoB}}/cohortes', 200, 'tokenB', null, [
  "pm.test('Cohorte B no fue alterada',()=>{const b=pm.response.json();pm.expect(b).to.have.lengthOf(1);pm.expect(b[0].id).to.eql(pm.variables.get('cohorteB'));pm.expect(b[0].nombre).to.eql('Comisión 1B');pm.expect(b[0].periodo).to.eql('2026-2C');pm.expect(b[0].docenteId).to.eql(pm.variables.get('docenteB'));pm.expect(b[0].activo).to.eql(true);});"
]);
request('EXTRA encabezado B no suplanta JWT A', 'GET', '/api/v1/cursos', 200, 'tokenA', null, [
  "pm.test('Header no cambia tenant',()=>{const b=pm.response.json();pm.expect(b.totalItems).to.eql(2);b.items.forEach(x=>pm.expect(x.institucionId).to.eql(pm.variables.get('tenantA')));});"
], 'cursosUrl', [{ key: 'X-Institucion-Id', value: '{{tenantB}}' }]);
request('EXTRA rol Docente validado por Identidad', 'GET', '/api/auth/docente', 200, 'tokenA', null, [], 'identidadUrl');
request('EXTRA Docente no tiene acceso Administrador', 'GET', '/api/auth/administrador', 403, 'tokenA', null, [], 'identidadUrl');
const collection = {
  info: { name: 'PEI - QA04 aislamiento multi-tenant', schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json',
    description: 'Sólo entorno descartable del runner QA04. Reutiliza los UUID de CU06/ID08. No exportar variables de ejecución: contienen credenciales efímeras. QA04-09 exige vacío en lista,404 en detalle y400 al crear, según filtros y DomainException existentes; no admite respuestas alternativas.' },
  variable: [], item
};
writeFileSync(new URL('../postman/PEI-QA04-Aislamiento.postman_collection.json', import.meta.url), JSON.stringify(collection, null, 2) + '\n');
console.log(`${item.length} solicitudes generadas sin secretos.`);
