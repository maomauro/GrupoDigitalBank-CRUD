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
