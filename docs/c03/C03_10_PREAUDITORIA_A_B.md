# CRECISOL — C03-10 | Preauditoría independiente A/B de fuentes

**Fecha:** 11-11-2026. **Dictamen:** A y B **conformes únicamente en la muestra de controles estáticos examinada**, no aprobadas aún como auditorías integrales. C03-10 y C03 **NO CERTIFICADOS**, C04 bloqueado. No es CI ni prueba .NET/MySQL.

**Repositorio exclusivo:** `solqaryn-platform/CRECISOL`; rama `feature/c03-10-auditoria-integral-autonoma`; **SHA examinado:** `99e5a912e5330ba251c8fbb75a751cc4f0371dd6`; `dev` base `6b64271c53ed1c6ccccb81291b5794d897103cd3`.

## 1. Procedencia y técnica de prueba

El asistente consultó **19 archivos del repositorio privado directamente en GitHub** y el árbol de Git, ejecutó mediante JavaScript **19 aserciones estáticas (19/19)** y **9 mutaciones negativas exclusivamente textuales (9/9 detectadas)**, sobre cadenas en memoria. No compiló C#, Angular ni SQL; las mutaciones prueban la sensibilidad de los predicados, no una propiedad funcional en runtime.

Adicionalmente, el entorno del asistente dispone de las cuatro copias de Word/PDF oficiales. En ellas **sí ejecutó** SHA-256, verificación de integridad ZIP, parseo XML, C14N histórico y extracción del logotipo: **8/8 controles independientes de contenido Word comercial conformes**; la verificación PDF se limitó a sus bytes/SHA, no inspección visual. Estos archivos no se descargaron de GitHub en esta ejecución; se contrastó por separado el **Git blob SHA** de la rama para Word y PDF.

## 2. Auditoría A — 9/9 aserciones de fuente

| ID | Comprobación | Estado |
| --- | --- | --- |
| A-01 | Blobs Git de Word y PDF comerciales vigentes | CONFORME |
| A-02 | Guard maestro: 37 capítulos, huella histórica y edición vigente | CONFORME por lectura de fuente |
| A-03 | Estado documental C03-09 certificada, C03-10/C03 no certificadas | CONFORME |
| A-04 | Actas contiguas C03-01..09 presentes en árbol | CONFORME por existencia |
| A-05 | `Crecisol.Domain.csproj` sin referencias externas | CONFORME |
| A-06 | `Crecisol.Application.csproj` refiere solo Domain | CONFORME |
| A-07 | Infrastructure referencia Application, MySqlConnector en adaptador | CONFORME |
| A-08 | API referencia Application+Infrastructure; cuatro proyectos net10 | CONFORME |
| A-09 | Angular usa rutas públicas con `loadComponent` y fallback | CONFORME por lectura |

Los Git blobs verificados de Word/PDF: `fe7aaab60b603f1844e18666e2f99c0ea97e86e9` y `93c8412b2450e85a882b622e7a012477c61c2359`.

## 3. Auditoría B — 10/10 aserciones y 9/9 negativas textuales

| ID | Comprobación | Estado |
| --- | --- | --- |
| B-01 | API rechaza forwarded Host engañoso | CONFORME por texto |
| B-02 | DSN proviene del servidor, no de la petición | CONFORME por texto |
| B-03 | DTO de petición rechaza Authorization, X-Tenant-Id y spoofing de cuerpo | CONFORME por texto |
| B-04 | LocalRuntimeSettings prohíbe red externa y restringe hosts locales | CONFORME por texto |
| B-05 | Outbox tiene PK/FK compuestas por scope y restricciones SQL | CONFORME por texto |
| B-06 | Smoke MySQL emplea digest y `--network none` | CONFORME por texto |
| B-07 | Probe DEV exige `VERIFY_IDENTITY`, uso explícito y consulta fija | CONFORME por texto |
| B-08 | Eventos de outbox limitados a PLATFORM, adaptador no registrado en API | CONFORME por texto |
| B-09 | Word canónico y SHA del logo aprobado preservados | CONFORME por texto |
| B-10 | No se identifican asignaciones literales obvias de secreto **en la muestra examinada** | CONFORME con alcance limitado |

Las mutaciones en memoria y **detectadas**: dependencia externa agregada a Domain; Infrastructure introducida en Application; supresión de guard Authorization; habilitar red externa; pérdida de FK compuesta de tenant; `--network bridge` en Docker; TLS cambiado a DISABLED; hash Word degradado al histórico; ScopeId PLATFORM cambiado a identificador variable. **Son ensayos de aserciones textuales, no pentests ni pruebas de aislamiento real.**

## 4. Documento Maestro — comprobaciones reales adicionales

| Elemento | Valor comprobado |
| --- | --- |
| Word histórico SHA-256 | `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e` |
| PDF histórico SHA-256 | `17ffe48aab3b224cc9b11927c498e4f49c162d96bf639c894b84259f597738dd` |
| Word comercial SHA-256 | `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c` |
| PDF comercial SHA-256 | `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77` |

Word comercial: **1959** elementos del cuerpo, **1687** párrafos y **189** tablas originales, huella C14N `6238a81010d8bfd035eda77fa33897a009f4b363d4e281723c723b6b60c807d5`, **37** títulos, **14** requerimientos COM, **18** pruebas COM-T, logotipo SHA-256 `9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f`. **8/8 comprobaciones ejecutadas conformes.**

## 5. Hallazgos y límites

1. **Proceso P2:** algunos auditores C de microfases históricas requieren archivos `.c03-*-ci-proof`; no se reutilizarán ni falsificarán esos marcadores para simular resultados nuevos. C03-10 necesita evidencia C independiente, sin dependencia de Actions.
2. Varios textos históricos contienen estados antiguos, incluidos `C03-09 NO INICIADA`. La actualización vigente del 11-11-2026 se documentó al comienzo de los documentos principales; no reescribir ni invalidar actas previas.
3. **Pendientes de ejecución real sobre checkout:** los scripts A/B originales, compilaciones .NET10/Angular22, contratos públicos/Outbox, integración MySQL8.4, negativos efectivos, pruebas HTTP, cobertura Chromium si es aplicable y auditoría integral C. El entorno del asistente posee Python y Node 22, pero no checkout autenticado completo ni .NET 10, Node 24 o Docker. No se declaran PASS ficticios.
4. P0/P1: **ninguno demostrado en la muestra estática**, lo cual no prueba su ausencia en el runtime o en el repositorio completo.

**Estados firmes:** `A_SOURCE = 9/9 STATIC`, `B_SOURCE = 10/10 STATIC + 9/9 TEXT MUTATIONS`, `WORD = 8/8 REAL DOCUMENT CHECKS`, `A_INTEGRAL/B_INTEGRAL = PENDIENTES`, `C_RUNTIME = NO EJECUTADA`, `C03-10 = NO CERTIFICADA`, `C03 = NO CERTIFICADO`, `C04 = BLOQUEADO`.
