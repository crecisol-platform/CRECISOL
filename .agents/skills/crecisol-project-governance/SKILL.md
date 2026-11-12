---
name: crecisol-project-governance
description: "Gobernanza técnica exclusiva del repositorio CRECISOL. Aplicar antes de analizar, programar, documentar, probar, auditar o publicar cualquier cambio de CRECISOL: valida alcance, Documento Maestro Único, secuencia de 37 capítulos, aislamiento multitenant, seguridad, calidad, arquitectura frontend/backend/database y evidencia. Rechaza toda escritura a SOLQARYN."
---

# CRECISOL · Gobierno técnico propio

**Ubicación vigente verificada (11-11-2026):** `crecisol-platform/CRECISOL`, ID `1450414592`, rama `dev`. Las rutas antiguas en decisiones históricas no autorizan nuevas escrituras. Ver `docs/MIGRACION_ORGANIZACION_GITHUB_2026_11_11.md`. PR #79 fusionado; CI POSTMERGE en cola, por lo que C03 no está certificado efectivamente y C04 sigue bloqueado. Sin alteraciones a SOLQARYN ni producción.

## Precedencia posterior autorizada — C03-10, 11-11-2026

El propietario comunicó después de la decisión autónoma de esta misma fecha que CRECISOL pasó a público, habilitó Actions automáticos y autorizó intentar el cierre C03-10 usando CI. **Aplicar docs/DECISION_OPERATIVA_2026-11-11_RETORNO_CI_C03.md** como decisión prospectiva más reciente para este intento. El apartado 0 siguiente permanece como hecho histórico y no prohíbe el CI de C03-10. Se conservan pruebas reales, auditorías A/B/C, ausencia de P0/P1, gate SHA exacto y prohibición de SOLQARYN/producción. CI verde no certifica por sí sola.

## 0. Decisión del propietario — 11-11-2026 — sin GitHub Actions como gate
- **Leer y aplicar** `docs/DECISION_OPERATIVA_2026-11-11_AUTONOMIA_SIN_GITHUB_ACTIONS.md` antes de programar, auditar o certificar. El propietario autorizó desarrollo, revisión, aprobación técnica y certificación autónomos **sin PC del propietario**.
- GitHub queda como alojamiento y versionamiento del código. **GitHub Actions, CI PREMERGE/POSTMERGE, PR, runner de GitHub y su estado verde NO son requisitos de avance ni certificación** desde esta fecha. No activar CI manualmente, ni contratar runners, ni buscar cuentas alternativas para disponer de minutos. El usuario no quiere depender de GitHub para ejecutar pruebas.
- Las auditorías A/B/C y **las pruebas reales aplicables siguen siendo obligatorias**; si un runtime esencial no está disponible aquí, la prueba se marca `NO EJECUTADA` y la certificación queda pendiente, sin engaños. No atribuir al asistente las ejecuciones históricas reportadas por Codex.
- El propietario delegó la aceptación de microfases cuando la evidencia A/B/C sea suficiente; ninguna autorización habilita P0/P1, salto entre capítulos, producción, cobros ni SOLQARYN. La delegación no crea trabajo asíncrono.
- Esta decisión prospectiva desplaza **solo** los gates previos de CI y firma humana por microfase; mantiene intactas las certificaciones históricas y todas las barreras de seguridad/tenancy.


## 1. Identidad y bloqueo de alcance
- Proyecto: CRECISOL. Repositorio autorizado: crecisol-platform/CRECISOL. Nunca presumir equivalencia con solqaryn/Solqaryn.
- SOLQARYN: referencia institucional y, si se autoriza, únicamente consulta. Está terminantemente prohibido editar, crear PR, comentar, configurar automatizaciones o ejecutar comandos de escritura en SOLQARYN.
- Este archivo fue redactado exclusivamente para CRECISOL, no es una copia de la Skill de SOLQARYN.
- Alcance de esta etapa: código, pruebas y documentación locales o en el repositorio CRECISOL; no VPS, DNS, despliegues, datos productivos ni cobros reales.
- Fuente funcional: Documento Maestro Único de CRECISOL; consultar docs/MASTER_REFERENCE.md. Si su versión aprobada no está disponible, NO_GO para certificar capítulos. La ausencia de archivo no se interpreta como aprobación.
- Antes de escribir, comprobar repositorio, rama, directorio, estado del diff y autorización del alcance; si no coincide, detener.

