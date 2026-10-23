# QA-06: revisión contractual

Desde la raíz, con .NET 8 y Node 20:

```sh
npm ci --prefix tests/contracts
npm test --prefix tests/contracts
dotnet build PEI.sln -m:1
dotnet test services/evaluaciones/tests/PEI.Evaluaciones.UnitTests --no-build
dotnet test services/identidad/tests/PEI.Identidad.UnitTests --no-build
dotnet test services/ia/tests/PEI.IA.UnitTests --no-build
npm run capture --prefix tests/contracts
npm run compare --prefix tests/contracts
```

`capture` inicia y detiene secuencialmente los cinco servicios en puertos 18520–18524, con conexiones deliberadamente inaccesibles, JWT efímero y plataforma deshabilitada. No llama al proveedor de IA. Requiere puertos libres. No prueba persistencia ni recorridos funcionales; captura Swagger de los binarios locales recién compilados.

`compare` valida los cinco Swagger y compara rutas, métodos, parámetros, bodies, respuestas y seguridad con los dos OpenAPI versionados. `results/semantic-review.json` contiene estructuras desreferenciadas (tipos, campos requeridos, formatos y restricciones), sin descripciones editoriales. Diferencias de representaciones equivalentes también aparecen: son insumo de revisión, no defectos automáticamente. No falla por diferencias conocidas; sí por JSON/OpenAPI inválido. Los resultados no se versionan automáticamente.

26 pruebas Node: dos OpenAPI, 17 ejemplos de esquema y siete caracterizaciones de permisividades conocidas. Estas últimas **no son aceptación funcional**. 12 pruebas xUnit nuevas verifican el validador real que carga el mismo JSON Schema embebido; el proyecto conserva además un test vacío preexistente.

Ver [revisión y matriz](../../docs/qa/QA06_REVISION_CONTRATOS.md). El workflow publica evidencia; no sustituye la aprobación del PO ni asegura compatibilidad completa.
