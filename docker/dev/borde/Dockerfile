# SOLO DESARROLLO. Imagen del servicio "borde" (docker-compose.dev.yml, perfil con-proxy): un nginx con openssl que
# genera un certificado autofirmado al arrancar. Nunca se construye en CI ni se despliega.
FROM nginx:stable-alpine
RUN apk add --no-cache openssl
COPY entrypoint.sh /usr/local/bin/borde-entrypoint.sh
ENTRYPOINT ["/bin/sh", "/usr/local/bin/borde-entrypoint.sh"]
