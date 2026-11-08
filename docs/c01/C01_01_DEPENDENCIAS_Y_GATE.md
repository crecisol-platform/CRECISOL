# C01-01 · Dependencias, discrepancia de secuencia y puerta de aceptación

**Objeto:** extraer y analizar íntegramente el capítulo 01 conforme al Documento Maestro Único, sin desarrollar más fases. **Estado:** inventario y dependencias validados en C01-01; cierre técnico pendiente de los tres controles y la integración por PR. No certifica C01. No implica certificación de C01.

## 1. Procedencia, recuento y límites

- **Fuente única:** docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx, SHA-256 e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e.
- **Sección original 01 hasta §1.7:** **34 párrafos**: 27 con contenido y siete encabezados estructurales, identificados P01–P34. Los encabezados no son pruebas o requisitos funcionales independientes.
- **Tres tablas originales:** 7 + 8 + 6 = **21 filas de contenido**, más 3 encabezados: 24 filas totales. IDs T01-R01…R07, T02-R01…R08 y T03-R01…R06. Están en [la matriz](C01_01_MATRIZ_REQUISITOS.md).
- **Contrato posterior, distinto del original:** otras dos tablas con tres filas cada una (FRONTEND/BACKEND/BASE DE DATOS y tres ejemplos de requisitos de origen), prueba positiva/negativa, control TEN y ciclo de ocho pasos. Estas tablas **no se confunden** con las tres originales ni sustituyen las 21 filas.
- El inventario es **derivado para trazabilidad**, nunca un segundo Documento Maestro. Prueba automatizada de lectura OOXML: qa/c01_verify_source.py.

## 2. DIF-C01-01 · Dos secuencias con los mismos códigos

**Hallazgo identificado y resuelto operativamente por doble nomenclatura:** el contrato interno del Word dice literalmente:

> C01-01 Extraer / C01-02 Diseñar / C01-03 Codificar / C01-04 Probar / C01-05 Auditar A / C01-06 Auditar B / C01-07 Auditar C / C01-08 Acta.

El plan operativo acordado después con el propietario define las ocho microtareas por entregables técnicos: requisitos, contratos, MySQL, API, Angular, logotipo, pruebas y auditorías/cierre. Ambas secuencias reutilizan C01-01…C01-08 pero **no expresan las mismas unidades de trabajo**. No se edita ni se silencia el maestro.

Para mantener la secuencia operativa aprobada y garantizar todas las obligaciones originales, se conserva la siguiente **equivalencia funcional** aprobada para gestión técnica bajo la delegación expresa del propietario (8 de noviembre de 2026), sin alterar ni sustituir el Word:

| Ciclo literal del Word | Microtarea(s) operativa(s) que entregan la evidencia | Criterio |
| --- | --- | --- |
| C01-01 Extraer | C01-01: análisis, matriz, dependencias | 34 párrafos + 21 filas y anexos verificados |
| C01-02 Diseñar | C01-02 contratos y C01-03 MySQL | Diseño tipado, integridad y seguridad previa |
| C01-03 Codificar | C01-04 API, C01-05 Angular, C01-06 logo | Código, compilación y aceptación visual |
| C01-04 Probar | C01-07 pruebas de integración y negativas | Evidencias reproducibles |
| C01-05 Auditar A | C01-08: auditoría A independiente | Trazabilidad real por requisito |
| C01-06 Auditar B | C01-08: auditoría B independiente | Seguridad, tenancy y cero P0/P1 |
| C01-07 Auditar C | C01-08: auditoría C independiente | Arquitectura, rendimiento, UI y accesibilidad |
| C01-08 Acta | C01-08: acta de cierre | Aprobación expresa y habilitación de C02 |

**Resolución de DIF-C01-01:** referencias **MAE-C01-01...MAE-C01-08** para los ocho pasos literales del maestro; referencias **C01-01...C01-08** para las ocho microfases técnicas operativas acordadas. La equivalencia de la tabla es exhaustiva (8 de 8). Es una convención documental derivada, no una enmienda al maestro. El usuario delegó auditoría y cierre rutinario de microfases sujetas a pruebas; la **certificación completa de C01** sigue reservada a C01-08 con acta y aceptación del capítulo.

