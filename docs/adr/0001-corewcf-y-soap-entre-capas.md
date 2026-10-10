# ADR 0001: CoreWCF y SOAP entre las capas de presentación y negocios

- **Estado:** Aceptada
- **Fecha:** 2026-10-10 (documenta la decisión original del proyecto)
- **Referencias:** especificación del proyecto, [arquitectura](../arquitectura.md), hallazgos B2 y B8

## Contexto

La especificación pide una arquitectura por capas en la que la **presentación solo conversa con la capa de negocios a través de un servicio** (estilo WCF), y que solo negocios toque la base de datos. El proyecto es .NET 10 y se despliega en contenedores Linux: **WCF de .NET Framework no existe en ese entorno**, y el WCF clásico no tiene servidor en .NET moderno.

## Decisión

Exponer la lógica de negocio como un servicio SOAP con **CoreWCF 1.9.1** y `BasicHttpBinding` (SOAP 1.1 sobre HTTP, red interna), con contrato explícito (`[ServiceContract]`, `[DataContract]`) y errores tipados mediante `[FaultContract(typeof(ErrorServicio))]`. Presentación consume el servicio con `ChannelFactory` de `System.ServiceModel.Http`.

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| REST o gRPC entre capas | Cumpliría el aislamiento, pero la especificación pide un servicio con contrato al estilo WCF; REST/gRPC cambiarían el estilo pedido |
| Referenciar el proyecto Negocios desde Presentación | Más simple, pero **rompe el aislamiento de capas** que es el objetivo del ejercicio |
| WCF sobre .NET Framework | No corre en contenedores Linux ni en .NET 10 |

## Consecuencias

- ➕ Contrato explícito y versionable; errores de negocio tipados que la presentación traduce a mensajes amigables; el servicio no se expone al exterior.
- ➕ Cada capa se puede probar y desplegar por separado.
- ➖ Hay que mantener un contrato **duplicado** en Presentación ([ADR 0003](0003-contrato-soap-duplicado.md)).
- ➖ `BasicHttpBinding` va sin cifrado de transporte: es aceptable porque Negocios solo es alcanzable dentro de la red de Docker y el proxy no lo enruta. Si algún día se expusiera, habría que pasar a HTTPS (el cliente ya elige `Transport` cuando la URL es `https`).
- ➖ `CoreWCF.Primitives` arrastra `System.Security.Cryptography.Xml` 8.0.x con una vulnerabilidad conocida (NU1903). En la aplicación queda **podado** por el framework `Microsoft.AspNetCore.App` y no llega a la imagen; los proyectos de prueba referencian ese framework por la misma razón. Vigilar con cada versión nueva de CoreWCF.
- ➖ El WSDL expone el contrato completo: solo se publica en `Development` (hallazgo B8, verificado por pruebas).
- 🔁 Revisar si el servicio debiera consumirse desde otros clientes que no sean .NET.
