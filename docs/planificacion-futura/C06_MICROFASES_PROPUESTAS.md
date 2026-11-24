# C06 · Autorización fina, protección de datos y privacidad — microfases propuestas

**ADENDA C06-03 — CÓDIGO Y E2E PRE/POST APROBADOS (24-11-2026), ACTA PENDIENTE DE CI PROPIA:** PR #199 integrado en dev SHA ea7e341010734665c1f3975047aef12d0aaebfb2. Auditorías C06-03 A/B/C PRE #45506430723 y POST #45508442081 3/3 SUCCESS; E2E C05 PRE #45506430398 y POST #45508442240 2/2 SUCCESS, 340 aserciones integradas, 73 Chromium PASS; sin permisos ABAC, lectura de tenant desde el browser, nueva UI ni DB. [Acta propia](../c06/C06_03_ACTA_CIERRE.md) debe obtener A/B/C PRE/POST por su SHA; al hacerlo, **C06-03 CERTIFICADA en su alcance de identidad y pertenencia**, C06 **3/11** microfases y 5/37 capítulos completos; C06-04 solo preparación, C06 global NO CERTIFICADO, C07 bloqueado. Antes del cierre documental oficial: 2/11.

**ADENDA DE COMPLETITUD FUNCIONAL C06-02 (24-11-2026; pendiente solo puerta institucional de su acta):** [PR #197](https://github.com/crecisol-platform/CRECISOL/pull/197) añadió la matriz canónica `docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json` con **10 roles, 108/108 acciones y 12 contratos internos**; definió funciones de operadores PLATFORM, administradores de empresa, encargados de sucursal, colaboradores, lectores, financieros y roles personalizados vacíos por defecto. En `dev` SHA `68f9e3d638dff77ef95870b9a15cb1b18825c671`, auditorías A/B/C [PRE #45488601928](https://github.com/crecisol-platform/CRECISOL/actions/runs/45488601928) y [POST #45488939368](https://github.com/crecisol-platform/CRECISOL/actions/runs/45488939368) **3/3 SUCCESS**; 31 mutaciones negativas y verificaciones reales .NET/MySQL. Rol ≠ grant: las 28 futuras siguen no concedibles y no se habilita política HTTP C06. [Adenda al acta C06-02](../c06/C06_02_ACTA_CIERRE.md), con eficacia institucional sujeta a CI de la adenda. C06-03 solo preparación, C06 global NO CERTIFICADO, C07 BLOQUEADO.

**ADENDA C06-02 (24-11-2026; EFECTIVA SOLO TRAS GATES DE SU ACTA DOCUMENTAL):** modelo interno de roles y propuestas por tenant/sucursal completado en PR técnico #194, `dev` SHA `5022328e499f1e3f039a3d308e2b1d754231215c`, A/B/C PRE/POST 3/3 SUCCESS y regresiones C05 SHA1/catálogo C06-01 aprobadas en SHA; QA histórica C05 normalizada mediante PR #195 sin cambiar TOTP. [Acta de alcance limitado](../c06/C06_02_ACTA_CIERRE.md). **No hay concesión de permisos, roles operativos ni API ABAC**; las 28 acciones futuras siguen no concedibles. Cuando se certifique el acta documental, C06 avanza a **2/11 microfases**, C06-03 solo preparación, C06 global NO CERTIFICADO y C07 BLOQUEADO. Se preservan literalmente las propuestas históricas de más abajo y el Documento Maestro único.


**ADENDA DE ACTIVACIÓN PARCIAL (24-11-2026; ESTADO VIGENTE):** `C06-01` (inventario y catálogo de acciones/ámbitos) **CERTIFICADA en su alcance documental y QA automatizada** sobre `dev` SHA `690a59a74e4607e2c31e008d715f2f6dc9f2f50b`, PR #192, PRE/POSTMERGE A/B/C 3/3 SUCCESS cada uno. Contrato JSON canónico `docs/c06/C06_01_CATALOGO_ACCIONES.json`, 80 rutas, 12 contratos no HTTP y 28 acciones futuras sin grants, acta `docs/c06/C06_01_ACTA_CIERRE.md`. **C06-02 puede prepararse** con su propio contrato y prueba negativa; C06-02..11 permanecen NO IMPLEMENTADAS/NO CERTIFICADAS. C06 capítulo y C07 no certificados/inhabilitados. La nota de «BORRADOR NO HABILITADO» que sigue corresponde al plan anterior a esta activación selectiva; las once microfases conservan su numeración.

> **BORRADOR DE PLANIFICACIÓN; NO HABILITADO PARA DESARROLLO NI CERTIFICACIÓN.** Esta guía deriva del índice `docs/PLAN_37_CAPITULOS.md` y de las políticas transversales de CRECISOL. Antes de activar C06, contrastar cada punto con el texto íntegro del capítulo en el Word canónico, su SHA registrado en `docs/MASTER_REFERENCE.md` y sus anexos; ajustar el número o alcance por PR trazable. Este archivo **no modifica el Documento Maestro Único**.

**Puerta de entrada:** C05 certificado, acta aceptada y CI de `dev` verde. Ninguna microfase de C06 puede codificarse durante C01 u otro capítulo anterior.

**Riesgo rector:** P0: pertenencia, rol, acción, branch y dueño verificados en backend.

## Plan secuencial de 11 microfases

| Código | Entregable acotado | Prueba o evidencia positiva esperada | Negativa que debe impedir cierre |
| --- | --- | --- | --- |
| C06-01 | Inventario de acciones por módulo | Catálogo canónico de permisos y ámbitos | No usar etiquetas UI como autorización |
| C06-02 | Modelo de roles y capacidades | Asignaciones por tenant y sucursal testeadas | No heredar rol de otra organización |
| C06-03 | Pertenencias y resolución de actor | Verificación real de identidad/membresía | No aceptar actor indicado por navegador |
| C06-04 | Políticas ABAC y propiedad | Evaluación por recurso y acción en backend | Denegar IDOR por ID ajeno |
| C06-05 | Reautenticación de operaciones sensibles | Step-up y finalidad justificada | No bypass vía petición directa API |
| C06-06 | SuperAdmin excepcional | MFA, motivo, duración y log separados | No acceso permanente a datos privados |
| C06-07 | Privacidad y minimización de DTO | Proyección estricta y retención definida | No serializar campos internos o PII extra |
| C06-08 | Aislamiento de listados y paginación | Filters servidor en todas las consultas | No count/enumeración entre tenants |
| C06-09 | Descargas, logs y exportaciones | Autorización en medios y auditoría | No fuga cross-tenant por URL firmada |
| C06-10 | Pruebas BOLA/IDOR de regresión | Matriz de actor/tenant/rol/branch ejecutada | Cero acceso horizontal o escalado |
| C06-11 | Auditorías integrales y acta C06 | A/B/C y aceptación con ausencia P0/P1 | No habilitar C07 sin puertas privadas cerradas |

## Contrato obligatorio para C06-XX

Para activar cada fila, validar dependencia de la microfase anterior, matriz de requisitos del capítulo y archivos/rutas afectados. Producir `docs/c06/C06_XX_ESPECIFICACION.md` al **iniciar** esa microfase (no fabricar actas futuras); rama `feature/c06-xx-*`; cambio pequeño en `frontend/`, `backend/` o `database/` según corresponda; pruebas positivas y negativas **realmente ejecutadas**, auditorías A (trazabilidad), B (seguridad, tenancy y privacidad), C (arquitectura, rendimiento y UX), diff, PR, CI del último SHA, merge a `dev`, CI posterior, acta y reversión. P0/P1 o cualquier control no ejecutado obligatorio implica **BLOQUEADO**, no PASS. No modificar SOLQARYN ni producción.

## Puerta de certificación del capítulo

Cerrar las 11 microfases **no** certifica C06. Exigir auditorías integrales A/B/C del capítulo, regresión cruzada, ausencia comprobada de P0/P1, aceptación expresa prevista en la gobernanza, acta final y evidencia GitHub del SHA integrado antes de habilitar C07. No confundir definición de prueba con prueba ejecutada; ninguna prestación del presente plan está implementada por el hecho de documentarla.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **ya incorporada en la rama del PR #73, con blob y SHA verificados; pendiente de CI, auditorías y fusión a dev**; esta nota no sustituye el Word ni habilita C06.

- **COM:** COM-008, COM-010, COM-012, COM-014.
- **Microfases a revisar sin renumerar:** C06-01, C06-02, C06-04, C06-05, C06-06, C06-07, C06-08, C06-10 y C06-11.
- **Aceptación futura:** Políticas para configurar ofertas (plataforma), consultar cuenta (propietario), solicitar baja (propietario), exportar sus propios datos y auditar administración.
- **Negativos:** Propietario no modifica precios globales ni contratos ajenos; SuperAdmin sin grant no inspecciona expedientes privados.
- **Pruebas definidas y NO ejecutadas:** COM-T07, COM-T08, COM-T10 y COM-T17.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
