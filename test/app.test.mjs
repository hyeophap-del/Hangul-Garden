import {test} from 'node:test';
import assert from 'node:assert/strict';
import dgram from 'node:dgram';
import {once} from 'node:events';
import {compose,Composer} from '../public/hangul.mjs';
import {chatPacket,validateText} from '../osc.mjs';
import {createApp} from '../server.mjs';

for(const [keys,expected] of [
  ['ㅇㅏㄴㄴㅕㅇㅎㅏㅅㅔㅇㅛ','안녕하세요'],['ㅎㅏㄴㄱㅡㄹ','한글'],
  ['ㄱㅏㅂㅅㅇㅣ','값이'],['ㄱㅏㅂㅅㅏ','갑사'],['ㄷㅏㄹㄱㅏ','달가'],
  ['ㄱㅗㅏㄴ','관'],['ㄱㅜㅓㄴ','권'],['ㄲㅗㅊ','꽃'],['ㄱㅏㄴㅏ','가나'],
  ['ㅋㅋ ㅎㅎ!','ㅋㅋ ㅎㅎ!'],['ㅇㅡㅣ','의'],['ㅏㅣㄱ','ㅏㅣㄱ'],['ㄱㅏㄸㅏ','가따']
])test(`조합: ${expected}`,()=>assert.equal(compose([...keys]),expected));

test('겹받침과 복합 모음을 한 입력씩 되돌린다',()=>{
  const c=new Composer();for(const x of 'ㄱㅗㅏㄹㄱ')c.push(x);
  assert.equal(c.text,'괅');
  for(const expected of ['괄','과','고','ㄱ','']){c.backspace();assert.equal(c.text,expected);}
  c.set('안녕♥');c.backspace();assert.equal(c.text,'안녕');
});
test('모든 현대 한글 음절의 초성 중성 종성 조합',()=>{
  const l=[...'ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ'];
  const v=[...'ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ'];
  const t=['',...'ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ'];
  const split={'ㄳ':'ㄱㅅ','ㄵ':'ㄴㅈ','ㄶ':'ㄴㅎ','ㄺ':'ㄹㄱ','ㄻ':'ㄹㅁ','ㄼ':'ㄹㅂ','ㄽ':'ㄹㅅ','ㄾ':'ㄹㅌ','ㄿ':'ㄹㅍ','ㅀ':'ㄹㅎ','ㅄ':'ㅂㅅ'};
  for(let a=0;a<19;a++)for(let b=0;b<21;b++)for(let c=0;c<28;c++){
    assert.equal(compose([...l[a],...v[b],...(split[t[c]]||t[c])]),String.fromCharCode(0xac00+(a*21+b)*28+c));
  }
});
function decode(packet){
  let offset=0;const parts=[];
  while(offset<packet.length){const end=packet.indexOf(0,offset);assert.ok(end>=offset);parts.push(packet.toString('utf8',offset,end));offset=Math.ceil((end+1)/4)*4;}
  return parts;
}
test('OSC 한국어 UTF-8 / bool 태그 / 패딩',()=>{
  for(const sound of [false,true]){
    const packet=chatPacket('안녕하세요 ♥',sound);
    assert.equal(packet.length%4,0);
    assert.deepEqual(decode(packet),['/chatbox/input',sound?',sTT':',sTF','안녕하세요 ♥']);
  }
});
test('메시지 제한',()=>{
  assert.equal(validateText('가'.repeat(144)).length,144);
  for(const text of ['',null,'   ','가'.repeat(145),'a\n'.repeat(9),'a\0b'])assert.throws(()=>validateText(text));
});
test('HTTP에서 실제 UDP로 한글 전송 및 검증',async()=>{
  const receiver=dgram.createSocket('udp4');receiver.bind(0,'127.0.0.1');await once(receiver,'listening');
  const app=createApp({oscPort:receiver.address().port,cooldown:10000});app.listen(0,'127.0.0.1');await once(app,'listening');
  const base=`http://127.0.0.1:${app.address().port}`;
  try{
    const status=await (await fetch(base+'/api/status')).json();
    const send=(body,headers={})=>fetch(base+'/api/send',{method:'POST',headers:{'Content-Type':'application/json','X-Session-Token':status.token,'Origin':base,...headers},body:JSON.stringify(body)});
    assert.equal((await send({text:'한글'},{Origin:'https://example.com'})).status,403);
    assert.equal((await send({text:'한글'},{'X-Session-Token':'bad'})).status,403);
    assert.equal((await send({text:'가'.repeat(145)})).status,400);
    assert.equal((await send({text:'테스트',sound:'false'})).status,400);
    const arrival=once(receiver,'message',{signal:AbortSignal.timeout(2000)});
    const response=await send({text:'안녕하세요!'});assert.equal(response.status,200);
    const [packet]=await arrival;assert.deepEqual(decode(packet),['/chatbox/input',',sTF','안녕하세요!']);
    assert.equal((await send({text:'중복'})).status,429);
    for(const path of ['/','/style.css','/app.mjs','/hangul.mjs'])assert.equal((await fetch(base+path)).status,200);
    assert.equal((await fetch(base+'/package.json')).status,404);
  }finally{app.closeAllConnections();await new Promise(r=>app.close(r));receiver.close();}
});
