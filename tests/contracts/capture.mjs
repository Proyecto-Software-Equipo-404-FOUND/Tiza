// Captura reproducible sin tocar bases reales ni invocar proveedores de IA.
import { spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const root = fileURLToPath(new URL('../../', import.meta.url));
const output = path.join(root, 'tests/contracts/results');
mkdirSync(output, { recursive: true });
const key = randomBytes(48).toString('hex');
const connection = 'Host=127.0.0.1;Port=1;Database=qa06_inaccessible;Username=qa06;Password=unused;Timeout=1';
const names = { cursos: 'Cursos', identidad: 'Identidad', ia: 'IA', evaluaciones: 'Evaluaciones', 'sala-en-vivo': 'SalaEnVivo' };
for (const [index, [service, name]] of Object.entries(names).entries()) {
  const project = path.join(root, `services/${service}/src/PEI.${name}.Api`);
  const port = 18520 + index;
  const child = spawn('dotnet', [path.join(project, `bin/Debug/net8.0/PEI.${name}.Api.dll`)], {
    cwd: project, windowsHide: true, stdio: 'ignore', env: { ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
      Jwt__SecretKey: key, Jwt__Issuer: 'qa06-local', Jwt__Audience: 'qa06-local', Platform__Enabled: 'false',
      ConnectionStrings__CursosDb: connection, ConnectionStrings__IdentidadDb: connection,
      ConnectionStrings__Default: connection, ConnectionStrings__DefaultConnection: connection }
  });
  try {
    let document;
    for (let attempt = 0; attempt < 90; attempt++) {
      if (child.exitCode !== null) throw new Error(`${service}: el proceso terminó con ${child.exitCode}`);
      try { const response = await fetch(`http://127.0.0.1:${port}/swagger/v1/swagger.json`, { signal: AbortSignal.timeout(2000) });
        if (response.ok) { document = await response.json(); break; }
      } catch { /* esperar arranque */ }
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    if (!document) throw new Error(`${service}: Swagger no disponible`);
    writeFileSync(path.join(output, `${service}.json`), JSON.stringify(document, null, 2) + '\n');
    console.log(`${service}: Swagger capturado (${Object.keys(document.paths).length} rutas)`);
  } finally { child.kill(); await new Promise(resolve => child.exitCode !== null ? resolve() : child.once('exit', resolve)); }
}