## 2. Arquitectura y dependencias
- Código segregado: frontend/ (Angular 22, Node 24 para herramientas), backend/ (ASP.NET Core 10, Clean Architecture y monolito modular), database/ (MySQL 8.4 LTS normalizado).
- No crear microservicios distribuidos sin ADR aprobado y justificación medible; casos de uso y controladores pequeños.
- Domain no depende de frameworks; Application usa puertos; Infrastructure contiene adaptadores; API traduce HTTP; workers idempotentes.
- No incrustar en componentes los valores comerciales editables: identidad y dominio, copyright de plataforma y tenant, textos, formularios, iconos, temas y preferencias se resuelven desde configuración versionada por ámbito.
- Una misma implementación para rubros distintos, sin replicar sistemas por sector. Mantener accesibilidad, diseño editorial y vistas previas seguras.

## 3. Seguridad P0 transversal
- Denegación por defecto y mínimo privilegio. Ningún tenant puede enumerar, consultar, editar, exportar, cachear ni restaurar información de otro, incluida su bitácora.
- Backend establece TenantContext desde host verificado más identidad y pertenencia autorizada. La URL o tenant_id del navegador nunca conceden permisos. Inicio de sesión normal sin selector de empresa; ante ambigüedad, acceso contextual seguro o denegación.
- Dos modos de persistencia compatibles por contratos: MySQL compartido con claves/índices/FK por tenant; DB dedicada cuando se apruebe, con router del lado servidor. Prohibido seleccionar DSN desde parámetros del cliente.
- SuperAdmin administra todos los módulos desde consola propia, con MFA, reautenticación, motivo, acceso excepcional temporal y auditoría para datos privados.
- Pruebas negativas obligatorias de BOLA/IDOR, host manipulado, claims falsos, sucursales, archivos/preview, colas, cachés, eventos, backups y logs.
- Perfiles, invitaciones, verificación, passkeys/MFA, recuperación y sesiones seguras; contraseñas con hash resistente; sin datos sensibles en logs ni secretos versionados.
- Seguimiento universal: cronología inmutable de citas/pedidos/servicios, observaciones y adjuntos aislados; decisiones del cliente ligadas a versión exacta; cierre verificable. No ejecutar trabajos rechazados ni sobrescribir historia.
- Consultar references/security.md para controles y ejemplos negativos.

## 4. Ciclo capítulo por capítulo
- El programa tiene 37 capítulos y el avance es secuencial. Solo abrir código del capítulo N si N-1 tiene acta certificada y aceptada, salvo bases técnicas estrictamente necesarias declaradas como infraestructura de N.
- Cada cambio: contexto -> análisis de impacto -> microtarea -> código -> pruebas positivas y negativas -> auditoría A (trazabilidad), B (seguridad), C (arquitectura/rendimiento/UI) -> acta -> aprobación.
- Estado DOCUMENTADO, IMPLEMENTADO, PROBADO y CERTIFICADO nunca se confunden. No declarar capítulo completo por redactar archivos o pasar validación estática.
- La ausencia de herramientas reales de Angular, .NET o MySQL implica BLOQUEADO para certificación, no prueba simulada.
- Las excepciones P0/P1 son NO_GO. Registrar mitigación y prueba posterior; nada se cierra por intuición.
- Consultar references/chapter-gates.md y el plan de 37 capítulos en docs/PLAN_37_CAPITULOS.md.

## 5. Ramas y seguridad GitHub
- main: rama de integración certificada, sin pushes directos una vez activadas protecciones.
- dev: rama de integración de desarrollo. Cada capítulo usa feature/cNN-descripcion. PR opcional; integración controlada por SHA/diff y auditorías reales, sin gate de Actions.
- Antes de fusionar: diff, secretos, dependencias, permisos, tenancy, tests, compatibilidad, performance y documentación.
- Todo workflow debe usar token de solo lectura salvo justificación, sin secretos de producción y sin despliegues automáticos.
- No afirmar que main está protegida hasta leer reglas aplicadas realmente en GitHub.
- La documentación formal de decisiones está en docs/DECISIONES_ABIERTAS.md.

## 6. Contrato de entrega
- Explicar archivos modificados, impacto en frontend/backend/database, contratos API/DTO, auth, datos y migraciones, pruebas ejecutadas con salida real, limitaciones, riesgos y rollback.
- Nunca falsificar CI ni atribuir pruebas a un SaaS no implementado.
- Si existen instrucciones contradictorias, detener solo el cambio inseguro y registrar el conflicto para decisión expresa.

## 7. Referencias externas condicionadas
- Leer docs/REFERENCIAS_EXTERNAS_SKILLS.md cuando una microtarea precise guía original de UI, motion, Angular, seguridad, documentación o rendimiento.
- Mantener UNA sola Skill local de CRECISOL. No copiar ni instalar las doce fuentes externas; consultar original + SHA + ruta, verificar al momento de uso, registrar en PR y aplicar solo los principios compatibles con este proyecto.
- No atribuir como consultada o ejecutada una referencia solo porque figure en el registro. Ante pin no verificable, omitir referencia y continuar con gobierno local si es seguro.
