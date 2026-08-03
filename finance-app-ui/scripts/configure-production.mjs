import { writeFile } from 'node:fs/promises';

const apiUrl = process.env.API_URL?.replace(/\/$/, '');
if (!apiUrl || !/^https:\/\//.test(apiUrl)) {
  throw new Error('Defina API_URL com a URL HTTPS pública da API, incluindo /api.');
}

const target = new URL('../src/environments/environment.prod.ts', import.meta.url);
const source = `export const environment = {\n  production: true,\n  apiUrl: '${apiUrl.replace(/'/g, "\\'")}'\n};\n`;
await writeFile(target, source, 'utf8');
