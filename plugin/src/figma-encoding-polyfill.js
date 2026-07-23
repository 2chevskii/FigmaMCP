// Figma's plugin sandbox does not provide the WHATWG encoding globals that
// @msgpack/msgpack initializes while loading. The UI runs in a browser, but
// the controller bundle needs these small compatible fallbacks before imports.
(() => {
  const host = globalThis;

  function utf8Length(value) {
    let length = 0;
    for (let index = 0; index < value.length; index += 1) {
      let codePoint = value.charCodeAt(index);
      if (codePoint < 0x80) length += 1;
      else if (codePoint < 0x800) length += 2;
      else {
        if (codePoint >= 0xd800 && codePoint <= 0xdbff && index + 1 < value.length) {
          const lowSurrogate = value.charCodeAt(index + 1);
          if (lowSurrogate >= 0xdc00 && lowSurrogate <= 0xdfff) {
            codePoint = ((codePoint & 0x3ff) << 10) + (lowSurrogate & 0x3ff) + 0x10000;
            index += 1;
          }
        }
        length += codePoint < 0x10000 ? 3 : 4;
      }
    }
    return length;
  }

  function encodeUtf8(value) {
    const bytes = new Uint8Array(utf8Length(value));
    let offset = 0;
    for (let index = 0; index < value.length; index += 1) {
      let codePoint = value.charCodeAt(index);
      if (codePoint >= 0xd800 && codePoint <= 0xdbff && index + 1 < value.length) {
        const lowSurrogate = value.charCodeAt(index + 1);
        if (lowSurrogate >= 0xdc00 && lowSurrogate <= 0xdfff) {
          codePoint = ((codePoint & 0x3ff) << 10) + (lowSurrogate & 0x3ff) + 0x10000;
          index += 1;
        }
      }
      if (codePoint < 0x80) bytes[offset++] = codePoint;
      else if (codePoint < 0x800) {
        bytes[offset++] = 0xc0 | (codePoint >> 6);
        bytes[offset++] = 0x80 | (codePoint & 0x3f);
      } else if (codePoint < 0x10000) {
        bytes[offset++] = 0xe0 | (codePoint >> 12);
        bytes[offset++] = 0x80 | ((codePoint >> 6) & 0x3f);
        bytes[offset++] = 0x80 | (codePoint & 0x3f);
      } else {
        bytes[offset++] = 0xf0 | (codePoint >> 18);
        bytes[offset++] = 0x80 | ((codePoint >> 12) & 0x3f);
        bytes[offset++] = 0x80 | ((codePoint >> 6) & 0x3f);
        bytes[offset++] = 0x80 | (codePoint & 0x3f);
      }
    }
    return bytes;
  }

  function decodeUtf8(bytes) {
    const units = [];
    for (let offset = 0; offset < bytes.length;) {
      const first = bytes[offset++];
      if ((first & 0x80) === 0) units.push(first);
      else if ((first & 0xe0) === 0xc0) units.push(((first & 0x1f) << 6) | (bytes[offset++] & 0x3f));
      else if ((first & 0xf0) === 0xe0) units.push(((first & 0x0f) << 12) | ((bytes[offset++] & 0x3f) << 6) | (bytes[offset++] & 0x3f));
      else if ((first & 0xf8) === 0xf0) {
        let codePoint = ((first & 0x07) << 18) | ((bytes[offset++] & 0x3f) << 12) | ((bytes[offset++] & 0x3f) << 6) | (bytes[offset++] & 0x3f);
        codePoint -= 0x10000;
        units.push(0xd800 | (codePoint >> 10), 0xdc00 | (codePoint & 0x3ff));
      } else units.push(first);
    }
    return String.fromCharCode(...units);
  }

  if (typeof host.TextEncoder === "undefined") {
    host.TextEncoder = class TextEncoder {
      encode(value = "") { return encodeUtf8(String(value)); }
      encodeInto(value, destination) {
        const bytes = encodeUtf8(String(value));
        const written = Math.min(bytes.length, destination.length);
        destination.set(bytes.subarray(0, written));
        return { read: String(value).length, written };
      }
    };
  }

  if (typeof host.TextDecoder === "undefined") {
    host.TextDecoder = class TextDecoder {
      decode(input = new Uint8Array()) { return decodeUtf8(new Uint8Array(input)); }
    };
  }
})();
