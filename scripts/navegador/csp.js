// Comprobacion en un Chrome real: CSP sin violaciones, CSS/JS externos operativos y regresion H1 con clic real.
const puppeteer = require('puppeteer-core');

// Ruta del navegador: la pasa scripts/probar-navegador.ps1 por CHROME_PATH (Chrome o Edge)
const CHROME = process.env.CHROME_PATH || String.raw`C:\Program Files\Google\Chrome\Application\chrome.exe`;
const BASE = process.argv[2] || 'https://localhost';
const USUARIO = process.argv[3] || 'admin';
const CLAVE = process.env.CLAVE_WEB;
if (!CLAVE) {
  console.error('Defina la variable de entorno CLAVE_WEB con la clave de acceso a la web.');
  process.exit(2);
}
const CONTROL = process.argv[4];   // URL del control positivo (pagina que SI viola la CSP); opcional

let fallos = 0;

// Una comprobacion es [cumple, descripcion, detalle]; se acumulan y se informan juntas para poder
// ejecutar pruebas independientes en paralelo sin mezclar la salida.
const comprobar = (cumple, texto, extra = '') => [Boolean(cumple), texto, extra];

function informar(comprobaciones) {
  for (const [cumple, texto, extra] of comprobaciones) {
    if (!cumple) {
      fallos++;
    }
    console.log(`${cumple ? 'OK   ' : 'FALLA'} ${texto} ${extra}`);
  }
}

async function nuevaPagina(browser, autenticar = true) {
  const page = await browser.newPage();
  if (autenticar) {
    await page.authenticate({ username: USUARIO, password: CLAVE });
  }
  await page.evaluateOnNewDocument(() => {
    window.__violaciones = [];
    document.addEventListener('securitypolicyviolation', e =>
      window.__violaciones.push(`${e.violatedDirective} -> ${e.blockedURI || 'inline'}`));
  });
  page.__consola = [];
  page.on('console', m => page.__consola.push(`${m.type()}: ${m.text()}`));
  page.on('pageerror', e => page.__consola.push(`pageerror: ${e.message}`));
  return page;
}

const violaciones = page => page.evaluate(() => window.__violaciones);

// ---- Control positivo: el detector debe ver violaciones en una pagina que SI las provoca ----
async function controlPositivo(browser) {
  const p = await nuevaPagina(browser, false);
  await p.goto(CONTROL, { waitUntil: 'load' });
  const v = await violaciones(p);
  await p.close();
  return [comprobar(v.length >= 2,
    'CONTROL POSITIVO: el detector registra violaciones en una pagina con estilo y script en linea',
    `(${v.length}: ${v.join(' | ')})`)];
}

// ---- Una pagina real: sin violaciones y con CSS/JS externos operativos ----
async function probarRuta(browser, ruta) {
  const page = await nuevaPagina(browser);
  const resp = await page.goto(BASE + ruta, { waitUntil: 'networkidle0' });
  const v = await violaciones(page);
  const cspConsola = page.__consola.filter(m => /content security policy|refused to/i.test(m));
  // El CSS externo se aplico: el encabezado no tiene el fondo transparente por omision del navegador
  const estilo = await page.evaluate(() => {
    const h = document.querySelector('header');
    return h ? { fondo: getComputedStyle(h).backgroundColor } : null;
  });
  const scriptCargado = await page.evaluate(() => [...document.scripts].some(s => s.src.endsWith('/js/site.js')));
  await page.close();

  return [
    comprobar(resp.status() === 200, `GET ${ruta} responde 200`, `(HTTP ${resp.status()})`),
    comprobar(v.length === 0, `GET ${ruta}: ninguna violacion de la CSP`, v.length ? `(${v.join(' | ')})` : ''),
    comprobar(cspConsola.length === 0, `GET ${ruta}: sin mensajes de CSP en la consola`, cspConsola.join(' | ')),
    comprobar(estilo?.fondo !== undefined && estilo.fondo !== 'rgba(0, 0, 0, 0)', `GET ${ruta}: el CSS externo se aplica`, `(fondo del encabezado: ${estilo?.fondo})`),
    comprobar(scriptCargado, `GET ${ruta}: el script externo site.js esta cargado`)
  ];
}

