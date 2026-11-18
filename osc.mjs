export function oscString(value) {
  const bytes = Buffer.from(value, 'utf8');
  const padded = Buffer.alloc(Math.ceil((bytes.length + 1) / 4) * 4);
  bytes.copy(padded);
  return padded;
}
export function chatPacket(text, sound = false) {
  return Buffer.concat([oscString('/chatbox/input'), oscString(sound ? ',sTT' : ',sTF'), oscString(text)]);
}
export function validateText(text) {
  if (typeof text !== 'string' || !text.trim()) throw new Error('보낼 내용을 입력해 주세요.');
  if (text.length > 144) throw new Error('144자 이내로 작성해 주세요. (이모지는 2자로 계산될 수 있습니다.)');
  if (text.split('\n').length > 9) throw new Error('줄바꿈은 최대 9줄까지 가능합니다.');
  if (/[\u0000-\u0008\u000b-\u001f\u007f]/u.test(text)) throw new Error('지원하지 않는 제어 문자가 들어 있습니다.');
  return text;
}
