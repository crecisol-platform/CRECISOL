# CRECISOL C06-02 — Acta institucional de certificación técnica acotada

## Adenda C06-02 — Cierre de funciones por perfil y matriz canónica (24-11-2026)

**Estado institucional de esta ADENDA:** el código está integrado y A/B/C técnicas aprobadas; el reconocimiento documental de la ampliación se activa **solo después de A/B/C PREMERGE, fusión en dev y A/B/C POSTMERGE del PR de la adenda en SHA exacto**. Conserva íntegro el acta anterior y su alcance probado.

**Precisión del entregable:** definir qué funciones **candidatas** corresponden a operadores de plataforma, administradores de empresas, encargados de sucursales y usuarios con permisos específicos. La asignación verificable es la **propuesta interna** ligada a tenant+membresía+rol+sucursal, no un grant operativo del producto. Las políticas C06-03 (identidad/membresía) y C06-04 (ABAC/propiedad) siguen pendientes y ningún rol permite acceso privado por el solo hecho de estar definido.

### Inventario funcional y perfiles revisados

Fuente estructurada única: `docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json`; explicación: `docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.md`. La matriz cubre **108/108 acciones** de C06-01 (80 HTTP + 28 futuras); **30 acciones con perfiles candidatos no ejecutables**, **78 acciones sin grants de rol** (cuenta, preauth, titular, excepción, público, arnés); **12/12 contratos no HTTP no expuestos**. Diez perfiles canónicos:
- **PLATFORM:** `platform-operator`, `platform-security-operator`, `platform-incident-responder` (último sin capacidades automáticas, C06-06).
- **TENANT:** `tenant-owner`, `tenant-admin`, `tenant-branch-manager`, `tenant-staff`, `tenant-viewer`, `tenant-finance-specialist`, `tenant-custom` (custom sin grants por defecto).

La clasificación separa *operación propia de cuenta*, *operación pública/preautenticada*, *acción empresarial/sucursal*, *permiso financiero sensible* y *derecho del titular/excepción break-glass*. La aprobación de un titular no pasa al propietario de la empresa; ninguna autorización financiera deriva automáticamente de la propiedad o administración. Los operadores PLATFORM no heredan pertenencia TENANT. Los roles de sucursal requieren `branch_id` explícito. Todas las propuestas preservan `grantable=false`, `policy_activated=false`, `default_grants=[]`, `inherited_across_tenants=false`.

### Código, seguridad, pruebas y evidencias

**PR técnico [#197](https://github.com/crecisol-platform/CRECISOL/pull/197)** integrado solo a `dev` SHA `68f9e3d638dff77ef95870b9a15cb1b18825c671`. Diff: siete archivos iniciales más la ampliación exacta del auditor de rutas C06-02, sin cambios `frontend/src`, `backend/src/Crecisol.Api`, credenciales, MFA, sesiones C05, `main`, Word/PDF, VPS, pagos, producción, datos reales ni SOLQARYN.

- **PREMERGE** en `53f36704eb3195da607c961d712de306bb62fec4`: [C06-02 A/B/C #45488601928](https://github.com/crecisol-platform/CRECISOL/actions/runs/45488601928), **3/3 SUCCESS**; [C06-01 #45488601987](https://github.com/crecisol-platform/CRECISOL/actions/runs/45488601987) SUCCESS y fundamentos [#45488601855](https://github.com/crecisol-platform/CRECISOL/actions/runs/45488601855) SUCCESS.
- **POSTMERGE** en `dev` SHA `68f9e3d638dff77ef95870b9a15cb1b18825c671`: [C06-02 A/B/C #45488939368](https://github.com/crecisol-platform/CRECISOL/actions/runs/45488939368), **3/3 SUCCESS**, [C06-01 #45488939384](https://github.com/crecisol-platform/CRECISOL/actions/runs/45488939384) **3/3 SUCCESS** y fundamentos [#45488939203](https://github.com/crecisol-platform/CRECISOL/actions/runs/45488939203) SUCCESS.
- **Pruebas de seguridad nuevas y anteriores ejecutadas sobre el último SHA técnico:** **31 mutaciones ofensivas del catálogo de roles rechazadas**, **40 verificaciones .NET** de candidatos (incluidas 3 de branch-manager por tenant/sucursal), **23 negativas SQL reales**, **20 verificaciones SQL→.NET** antes/después de revocar, rollback MySQL 8.4 y cero grants de producto.
- **Defensa adicional:** CHECK SQL `ck_c06_role_realm_separation` rechaza `platform-*`, `superadmin` y `super-admin` dentro de `c06_tenant_role`; no se materializó ninguna cuenta o permiso de plataforma como tenant.
- **Auditoría A:** Word vigente (SHA-256 previamente verificado), coincidencia 108/108 y 12/12 de C06-01, perfiles explícitos y alcance. **B:** default-deny, realm, branch, financieros, titular, excepciones, grants cero, mutaciones. **C:** Domain/Application/Infrastructure; schema de propuestas; compilación API .NET 10 warnings-as-errors, Angular 22. Sin cambios de interfaz: **nota UX/visual 9.5 no aplicable**, y las advertencias históricas CSS/CommonJS de Angular no desaparecen por esta ampliación.

**Observaciones:** en la primera CI la lista estricta de rutas C06-02 rechazó correctamente los dos nombres documentales nuevos; se añadieron solo esos paths a la lista y se ejecutaron de nuevo A/B/C, sin eliminar el bloqueo del auditor. Las regresiones amplias C03/C05 deben consignarse por SHA y estado verificado; no declarar SUCCESS mientras estén en ejecución. No se emitió evaluación BOLA/IDOR de permisos activos, reservada a C06-10.

### Regla de calidad 9.5/10

Para el **alcance técnico documentado**, el mínimo solo se acredita cuando pasen: catálogo total, perfiles completos, aislamiento PLATFORM/TENANT, sucursal explícita, finanzas segregadas, derechos del titular protegidos, excepciones sin grants, contratos .NET, MySQL/rollback y A/B/C PRE/POST por SHA. El resultado técnico de estos 10 controles es **10/10 comprobaciones requeridas** en las CI identificadas; **no es una valoración visual subjetiva** ni una garantía de seguridad de todo CRECISOL.

**Dictamen prospectivo de esta adenda:** tras sus gates documentales, C06-02 conserva certificación **completa en la definición probada de roles/funciones y propuestas sin acceso efectivo**. Se mantienen **2/11 microfases C06 en sus alcances** y **5/37 capítulos completos**; C06 global NO CERTIFICADO, C06-03 solo preparación, C07 bloqueado. Rollback: revertir PR técnico y adenda con revisión de dependencias; SQL 019 solo en base autorizada/desechable tras backup; jamás modificar datos reales en esta etapa.

---

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