## 3. Puertas de fase en orden estricto

| Fase | Entrada obligatoria | Salida para considerar su cierre |
| --- | --- | --- |
| C01-01 | Maestro y repositorio verificados | Matriz completa, anexos, dependencias y decisión de numeración |
| C01-02 | C01-01 cerrada | Modelos tipados plataforma/tenant y estados borrador/previa/publicado; sin escrituras inseguras |
| C01-03 | C01-02 cerrada | Modelo MySQL, scopes, FK, configuración de conexión sin secretos y estrategia reversible |
| C01-04 | C01-03 cerrada | Domain, Application, API de solo lectura, validación y negativas de exposición |
| C01-05 | C01-04 cerrada | Angular responsive, cabecera, pie, SEO, accesibilidad y revisión en navegador real |
| C01-06 | C01-05 cerrada | Logo oficial sin deformación ni sustitución, validación visual final |
| C01-07 | C01-06 cerrada | Pruebas positivas, negativas, abuso, host, caché, inyección, tenant y rendimiento |
| C01-08 | C01-07 cerrada | Auditorías A/B/C, cero hallazgos P0/P1, acta y aprobación de C01 |

**Código adelantado:** PR #10 contiene implementación y CI preliminares de otras fases. Ese trabajo se conserva sin considerarlo fase cerrada ni justificar saltarse sus dependencias.

## 4. Dependencias transversales

| Referencias originales | Control y dependencia posterior | Política de C01 |
| --- | --- | --- |
| P01/P02, T01-R01/R02/R07, T02-R05/R08 | Producto universal y rubros mixtos; C02, C09–C17 y C30 | Solo alcance y contratos; no construir módulos sectoriales enteros |
| P04/P06/P08/P21, T01-R04, T03-R02/R04 | Autorización backend, tenant y SuperAdmin; C04–C08, C26 y C36 | **P0**: denegar acceso privado hasta disponer de control real |
| P07 y T01-R03/T02-R07 | Editor seguro y branding del negocio; C09, C23 y C34 | Esquemas tipados, previa y permisos antes de habilitar escrituras |
| P05/P09 y T01-R02/T03-R05 | Pago idempotente, timeline y enlaces; C12–C19 y C31 | No activar cobros ni comunicaciones reales |
| P10/P19/P20 y T03-R04 | Respaldo, Hostinger, R2, DNS/TLS; C21, C24, C25, C29, C32 y C35 | Diseñar sin tocar infraestructura productiva |
| P14/P15/P17/P22/P24/P29 y T02-R01–R06 | Marca, titularidad, emisor; C23, C24 y C29 | **P1** legal: no inventar marcas registradas, dominio adquirido ni entidad jurídica |
| P25–P28 y T03-R01/R05 | SEO, emails, QR, tokens, host y slugs; C04, C05, C19, C20 y C31 | Diseñar negativas; no desplegar dominios ni mails |
| P31–P33, T02-R02/R03 | Copy comercial, CTAs y capacidades honestas; C08 y C29 | No presentar como contratables funcionalidades ausentes |

### Controles TEN del contrato adicional

La sección adicional solicita considerar **TEN-001, TEN-002, TEN-008, TEN-009, TEN-010, TEN-021 y TEN-032** «si la superficie del capítulo lo permite» y documentar NO APLICA con justificación verificable para los demás. Su presencia en la lista **no demuestra** pruebas cross-tenant. En C01-07 deberán identificarse definiciones fuente, contexto, datos sintéticos, resultados y riesgos.

## 5. VPS Hostinger y dominio: previstos, NO VERIFICADO

El maestro contempla crecisol.com y frontend/API/workers/MySQL propios en Hostinger VPS. El propietario informó que quizá la base ya existe y el dominio quizá esté configurado. **NO VERIFICADO:** propiedad, registros DNS, TLS, versión instalada, capacidad de recuperación, credenciales, accesos o configuración efectiva.

En C01-03 solo se prepararán nombres de parámetros (sin valores secretos): ConnectionStrings__CrecisolDb, Crecisol__PublicBaseUrl y Crecisol__AllowedHosts. **Nunca** exponer la cadena en el frontend, aceptar DSN desde URL/tenant ni configurar VPS productivo aquí. La activación futura requiere TLS, credenciales mínimas, controles tenant, migraciones, pruebas de rollback y respaldo; no son solo tres líneas de código.

