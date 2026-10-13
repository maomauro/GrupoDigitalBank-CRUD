// Confirmacion de acciones destructivas sin JavaScript en linea (hallazgo H1).
// Un formulario con data-confirmar="mensaje" pide confirmacion antes de enviarse.
// El mensaje se lee del atributo como TEXTO y se pasa a confirm(): nunca se evalua como codigo,
// de modo que un nombre con comillas o etiquetas no puede ejecutar nada.
document.addEventListener("submit", function (evento) {
    var formulario = evento.target;
    if (!(formulario instanceof HTMLFormElement)) return;

    var mensaje = formulario.getAttribute("data-confirmar");
    if (mensaje !== null && !window.confirm(mensaje)) {
        evento.preventDefault();
    }
});

// Lo mismo para enlaces (por ejemplo "Cerrar sesion"): data-confirmar="mensaje" pide confirmacion antes de seguir el
// enlace. El mensaje se lee como TEXTO; nada se evalua como codigo.
//
// Un enlace con data-cerrar-sesion cierra la sesion de Basic Auth SIN el cuadro de usuario y clave del navegador: tras la
// confirmacion hace, en segundo plano, una peticion al enlace con datos invalidos (el navegador reemplaza lo que recordaba y
// ya no los reenvia) y despues va a data-destino. Sin JavaScript, el enlace sigue funcionando como navegacion normal
// (el servidor responde 401 y el navegador muestra su cuadro, que se cancela).
document.addEventListener("click", function (evento) {
    var enlace = evento.target instanceof Element ? evento.target.closest("a[data-confirmar]") : null;
    if (enlace === null) return;

    var mensaje = enlace.getAttribute("data-confirmar");
    if (mensaje !== null && !window.confirm(mensaje)) {
        evento.preventDefault();
        return;
    }

    if (enlace.hasAttribute("data-cerrar-sesion")) {
        evento.preventDefault();
        var destino = enlace.getAttribute("data-destino") || "/";
        var peticion = new XMLHttpRequest();
        peticion.open("GET", enlace.getAttribute("href"), true, "salir", "salir");
        peticion.timeout = 5000;
        peticion.onloadend = function () { window.location.assign(destino); };
        peticion.send();
    }
});

// Tras cerrar sesion, "/?sesion=cerrada" solo sirve para que el servidor muestre el aviso: una vez mostrado se deja la
// direccion limpia ("/") sin recargar la pagina ni perder el aviso.
if (window.location.pathname === "/" && window.location.search === "?sesion=cerrada" && window.history && window.history.replaceState) {
    window.history.replaceState(null, "", "/");
}

// Un aviso con data-autocierre="milisegundos" se desvanece solo pasado ese tiempo (por ejemplo "Sesion cerrada", 5 s).
document.querySelectorAll("[data-autocierre]").forEach(function (aviso) {
    var milisegundos = parseInt(aviso.getAttribute("data-autocierre"), 10);
    if (!(milisegundos > 0)) return;
    window.setTimeout(function () {
        aviso.classList.add("desvaneciendo");
        window.setTimeout(function () { aviso.remove(); }, 400);
    }, milisegundos);
});

// Edad calculada junto a la fecha de nacimiento: data-edad-destino="id" en el input de fecha. "Hoy" es la fecha de
// America/Bogota (como en Negocios), no la del equipo del usuario. Solo orienta; la validacion real es la del servidor.
(function () {
    var entrada = document.querySelector("input[data-edad-destino]");
    if (entrada === null) return;
    var destino = document.getElementById(entrada.getAttribute("data-edad-destino"));
    if (destino === null) return;

    function hoyEnBogota() {
        var partes = new Intl.DateTimeFormat("en-CA", { timeZone: "America/Bogota", year: "numeric", month: "2-digit", day: "2-digit" })
            .format(new Date()).split("-");
        return { a: +partes[0], m: +partes[1], d: +partes[2] };
    }

    function actualizar() {
        var v = /^(\d{4})-(\d{2})-(\d{2})$/.exec(entrada.value);
        var hoy = hoyEnBogota();
        var edad = null;
        if (v !== null && +v[1] >= 1900) {
            edad = hoy.a - +v[1];
            if (hoy.m < +v[2] || (hoy.m === +v[2] && hoy.d < +v[3])) edad--;
        }
        if (edad === null || edad < 0) {
            destino.hidden = true;
            destino.textContent = "";
            return;
        }
        destino.textContent = "Edad: " + edad + (edad === 1 ? " año" : " años");
        destino.hidden = false;
    }

    // Limites del propio campo (el calendario no deja elegir fechas futuras); el servidor valida igual
    var h = hoyEnBogota();
    entrada.min = "1900-01-01";
    entrada.max = h.a + "-" + String(h.m).padStart(2, "0") + "-" + String(h.d).padStart(2, "0");

    entrada.addEventListener("input", actualizar);
    entrada.addEventListener("change", actualizar);
    actualizar();
})();
