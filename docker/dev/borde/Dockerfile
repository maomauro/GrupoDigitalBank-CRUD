# SOLO DESARROLLO. Imagen del servicio "borde" (docker-compose.dev.yml, perfil con-proxy): un nginx con openssl que
# genera un certificado autofirmado al arrancar. Nunca se construye en CI ni se despliega.
# Corre SIN privilegios (usuario nginx): escucha en los puertos 80 y 443 sin ser root porque Docker permite a los
# contenedores abrir puertos bajos (net.ipv4.ip_unprivileged_port_start=0), y solo escribe en carpetas que le pertenecen.
FROM nginx:stable-alpine
# Sin root: el PID va a /tmp (en /run no puede escribir) y se quita la directiva "user", que solo vale para root
RUN apk add --no-cache openssl \
    && mkdir -p /etc/nginx/ssl \
    && chown -R nginx:nginx /etc/nginx/ssl /etc/nginx/conf.d /var/cache/nginx \
    && sed -i -e 's#/run/nginx.pid#/tmp/nginx.pid#' -e '/^user /d' /etc/nginx/nginx.conf
COPY entrypoint.sh /usr/local/bin/borde-entrypoint.sh
USER nginx
ENTRYPOINT ["/bin/sh", "/usr/local/bin/borde-entrypoint.sh"]
