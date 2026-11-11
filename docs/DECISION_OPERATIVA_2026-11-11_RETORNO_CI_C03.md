# CRECISOL — Decisión posterior: intentar restitución de GitHub Actions para C03-10

**Fecha:** 11-11-2026, posterior a la decisión autónoma sin Actions del mismo día.
**Autoridad:** mensaje expreso del propietario: «HAY QUE INTENTAR ... CON EL CIERRE DE LA MICROFASE 10 DEL C03 ... YA DEJE EL REPOSITORIO PUBLICO Y GITHUB ACTIONS AUTOMATICOS».

**Identidad verificada por API:** solqaryn-platform/CRECISOL, visibility=public, rama principal operativa dev. Esta decisión se aplica **únicamente** a CRECISOL.

## Modificación prospectiva, sin reescribir el histórico

El propietario ha cambiado expresamente la instrucción anterior que no usaba GitHub Actions. **Para el intento C03-10**, se autorizan PR, validaciones automáticas GitHub Actions, auditorías A/B/C y comprobación PREMERGE/POSTMERGE en runners estándar públicos. Esta decisión posterior prevalece sobre la prohibición operativa de usar CI de la decisión DECISION_OPERATIVA_2026-11-11_AUTONOMIA_SIN_GITHUB_ACTIONS.md, solo en lo relativo al uso de CI para C03-10. No reinterpreta ejecuciones fallidas anteriores ni las pruebas locales de Codex.

No implica autorización de despliegues, compras, producción, dependencias externas, cobros ni modificaciones de solqaryn/Solqaryn. Todo workflow debe usar permisos mínimos y no usar secretos productivos. No se utilizarán ejecuciones omitidas como prueba.

**Puerta estricta:** no fusionar o certificar por el solo cambio de visibilidad. Se debe observar una corrida PREMERGE exitosa del SHA concreto, sin P0/P1, y luego evidencia POSTMERGE del SHA integrado, más acta con tres auditorías y aceptación expresa del capítulo. El resultado de este intento se registrará sin falsificar éxito ni declarar certificación anticipada.

**Estado a la apertura:** C03-09 = certificada en su alcance; C03-10 = pendiente; C03 = no certificado; C04 = bloqueado.
