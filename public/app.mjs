import { Composer } from './hangul.mjs';
const $ = s => document.querySelector(s);
const composer = new Composer();
let mode='ko', shift=false, token='', busy=false, readyAt=0, cooldown=1500;
const undo=[];
const defaults=['안녕하세요!','반가워요 :)','감사합니다','잠시만요','같이 가요!','마이크를 쓰기 어려워요'];
const read = (key,fallback) => { try { return JSON.parse(localStorage.getItem(key)) ?? fallback; } catch { return fallback; } };
const write = (key,value) => { try { localStorage.setItem(key,JSON.stringify(value)); } catch { status('브라우저 저장 공간을 사용할 수 없습니다.'); } };
let phrases=read('phrases',defaults);
if (!Array.isArray(phrases)) phrases=[...defaults];
phrases=phrases.filter(x=>typeof x==='string' && x.length<=144).slice(0,16);
composer.set(String(read('draft','')).slice(0,144));
$('#sound').checked=read('sound',false)===true;
$('#clearAfter').checked=read('clearAfter',true)!==false;
function status(message){$('#status').textContent=message;}
function render(){
  const text=composer.text;
  $('#message').textContent=text;
  $('#message').scrollTop=$('#message').scrollHeight;
  $('#count').textContent=`${text.length} / 144`;
  $('#count').classList.toggle('over',text.length>144);
  $('#send').disabled=busy || !token || !text.trim() || text.length>144 || Date.now()<readyAt;
  $('#undo').disabled=!undo.length;
  write('draft',text);
}
function change(action){
  undo.push({base:composer.base,keys:[...composer.keys]});
  if(undo.length>100) undo.shift();
  action(); render();
}
function insert(text){change(()=>{composer.set(composer.text+text);});}
function button(label,action,classes=''){
  const b=document.createElement('button'); b.textContent=label;b.className=classes;
  b.addEventListener('click',action);return b;
}
function keyboard(){
  const ko=shift?['ㅃㅉㄸㄲㅆㅛㅕㅑㅒㅖ','ㅁㄴㅇㄹㅎㅗㅓㅏㅣ','ㅋㅌㅊㅍㅠㅜㅡ']:['ㅂㅈㄷㄱㅅㅛㅕㅑㅐㅔ','ㅁㄴㅇㄹㅎㅗㅓㅏㅣ','ㅋㅌㅊㅍㅠㅜㅡ'];
  const en=shift?['QWERTYUIOP','ASDFGHJKL','ZXCVBNM']:['qwertyuiop','asdfghjkl','zxcvbnm'];
  const rows=mode==='ko'?ko:mode==='en'?en:['1234567890','!?.,~@#%&','()[]+-=♡♥'];
  const root=$('#keyboard');root.replaceChildren();
  for(const [i,row] of rows.entries()){
    const el=document.createElement('div');el.className='key-row';
    if(i===2){const b=button(shift?'쌍자음 / 대문자 ON':'⇧ 쌍자음',()=>{shift=!shift;keyboard();},'key wide'+(shift?' active':''));b.setAttribute('aria-pressed',String(shift));el.append(b);}
    for(const key of row)el.append(button(key,()=>{change(()=>composer.push(key));if(shift){shift=false;keyboard();}},'key'));
    if(i===2)el.append(button('⌫ 지우기',()=>change(()=>composer.backspace()),'key wide'));
    root.append(el);
  }
  const bottom=document.createElement('div');bottom.className='key-row';
  bottom.append(button('ㅋㅋ',()=>insert('ㅋㅋ'),'key wide'),button('띄어쓰기',()=>change(()=>composer.push(' ')),'key space'),button('줄바꿈',()=>change(()=>composer.push('\n')),'key wide'),button('♥',()=>insert('♥'),'key wide'));
  root.append(bottom);
}
function renderPhrases(){
  $('#phrases').replaceChildren(...phrases.map(p=>button(p,()=>insert((composer.text && !/\s$/.test(composer.text)?' ':'')+p))));
}
document.querySelectorAll('[data-mode]').forEach(b=>b.addEventListener('click',()=>{
  composer.set(composer.text);mode=b.dataset.mode;shift=false;
  document.querySelectorAll('[data-mode]').forEach(x=>{x.classList.toggle('active',x===b);x.setAttribute('aria-pressed',String(x===b));});keyboard();
}));
$('#clear').onclick=()=>{change(()=>composer.clear());status('내용을 비웠습니다. 되돌리기로 복구할 수 있어요.');};
$('#undo').onclick=()=>{const prev=undo.pop();if(prev){composer.base=prev.base;composer.keys=prev.keys;render();}};
$('#save').onclick=()=>{
  const text=composer.text.trim();
  if(!text || text.length>144)return status('1~144자의 문장을 작성한 다음 저장해 주세요.');
  if(phrases.includes(text))return status('이미 저장한 문장입니다.');
  if(phrases.length>=16)return status('최대 16개까지 저장할 수 있어요. 연결 방법에서 초기화할 수 있습니다.');
  phrases.push(text);write('phrases',phrases);renderPhrases();status('자주 쓰는 말에 저장했습니다.');
};
$('#resetPhrases').onclick=()=>{phrases=[...defaults];write('phrases',phrases);renderPhrases();status('기본 문장으로 초기화했습니다.');};
for(const key of ['sound','clearAfter'])$('#'+key).onchange=()=>write(key,$('#'+key).checked);
$('#fullscreen').onclick=async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await document.documentElement.requestFullscreen();}catch{status('이 환경에서는 전체화면을 사용할 수 없습니다. 브라우저 창을 크게 해 주세요.');}};
async function connect(){
  try{const r=await fetch('/api/status');if(!r.ok)throw Error();const data=await r.json();token=data.token;cooldown=data.cooldown;$('#connection').textContent=`● 로컬 서버 · OSC ${data.target}`;status('준비됐어요. VRChat에서 OSC를 켜 주세요.');}
  catch{token='';$('#connection').textContent='○ 서버 연결 끊김';status('서버를 실행해 주세요. 잠시 후 다시 연결합니다.');setTimeout(connect,3000);}
  render();
}
$('#send').onclick=async()=>{
  if($('#send').disabled)return;
  const text=composer.text;busy=true;render();status('전송 중…');
  try{
    const r=await fetch('/api/send',{method:'POST',headers:{'Content-Type':'application/json','X-Session-Token':token},body:JSON.stringify({text,sound:$('#sound').checked})});
    const result=await r.json();if(!r.ok)throw Error(result.error);
    readyAt=Date.now()+cooldown;
    if($('#clearAfter').checked && composer.text===text)change(()=>composer.clear());
    status(result.message);setTimeout(render,cooldown+20);
  }catch(e){status(e.message==='Failed to fetch'?'서버 연결이 끊겼습니다. 문장은 보관됩니다.':e.message);if(e.message==='Failed to fetch')setTimeout(connect,3000);}
  finally{busy=false;render();}
};
keyboard();renderPhrases();render();connect();
