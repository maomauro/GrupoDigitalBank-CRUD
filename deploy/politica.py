#!/usr/bin/env python3
"""Politica de seguridad que debe cumplir el compose que llega en cada despliegue.

Contexto (ADR 0011): la llave SSH de GitHub Actions solo puede ejecutar el script de entrada, pero el paquete que
entrega incluye los archivos de Compose, y un compose arbitrario equivale a root sobre un servidor que comparte
Docker con PortalCV y Colfe (contenedor privilegiado, montar "/" o el socket de Docker, unirse a la red de otro
proyecto...). Este script lee el compose YA RESUELTO por Docker (`docker compose config --format json`, con las
variables interpoladas) y rechaza todo lo que no sea exactamente lo que este proyecto necesita: una lista blanca,
no una lista negra.

Uso: politica.py <config.json> <directorio-de-la-entrega> <registro> <repositorio>
Codigo de salida: 0 si cumple; 1 si hay violaciones (se imprimen una por linea).

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


def revisar(config, directorio, registro, repositorio, red_esperada):
    errores = []
    directorio = os.path.realpath(directorio)

    servicios = config.get("services", {})
    for nombre in sorted(set(servicios) - SERVICIOS):
        errores.append(f"servicio no permitido: {nombre}")

    for nombre, s in servicios.items():
        for clave in PROHIBIDAS:
            if s.get(clave):
                errores.append(f"{nombre}: '{clave}' no esta permitido")
        if s.get("ports"):
            errores.append(f"{nombre}: no se publican puertos en el host (entra por el nginx compartido)")

        extra = [o for o in (s.get("security_opt") or []) if o != "no-new-privileges:true"]
        if extra:
            errores.append(f"{nombre}: security_opt solo admite no-new-privileges:true, no {extra}")

        # Imagenes: las de este proyecto en el registro esperado, o las oficiales de postgres y caddy
        imagen = s.get("image", "")
        propias = [f"{registro}/{repositorio}/negocios:", f"{registro}/{repositorio}/web:"]
        if not (imagen.startswith(("postgres:", "caddy:")) or imagen.startswith(tuple(propias))):
            errores.append(f"{nombre}: imagen no permitida: {imagen!r}")

        # Montajes: volumenes con nombre del proyecto, o carpetas DENTRO de la entrega; nunca "/" ni el socket
        for v in s.get("volumes", []) or []:
            tipo = v.get("type")
            origen = v.get("source", "")
            if tipo == "bind":
                real = os.path.realpath(origen)
                if not (real == directorio or real.startswith(directorio + os.sep)):
                    errores.append(f"{nombre}: montaje fuera de la entrega: {origen}")
            elif tipo == "volume":
                if origen not in VOLUMENES:
                    errores.append(f"{nombre}: volumen no permitido: {origen}")
            elif tipo != "tmpfs":
                errores.append(f"{nombre}: tipo de montaje no permitido: {tipo}")
            if "docker.sock" in origen:
                errores.append(f"{nombre}: el socket de Docker no se monta")

        # Redes: solo la interna del proyecto; el proxy ademas la compartida con el alias esperado
        for red, cfg in (s.get("networks") or {}).items():
            if red == "default":
                continue
            if nombre == "proxy" and red == RED_COMPARTIDA:
                alias = sorted((cfg or {}).get("aliases") or [])
                if alias != [ALIAS_PROXY]:
                    errores.append(f"proxy: el alias en la red compartida debe ser exactamente {ALIAS_PROXY}, no {alias}")
            else:
                errores.append(f"{nombre}: red no permitida: {red}")

    redes = config.get("networks", {}) or {}
    for nombre, cfg in redes.items():
        if nombre == "default":
            continue
        if nombre != RED_COMPARTIDA:
            errores.append(f"red no permitida: {nombre}")
        elif not (cfg or {}).get("external") or cfg.get("name") != red_esperada:
            errores.append(f"la red {RED_COMPARTIDA} debe ser externa y llamarse {red_esperada}")

    for nombre in sorted(set(config.get("volumes", {}) or {}) - VOLUMENES):
        errores.append(f"volumen no permitido: {nombre}")

    # Cadenas que no deben aparecer en ningun lado de la configuracion resuelta
    texto = json.dumps(config)
    for prohibido in ("docker.sock", "/var/run"):
        if prohibido in texto:
            errores.append(f"no se permite referenciar {prohibido}")

    return errores


def main(argumentos):
    if len(argumentos) != 5:
        print(__doc__, file=sys.stderr)
        return 2
    ruta, directorio, registro, repositorio = argumentos[1:5]
    red_esperada = os.environ.get("RED_COMPARTIDA", "curriculum-vitae-web_portalcv-net-prod")
    with open(ruta, encoding="utf-8") as f:
        config = json.load(f)
    errores = revisar(config, directorio, registro, repositorio, red_esperada)
    for e in errores:
        print(f"POLITICA: {e}")
    return 1 if errores else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
