#!/usr/bin/env python3
"""Politica de seguridad que debe cumplir el compose que llega en cada despliegue.

Contexto (ADR 0011 y 0013): la llave SSH de GitHub Actions solo puede ejecutar el script de entrada, pero el paquete que
entrega incluye los archivos de Compose, y un compose arbitrario equivale a root sobre un servidor que comparte
Docker con PortalCV y Colfe (contenedor privilegiado, montar "/" o el socket de Docker, unirse a la red de otro
proyecto...). Este script lee el compose YA RESUELTO por Docker (`docker compose config --format json`, con las
variables interpoladas) y rechaza todo lo que no sea exactamente lo que este proyecto necesita: una lista blanca,
no una lista negra.

El JSON llega por la ENTRADA ESTANDAR: asi el script no abre ninguna ruta recibida por la linea de comandos (nada
que recorrer ni que sustituir).

    docker compose ... config --format json | politica.py <directorio-de-la-entrega> <registro> <repositorio>

Codigo de salida: 0 si cumple; 1 si hay violaciones (se imprimen una por linea); 2 si se usa mal.

Se instala en el servidor con dueno root (deploy/instalar.sh), fuera de la carpeta donde se extrae el paquete: un
paquete no puede reemplazarlo.
"""
import json
import os
import sys

SERVICIOS = {"db", "migrador", "negocios", "web", "proxy"}
VOLUMENES = {"pgdata", "caddy_data", "caddy_config", "dpkeys"}
RED_COMPARTIDA = "compartida"
ALIAS_PROXY = "gdb-crud-web"
# Claves de servicio que dan privilegios sobre el host o escapan del aislamiento de red
PROHIBIDAS = ("privileged", "network_mode", "pid", "ipc", "uts", "userns_mode", "cap_add", "devices", "sysctls",
              "volumes_from", "extra_hosts", "cgroup_parent", "build", "device_cgroup_rules", "group_add")
MONTAJE_NO_SEGURO = "docker.sock"


def _opciones(nombre, servicio):
    """Claves prohibidas, puertos publicados y security_opt distinto de no-new-privileges."""
    errores = [f"{nombre}: '{clave}' no esta permitido" for clave in PROHIBIDAS if servicio.get(clave)]
    if servicio.get("ports"):
        errores.append(f"{nombre}: no se publican puertos en el host (entra por el nginx compartido)")
    extra = [o for o in (servicio.get("security_opt") or []) if o != "no-new-privileges:true"]
    if extra:
        errores.append(f"{nombre}: security_opt solo admite no-new-privileges:true, no {extra}")
    return errores


def _imagen(nombre, servicio, registro, repositorio):
    """Imagenes propias del registro y repositorio esperados, u oficiales de postgres y caddy."""
    imagen = servicio.get("image", "")
    propias = (f"{registro}/{repositorio}/negocios:", f"{registro}/{repositorio}/web:")
    if imagen.startswith(("postgres:", "caddy:")) or imagen.startswith(propias):
        return []
    return [f"{nombre}: imagen no permitida: {imagen!r}"]


def _montaje(nombre, volumen, directorio):
    tipo = volumen.get("type")
    origen = volumen.get("source", "")
    errores = []
    if MONTAJE_NO_SEGURO in origen:
        errores.append(f"{nombre}: el socket de Docker no se monta")
    if tipo == "bind":
        real = os.path.realpath(origen)
        if real != directorio and not real.startswith(directorio + os.sep):
            errores.append(f"{nombre}: montaje fuera de la entrega: {origen}")
    elif tipo == "volume":
        if origen not in VOLUMENES:
            errores.append(f"{nombre}: volumen no permitido: {origen}")
    elif tipo != "tmpfs":
        errores.append(f"{nombre}: tipo de montaje no permitido: {tipo}")
    return errores


def _montajes(nombre, servicio, directorio):
    """Volumenes con nombre del proyecto, o carpetas DENTRO de la entrega; nunca '/' ni el socket de Docker."""
    errores = []
    for volumen in servicio.get("volumes") or []:
        errores += _montaje(nombre, volumen, directorio)
    return errores


def _redes(nombre, servicio):
    """Solo la red interna; el proxy ademas la compartida, con el alias exacto."""
    errores = []
    for red, cfg in (servicio.get("networks") or {}).items():
        if red == "default":
            continue
        if nombre == "proxy" and red == RED_COMPARTIDA:
            alias = sorted((cfg or {}).get("aliases") or [])
            if alias != [ALIAS_PROXY]:
                errores.append(f"proxy: el alias en la red compartida debe ser exactamente {ALIAS_PROXY}, no {alias}")
        else:
            errores.append(f"{nombre}: red no permitida: {red}")
    return errores


def _servicio(nombre, servicio, directorio, registro, repositorio):
    return (_opciones(nombre, servicio) + _imagen(nombre, servicio, registro, repositorio)
            + _montajes(nombre, servicio, directorio) + _redes(nombre, servicio))


def _globales(config, red_esperada):
    errores = []
    for nombre, cfg in (config.get("networks") or {}).items():
        if nombre == "default":
            continue
        if nombre != RED_COMPARTIDA:
            errores.append(f"red no permitida: {nombre}")
        elif not (cfg or {}).get("external") or cfg.get("name") != red_esperada:
            errores.append(f"la red {RED_COMPARTIDA} debe ser externa y llamarse {red_esperada}")
    for nombre in sorted(set(config.get("volumes") or {}) - VOLUMENES):
        errores.append(f"volumen no permitido: {nombre}")
    # Cadenas que no deben aparecer en ningun lado de la configuracion resuelta
    texto = json.dumps(config)
    for prohibido in (MONTAJE_NO_SEGURO, "/var/run"):
        if prohibido in texto:
            errores.append(f"no se permite referenciar {prohibido}")
    return errores


def revisar(config, directorio, registro, repositorio, red_esperada):
    directorio = os.path.realpath(directorio)
    servicios = config.get("services") or {}
    errores = [f"servicio no permitido: {nombre}" for nombre in sorted(set(servicios) - SERVICIOS)]
    for nombre, servicio in servicios.items():
        errores += _servicio(nombre, servicio, directorio, registro, repositorio)
    return errores + _globales(config, red_esperada)


def main(argumentos):
    if len(argumentos) != 4:
        print(__doc__, file=sys.stderr)
        return 2
    directorio, registro, repositorio = argumentos[1:4]
    red_esperada = os.environ.get("RED_COMPARTIDA", "curriculum-vitae-web_portalcv-net-prod")
    try:
        config = json.load(sys.stdin)
    except json.JSONDecodeError as error:
        print(f"POLITICA: la configuracion de Compose no es un JSON valido: {error}")
        return 1
    errores = revisar(config, directorio, registro, repositorio, red_esperada)
    for error in errores:
        print(f"POLITICA: {error}")
    return 1 if errores else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
