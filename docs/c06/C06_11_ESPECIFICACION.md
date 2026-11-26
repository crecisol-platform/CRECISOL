# C06-11 — Auditoría integral y certificación de C06

**Estado: CANDIDATA NO CERTIFICADA.** Base dev 076118ab5370e1fcbc7bfe0f1caefe07a664365b. C06 10/11 microfases ratificadas en sus alcances; capítulo C06 NO CERTIFICADO; C07 bloqueado. No se abre código de C07.

## 1. Puerta de entrada efectiva C06-10

Se verificaron por API GitHub (repositorio original 1450414592, ubicación crecisol-platform/CRECISOL) las rondas técnicas PRE [#45734643721](https://github.com/crecisol-platform/CRECISOL/actions/runs/45734643721) y POST [#45737693483](https://github.com/crecisol-platform/CRECISOL/actions/runs/45737693483), ambas 5/5 auditorías y 15/15 workflows SUCCESS sobre sus SHA exactos. PR #216 integrado en ec0258b1d2c0738dbcb0c43aa305be18db0f9a3f con árbol 9072c2694fa6e565503ac75c562390c35af0dacc. PR #217 integrado en 076118ab5370e1fcbc7bfe0f1caefe07a664365b, árbol bec5595b14024c9b3e10a83774af00e9a2febc8f, PRE institucional 7/7 SUCCESS, POST 6/6 SUCCESS, incluido A/B/C [#45740302813](https://github.com/crecisol-platform/CRECISOL/actions/runs/45740302813) 3/3 SUCCESS. Los IDs de los seis workflows y tres jobs están en C06_11_MATRIZ_CIERRE.json. Se cumple el gate condicionado del acta C06-10 y del registro vigente; C06-10 ratificada 9.5/10 técnico, no nueva nota estética. Se conserva literalmente su estado histórico condicionado.

## 2. Alcance y trazabilidad

Leer el Word canónico real, 70 párrafos C06, SHA-256 aprobado y preservación del prefijo histórico. Verificar las diez actas y sus blobs de la base; reconciliar catálogo HTTP y contratos, 79 operaciones, 57 autenticadas, 22 proof, 228 celdas, 10 roles/108 capacidades y 25 acciones normativas futuras sin activación. No equivale a certificar funciones no implementadas: gestión/delegación productiva de roles, finanzas, derechos de privacidad, realtime, medios/exportaciones futuras y condición comercial requieren disposición explícita por capítulo y evidencia de su propio alcance. La auditoría final debe mapear requisitos del Maestro frente al código, identificar toda deuda real y resolver P0/P1 antes de una certificación global. No declarar cero P0/P1 por ausencia de registros o sumar actas anteriores.

## 3. Cinco auditorías, siete trabajos independientes

| Auditoría | Entregable y gate |
|---|---|
| A | Maestro y catálogos; diez actas inmutables; evidencia de ratificación C06-10; diff exacto y pruebas negativas del scope. |
| B | Invariantes de autorización, 14 mutaciones de catálogo, mutaciones RBAC/ABAC, siete suites .NET C06, oráculo privacidad y contratos. |
| C | Build .NET10, Angular22/Node24, separación de capas, conservación de las fuentes UI aprobadas, cobertura y validación de arnés. Rendimiento: no afirmar benchmark productivo; revisar límites de consultas, índices y pruebas existentes. |
| D | Siete suites reales MySQL 8.4 en contenedores desechables (C06-02/04/06/07/08/09/10), FK, grants, negativos, colisiones, conservación y reversión conforme a sus contratos. |
| E | Tres trabajos obligatorios: integración real Angular/Chromium/Kestrel/MySQL con las regresiones completas C05+C06-04..10, 921 aserciones/862 verificaciones HTTP/228 celdas; 65 superficies en cinco resoluciones con 439 escenas y comparación PNG contra artefacto original; batería Chromium F02..F06 y modales/estados/accesibilidad. Ninguno sustituye a otro. |

## 4. Calidad visual y aceptación

Baseline aprobado F06 96174cf1d401b66464c02ba3e0d3a0cc2dd88cdf, artefacto original 14862391857 del run 45265008041, 439 PNG/JSON sintéticos. Su disponibilidad se comprobó por API antes de abrir la candidata. El árbol frontend/src cc2530167bf9f4c4546ef431f83590bd6d367846, package-lock y configuración de build siguen idénticos a la base visual; las únicas diferencias frontend históricas son fixtures de tests. Se repite el capturador original vigente y comparador estricto F07 sin cambiar tolerancias: 65 IDs, cinco anchos, todas las escenas, 10 excepciones históricas acotadas a metadatos con contenido idéntico. No se heredará ninguna nota si el comparador falla, faltan capturas o fuente cambia. Publicar PNG originales individuales y matriz; no se afirma inspección editorial directa ni WCAG manual, ni se concede autorización por imagen. Umbral individual 9.5/10, sin promedio compensatorio.

## 5. Cierre y riesgos

Cambios iniciales exclusivamente cuatro rutas QA/documentación/CI. Sin producto, migraciones, permisos, Maestro, main, SOLQARYN, VPS, cobros ni datos reales. Workflow con permisos de lectura, entorno sintético; únicamente el trabajo visual necesita lectura de Actions para el artefacto aprobado. No copiar secretos ni exponer respuestas privadas. Rollback: revertir las cuatro rutas; no altera datos ni contratos.

Candidato requiere los siete jobs SUCCESS, regresiones aplicables, evaluación de hallazgos P0/P1, trazabilidad de todas las exigencias y aceptación técnica del asistente >=9.5/10, visual individual según evidencia, integración exacta en dev y acta final con evidencia posterior. Ninguna nota ni certificado final se emite en esta especificación. Un fallo se corrige y reejecuta, no se relaja el guard. La protección de main no está activa según el endpoint de ramas (no se modifica). C07 permanece bloqueado.
