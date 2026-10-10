// Valida los diagramas Mermaid de uno o varios .md: los analiza y los DIBUJA con el motor real dentro de Chrome.
// Uso (lo invoca scripts/verificar-docs.ps1):  node mermaid-check.js archivo1.md [archivo2.md ...]
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const path = require('path');

const CHROME = process.env.CHROME_PATH || String.raw`C:\Program Files\Google\Chrome\Application\chrome.exe`;
const archivos = process.argv.slice(2);
const motor = fs.readFileSync(path.join(__dirname, 'node_modules', 'mermaid', 'dist', 'mermaid.min.js'), 'utf8');

function bloques(archivo) {
  const md = fs.readFileSync(archivo, 'utf8');
  const lista = [];
  for (const m of md.matchAll(/```mermaid\r?\n([\s\S]*?)```/g)) {
    lista.push({ archivo, linea: md.slice(0, m.index).split('\n').length, texto: m[1] });
  }
  return lista;
}

// Analiza y dibuja un diagrama dentro de la pagina; devuelve un objeto serializable
function procesarEnPagina(texto, id) {
  return mermaid.parse(texto)
    .then(() => mermaid.render(id, texto))
    .then(({ svg }) => ({ ok: true, tipo: texto.trim().split(/\s/)[0], nodos: (svg.match(/class="node/g) || []).length }))
    .catch(e => ({ ok: false, error: String((e && e.message) || e) }));
}

async function main() {
  const todos = archivos.flatMap(bloques);
  const browser = await puppeteer.launch({ executablePath: CHROME, headless: 'new' });
  let fallos = 0;
  try {
    const page = await browser.newPage();
    await page.setContent('<html><body></body></html>');
    await page.addScriptTag({ content: motor });
    await page.evaluate(() => mermaid.initialize({ startOnLoad: false, securityLevel: 'strict' }));

    // Mermaid dibuja de uno en uno: se encadenan las promesas en lugar de esperar dentro de un bucle
    const resultados = await todos.reduce(async (previo, b, i) => {
      const acumulado = await previo;
      const r = await page.evaluate(procesarEnPagina, b.texto, `diagrama${i}`);
      return [...acumulado, { b, r }];
    }, Promise.resolve([]));

    for (const { b, r } of resultados) {
      if (!r.ok) {
        fallos++;
      }
      const detalle = r.ok ? `${r.tipo}, ${r.nodos} nodos` : r.error.split('\n')[0];
      console.log(`${r.ok ? 'OK   ' : 'FALLA'} ${path.basename(b.archivo)}:${b.linea} ${detalle}`);
    }
  } finally {
    await browser.close();
  }
  console.log(`\n${todos.length} diagrama(s), ${fallos} con error.`);
  return fallos === 0 ? 0 : 1;
}

main().then(codigo => process.exit(codigo)).catch(e => {
  console.error('ERROR', e);
  process.exit(2);
});
