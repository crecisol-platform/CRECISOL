# CRECISOL — C05 · Acta complementaria de certificación institucional de interfaces y modales

**Fecha de dictamen:** 16 de noviembre de 2026 (Honduras). **Estado:** **CERTIFICADA TÉCNICAMENTE la actualización UX de C05 en código, integración y pruebas locales automatizadas**, una vez verificados PREMERGE y POSTMERGE completos. La certificación integral histórica de C05 del 15-11-2026 permanece intacta: **5/37 capítulos certificados en sus alcances delimitados; C06 BLOQUEADO**. Esta adenda no habilita producción, cobros, VPS, Cloudflare, proveedores de correo externos, identidades reales ni despliegues.

## 1. Autoridad, trazabilidad e identidad

- Repositorio operativo autorizado: `crecisol-platform/CRECISOL` (ID 1450414592), **exclusivamente `dev`**. Fuente: `AGENTS.md`, `.agents/skills/crecisol-project-governance/SKILL.md`, `docs/MASTER_REFERENCE.md`, `docs/ESTADO_VIGENTE_CAPITULOS.md`, `docs/PLAN_37_CAPITULOS.md` y Documento Maestro Único.
- Documento Maestro Word (SHA-256 documentado y protegido): `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`. Copia PDF: `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Los guards de fuente canónica se ejecutaron en CI; no se editó ninguno de estos binarios.
- [PR técnico #166](https://github.com/crecisol-platform/CRECISOL/pull/166), HEAD PREMERGE `5cd97df5d3d120622e95c1a8e6aae70e35010e82`, rama `feature/c05-institutional-modals-20261116` sobre `dev` anterior `94c1c0a1531b8f2453461f7814abd17b55a9607f`.
- Integración exclusiva a `dev`: **squash `3d132ace38f07cf73aede518037f2bca6f70e80d`**. Se verificó que el árbol Git de HEAD técnico y del squash son idénticos: `aca770599fddbb045580959208fc917444322f28`. `main` siguió en `b794181148d2c5e36c1c88127f19a8a9082acff5`.
- Diff integrado: **25 archivos de frontend, QA y workflow institucional**. **Cero archivos de `backend/`, `database/`, migraciones, Documento Maestro, recursos productivos o SOLQARYN modificados.** No hay nueva autorización concedida desde Angular.

## 2. Cinco auditorías ejecutadas

| Auditoría | Alcance y resultados comprobados |
| --- | --- |
| **A — funcional/trazabilidad** | Conservados **18/18 formularios del operador y 15/15 de cuenta** con sus campos críticos; consola plana por **Casos, Sesiones, Perfil, Seguridad y Monitor**; navegación contextual y flujos sin selector de tenant. El QA verificó la fuente canónica y barreras entre operador y cuenta de negocio. |
| **B — seguridad, privacidad y negativas** | Modal nativo `showModal()`, fondo inerte, salida bloqueada durante `busy`, restauración de foco, limpieza de formularios y **seis mutaciones negativas** del modal rechazadas. La parte sensible de recuperación se muestra solo en su modal y se descarta al cerrarlo. E incluye rechazos por tenants, MFA, autorrevisión, revocación, tokens de uso único, races y permisos SQL. |
| **C — arquitectura y experiencia** | Angular 22 standalone/Signals, TypeScript y compilación de producción; pruebas Chromium, regresión C03 y contrato Angular → API .NET10 → MySQL 8.4. Estilos institucionales, navegación por tareas y ausencia de dependencias remotas nuevas. Separación `frontend/`, `backend/`, `database/` conservada. |
| **D — accesibilidad visual automatizada** | Cuatro pruebas Chromium adicionales: anchos de **320/768/1440 px**, foco contenido, Escape y retorno, campos cancelados, sin desbordamiento horizontal; preferencias `forced-colors` y `reduced-motion`; comprobación axe WCAG A/AA sin violaciones graves o críticas en esos recorridos. La regresión C02 POSTMERGE sumó **42 pruebas Chromium PASS** (19+8+10+5). |
| **E — integración operativa y regresiones** | **296 comprobaciones integradas PASS** en el SHA final de `dev`; Angular real, Kestrel .NET10, MySQL 8.4 descartable, receptor SMTP de loopback, MFA/respaldos, WebAuthn, QR, sesiones, perfiles, recuperación asistida, recuperación de operador con doble control, privacidad intertenant, cuotas y antifraude. **16 migraciones forward y 16 down**, final con cero tablas de esquema residual en el fixture. Job de regresiones históricas C01–C05: SUCCESS. |

**Límite metodológico:** las pruebas D/C incluyen axe y Chromium reales, pero **no constituyen una revisión estética humana píxel a píxel ni una sesión presencial con lector de pantalla real**. No se atribuye tal evidencia inexistente. El dictamen es técnico, automatizado y de entorno local aislado, no comercial ni productivo.

## 3. Evidencia PREMERGE (SHA `5cd97df5...`)

| Puerta | Ejecución GitHub Actions | Estado verificado |
| --- | --- | --- |
| C05 A/B/C/D + gate conjunto (5/5 jobs) | [43980822409](https://github.com/crecisol-platform/CRECISOL/actions/runs/43980822409) | completed/success |
| E operativa + regresiones (2/2 jobs, 296 comprobaciones) | [43980822541](https://github.com/crecisol-platform/CRECISOL/actions/runs/43980822541) | completed/success |
| C03 integral/regresiones | [43980822674](https://github.com/crecisol-platform/CRECISOL/actions/runs/43980822674) | completed/success |
| Fundamentos y fuente canónica | [43980822496](https://github.com/crecisol-platform/CRECISOL/actions/runs/43980822496) | completed/success |

## 4. Evidencia POSTMERGE (SHA `3d132ace38f07cf73aede518037f2bca6f70e80d`)

| Puerta | Ejecución GitHub Actions | Estado verificado |
| --- | --- | --- |
| C05 A/B/C/D + gate conjunto | [43982501175](https://github.com/crecisol-platform/CRECISOL/actions/runs/43982501175) | completed/success |
| E operativa + regresiones (296 comprobaciones) | [43982501163](https://github.com/crecisol-platform/CRECISOL/actions/runs/43982501163) | completed/success |
| Regresión C02-07 y 42 Chromium | [43982501058](https://github.com/crecisol-platform/CRECISOL/actions/runs/43982501058) | completed/success |
| C03 integral | [43982501091](https://github.com/crecisol-platform/CRECISOL/actions/runs/43982501091) | completed/success |
| Fundamentos | [43982501067](https://github.com/crecisol-platform/CRECISOL/actions/runs/43982501067) | completed/success |

Todas las ejecuciones anteriores se consultaron sobre los **SHAs exactos** indicados. Los `skipped` condicionales de otros workflows no se cuentan como pruebas aprobadas ni como cinco auditorías.

## 5. Incidencias corregidas y precedentes preservados

Los fallos anteriores de E se conservan como evidencia histórica y no se reinterpretan como PASS. Se corrigieron las transiciones del modal de aprobación de navegador a revocación; revisión de caso a entrega; cierre explícito de perfil/revisiones; confidencialidad de la parte de recuperación; y la duplicación de nombres accesibles entre abrir y confirmar la revocación general de sesiones. Se ajustaron las pruebas para exigir interacciones auténticas sin `force` ni desactivar controles.

[PR antiguo #156](https://github.com/crecisol-platform/CRECISOL/pull/156) fue **cerrado sin fusionar** por estar desfasado, recuperando su separación de tareas sobre el árbol nuevo en #166. [PR documental antiguo #121](https://github.com/crecisol-platform/CRECISOL/pull/121) fue **cerrado sin fusionar**; su propuesta de C05-02 no sobrescribe evidencias operativas posteriores ni autoriza correo productivo.

## 6. Dictamen, límites y reversión

**GO técnico para C05 UX institucional en `dev` y entorno local aislado**, con cinco auditorías positivas en su alcance, evidencia real del SHA final y sin P0/P1 evidenciados por estos gates. **C05 continúa 5/37**; esta adenda no inicia C06. No se modifica `main`, no se usa SOLQARYN, no se despliega ni se activan cobros, SMTP externo, Cloudflare, datos reales o servicios comprados. Los ensayos no demuestran ausencia absoluta de vulnerabilidades ni certificación de producción.

**Reversibilidad:** revertir exclusivamente el squash técnico `3d132ace38f07cf73aede518037f2bca6f70e80d` en una rama de corrección basada en `dev`, repetir A/B/C/D/E y comparar contrato de UI. No revertir ni volver a aplicar migraciones históricas: el rediseño no modificó el esquema. La presente acta y el registro de estado son documentación complementaria, no un nuevo Documento Maestro.
