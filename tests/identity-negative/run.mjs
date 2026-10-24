import { spawn, execFileSync } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import assert from 'node:assert/strict';
import newman from 'newman';

const root=fileURLToPath(new URL('../../',import.meta.url));
const results=path.join(root,'tests/identity-negative/results');
mkdirSync(results,{recursive:true});
const collection=JSON.parse(readFileSync(path.join(root,'tests/postman/identidad.postman_collection.json'),'utf8'));
const folder='QA-02 — Casos negativos y permisos de Identidad';
const cases=JSON.parse(readFileSync(new URL('cases.json',import.meta.url),'utf8'));
const evidence={date:new Date().toISOString(),base:execFileSync('git',['rev-parse','HEAD'],{cwd:root,encoding:'utf8'}).trim(),fixture:'TestServer+fakes existentes; sin PostgreSQL',cases:[],completed:false};
let host;
async function command(args,label){
  console.log(label);
  await new Promise((resolve,reject)=>{
    const child=spawn('dotnet',args,{cwd:root,windowsHide:true,stdio:['ignore','pipe','pipe']});
    let log='';child.stdout.on('data',x=>log+=x);child.stderr.on('data',x=>log+=x);
    child.on('error',reject);child.on('exit',code=>code===0?resolve():reject(new Error(`${label}: salida ${code}; revisar diagnóstico local sin publicar datos sensibles.`)));
  });
}
async function run(environment){return await new Promise((resolve,reject)=>newman.run({collection,folder,environment:{values:Object.entries(environment).map(([key,value])=>({key,value,enabled:true}))},reporters:[],timeoutRequest:30000,timeoutScript:10000,timeout:300000},(err,result)=>err?reject(err):resolve(result)));}
try{
  const original=JSON.parse(execFileSync('git',['show','853f3f6:tests/postman/identidad.postman_collection.json'],{cwd:root,encoding:'utf8'}));
  assert.deepEqual({...collection,item:collection.item.filter(x=>x.name!==folder)},original,'Se alteró contenido previo de la colección');
  evidence.previousCollectionPreserved=true;
  await new Promise((resolve,reject)=>{const s=createServer();s.on('error',()=>reject(new Error('Puerto18502 ocupado; no se detuvo ningún proceso ajeno.')));s.listen(18502,'127.0.0.1',()=>s.close(resolve));});
  await command(['build','services/identidad/PEI.Identidad.sln','--nologo','-v','quiet','-m:1'],'Compilar Identidad');
  await command(['build','services/identidad/tests/PEI.Identidad.Qa02Host','--nologo','-v','quiet','-m:1'],'Compilar puente QA02');
  await command(['test','services/identidad/tests/PEI.Identidad.UnitTests','--no-build','--logger','trx;LogFileName=qa02.trx','--results-directory',results,'--nologo','-v','quiet','-m:1'],'Regresión xUnit Identidad');
  const trx=readFileSync(path.join(results,'qa02.trx'),'utf8');
  evidence.dotnet=Object.fromEntries([...trx.match(/<Counters ([^>]+)\/>/)[1].matchAll(/(\w+)="(\d+)"/g)].map(x=>[x[1],Number(x[2])]));
  assert.equal(evidence.dotnet.notExecuted,0);
  const variables=await new Promise((resolve,reject)=>{
    host=spawn('dotnet',['services/identidad/tests/PEI.Identidad.Qa02Host/bin/Debug/net8.0/PEI.Identidad.Qa02Host.dll'],{cwd:root,windowsHide:true,stdio:['ignore','pipe','ignore']});
    let pending='';
    const timer=setTimeout(()=>reject(new Error('Fixture QA02 no inició en60 segundos')),60000);
    host.on('error',error=>{clearTimeout(timer);reject(error);});
    host.on('exit',code=>{clearTimeout(timer);reject(new Error(`Fixture terminó antes de readiness (${code})`));});
    host.stdout.on('data',chunk=>{
      pending+=chunk;
      const lines=pending.split('\n');pending=lines.pop();
      for(const line of lines){try{const json=JSON.parse(line);if(json.institutionalAdminToken){clearTimeout(timer);resolve(json);}}catch{/* Nunca imprimir stdout que contiene secretos efímeros. */}}
    });
  });
  const baseUrl='http://127.0.0.1:18502';
  const audit=async()=>await (await fetch(baseUrl+'/__qa02/audit')).json();
  const before=await audit();
  console.log('Verificar guardas: sin fixture autorizado no se envían peticiones');
  const disabled=await run({baseUrl,...variables,allowMutations:'false'});
  assert.equal(disabled.run.stats.requests.total,0,'La guarda no evitó peticiones sin fixture');
  assert.equal(disabled.run.failures.length,0,'Error de scripts en ejecución omitida');
  evidence.disabledGuard={requests:0,result:'PASS'};
  const readOnly=await run({baseUrl,...variables,allowMutations:'false',qa02Fixture:'isolated-memory-v1'});
  assert.equal(readOnly.run.stats.requests.total,cases.filter(c=>c.method==='GET').length);
  assert.equal(readOnly.run.failures.length,0);
  assert.ok(readOnly.run.executions.filter(e=>e.response).every(e=>e.request.method==='GET'));
  evidence.readOnlyGuard={requests:readOnly.run.stats.requests.total,result:'PASS'};
  console.log('Ejecutar carpeta QA02 sobre TestServer real con persistencia en memoria');
  const summary=await run({baseUrl,...variables,allowMutations:'true',qa02Fixture:'isolated-memory-v1'});
  for(const execution of summary.run.executions){
    const id=execution.item.name.split(' — ')[0];const expected=cases.find(c=>c.id===id);
    const assertions=(execution.assertions??[]).map(x=>({name:x.assertion,passed:!x.error}));
    evidence.cases.push({id,name:expected.name,method:expected.method,endpoint:expected.path,tokenProfile:expected.token||'anónimo',expected:expected.expected,actual:execution.response?.code??null,
      contentType:execution.response?.headers.get('Content-Type')??null,assertions,result:execution.response?.code===expected.expected && assertions.length>0 && assertions.every(a=>a.passed)?'PASS':'FAIL'});
  }
  const after=await audit();evidence.persistence={before,after,unchanged:JSON.stringify(before)===JSON.stringify(after)};
  evidence.newman={requests:summary.run.stats.requests.total,assertions:summary.run.stats.assertions.total,failed:summary.run.stats.assertions.failed,failures:summary.run.failures.length};
  evidence.failureTypes=summary.run.failures.map(f=>({name:f.error?.name,code:f.error?.code}));
  assert.deepEqual(after,before,'Hubo persistencia inesperada');
  assert.equal(evidence.newman.requests,cases.length,'Casos omitidos inesperadamente');
  assert.equal(summary.run.failures.length,0,'Fallos Newman; consultar nombres de aserciones sanitizados');
  assert.ok(evidence.cases.every(c=>c.result==='PASS'));
  evidence.completed=true;
  console.log(`PASS: ${evidence.newman.requests} solicitudes, ${evidence.newman.assertions} aserciones; ${evidence.dotnet.passed} casos xUnit.`);
}catch(error){evidence.error=error.message;console.error(error.message);process.exitCode=1;}
finally{
  if(host && host.exitCode===null){host.kill();await new Promise(resolve=>{host.once('exit',resolve);setTimeout(resolve,5000);});}
  writeFileSync(path.join(results,'summary.json'),JSON.stringify(evidence,null,2)+'\n');
}
