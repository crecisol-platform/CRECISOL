# Cierre secuencial de capítulos

## Secuencia
C01 -> C02 -> ... -> C37. Para abrir desarrollo de C(N+1) exigir acta C(N) certificada y aceptada. Se permite leer requisitos futuros para dependencias, sin certificar varios capítulos juntos.

## Microtareas
Cada una: ID, alcance, contratos, rutas, dependencias, datos, PII, auth, riesgo, prueba positiva, prueba negativa, UI y rendimiento, evidencia, rollback y estado.

## Tres auditorías
A: correspondencia párrafo/tablas del maestro -> criterio -> código -> prueba.
B: auth, PII, multitenancy de shared/dedicated, seguridad del navegador y archivos; vulnerabilidades P0/P1 resueltas.
C: clean architecture, rendimiento, accesibilidad, responsive, regresión y mantenibilidad.

## Acta de cierre
Requiere hash exacto del árbol/commit, versiones realmente instaladas y probadas, log de ejecución, pruebas negativas, revisión de cambios, hallazgos 0 P0/P1, plan de reversión y aprobación humana. Sin esas evidencias: DOCUMENTADO, IMPLEMENTADO o BLOQUEADO, nunca CERTIFICADO.
