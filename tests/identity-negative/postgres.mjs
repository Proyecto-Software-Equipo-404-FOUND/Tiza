// Regresión opcional de la suite existente: PostgreSQL descartable, nunca una conexión externa.
import { spawn } from 'node:child_process';
import { randomUUID, randomBytes } from 'node:crypto';
import { createServer } from 'node:net';
import { mkdirSync,readFileSync,writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const root=fileURLToPath(new URL('../../',import.meta.url));
const results=path.join(root,'tests/identity-negative/results');mkdirSync(results,{recursive:true});
const docker=process.env.DOCKER_EXE || (process.platform==='win32'?'C:/Program Files/Docker/Docker/resources/bin/docker.exe':'docker');
const id=randomUUID(),name=`tiza-qa02-${id}`,password=randomBytes(32).toString('hex');
const env={...process.env,POSTGRES_PASSWORD:password,PEI_IDENTIDAD_TEST_CONNECTION:`Host=127.0.0.1;Port=5541;Database=pei_identidad_tests;Username=qa02;Password=${password};Pooling=false`};
let created=false;
const evidence={date:new Date().toISOString(),runId:id,completed:false};
async function command(executable,args){return await new Promise((resolve,reject)=>{
  const child=spawn(executable,args,{cwd:root,env,windowsHide:true,stdio:['ignore','pipe','pipe']});let output='';
  child.stdout.on('data',x=>output+=x);child.stderr.on('data',()=>{});child.on('error',reject);
  child.on('exit',code=>code===0?resolve(output.trim()):reject(new Error(`${path.basename(executable)} ${args[0]}: salida ${code}; no se publican logs sensibles`)));
});}
try{
  await new Promise((resolve,reject)=>{const s=createServer();s.on('error',()=>reject(new Error('Puerto5541 ocupado; no se reutilizó el servicio existente')));s.listen(5541,'127.0.0.1',()=>s.close(resolve));});
  console.log('Crear PostgreSQL QA02 descartable');
  await command(docker,['run','-d','--name',name,'--label',`tiza.qa02.run=${id}`,'-p','127.0.0.1:5541:5432','-e','POSTGRES_PASSWORD','-e','POSTGRES_USER=qa02','-e','POSTGRES_DB=pei_identidad_tests','postgres:16-alpine']);created=true;
  let ready=false;
  for(let i=0;i<60;i++){try{await command(docker,['exec',name,'pg_isready','-h','127.0.0.1','-U','qa02','-d','pei_identidad_tests']);ready=true;break;}catch{await new Promise(r=>setTimeout(r,1000));}}
  if(!ready)throw new Error('Timeout PostgreSQL');
  console.log('Ejecutar integración existente con conexión dedicada autorizada');
  await command('dotnet',['test','services/identidad/tests/PEI.Identidad.IntegrationTests','--logger','trx;LogFileName=postgres.trx','--results-directory',results,'--nologo','-v','quiet','-m:1']);
  const trx=readFileSync(path.join(results,'postgres.trx'),'utf8');
  evidence.counters=Object.fromEntries([...trx.match(/<Counters ([^>]+)\/>/)[1].matchAll(/(\w+)="(\d+)"/g)].map(x=>[x[1],Number(x[2])]));
  if(evidence.counters.notExecuted || evidence.counters.failed)throw new Error('Regresión incompleta o fallida');
  evidence.completed=true;console.log(`PASS: ${evidence.counters.passed} integración,0 omitidos.`);
}catch(error){evidence.error=error.message;console.error(error.message);process.exitCode=1;}
finally{
  if(created){try{const label=await command(docker,['inspect','--format','{{index .Config.Labels "tiza.qa02.run"}}',name]);if(label!==id)throw new Error('Etiqueta distinta: no se eliminó el contenedor');await command(docker,['rm','-f','-v',name]);evidence.cleanedUp=true;}catch(error){evidence.cleanupError=error.message;process.exitCode=1;}}
  writeFileSync(path.join(results,'postgres-summary.json'),JSON.stringify(evidence,null,2)+'\n');
}
