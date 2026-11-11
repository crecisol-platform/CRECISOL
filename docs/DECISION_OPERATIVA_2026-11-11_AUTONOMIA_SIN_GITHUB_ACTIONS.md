# CRECISOL — Decisión operativa: desarrollo y certificación autónomos SIN GitHub Actions

**Fecha de decisión:** 11 de noviembre de 2026, horario de Honduras.

**Autoridad de la decisión:** instrucción expresa del propietario en conversación CRECISOL: «GitHub solo será nuestro repositorio ... nada de estar dependiendo de pruebas de CI de GitHub Actions»; el propietario delega en el asistente la programación, revisión, aprobación y decisión técnica de cierre, con tres auditorías más estrictas.

**Repositorio único:** `solqaryn-platform/CRECISOL`. **Nunca modificar `solqaryn/Solqaryn`.**

## 1. Cambio de gobierno prospectivo, sin reescribir el pasado

**Vigente desde esta decisión:** GitHub se utiliza solamente como alojamiento, lectura y versionamiento de código/documentación, con comprobaciones de SHA y diffs. **No se exige ni se utilizará GitHub Actions como requisito de desarrollo, aprobación, integración o certificación**. No solicitar nuevas cuentas, minutos, planes de pago, runners, despliegues o una PC del propietario para superar un gate CI.

Este mandato **sustituye prospectivamente** cualquier exigencia anterior de `CI PREMERGE`/`CI POSTMERGE` o `completed/success` contenida en las instrucciones operativas, actas preparatorias o protocolos de CRECISOL. Los SHA y resultados CI **históricos** mantienen su significado original y no se alteran ni se inventan ejecuciones nuevas.

Se conserva el control de versiones: ramas `feature/`, `dev` y `main` con diferencias registradas. Un PR es opcional y **no será una puerta obligatoria**; no se abrirán PR ni se relanzará Actions solo para conseguir un sello verde. Una operación de Git podría disparar workflows que ya existen mientras la configuración de Actions siga habilitada; **no se tomarán sus resultados como requisito ni se afirmará que fueron desactivados desde este documento**. El apagado efectivo de Actions requiere control autorizado de configuración del repositorio, no falsificación ni borrado apresurado de workflows históricos.

## 2. Delegación explícita y límites reales

El propietario autoriza al asistente a **diseñar, programar, revisar, corregir, auditar, aprobar cierres técnicos y decidir certificaciones** de microfases, autónomamente y en secuencia C01→C37, sin pedir una aprobación humana nueva en cada microfase, siempre que las pruebas y los criterios de calidad estén realmente satisfechos.

Esta delegación **no** permite:
- afirmar que se hizo una compilación o una prueba que no se ejecutó, o inventar salida, métricas, firmas o hashes;
- pasar una falla P0/P1, saltarse pruebas esenciales de aislamiento multitenant o marcar `CERTIFICADA` una implementación solo revisada estáticamente si su validez depende de un runtime no disponible;
- habilitar producción, VPS, pagos, compras, servicios externos o automatizaciones permanentes;
- modificar el proyecto SOLQARYN ni su Skill;
- fingir ejecución en segundo plano: el trabajo se realiza en la interacción actual con herramientas realmente disponibles, sin prometer trabajo asincrónico.

## 3. Tres auditorías obligatorias, independientes de GitHub

**A — Requisitos, integridad y trazabilidad:** identificar la versión real del Documento Maestro Único y su huella; capítulo/microfase vigente, requisitos, alcance, dependencias, SHA del código y delta respecto de `dev`; validar invariantes históricos, parametrización y rollback. Hacer una revisión cruzada de rutas afectadas, documentación y pruebas.

**B — Seguridad adversarial y aislamiento:** modelar amenazas, secretos, exposición de datos, autorización denegada por defecto y tenancy; ejecutar pruebas negativas disponibles, revisar casos de abuso BOLA/IDOR, sucursales, archivos, caches, outbox, logs, credenciales y fallos de entrada. Ninguna prueba sintética se presenta como aislamiento real de una función privada aún no implementada.

