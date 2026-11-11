# CRECISOL — C03-10 | Auditorías integrales A/B/C y acta del capítulo C03

**Estado actual:** **INICIADA (PREPARACIÓN Y MATRIZ), NO CERTIFICADA**. **C03-01..09 certificadas en su alcance**, incluida [C03-09 (9/10)](C03_09_CERTIFICACION_AUTONOMA.md). El capítulo **C03 NO ESTÁ CERTIFICADO**, y C04 permanece **BLOQUEADO**.

**Repositorio:** `solqaryn-platform/CRECISOL`; **rama activa:** `feature/c03-10-auditoria-integral-autonoma`. **Base exacta de inicio `dev`:** `6b64271c53ed1c6ccccb81291b5794d897103cd3`. No cambiar `main` ni `solqaryn/Solqaryn`.

**Autoridad:** Documento Maestro Único de CRECISOL y sus capítulos, [referencia maestra](../MASTER_REFERENCE.md), [plan 37 capítulos](../PLAN_37_CAPITULOS.md), [plan de microfases C03](../planificacion-futura/C03_MICROFASES_PROPUESTAS.md), Skill propia y [decisión autónoma sin Actions](../DECISION_OPERATIVA_2026-11-11_AUTONOMIA_SIN_GITHUB_ACTIONS.md).

## 1. Objetivo exacto y alcance negativo

Cerrar **las diez microfases del capítulo 03 como capítulo**, mediante tres auditorías **independientes**, regresión cruzada, evidencias de ejecución real, análisis de impactos y acta final. No equivale a repetir o volver a certificar automáticamente lo previamente aprobado.

C03 abarca la base arquitectónica: grafo de módulos y dependencias (01), Domain sin frameworks (02), Application y puertos (03), Infrastructure/adaptadores (04), API/DTO pública (05), módulos y DI (06), outbox/eventos sintéticos (07), configuración local segura (08) y compatibilidad/benchmark real (09). C03-10 certifica solo la coherencia integral y regresiones de estas bases.

**Quedan expresamente fuera:** autenticación privada y MFA completos (capítulos posteriores), rutas multitenant productivas, pagos, workers productivos, clientes reales, VPS/DNS, despliegues, compras o acceso a producción. No crear APIs o funcionalidades fuera de C03 para aparentar cobertura.

## 2. Estrategia sin dependencia de GitHub Actions

GitHub sirve solo para alojar/versionar código, verificar SHA, historial y diffs. **No usar Actions como gate**, no habilitar workflows ni contratar runners.

- **Responsable:** el asistente desarrolla, revisa código y documentación, ejecuta todas las pruebas disponibles en su entorno y dirige las auditorías.
- **Apoyo Codex:** solamente para las pruebas que el entorno del asistente **no pueda ejecutar** realmente (compilaciones .NET 10, Angular 22, MySQL 8.4, Chromium u otras). El usuario asigna el encargo; Codex entrega salida verificable y no decide certificación.
- **Trazabilidad:** distinguir observación directa, pruebas históricas verificadas por Codex y nuevas ejecuciones. Una prueba local histórica puede conservarse cuando no cambió su código; la regresión integral del capítulo necesita evidencia propia actual para las funciones afectadas.
- **Seguridad:** no entregar ni registrar secretos, DSN, `.env`, credenciales, datos de clientes; MySQL para pruebas en contenedor efímero `--network none`; no usar `crecisol_dev` remoto.

## 3. Auditoría A — autoridad, cobertura y dependencias

**Positivos obligatorios:**
1. Validar el Documento Maestro Único **vigente** y la integridad de Word/PDF, logotipo aprobado y prefijo histórico; comparar SHAs contra `docs/MASTER_REFERENCE.md`.
2. Inventariar **C03-01..09** con requisitos, rutas de código, casos de prueba, tres auditorías y actas precedentes; impedir huecos, actas duplicadas o certificaciones extrapoladas.
3. Verificar los límites de los proyectos `Domain → Application → Infrastructure → API`, grafo acíclico, Angular separado, módulos y dependencias explícitas de DI, sin service locator.
4. Contrastar la matriz de ocho MAE-C03 normativas versus diez microfases técnicas, sin agregar un capítulo 38.
5. Comprobar `dev` y feature por SHA, alcance, dependencias, reversibilidad, documentación y registros históricos inmutables.

**Negativos:** circularidades, saltos de capítulo, dependencias de infraestructura dentro de Domain, cambio no aprobado del logo o Maestro, módulos no registrados y evidencia asociada a SHA incorrecto producen **NO GO**.

**Fuente de pruebas estáticas ya existente:** `qa/c03_01_source.py`, `qa/c03_local_preflight.py`, `qa/c03_02_domain_audits.py`, `qa/c03_03_application_audits.py`, `qa/c03_04_infrastructure_audits.py`, `qa/c03_05_api_audits.py`, `qa/c03_06_module_audits.py`, `qa/master_source_guard.py`. Los comandos concretos se verifican contra la interfaz de cada script; **no se declara ejecución por solo citarlos**.

## 4. Auditoría B — seguridad transversal adversarial

