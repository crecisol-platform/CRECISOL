# CRECISOL — Registro técnico de migración de organización GitHub

**Fecha de verificación:** 11 de noviembre de 2026, Honduras.  
**Ámbito:** solo identidad, permisos de lectura/escritura verificados y referencias operativas del repositorio CRECISOL. **No es acta de certificación del capítulo C03 ni autorización de C04.**

## 1. Identidad y alcance inmutables

| Concepto | Valor comprobado |
| --- | --- |
| Repositorio original (identidad histórica) | `solqaryn-platform/CRECISOL` |
| Repositorio actual autorizado | `crecisol-platform/CRECISOL` |
| ID GitHub conservado | `1450414592` |
| Rama de integración | `dev`, SHA base `833d82d6b0293bf556e4d493685afdbc1fac7e0e` |
| Rama de este ajuste | `feature/docs-crecisol-org-migration-20261111` |
| Rama `main` | Sin cambios por este trabajo |
| Visibilidad comprobada | Pública, según GitHub API; no equivale a autorización para publicar secretos |
| Pull request documental de capítulo | PR #79, fusionado a `dev` en el SHA base indicado |
| Estado de CI posterior a #79 | Run `43097016405` en `queued`, conclusión nula, sin pruebas acreditables |
| Soporte | El propietario presentó un ticket a GitHub Support; número aún no verificado |

La transferencia **ya se efectuó** y preservó el ID original. Las URL y el nombre del propietario previo que constan en PR, actas, notas y ejecuciones antiguas son **evidencia histórica**; no se utilizan para crear nuevas ramas, commits o integraciones. Se prohíbe cualquier escritura en `solqaryn/Solqaryn`.

## 2. Archivos y efectos

Se actualizaron exclusivamente `AGENTS.md`, `.agents/skills/crecisol-project-governance/SKILL.md`, `README.md`, `docs/ESTADO_VIGENTE_CAPITULOS.md`, `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md` y `docs/PROTOCOLO_CONTINUIDAD_CRECISOL.md`, más este registro.

- El repositorio autorizado es `crecisol-platform/CRECISOL`; `dev` sigue siendo la rama de integración.
- Se preservaron intencionalmente PR, SHA, URL y estados históricos sin reescribir sus afirmaciones originales.
- En el primer corte no se editaron `frontend/`, `backend/`, `database/`, `qa/` ni `.github/workflows/`. La corrección posterior de `qa/c03_chapter_certification.py` se describe en la sección 8; frontend/backend/database/workflows permanecen intactos.
- Los blobs Git de `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` (`fe7aaab60b603f1844e18666e2f99c0ea97e86e9`) y del PDF (`93c8412b2450e85a882b622e7a012477c61c2359`) coincidieron entre el SHA base y la rama de corrección. El Word/PDF y sus huellas documentadas no se alteraron.

## 3. Auditoría A — trazabilidad e integridad documental

**Dictamen: PASS DOCUMENTAL en el alcance de esta migración.**

La consulta GitHub API confirmó owner, ID, rama, HEAD, PR #79 fusionado, comparación de commits y blobs de ambos maestros. Antes de añadir este registro, el diff contenía **exactamente siete archivos Markdown**, todos previstos, con siete commits sobre el SHA base y cero cambios de código. El plan mantiene sus 37 capítulos y la jerarquía del Documento Maestro Único. Las declaraciones históricas permanecen intactas salvo la corrección del propietario **activo**.

**Límite:** no se ha validado la certificación de C03 con el nuevo SHA mediante CI.

## 4. Auditoría B — seguridad, perímetro y aislamiento

**Dictamen: PASS ESTÁTICO del alcance documental; SIN pentest del producto.**

Se verificó por lectura del contenido actual la prohibición de escrituras en SOLQARYN, los paths operativos autorizados, la Skill local, la ausencia de cambios en contratos/backend, configuración productiva y fuentes de datos, y las exclusiones `.env`, `node_modules/`, `bin/`, `obj/` y `backups/` de `.gitignore`. No se introdujeron secretos, claves ni endpoints productivos en los textos agregados. El propietario mostró una confirmación de 2FA en GitHub durante la preparación; **no se ha auditado todavía la exigencia de 2FA de toda la organización**.

**Riesgos pendientes:** segundo Owner de confianza con 2FA; verificar acceso de miembros temporales y de conectores (Codex, Antigravity, ChatGPT) antes de retirar `solqaryn-platform`; revisar los permisos efectivos del GitHub App a nivel organización. No aplicar cambios a cuentas sin verificación previa.

## 5. Auditoría C — compatibilidad de contratos, arquitectura y ejecución

**Dictamen: PASS de comprobaciones estructurales por API; pruebas de runtime y CI NO EJECUTADAS.**

Se evaluaron **18/18 aserciones estáticas** contra archivos obtenidos de la rama de corrección: propietario activo único en AGENTS/Skill/registro, prohibición SOLQARYN, header válido de Skill, condición CI de C03, 37 capítulos y límite C04, invariancia de los hashes maestros documentados, marcadores históricos de acta, paths no permitidos, exclusiones de secretos y enlaces al presente registro. Por comparación de commits y árboles Git se comprobó que no cambió código ni Word/PDF.

