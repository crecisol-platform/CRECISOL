# C14 · Restaurantes, mesas QR y autoservicio — microfases propuestas

**ESTADO: PLAN PRELIMINAR; NO HABILITADO NI CERTIFICADO.** El nombre y orden provienen de `docs/PLAN_37_CAPITULOS.md`. Las unidades aquí son propuesta derivada de arquitectura y seguridad; cotejar primero todo el capítulo y anexos del Word canónico identificado por `docs/MASTER_REFERENCE.md`. El Word no se edita ni sustituye por este archivo.

**Puerta de entrada:** C13 certificado, acta aceptada y CI `dev` verde. **Riesgo clave:** QR público sin identidad privada; permisos en backend y saldos por mesa.

## 11 microfases propuestas, estrictamente secuenciales

| Identificador | Microentregable | Comprobación positiva real exigida | Negativa/bloqueo obligatorio |
|---|---|---|---|
| C14-01 | Modelo de zonas, mesas y turnos | Claves por sucursal y capacidad comprobadas | No cruzar mesa de otro restaurante |
| C14-02 | Asignación temporal de mesa | Estados libre, ocupada y cierre transaccional | No asignación simultánea incompatible |
| C14-03 | Menú público por revisión | Solo artículos publicados y precios autorizados | No filtrar costes o borradores |
| C14-04 | QR firmado/rotado por mesa | Tokens opacos con expiración o rotación | No autorizar por ID predecible |
| C14-05 | Carrito por sesión y contexto | Casos de múltiples comensales aislados | No mezclar pedidos de otras mesas |
| C14-06 | Comandas y cocina | Estados idempotentes y eventos auditables | No duplicar comandas al reintentar |
| C14-07 | División de cuenta y propinas simuladas | Cálculo decimal consistente por participante | No cobrar tarjetas ni mezclar mesas |
| C14-08 | Alergias y notas de pedido | Campos de texto sanitizados y trazables | No ejecutar HTML en cocina |
| C14-09 | Autoservicio accesible y móvil | Flujo QR con teclado y pantalla pequeña | No depender exclusivamente de cámara |
| C14-10 | Seguridad QR y abuso | Reutilización, expiración y BOLA probados | No alterar pedido de otra mesa |
| C14-11 | Auditorías integrales y acta C14 | A/B/C, regresiones y aceptación | No iniciar C15 sin integridad |

## Cierre obligatorio de cada microfase

Solo iniciar una fila tras cerrar la anterior; crear su especificación individual `docs/c14/C14_XX_ESPECIFICACION.md` **en el momento de activarla**, con requisito trazable, directorios, autorizaciones, tests positivos y negativos ejecutados, 3 auditorías A/B/C, evidencia con SHA, PR de alcance mínimo, CI del último HEAD y post-merge en dev, acta, incidentes y rollback. Bloquear con hallazgos P0/P1, pruebas sin ejecutar, secreto o incompatibilidad; no relajar criterios ni hacer despliegues. Arquitectura separada frontend/backend/database; nunca editar SOLQARYN.

**Certificación C14:** 11/11 no basta. Requiere auditoría integral del capítulo con tres perspectivas, regresión transversal, cero P0/P1 sin resolver, acta y aceptación expresa antes de C15. Todo permanece **PENDIENTE** hasta habilitación secuencial.