// ---- Regresion H1 con CLIC REAL: un nombre malicioso no ejecuta nada ----
async function regresionH1(browser) {
  const resultados = [];
  const payload = "x');alert(1);//";
  const page = await nuevaPagina(browser);
  const dialogos = [];
  let aceptar = false;
  page.on('dialog', async d => {
    dialogos.push({ tipo: d.type(), mensaje: d.message() });
    if (aceptar) {
      await d.accept();
    } else {
      await d.dismiss();
    }
  });

  await page.goto(BASE + '/Usuarios/Registrar', { waitUntil: 'networkidle0' });
  await page.type('input[name="Nombre"]', payload);
  await page.evaluate(() => { document.querySelector('input[name="FechaNacimiento"]').value = '1990-01-01'; });
  await page.select('select[name="Sexo"]', 'M').catch(() => page.click('input[name="Sexo"][value="M"]'));
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('button[type="submit"]')]);
  resultados.push(comprobar(dialogos.length === 0, 'H1: registrar el usuario con el nombre malicioso no abre ningun dialogo', `(${dialogos.length})`));

  const fila = () => page.evaluateHandle(p => [...document.querySelectorAll('tbody tr')].find(tr => tr.children[1].textContent === p) || null, payload);
  let f = await fila();
  resultados.push(
    comprobar(f.asElement() !== null, 'H1: el nombre malicioso aparece en la grilla como TEXTO (no se ejecuto)', `(${payload})`),
    comprobar(dialogos.length === 0, 'H1: cargar la grilla con el nombre malicioso NO ejecuta alert(1)', `(dialogos: ${dialogos.length})`));

  // Clic en Eliminar y CANCELAR: debe haber exactamente un confirm con el nombre literal, y el usuario sigue ahi
  await (await f.asElement().$('button.peligro')).click();
  await new Promise(r => setTimeout(r, 600));
  resultados.push(
    comprobar(dialogos.length === 1 && dialogos[0].tipo === 'confirm', 'H1: el clic en Eliminar abre UN solo dialogo y es un confirm (ningun alert)', JSON.stringify(dialogos.map(d => d.tipo))),
    comprobar(dialogos[0]?.mensaje === `¿Eliminar a ${payload}?`, 'H1: el confirm muestra el nombre literal', `(${dialogos[0]?.mensaje})`));
  f = await fila();
  resultados.push(comprobar(f.asElement() !== null, 'H1: al CANCELAR el confirm el usuario NO se elimina'));

  // Clic en Eliminar y ACEPTAR: se elimina solo ese usuario (el de prueba)
  aceptar = true;
  dialogos.length = 0;
  const botonEliminar = await f.asElement().$('button.peligro');
  await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), botonEliminar.click()]);
  resultados.push(comprobar(dialogos.length === 1 && dialogos[0].tipo === 'confirm', 'H1: al aceptar, sigue habiendo un unico dialogo confirm'));
  f = await fila();
  resultados.push(comprobar(f.asElement() === null, 'H1: al ACEPTAR el confirm el usuario de prueba se elimina'));
  const v = await violaciones(page);
  resultados.push(comprobar(v.length === 0, 'H1: ninguna violacion de la CSP durante todo el flujo', v.join(' | ')));
  const restantes = await page.evaluate(() => [...document.querySelectorAll('tbody tr')].map(tr => tr.children[1].textContent));
  resultados.push(comprobar(true, 'H1: usuarios que quedan en la grilla', JSON.stringify(restantes)));
  await page.close();
  return resultados;
}

(async () => {
  const browser = await puppeteer.launch({
    executablePath: CHROME, headless: 'new',
    args: ['--ignore-certificate-errors', '--no-first-run', '--no-default-browser-check']
  });
  try {
    if (CONTROL) {
      informar(await controlPositivo(browser));
    }
    // Las paginas son independientes entre si: se prueban en paralelo y se informan en orden
    const rutas = ['/', '/Usuarios/Registrar', '/Usuarios/Editar/12'];
    for (const resultado of await Promise.all(rutas.map(ruta => probarRuta(browser, ruta)))) {
      informar(resultado);
    }
    // El flujo de H1 crea y elimina un usuario: va despues y en solitario
    informar(await regresionH1(browser));
  } finally {
    await browser.close();
  }
  console.log(fallos === 0 ? '\nNavegador: todas las comprobaciones pasaron.' : `\nNavegador: ${fallos} comprobacion(es) fallaron.`);
  process.exit(fallos === 0 ? 0 : 1);
})().catch(e => {
  console.error('ERROR', e);
  process.exit(2);
});
