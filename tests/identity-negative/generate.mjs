import { readFileSync, writeFileSync } from 'node:fs';
const cases=[];
const register={email:'qa02-new@test.invalid',password:'{{qa02Password}}',nombre:'Prueba',apellido:'Sintética',rol:'Alumno'};
function add(id,name,path,expected,token='',body,errors=false,type='application/problem+json') {
  cases.push({id,name,path,method:body===undefined?'GET':'POST',expected,token,
    raw:body===undefined?null:typeof body==='string'?body:JSON.stringify(body),errors,contentType:type});
}
add('QA02-01','Contraseña incorrecta','/api/auth/login',401,'',{email:'{{qa02AdminEmail}}',password:'{{qa02WrongPassword}}'});
add('QA02-01b','Cuenta inexistente','/api/auth/login',401,'',{email:'unknown@test.invalid',password:'{{qa02WrongPassword}}'});
add('QA02-02','Sin JWT','/api/auth/administrador',401);
add('QA02-03','JWT malformado','/api/auth/administrador',401,'qa02MalformedToken');
add('QA02-04','JWT expirado','/api/auth/administrador',401,'qa02ExpiredToken');
add('QA02-05','Firma incorrecta','/api/auth/administrador',401,'qa02WrongSignatureToken');
add('QA02-06','Alumno no es Administrador','/api/auth/administrador',403,'institutionalStudentToken');
add('QA02-07','Docente no es Administrador','/api/auth/administrador',403,'institutionalTeacherToken');
add('QA02-08','Alumno no es Docente','/api/auth/docente',403,'institutionalStudentToken');
add('QA02-09','JWT institucional no es de Plataforma','/api/plataforma/instituciones',401,'institutionalAdminToken',undefined,false,'application/json');
add('QA02-10','Login sin campos','/api/auth/login',400,'',{},true);
add('QA02-11','Email inválido','/api/auth/login',400,'',{email:'no-email',password:'{{qa02Password}}'},true);
add('QA02-12','Rol no permitido','/api/auth/register',400,'institutionalAdminToken',{...register,rol:'Usuario'},true);
add('QA02-13','JSON incompleto','/api/auth/login',400,'','{"email":',true);
add('QA02-14','Email numérico','/api/auth/login',400,'',{email:123,password:'{{qa02Password}}'},true);
add('QA02-15','Nombre vacío','/api/auth/register',400,'institutionalAdminToken',{...register,nombre:''},true);
add('QA02-16','Email mayor a150','/api/auth/register',400,'institutionalAdminToken',{...register,email:'a'.repeat(151)+'@test.invalid'},true);
add('QA02-17','Password menor a8','/api/auth/register',400,'institutionalAdminToken',{...register,password:'{{qa02ShortPassword}}'},true);
add('QA02-18','Password mayor a72 bytes UTF8','/api/auth/register',400,'institutionalAdminToken',{...register,password:'{{qa02LongPassword}}'},true);
add('QA02-19','Nombre mayor a100','/api/auth/register',400,'institutionalAdminToken',{...register,nombre:'N'.repeat(101)},true);
add('QA02-20','Apellido mayor a100','/api/auth/register',400,'institutionalAdminToken',{...register,apellido:'A'.repeat(101)},true);
add('QA02-21','Institución enviada por cliente','/api/auth/register',400,'institutionalAdminToken',{...register,institucionId:'11111111-1111-1111-1111-111111111111'},true);
add('QA02-22','JSON null','/api/auth/login',400,'','null',true);
add('QA02-23','Registro no asigna Administrador','/api/auth/register',400,'institutionalAdminToken',{...register,rol:'Administrador'},true);
add('QA02-24','Rol distingue mayúsculas','/api/auth/register',400,'institutionalAdminToken',{...register,rol:'alumno'},true);
add('QA02-25','Refresh demasiado largo','/api/auth/refresh',400,'',{refreshToken:'{{qa02LongRefresh}}'},true);
add('QA02-26','Logout demasiado largo','/api/auth/logout',400,'',{refreshToken:'{{qa02LongRefresh}}'},true);
add('QA02-27','Refresh desconocido','/api/auth/refresh',401,'',{refreshToken:'{{qa02UnknownRefresh}}'});
add('QA02-28','Rol ausente invalida identidad','/api/auth/administrador',401,'qa02MissingRoleToken');
add('QA02-C1','Control Administrador válido','/api/auth/administrador',200,'institutionalAdminToken');
add('QA02-C2','Control Docente válido','/api/auth/docente',200,'institutionalTeacherToken');
add('QA02-C3','Control Alumno válido','/api/auth/alumno',200,'institutionalStudentToken');
add('QA02-C4','Logout vacío idempotente','/api/auth/logout',204,'',{});
const name='QA-02 — Casos negativos y permisos de Identidad';
const collectionPath=new URL('../postman/identidad.postman_collection.json',import.meta.url);
const collection=JSON.parse(readFileSync(collectionPath,'utf8'));
const folder={name,description:'Sólo fixture QA02 en memoria. Sin qa02Fixture y allowMutations las escrituras quedan omitidas. Tokens y contraseñas efímeros provistos por el runner. No ejecutar sobre una base persistente.',item:cases.map(c=>({
  name:`${c.id} — ${c.name}`,
  request:{method:c.method,auth:c.token?{type:'bearer',bearer:[{key:'token',value:`{{${c.token}}}`,type:'string'}]}:{type:'noauth'},
    header:c.raw===null?[]:[{key:'Content-Type',value:'application/json'}],url:`{{baseUrl}}${c.path}`,
    ...(c.raw===null?{}:{body:{mode:'raw',raw:c.raw,options:{raw:{language:'json'}}}})},
  event:[{listen:'prerequest',script:{type:'text/javascript',exec:[
    "if(pm.environment.get('qa02Fixture')!=='isolated-memory-v1'){pm.execution.skipRequest();}",
    ...(c.method==='POST'?["if(pm.environment.get('allowMutations')!=='true'){pm.execution.skipRequest();}"]:[]),
    ...(c.token?[`if(!pm.environment.get('${c.token}')){throw new Error('Falta token sintético del fixture QA02');}`]:[])
  ]}},{listen:'test',script:{type:'text/javascript',exec:[
    `pm.test('HTTP ${c.expected}',()=>pm.response.to.have.status(${c.expected}));`,
    ...(c.expected>=400?[
      `pm.test('Content-Type contractual',()=>pm.expect(pm.response.headers.get('Content-Type')).to.include('${c.contentType}'));`,
      "pm.test('ProblemDetails coherente',()=>{const b=pm.response.json();pm.expect(b.status).to.eql(pm.response.code);pm.expect(b.title).to.be.a('string').and.not.empty;['type','detail','instance'].forEach(k=>{if(b[k]!=null)pm.expect(b[k]).to.be.a('string');});});",
      ...(c.errors?["pm.test('ValidationProblemDetails con errors',()=>{const b=pm.response.json();pm.expect(b.errors).to.be.an('object');pm.expect(Object.keys(b.errors).length).to.be.above(0);});"]:[]),
      "pm.test('Sin tokens ni diagnóstico sensible',()=>{const b=pm.response.json();pm.expect(b).not.to.have.property('accessToken');pm.expect(b).not.to.have.property('refreshToken');const text=pm.response.text();pm.expect(text).not.to.match(/eyJ[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+|stacktrace|Npgsql|PasswordHash|System\\.[A-Za-z]+Exception/i);['qa02Password','qa02WrongPassword','qa02LongPassword','qa02ShortPassword','qa02AdminEmail'].forEach(k=>{const s=pm.environment.get(k);if(s)pm.expect(text.includes(s),k+' no expuesto').to.eql(false);});});"
    ]:c.expected===204?["pm.test('Sin cuerpo',()=>pm.expect(pm.response.text()).to.eql(''));"]:["pm.test('Recurso protegido accesible',()=>pm.expect(pm.response.json().mensaje).to.be.a('string'));"])
  ]}}]
}))};
const index=collection.item.findIndex(x=>x.name===name);
if(index<0)collection.item.push(folder);else collection.item[index]=folder;
writeFileSync(collectionPath,JSON.stringify(collection,null,2)+'\n');
writeFileSync(new URL('cases.json',import.meta.url),JSON.stringify(cases,null,2)+'\n');
console.log(`${cases.length} casos QA02; carpetas previas preservadas.`);
