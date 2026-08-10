import { writeFile } from 'node:fs/promises';

// API_URL can override this deployed API address in Vercel.
const defaultApiUrl = 'https://finance-4vj8.onrender.com/api';
const apiUrl = (process.env.API_URL || defaultApiUrl).replace(/\/$/, '');
if (!apiUrl || !/^https:\/\//.test(apiUrl)) {
  throw new Error('Defina API_URL com a URL HTTPS pública da API, incluindo /api.');
}

const target = new URL('../src/environments/environment.prod.ts', import.meta.url);
const source = `export const environment = {\n  production: true,\n  apiUrl: '${apiUrl.replace(/'/g, "\\'")}'\n};\n`;
await writeFile(target, source, 'utf8');
