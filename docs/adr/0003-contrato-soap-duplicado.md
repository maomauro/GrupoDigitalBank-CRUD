# ADR 0003: El contrato SOAP se duplica a mano en Presentación

- **Estado:** Aceptada (con riesgo conocido y mitigación pendiente)
- **Fecha:** 2026-10-10
- **Referencias:** hallazgo B2 (issue [#19](https://github.com/maomauro/GrupoDigitalBank-CRUD/issues/19)), [ADR 0001](0001-corewcf-y-soap-entre-capas.md)

## Contexto

Presentación necesita conocer el contrato del servicio (operaciones y tipos) para llamarlo, pero **no puede referenciar el proyecto Negocios** (aislamiento de capas, [ADR 0001](0001-corewcf-y-soap-entre-capas.md)). Hay que decidir cómo se comparte ese contrato.

## Decisión

Mantener en `Presentacion/Servicios/ContratoServicio.cs` un **espejo escrito a mano** del contrato de Negocios, con el mismo `Name` y `Namespace` (`http://grupodigitalbank.com/usuarios/2026`), de modo que el SOAP en el cable sea idéntico.

## Alternativas consideradas

| Alternativa | Por qué no (por ahora) |
|---|---|
| Proyecto compartido `Contratos` referenciado por ambas capas | Simple y sin desfase, pero **acopla** las capas por un ensamblado común; es una opción razonable si el contrato crece |
| Generar el cliente desde el WSDL (`svcutil` / referencia de servicio) | El WSDL solo se publica en desarrollo ([ADR 0001](0001-corewcf-y-soap-entre-capas.md)), y la generación añade un paso de compilación frágil |

## Consecuencias

- ➕ Aislamiento total entre capas; el contrato es corto y estable (4 operaciones y 3 tipos).
- ➖ **Riesgo de desfase:** cambiar un `DataContract` en un lado y olvidar el otro rompe la comunicación en ejecución, no en compilación.
- 🛡️ Mitigación: la regla está escrita en [`CLAUDE.md`](../../CLAUDE.md) («al cambiar `Negocios/Contratos/*`, actualizar también `ContratoServicio.cs`»). **Resuelto (#19):** `tests/GrupoDigitalBank.Contrato.Tests` compara los dos contratos por reflexión y ejecuta el cliente real contra el servicio real de Negocios; falla si divergen.
- 🔁 Revisar y pasar a un proyecto de contratos compartido si el contrato supera ~10 operaciones o si el desfase ocurre una vez.
