import { existsSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';

const dist = resolve(process.cwd(), 'dist');
const html = readFileSync(resolve(dist, 'index.html'), 'utf8');
const references = [...html.matchAll(/(?:src|href)="([^"]+\.(?:js|css))"/g)].map((match) => match[1]);

if (references.length < 2) throw new Error('构建产物没有找到 CSS/JS 资源引用');
for (const reference of references) {
  if (!reference.startsWith('./assets/')) throw new Error(`资源不是 Pages 安全的相对路径: ${reference}`);
  const assetPath = resolve(dist, reference.slice(2));
  if (!existsSync(assetPath)) throw new Error(`构建资源不存在: ${reference}`);
  if (reference.endsWith('.js')) {
    const chunk = readFileSync(assetPath, 'utf8');
    const lazyChunks = [...chunk.matchAll(/["'](\.\/[^"']+\.js)["']/g)].map((match) => match[1]);
    for (const lazyChunk of lazyChunks) {
      if (!existsSync(resolve(dirname(assetPath), lazyChunk))) throw new Error(`延迟加载资源不存在: ${lazyChunk}`);
    }
  }
}
console.log(`已验证入口与延迟加载的相对静态资源路径，可部署到根域名或仓库子路径。`);
