# CRECISOL C06-02 — Acta institucional de certificación técnica acotada

**Fecha:** 24-11-2026 (America/Tegucigalpa). **Repositorio:** `crecisol-platform/CRECISOL`, ID original `1450414592`.  
**Microfase:** C06-02 — Modelo de roles y capacidades. **Dictamen:** **CERTIFICABLE ÚNICAMENTE COMO MODELO INTERNO DE ROLES Y PROPUESTAS POR TENANT/SUCURSAL, CON CONTRATOS .NET Y MYSQL AISLADOS**. **NO es RBAC/ABAC de producto ni concede derechos sobre HTTP o recursos privados**. Este cierre se considera **institucionalmente efectivo solo cuando el PR documental de esta acta sea fusionado a `dev` tras A/B/C PREMERGE y su CI POSTMERGE sobre SHA exacto**.

## 1. Dependencia, identidad y procedencia
- C01–C05 certificados en sus alcances documentados: **5/37 capítulos**; C06-01 certificada como inventario con PR #192/193. C06 global NO CERTIFICADO; C06-03 solo preparación después de este dictamen.
- Documento Maestro Único Word canónico, comprobado por CI: SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, capítulo 06 con 70 párrafos. **No se modificaron Word/PDF ni sus huellas.**
- C06-01 inventario preservado sin mutación: 80 entradas HTTP (79 registrables condicionales, 1 solo arnés), 12 contratos no HTTP y 28 acciones futuras `grantable=false` y `http_registered=false`. Los roles C06-02 no los activan.
- Base previa C06-02 `dev` `b2e436221bca684eb43d4ce72e7044080cfd6601`; mantenimiento transversal QA C05 PR [#195](https://github.com/crecisol-platform/CRECISOL/pull/195) fusionado SHA `e40ce13cfa52f8cde05aa767b8eb1596ac990169`; código C06-02 PR [#194](https://github.com/crecisol-platform/CRECISOL/pull/194) fusionado **solo a `dev` SHA `5022328e499f1e3f039a3d308e2b1d754231215c`**; `main` intacta.

## 2. Implementación exacta
| Capa | Evidencia y límites |
| --- | --- |
| Domain | `C06PreparedRoleSnapshot`: tenant, actor, sucursal, acción, membresía, rol, vigencia, versión, revocación; `actionGrantable=true` rechazado |
| Application | `InspectPreparedTenantRoles`: evidencia interna verificada, límite 128 candidatos, reloj UTC, comprobación de filas ajenas/duplicadas; `AuthorizesPrivateResource=false` |
| Infrastructure | `MySqlPreparedRoleReader`: consulta SELECT parametrizada, JOIN tenant+membresía+actor+sucursal+rol+acción, lectura limitada y fallos seguros, sin registrar adaptador en API/producción |
| Database | Migración aditiva `019_c06_prepared_roles.sql`: cinco tablas de catálogo/propuestas/auditoría, FK compuestas por tenant, CHECK sin grants, rol versionado, revocación, auditoría append-only; reversión `019_c06_prepared_roles.down.sql` |
| QA/CI | Proyectos `Crecisol.PreparedRoles.ContractsTests` y `Crecisol.PreparedRoles.MySqlTests`, arnés MySQL descartable, auditor independiente A/B/C y workflow `c06-02-roles-capabilities.yml` |

**No** se modificaron `frontend/src`, `backend/src/Crecisol.Api`, autenticación, MFA, tiempo de inactividad (30 min parametrizable en C05), altas ni datos existentes, recursos físicos, producción, VPS ni cobros. La migración solo se ejecutó sobre **MySQL 8.4 desechable** en CI; ningún tenant real fue asignado.

## 3. Pruebas y auditorías reproducibles
| Evidencia | Resultado probado |
| --- | --- |
| Contratos .NET 10 | **37 verificaciones** positivas/negativas PASS: tenant, actor, sucursal, acción, revocación, reloj UTC, duplicidad, cupos, no-privilegios |
| MySQL 8.4 real | **21 ataques negativos** rechazados: cruces de tenant, membresía, rol, branch, realm, wildcard, versión, revocación, integridad de bitácora |
| Integración SQL→.NET | **20 verificaciones reales**: diez antes y diez después de revocar, sin grant de recurso |
| Mutación estructural QA | **10 controles SQL adversariales** detectados si se omiten las FK, CHECK, triggers o prohibiciones de grant |
| Rollback | Aplicación/reversión de 019 sobre base efímera: tablas C06 retiradas, tablas C05 conservadas |
| Compilación | API .NET 10 en Release con warnings-as-errors SUCCESS, Angular 22 check:types/build SUCCESS; sin pantallas C06 nuevas |

**A — trazabilidad y funcionalidad:** Word aprobado, 80/12/28 de C06-01, diff acotado, rutas/producto inalteradas.  
**B — seguridad y privacidad:** fail-closed, 21 ataques SQL, 10 mutaciones negativas, segregación TENANT/PLATFORM, revocación y cero permisos efectivos.  
**C — arquitectura, compatibilidad y UX:** capas Domain/Application/Infrastructure sin nuevas rutas, compilaciones, migración/reversión aislada, sin UI funcional alterada.

**PREMERGE técnico en HEAD `8939e3d64b8a68479de6165021112ddfe458b73b`:** [C06-02 A/B/C 3/3 SUCCESS #45469395284](https://github.com/crecisol-platform/CRECISOL/actions/runs/45469395284); [C06-01 A/B/C #45469395413](https://github.com/crecisol-platform/CRECISOL/actions/runs/45469395413) 3/3 SUCCESS; [C05 SHA1 A/B/C #45469395142](https://github.com/crecisol-platform/CRECISOL/actions/runs/45469395142) 3/3 SUCCESS; fundamentos #45469395183 SUCCESS.

**POSTMERGE técnico en `dev` SHA `5022328e499f1e3f039a3d308e2b1d754231215c`:** [C06-02 A/B/C #45469970937](https://github.com/crecisol-platform/CRECISOL/actions/runs/45469970937) **3/3 SUCCESS**; [C06-01 A/B/C #45469971292](https://github.com/crecisol-platform/CRECISOL/actions/runs/45469971292) **3/3 SUCCESS**; [C05 SHA1 A/B/C #45469970986](https://github.com/crecisol-platform/CRECISOL/actions/runs/45469970986) **3/3 SUCCESS**; fundamentos #45469970902 SUCCESS. Los cuatro workflows figuran `completed/success` en el mismo SHA.

## 4. Incidencias y límites transparentes
- Primera candidata C06-02: rechazo por falso positivo de la palabra `GRANT` en comentario SQL; corregido para analizar únicamente instrucciones ejecutables, conservando rechazo de GRANT real.
- Bloqueo heredado del auditor SHA1 C05: comparaba todas las rutas backend/database contra el commit histórico de C05. Corregido **por PR independiente #195**, exclusivamente QA, conservando invariantes TOTP/MFA y añadiendo siete negativos de rutas sensibles C05. [PREMERGE #45468948324](https://github.com/crecisol-platform/CRECISOL/actions/runs/45468948324) y [POSTMERGE #45469291745](https://github.com/crecisol-platform/CRECISOL/actions/runs/45469291745) ambos A/B/C SUCCESS; no se borran los fallos previos de la historia.
- Auditor C06-02 preparado para comparar el diff real de PR/push y no bloquear capítulos futuros por SHA fijo.
- Angular compila pero mantiene advertencias anteriores de presupuestos CSS de cuatro componentes y módulos CommonJS `qrcode`/`dijkstrajs`. **No se emite calificación visual 9.5/10 de una UI C06-02 que no existe**. Los requisitos visuales 9.5/10 se exigirán en cualquier UI futura.
- Los workflows amplios históricos C05 operativa Angular/API/MySQL y C03-10 integral podían estar en ejecución durante el cierre técnico; **NO se presume PASS ni se hace afirmación de certificación de esas regresiones sin consultar sus resultados**. La certificación aquí no activa una API RBAC/ABAC, pruebas BOLA/IDOR C06-10, acceso break-glass, privacidad operativa de C06-07, ni producción.

## 5. Riesgo, reversibilidad y condición institucional
En los cambios internos probados no se identificó P0/P1 atribuible al alcance de C06-02. No equivale a afirmar que toda la plataforma carece de vulnerabilidades. Revertir PR #194 retira contratos sin exponer permisos; en bases descartables aplicar `019_c06_prepared_roles.down.sql` tras copia y validación, conservando C05. No ejecutar ese rollback en datos del propietario.

**Decisión de alcance:** cierre técnico de **C06-02 solo como contratos internos, modelo de propuestas por tenant y sucursal, prueba aislada y QA**. Tras aprobación de los gates de esta propia acta, total **2/11 microfases C06 cerradas en sus alcances**; **5/37 capítulos completos certificados**; **C06 global NO CERTIFICADO**; **C06-03 habilitada únicamente para preparar su propia puerta, no para activar grants de C06-02**; C07 BLOQUEADO. `main`, producción, Word/PDF, SOLQARYN y los permisos existentes sin cambios.
