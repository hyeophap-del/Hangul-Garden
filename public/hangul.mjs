const L = [...'ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ'];
const V = [...'ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ'];
const T = ['', ...'ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ'];
const vowels = {'ㅗㅏ':'ㅘ','ㅗㅐ':'ㅙ','ㅗㅣ':'ㅚ','ㅜㅓ':'ㅝ','ㅜㅔ':'ㅞ','ㅜㅣ':'ㅟ','ㅡㅣ':'ㅢ'};
const finals = {'ㄱㅅ':'ㄳ','ㄴㅈ':'ㄵ','ㄴㅎ':'ㄶ','ㄹㄱ':'ㄺ','ㄹㅁ':'ㄻ','ㄹㅂ':'ㄼ','ㄹㅅ':'ㄽ','ㄹㅌ':'ㄾ','ㄹㅍ':'ㄿ','ㄹㅎ':'ㅀ','ㅂㅅ':'ㅄ'};
const splitFinal = Object.fromEntries(Object.entries(finals).map(([a,b]) => [b,[...a]]));

// Replay key strokes: deleting one stroke also reverses compound vowels/finals.
export function compose(keys) {
  let result = '', l = '', v = '', t = '';
  const current = () => l && v ? String.fromCharCode(0xac00 + (L.indexOf(l)*21 + V.indexOf(v))*28 + T.indexOf(t)) : l || v;
  const flush = () => { result += current(); l = v = t = ''; };
  for (const key of keys) {
    if (V.includes(key)) {
      if (l && v && t) {
        const parts = splitFinal[t] || ['',t];
        t = parts[0]; flush(); l = parts[1]; v = key;
      } else if (v) {
        if (vowels[v+key]) v = vowels[v+key];
        else { flush(); v = key; }
      } else v = key;
    } else if (L.includes(key)) {
      if (l && v && !t && T.includes(key)) t = key;
      else if (l && v && t && finals[t+key]) t = finals[t+key];
      else { flush(); l = key; }
    } else { flush(); result += key; }
  }
  return result + current();
}

export class Composer {
  constructor() { this.base = ''; this.keys = []; }
  get text() { return this.base + compose(this.keys); }
  push(key) { this.keys.push(key); }
  backspace() {
    if (this.keys.length) this.keys.pop();
    else this.base = [...this.base].slice(0,-1).join('');
  }
  set(text) { this.base = text; this.keys = []; }
  clear() { this.set(''); }
}