**Cobertura obligatoria:** negación por defecto, contratos públicos/DTO, límites de confianza y de rutas, fuente de configuración, redirecciones, secretos en logs/artefactos, aislamiento de contenedores, inputs inválidos, errores fail-closed y controles negativos de los puertos/adaptadores, módulos/DI, outbox idempotente y el probe DEV de solo lectura.

Regresar los casos negativos documentados C03-02..09, con énfasis en:
- DTO rechazando serialización de secretos y entidades internas.
- Configuración y DI no resolubles desde headers/host/tenant de un cliente.
- Outbox local rechazando reintentos duplicados y operaciones cruzadas en contratos sintéticos.
- Contenedor MySQL de prueba sin red; ningún acceso ni escritura a datos reales.
- Validación de la edición actual del Word y logotipo oficial; negativas de documento adulterado.
- Comandos filtrados, subprocess con timeout, sin ejecución remota ni exposición de `.env`.
- Verificar que no se añadió worker operativo ni endpoint privado fuera de capítulo.

**Límite declarado:** C03 puede probar **contratos sintéticos** de tenant, pero no afirmar BOLA/IDOR de endpoints privados **no implementados**. Esos controles siguen pendientes en sus capítulos funcionales.

**Resultado:** todas las negativas aplicables `PASS`, sin P0/P1 ni fuga de secretos. Una prueba ausente se marca `NO EJECUTADA` y bloquea certificación si es esencial para el alcance.

## 5. Auditoría C — arquitectura real, compilación, regresión y rendimiento

Plan mínimo de ejecución, adaptado a herramientas y rutas verificadas en `dev`:

1. Confirmar Node 24.15+, Angular 22, SDK .NET 10.x, MySQL 8.4 **por digest** y runtimes realmente disponibles; sin simular versiones.
2. Compilar **API .NET 10 Release** y **Angular 22 producción**, realizar compilaciones de contratos Domain, Application, Infrastructure, API, Module, LocalConfig y Outbox relevantes; ejecutar los ejecutables de contratos y sus casos negativos.
3. Ejecutar pruebas de integración MySQL 8.4 local desechable para API pública y Outbox, `--network none`, limpieza y reporte de exit codes; no conexión a base DEV remota.
4. Regresar los contratos y pruebas funcionales públicos de C01/C02 que se vean afectados; ejecutar Chromium/navegador **si existe y aplica** a los flujos cubiertos. No afirmar accesibilidad manual WCAG ni pruebas privadas si no se ejecutan.
5. Validar de nuevo las regresiones C03-07/08/09 necesarias, pero **no repetir tres veces los nueve builds del benchmark C03-09** salvo cambio de código o anomalía de performance; conservar los valores históricos como baseline de otra sesión y ambiente.
6. Medir tiempos y registrar recursos/restricciones del entorno sin confundir build, smoke, UX o latencia SQL.

**Criterio:** salida real de herramientas (comando, SHA, exit code, duración, resumen, errores) y limpieza del árbol. Si no hay SDK .NET/Docker/browser aquí, **asignar solo ese conjunto a Codex**; no inventar `PASS` ni exigir tu computadora.

## 6. Matriz de evidencia, auditorías independientes y resolución

| Control | Registro obligatorio | Situación al abrir C03-10 |
| --- | --- | --- |
| Fuente maestra, mapa 01..09 y diff | Auditoría A, SHA y paths | PENDIENTE de ejecución integral |
| Negativos y amenazas intermodulares | Auditoría B, escenarios/exit codes | PENDIENTE de ejecución integral |
| Compilación y pruebas de regresión | Auditoría C, runtime real | PENDIENTE |
| MySQL 8.4 local, Outbox, limpieza | Auditoría C, manifiesto y logs | PENDIENTE |
| Integridad/cobertura de evidencias | Manifiesto con SHA-256 y prueba de no falsificación | PENDIENTE |
| Hallazgos P0/P1 y remediación | Lista, commit de corrección, rerun de controles afectados | PENDIENTE |
| Acta integral del capítulo C03 | Dictamen firmado técnicamente por delegación | **NO EMITIDA** |

Cada auditoría debe hacerse de forma **separada**, detectar y registrar hallazgos y cerrarlos antes de dictamen. La revisión documental o un resultado histórico de una microfase nunca se presenta como prueba integral nueva. Las auditorías A/B/C del capítulo no se sustituyen mutuamente.

## 7. Puerta de salida y efectos

Solo si A, B y C integrales son satisfactorias, hay evidencia suficiente de los controles reales, sin P0/P1 y con SHAs/rollback reproducibles, el asistente puede **certificar C03-10 y el capítulo C03** bajo la delegación del propietario; después integrar a `dev` por SHA controlado, sin dependencia de Actions.

De existir un bloqueo esencial de pruebas, registrar `C03-10 EN PRUEBAS / NO CERTIFICADA`; no abrir C04. Una vez certificada y alojada la documentación final en `dev`, **C04: multitenancy, dominios y aprovisionamiento** será el próximo capítulo, pero no antes.

**Estado final de este documento:** plan/especificación de inicio; **no constituye ejecución de pruebas ni acta de cierre**.
