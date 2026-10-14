# Infraestructura Local con Docker - PEI / TIZA

Este directorio contiene la orquestación de bases de datos PostgreSQL y Redis para el desarrollo local de los microservicios de PEI.

## Requisitos
* Docker Desktop instalado y en ejecución (con WSL2 en Windows).

---

## 🚀 Cómo usar este entorno

### 1. Levantar solo la base de datos de tu microservicio (Recomendado para programar)
Para ahorrar memoria y trabajar enfocado en tu microservicio, levantá solo tu base correspondiente:

```bash
# Si trabajás en Identidad:
docker compose up -d postgres-identidad

# Si trabajás en Cursos:
docker compose up -d postgres-cursos

# Si trabajás en Evaluaciones:
docker compose up -d postgres-evaluaciones

# Si trabajás en Sala en Vivo:
docker compose up -d postgres-sala redis

# Si trabajás en IA:
docker compose up -d postgres-ia
```

### 2. Levantar todo el entorno (para QA / Demos / Pruebas integradas)
```bash
docker compose up -d
```

### 3. Ver qué contenedores están corriendo
```bash
docker compose ps
```

### 4. Apagar los contenedores (sin perder datos)
```bash
# Apagar un contenedor individual:
docker compose stop postgres-identidad

# Apagar todos los contenedores:
docker compose down
```

### 5. Reiniciar y borrar todas las bases (¡Cuidado: borra los datos!)
```bash
docker compose down -v
```

---

## 📌 Puertos y Cadenas de Conexión para .NET (`appsettings.Development.json`)

| Servicio | Contenedor | Puerto Local | Cadena de Conexión |
| :--- | :--- | :--- | :--- |
| **Identidad** | `pei-db-identidad` | `5441` | `Host=localhost;Port=5441;Database=pei_identidad_db;Username=postgres;Password=postgres` |
| **Cursos** | `pei-db-cursos` | `5442` | `Host=localhost;Port=5442;Database=pei_cursos_db;Username=postgres;Password=postgres` |
| **Evaluaciones** | `pei-db-evaluaciones` | `5443` | `Host=localhost;Port=5443;Database=pei_evaluaciones_db;Username=postgres;Password=postgres` |
| **Sala en Vivo** | `pei-db-sala` | `5444` | `Host=localhost;Port=5444;Database=pei_sala_db;Username=postgres;Password=postgres` |
| **IA** | `pei-db-ia` | `5445` | `Host=localhost;Port=5445;Database=pei_ia_db;Username=postgres;Password=postgres` |
| **Redis** | `pei-redis` | `6379` | `localhost:6379` |
