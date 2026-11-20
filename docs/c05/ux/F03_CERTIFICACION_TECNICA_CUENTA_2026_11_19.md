# CRECISOL · C05 · F03 — Acta de certificación técnica delimitada
Fecha de cierre técnico: **19 de noviembre de 2026 (Honduras)** / 20 de noviembre UTC.
**Alcance:** fase UX 03, *cuenta autenticada y aprobaciones*, en rama de funcionalidad y entorno de pruebas aislado. No reemplaza ni amplía la certificación original del capítulo C05, ni supone despliegue o fusión.

## 1. Repositorio, gobierno y trazabilidad
- Repositorio canónico actual: **crecisol-platform/CRECISOL**, ID GitHub **1450414592**, transferencia del anterior solqaryn-platform/CRECISOL; la identidad quedó contrastada con AGENTS.md, la Skill local, docs/MASTER_REFERENCE.md y docs/ESTADO_VIGENTE_CAPITULOS.md.
- PR de fase: **#176**, rama feature/c05-ux-f03-arquitectura-navegacion-20261119; **sin fusionar, DRAFT**. Base F02 en PR **#175** (DRAFT); F02 depende del PR **#172** de autenticación TOTP SHA1/inactividad (DRAFT).
- **SHA ejecutable certificado:** 7c7434fdb9b1c31163e22db9b39722996b11466e.
- [CI integral #44669677975](https://github.com/crecisol-platform/CRECISOL/actions/runs/44669677975): **completed/success; 2 de 2 trabajos SUCCESS** sobre exactamente ese SHA. La formalización documental posterior deberá repetir la CI y tener también resultado SUCCESS en su nuevo SHA antes de declararse cerrada en el HEAD documental.
- El Documento Maestro Único Word/PDF no fue modificado. La revisión está limitada a la F03 posterior a C05 y conserva la secuencia C01–C37. F04 no se inicia.

## 2. Evidencia de ejecución (sin simulación de permisos del backend)
| Prueba | Resultado verificado | Evidencia |
| --- | --- | --- |
| Auditoría A — navegación, contratos, estados y compatibilidad | **PASS**: rutas, seis vistas, formularios, build Angular 22/TypeScript; regresiones F02 y C05 | CI #44669677975 / job navigation |
| Auditoría B — seguridad y privacidad | **PASS**: controles de denegación, masking, identidad/tenant/sucursal, IDOR/BOLA, MFA/CSRF y respuesta tardía, cuotas, sesiones revocadas | CI #44669677975 / job .NET10/MySQL8.4 |
| Auditoría C — arquitectura, UI y evidencias | **PASS**: 320/390/768/1024/1440 px, 200 % zoom, axe WCAG AA, modal/foco, sin overflow de página y SHA de imágenes | CI #44669677975 / job navigation |
| Chromium navegación | **18/18 PASS** | Playwright F03 |
| Chromium cuenta autenticada | **52/52 PASS** | Playwright seis vistas, negativas, privacidad, accesibilidad y deep links |
| Regresión biblioteca F02 | **11/11 PASS** | Playwright institucional |
| Regresión modal C05 | **2/2 PASS** | Playwright C05 |
| Integración real Angular → ASP.NET Core 10 → MySQL 8.4 | **340/340 aserciones PASS** | E2E C05, contenedor MySQL desechable, APIs y navegador real |
| Migraciones SQL | **18 migraciones hacia delante y 18 reversas PASS** | Misma integración local, sin cambiar base de datos productiva |
| Evidencia visual | **30 PNG + 30 JSON, 6 vistas × 5 anchos, 0 huellas discordantes** | Artefacto c05-f03-navigation-7c7434fdb9b1c31163e22db9b39722996b11466e, ID 14598639600; manifiesto F03_ACCOUNT_MANIFEST.json |

Los 340 casos incluyen TOTP SHA1, respaldo de un solo uso, WebAuthn/passkeys, emparejamiento de navegador P-256, restauración y rotación de sesiones, expiración de inactividad en 30 minutos administrable, autorización empresarial, cursores falsos, error de permisos, recuperación con doble operador y enfriamiento comprobado en frontera de reloj de una base de pruebas, perfiles cifrados con AAD por tenant, auditoría sin secretos y reversión SQL. Una prueba de frontera de **24 horas** manipula exclusivamente el reloj del fixture MySQL aislado: no pretende que transcurrieron 24 horas reales. No se usan proveedores ni datos productivos.

## 3. Evaluación visual individual (dictamen editorial sobre capturas reales)
Rúbrica de la F03: composición 2,0; jerarquía y lectura 1,5; tareas y prevención de errores 2,0; estados/copy/privacidad 1,5; responsive y accesibilidad 2,0; consistencia C01/C02–F02 1,0. **Cada fila se evalúa por separado, sin sustituir una vista mala por una media.**

| Vista | Composición | Lectura | Tareas | Estados/privacidad | Responsive/AA | Consistencia | Nota / 10 | Evidencia/razón |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Resumen | 1,9 | 1,4 | 1,8 | 1,4 | 1,8 | 0,9 | **9,2** | Estado de sesión y plazo 30 min del servidor, dispositivos revocables de forma individual, acciones avanzadas progresivas |
| Perfil | 1,9 | 1,4 | 1,9 | 1,4 | 1,8 | 0,9 | **9,3** | Nombre y preferencias sin datos técnicos, foto gestionable, diálogo precargado y cancelación no destructiva |
| Llaves | 1,9 | 1,4 | 1,8 | 1,4 | 1,8 | 0,9 | **9,2** | Credenciales abreviadas, selección segura de llave antes de revocar y registro con segundo factor |
| Seguridad | 1,9 | 1,3 | 1,8 | 1,4 | 1,8 | 0,9 | **9,1** | Acciones sensibles separadas en diálogos protegidos; el flujo de recuperación es extenso pero mantiene scroll/foco |
| Historial | 1,9 | 1,4 | 1,8 | 1,4 | 1,8 | 0,9 | **9,2** | Cronología semántica con filtros, resultados comprensibles y eventos desconocidos sin código crudo |
| Aprobaciones | 1,9 | 1,4 | 1,8 | 1,4 | 1,8 | 0,9 | **9,2** | Bandeja real paginada por tenant, correo enmascarado y decisión contextual con motivo, confirmación y MFA |

**Resultado editorial: 6/6 vistas con calificación ≥9,0/10 en sus estados de presentación examinados.** Capturas de Chromium inspeccionadas en 320, 390, 768, 1024 y 1440 px; comparadas con los controles de estados vacíos, error, modal, foco y privacidad de la suite automatizada. Las 30 capturas permanentes corresponden a **estados cargados con datos sintéticos**; no se afirma haber fotografiado todas las combinaciones de error/diálogo en los cinco anchos. La nota es una **valoración visual editorial del asistente** y no una medición objetiva ni aprobación personal del propietario o Codex.

## 4. Cierre de la dependencia Aprobaciones
La carencia histórica de bandeja segura de F03 queda resuelta en el código ejecutable auditado: endpoint **memberships/pending** (límite de 12 por página, cursor validado, tenant derivado del servidor, permiso MEMBERSHIP_APPROVE vigente, contactos enmascarados). El navegador selecciona un expediente servido por API y **memberships/decide** exige motivo de 8–240 caracteres validado también en HTTP, confirmación y MFA. Negativas reales: rol limitado a sucursal, self-approval, tenant distinto, cursor malformado, motivo vacío o excesivo; concurrencia de 12 aprobaciones con un único ganador y trazabilidad. La UI **nunca** concede el permiso.

## 5. Límites, riesgos y cierre
**Sin P0/P1 activos detectados dentro de los controles ejecutados**. No se declara ausencia universal de vulnerabilidades. Las ejecuciones fallidas históricas, los selectores corregidos y sus commits se conservan como antecedentes, sin rebautizarlas como PASS. Este dictamen no certifica producción, proveedores externos, una auditoría humana con lector de pantalla, pruebas del propietario/Codex, ni cumplimiento COM-T08/09/10.

**Certificación técnica F03: APROBADA EN CÓDIGO Y PRUEBAS LOCALES AISLADAS sobre SHA 7c7434f.** La eficacia del **HEAD documental descendiente** queda supeditada a su propia CI SUCCESS, registrada en el PR. **Integración a dev: NO REALIZADA** por dependencia de #172 y #175 (ambos DRAFT); PR #176 permanece DRAFT; main, dev, SOLQARYN, el Maestro, VPS, DNS y pagos sin cambios. **F04 bloqueada** hasta resolver e integrar secuencialmente las dependencias correspondientes.

**Rollback:** no se requiere migración nueva de esquema para la bandeja; revertir los cambios de F03 en su rama/PR devuelve a la base F02 sin manipular otras empresas ni recursos productivos. No se ejecuta rollback en datos reales.
