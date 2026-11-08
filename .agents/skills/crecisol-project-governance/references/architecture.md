# Arquitectura propia de CRECISOL

## Límite de proyecto
Repositorio exclusivo: solqaryn-platform/CRECISOL. La relación de marca con SOLQARYN no autoriza dependencias de repositorio, base de datos, credenciales, Skills ni servicios.

## Tres áreas
frontend/: Angular 22 standalone, Signals, rutas lazy, contrato OpenAPI tipado, design system único, performance medida y accesibilidad.
backend/: ASP.NET Core 10; Domain -> Application -> Infrastructure; API ensambla, workers por Outbox para tareas asíncronas y retries idempotentes. Pequeños casos de uso por intención.
database/: MySQL 8.4 LTS, InnoDB, normalización de entidades y claves por tenant, índices medidos, migraciones versionadas y pruebas de transacciones.

Node 24 LTS solo tooling Angular, salvo ADR posterior. Monolito modular inicial, no procesos de microservicios por cada módulo.

## Aislamiento dual del almacenamiento
- Shared mode: mismas tablas pero datos segregados por tenant_id, claves externas/únicas compuestas por tenant, repositorios obligatoriamente scope-aware y pruebas negativas.
- Dedicated mode: base exclusiva seleccionada por un catálogo seguro de ubicaciones propiedad de la plataforma; identidad y autorización siempre globalmente verificadas; el cliente no envía cadenas de conexión.
- Mismas APIs, modelos, casos de uso y migraciones compatibles en ambas modalidades. Plan de traslado con snapshot, consistencia, cutover y rollback por aprobar.

## Módulos
Identity, Tenants, Plans/Billing, Catalog, Orders, Scheduling, Memberships, Workflow/Timeline, Media, Reviews/Marketing, Analytics. Compartir contratos y eventos tipados, no escribir tablas ajenas entre módulos.

## Configuración y UI
Platform/Tenant/Branch/User con tipos declarados, herencia explícita, versionado, draft, preview, publish y rollback. Contenido comercial editable; políticas de seguridad críticas solo código revisado. Una semántica de iconos, tokens, cinco tamaños de modal y preview fiel para archivos habilitados.

## Estado
Es arquitectura planeada; sin compilación, pruebas de carga ni certificación aún.