**No se ejecutaron** `qa/validate_foundation.py`, builds Angular/.NET, pruebas MySQL/Playwright, auditorías C dependientes de runner ni una prueba E2E: las ejecuciones GitHub Actions permanecen bloqueadas; el entorno de ejecución alternativo no pudo clonar el repositorio por falta de resolución DNS. Ninguna comprobación estática sustituye esas pruebas.

## 6. Estado de certificación y condiciones restantes

- **Migración de propiedad Git: VERIFICADA en identidad/historial/ubicación.**
- **Actualización documental: preparada en rama, pendiente de PR, revisión e integración controlada.**
- **C03: CERTIFICACIÓN PENDIENTE.** El acta `docs/c03/C03_CERTIFICACION_INTEGRAL_2026_11_11.md` exige evidencia CI PREMERGE/POSTMERGE del SHA correcto. Run `43097016405` continúa `queued`; nunca interpretar falta de resultado como SUCCESS.
- **C04-01: BLOQUEADA.** Sin código, pruebas ni avance a capítulos posteriores.
- **Infraestructura de pruebas: NO CERTIFICADA.** Solo al restablecerse el servicio, ejecutar una corrida controlada y confirmar los gates exigidos por la política vigente.
- **Sin producción:** no se despliega Cloudflare, VPS, DNS, cobros ni infraestructura externa.

## 7. Riesgos residuales, rollback y siguiente acción

1. Obtener resolución verificable de GitHub Support y ejecutar las verificaciones técnicas que el gate de C03 exige, sin reinicios masivos ni credenciales de producción.
2. Revisar e integrar este cambio documental solo cuando el PR tenga diff revisado, SHA identificado y verificaciones aplicables. La rama de características no cambia por sí sola el estado operativo de `dev`.
3. Comprobar miembros, un segundo Owner con 2FA y apps; retirar el miembro temporal solo cuando las integraciones y la recuperación administrativa estén cubiertas.
4. Rollback: no fusionar el PR, o revertir el merge documental si se detecta una regresión. Preservar actas históricas, blobs maestros, branches y evidencia de la transferencia.

**Cierre de este registro:** control documental delimitado; **sin certificación integral del SaaS ni del capítulo C03**.

## 8. Adenda correctiva — evidencia real de CI del PR #80

**Antecedente:** GitHub Actions pudo iniciar runners estándar para el PR #80 sin contratar un plan. En el SHA inicial `38abecc0b96a66c407335c2d013263e79279a0e7`, Foundation y la auditoría del maestro comercial fueron SUCCESS; C03-07, C03-08, C03-10 y el gate C03 fueron FAILURE; 11 workflows fueron SKIPPED por condiciones de rutas/ramas. Ninguno de estos hechos constituye certificación de C03.

**Corrección 1 (A/C, documentación, commit `407849c634d15a203cc00113b2ba4d4477bc94f6`):** la nota de transferencia agregada antes del encabezado histórico `**ESTADO VIGENTE:**` lo había desplazado fuera de las primeras siete líneas exigidas por `qa/c03_05_status.py`. Los logs de C03-07/08/10 reportaron `Active heading contradicts the locked C03 row`. Se reubicó la nota después del encabezado histórico, manteniendo ese estado inmutable y el bloqueo C04. Asimismo se añadió la referencia textual exacta `docs/ESTADO_VIGENTE_CAPITULOS.md`, exigida por el gate C03.

**Corrección 2 (B, prueba negativa, commit `b849ea5197dab4356667111fe2b42abf4ad1fb36`):** en el acta histórica de C03 el SHA de baseline `5b7c963c310d4947519ce4c8959568f8ad02143f` aparece dos veces. El escenario adversarial `forged base` mutaba solo una aparición y no podía demostrar el rechazo de evidencia falsificada por completo. Se fortaleció exclusivamente el fixture en `qa/c03_chapter_certification.py`, usando `acta.replace(BASE, "0" * 40)` para invalidar **todas** sus apariciones, en lugar de modificar solamente la primera. No se alteró el acta, ni el contrato de validación, ni se desactivaron pruebas.

**Auditoría A (trazabilidad):** modificaciones de PR limitadas a referencias del repositorio CRECISOL, al encabezado del plan y al fixture adversarial; contenido histórico y maestro Word/PDF preservados. **Auditoría B (seguridad):** no hay secretos, autorización debilitada ni despliegues; la mutación negativa es más exhaustiva. **Auditoría C (ejecución):** logs reales inspeccionados de los runs `43135261853`, `43135261829`, `43135261726`, `43135261665` y `43135792841`. La suite C03-07 llegó a ejecutar pruebas con .NET 10 y MySQL 8.4 antes del bloqueo documental. Las ejecuciones posteriores de los commits de corrección deben revisarse por separado; no se afirma SUCCESS para ellas hasta confirmación en GitHub.

**Estado y riesgos:** PR #80 en borrador; `dev` y `main` no modificadas por este PR. La ejecución anterior del commit de `dev`, run `43097016405`, sigue en `queued` sin resultado. El ticket de soporte `#4991772` aparece `Cerrado` en captura del propietario, pero la respuesta y justificación de cierre no están verificadas. Se debe solicitar reapertura o escalamiento si permanece bloqueada. No corresponde contratar GitHub Team para solventar estos errores.