**C — Arquitectura, funcionamiento, rendimiento y calidad visual:** verificar Clean Architecture, contratos y separación Angular/.NET/MySQL; ejecutar tests y builds con toolchains **realmente disponibles** y capturar la salida/exit code; comprobar, cuando aplique, DB MySQL 8.4 aislada, rendimiento, UX, accesibilidad y regresiones. Declarar con precisión si el resultado es análisis estático, simulación de test o ejecución real.

Las tres auditorías deben registrar método, evidencia, defectos encontrados, correcciones, reruns afectados, riesgos residuales y resultado explícito. **La rigurosidad aumenta; se retira exclusivamente la dependencia de GitHub Actions y de la PC del propietario.**

## 4. Puerta de certificación autónoma

1. Antes de escribir: comprobar repositorio autorizado, rama, SHA, documentos fuente, diff y restricciones de seguridad.
2. Implementar el alcance **de una microfase por vez**. Revisar código, seguridad, dependencias, estado de Git y regresiones.
3. Ejecutar las comprobaciones **posibles en el entorno propio del asistente**, sin exigir hardware del propietario ni Actions. Guardar evidencia reproducible y no exponer secretos. Si no existe .NET, Docker, MySQL, navegador o toolchain requerida, marcarla **NO EJECUTADA**.
4. Corregir todos los hallazgos P0/P1; completar A, B y C con conclusiones independientes y revisar de nuevo el SHA exacto del cambio.
5. Emitir acta con `CERTIFICADA` **solo cuando** se dispone de evidencia real suficiente de los controles **aplicables** a la microfase y no quedan fallos críticos. La CI de GitHub **no forma parte del gate**. La firma técnica es una decisión documentada del asistente bajo delegación del propietario; no se inventa una firma humana o criptográfica.
6. Si faltan pruebas esenciales por falta de herramientas o evidencia, usar `CERRADA TÉCNICAMENTE — CERTIFICACIÓN PENDIENTE` o `BLOQUEADA POR VALIDACIÓN`, especificando exactamente el riesgo y el último resultado real. Se puede adelantar análisis y tareas no dependientes **de la misma microfase/capítulo**, pero no afirmar certificado ni abrir el siguiente capítulo antes de cerrar el actual.
7. Integrar cambios al repositorio únicamente después de revisar rutas, SHA y reversibilidad. GitHub funciona como almacenamiento y control de versión, no como juez de calidad.

## 5. Estado transitorio concreto: C03-09

El usuario comunicó resultados locales anteriores, ejecutados por Codex cuando contaba con acceso a su equipo, para el SHA técnico `53cc2134eca8b33c808bcd5d7643684b51080fa8`: 9/9 builds reales, MySQL 8.4 `--network none`, A/B/C completadas y 16 evidencias con SHA-256 verificadas por Codex. El propietario aceptó posteriormente su **cierre técnico**. Esta historia no se borra ni se presenta como una nueva ejecución del asistente.

**Estado conservador de transición:** `C03-09 CERRADA TÉCNICAMENTE / NO CERTIFICADA` mientras se evalúa bajo el nuevo gate autónomo la suficiencia de esa evidencia y los cambios posteriores; CI GitHub **no es obstáculo ni requisito**. C03-10 no queda certificada por esta decisión y C04 continúa fuera de alcance.

**PR histórico de integración #74:** cerrado sin merge bajo este cambio de procedimiento; la rama técnica queda preservada. No se usará el PR como gate de pruebas. `dev`/ `main` no se alteraron al cerrarlo.

## 6. Precedencia y continuidad

Esta **decisión operativa prospectiva** complementa —sin reescribir ni sustituir el contenido funcional del Word— el Documento Maestro Único. Ante instrucciones anteriores que exijan **específicamente** una CI de GitHub o una firma humana por microfase, esta delegación explícita posterior las sustituye solamente para el proceso de certificación; no modifica las obligaciones de **prueba real, seguridad y tres auditorías**.

La evidencia histórica y certificaciones C01/C02/C03-01..08 permanecen inmutables. El desarrollo no se traslada a SOLQARYN, no se vuelve público el repositorio ni se configura producción.
