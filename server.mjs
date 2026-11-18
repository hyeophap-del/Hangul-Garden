import http from 'node:http';
import dgram from 'node:dgram';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { randomBytes } from 'node:crypto';
import { chatPacket, validateText } from './osc.mjs';

export function createApp({ oscPort = 9000, cooldown = 1500 } = {}) {
  const token = randomBytes(24).toString('hex');
  const udp = dgram.createSocket('udp4');
  let lastSend = 0, sending = false;
  const files = {'/':'index.html','/app.mjs':'app.mjs','/hangul.mjs':'hangul.mjs','/style.css':'style.css'};
  const server = http.createServer(async (req,res) => {
    const json = (status, body) => { res.writeHead(status, {'Content-Type':'application/json; charset=utf-8','Cache-Control':'no-store'}); res.end(JSON.stringify(body)); };
    const expectedHost = `127.0.0.1:${server.address().port}`;
    if (req.headers.host !== expectedHost && req.headers.host !== `localhost:${server.address().port}`) return json(403,{error:'로컬 주소로 접속해 주세요.'});
    if (req.method === 'GET' && req.url === '/api/status') return json(200,{token, target:`127.0.0.1:${oscPort}`, cooldown});
    if (req.method === 'POST' && req.url === '/api/send') {
      if (req.headers['x-session-token'] !== token || !['http://'+expectedHost,`http://localhost:${server.address().port}`].includes(req.headers.origin)) return json(403,{error:'페이지를 새로 열어 주세요.'});
      if (!req.headers['content-type']?.startsWith('application/json')) return json(415,{error:'JSON만 지원합니다.'});
      try {
        const chunks = []; let size = 0;
        for await (const chunk of req) {
          size += chunk.length;
          if (size > 4096) return json(413,{error:'요청이 너무 큽니다.'});
          chunks.push(chunk);
        }
        const {text, sound = false} = JSON.parse(Buffer.concat(chunks).toString('utf8'));
        validateText(text);
        if (typeof sound !== 'boolean') throw new Error('알림음 설정이 올바르지 않습니다.');
        if (sending || Date.now()-lastSend < cooldown) return json(429,{error:'잠깐 기다린 뒤 다시 보내 주세요.'});
        sending = true;
        try {
          await new Promise((resolve,reject) => udp.send(chatPacket(text,sound),oscPort,'127.0.0.1',e=>e?reject(e):resolve()));
          lastSend = Date.now();
          json(200,{ok:true, message:'OSC 전송 완료 · 게임 안에서 표시를 확인해 주세요.'});
        } finally { sending = false; }
      } catch (e) { json(400,{error:e instanceof SyntaxError ? '잘못된 요청입니다.' : e.message}); }
      return;
    }
    if (req.method !== 'GET' || !files[req.url]) return json(404,{error:'페이지를 찾을 수 없습니다.'});
    try {
      const name = files[req.url];
      const data = await readFile(new URL(`./public/${name}`,import.meta.url));
      res.writeHead(200,{'Content-Type':name.endsWith('.css')?'text/css':name.endsWith('.mjs')?'text/javascript':'text/html; charset=utf-8','Cache-Control':'no-store','X-Content-Type-Options':'nosniff','Content-Security-Policy':"default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'"});
      res.end(data);
    } catch { json(500,{error:'화면 파일을 읽을 수 없습니다.'}); }
  });
  server.on('close',()=>{ try { udp.close(); } catch {} });
  return server;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const port = Number(process.env.PORT || 18741);
  const oscPort = Number(process.env.OSC_PORT || 9000);
  if (![port,oscPort].every(p=>Number.isInteger(p)&&p>0&&p<65536)) throw new Error('포트는 1~65535여야 합니다.');
  const app = createApp({oscPort});
  app.on('error',e=>{ console.error(e.code === 'EADDRINUSE' ? '이미 실행 중이거나 포트가 사용 중입니다. http://127.0.0.1:'+port : e.message); process.exitCode=1; });
  app.listen(port,'127.0.0.1',()=>console.log(`한글뜰 실행 중: http://127.0.0.1:${port}\nOSC 대상: 127.0.0.1:${oscPort}\n종료: Ctrl+C`));
}