## 6. Riesgos y bloqueos

| Hallazgo | Prioridad | Acción |
| --- | --- | --- |
| DIF-C01-01 · numeración duplicada | **RESUELTO en C01-01** | Dos espacios de identificadores (MAE-C01-xx para maestro y C01-xx operativos); tabla 8/8, sin editar Word |
| C01-RISK-02 · auth/tenant no implementados | **P0 producto** | No habilitar endpoints ni datos privados |
| C01-RISK-03 · VPS/DNS no comprobados | P1 preparación futura | Usar únicamente configuración parametrizada sin secretos |
| C01-RISK-04 · emisor/legal sin evidencia | P1 legal | No inventar entidad jurídica, compra de dominio ni emails |
| C01-RISK-05 · UI/seguridad no terminadas | P1 certificación | Auditar tras completar fases; C02 bloqueado |

## 7. Checklist de aceptación de C01-01

- [x] Repositorio, rama, gobernanza y hash del Word comprobados.
- [x] P01–P34 inventariados, incluidos siete encabezados estructurales.
- [x] T01/T02/T03 y las 21 filas de datos inventariadas, sin confundir encabezados.
- [x] Dos tablas adicionales del contrato reconocidas.
- [x] Dependencias posteriores y los siete controles TEN identificados.
- [x] Hostinger, dominio y datos jurídicos marcados NO VERIFICADO.
- [x] Prueba reproducible de integridad y cobertura creada (ejecutar y conservar evidencia).
- [x] **DIF-C01-01:** doble nomenclatura definida; 8 equivalencias documentadas sin modificar la fuente; decisión operativa bajo delegación del usuario.
- [x] Verificación PASS del control de cobertura sobre el commit 04cfa9cc7e2711a15893f21c8f9e2c53a8bc0785; GitHub Actions #42518139216, tres trabajos aprobados.
- [ ] Cierre automático delegado de C01-01 **solo tras triple auditoría real y CI verde**, con acta técnica; esto no es la aprobación del capítulo completo.

**Decisión actual:** C01-01 **PREPARADA PARA TRIPLE AUDITORÍA**, su cierre dependerá de evidencia CI verificable y acta de microfase. C01-02 permanece bloqueada hasta ese cierre. **0/37 capítulos certificados**.

**Rollback:** revertir solo documentación y QA de C01-01 en feature/c01-identidad; sin escrituras en dev, main, VPS, producción o SOLQARYN.

### Evidencia reproducible de C01-01

- **CI verificada:** https://github.com/solqaryn-platform/CRECISOL/actions/runs/42518139216
- **Commit probado:** 04cfa9cc7e2711a15893f21c8f9e2c53a8bc0785.
- **Script:** python3 qa/c01_verify_source.py — verificación OOXML de 34 párrafos (27 de contenido, 7 encabezados), 3 tablas originales con 21 filas de datos, 2 tablas suplementarias, 7 controles TEN y 55 IDs únicos en matriz.
- **Resultado:** PASS; sin omisiones ni duplicaciones de identificadores. El test verifica **estructura y presencia**, no prueba implementación real de cada requisito ni reemplaza revisión semántica y aceptación humana.
- **CI restante:** frontend, backend y MySQL también aprobaron en la misma ejecución; son pruebas preliminares de C01, no cierres automáticos de fases futuras.
- **No se han escrito datos productivos ni realizado despliegues.**

### Nota de autoridad delegada de microfases

El propietario autorizó expresamente revisión, pruebas, auditoría repetida, cierre técnico y merge a `dev` de microfases **sin nueva consulta rutinaria**, siempre que no existan bloqueantes y se preserve el Documento Maestro. Esta autorización no habilita producciones, compras, VPS, cobros, exposición de datos, escritura a SOLQARYN ni eludir las tres auditorías y el acta final del capítulo C01. El alcance C01-01 es documental: controles P0 de un SaaS todavía no desarrollado se mantienen abiertos, pero no se introducen interfaces privadas.
