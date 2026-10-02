// WebSocket bridge for the WebGL build. C# polls KcWs_Next for events, so there are no callbacks into C#.
// Event strings: "O" (open), "M<text>" (message), "E" (error), "C<reason>" (closed).
mergeInto(LibraryManager.library, {

  KcWs_Open: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    if (!Module.kcWs) Module.kcWs = { next: 1, sockets: {} };
    var id = Module.kcWs.next++;
    var rec = { q: [], ws: null };
    Module.kcWs.sockets[id] = rec;
    try {
      var ws = new WebSocket(url);
      rec.ws = ws;
      ws.onopen = function () { rec.q.push("O"); };
      ws.onmessage = function (e) { if (typeof e.data === "string") rec.q.push("M" + e.data); };
      ws.onerror = function () { rec.q.push("E"); };
      ws.onclose = function (e) { rec.q.push("C" + (e.reason || ("code " + e.code))); };
    } catch (err) {
      rec.q.push("C" + err);
    }
    return id;
  },

  KcWs_Send: function (id, msgPtr) {
    var rec = Module.kcWs && Module.kcWs.sockets[id];
    if (rec && rec.ws && rec.ws.readyState === 1) rec.ws.send(UTF8ToString(msgPtr));
  },

  // Returns a malloc'd UTF-8 string (free it with KcWs_Free), or 0 when nothing is waiting.
  KcWs_Next: function (id) {
    var rec = Module.kcWs && Module.kcWs.sockets[id];
    if (!rec || rec.q.length === 0) return 0;
    var s = rec.q.shift();
    var len = lengthBytesUTF8(s) + 1;
    var buf = _malloc(len);
    stringToUTF8(s, buf, len);
    return buf;
  },

  KcWs_Free: function (ptr) { _free(ptr); },

  KcWs_Close: function (id) {
    var rec = Module.kcWs && Module.kcWs.sockets[id];
    if (rec && rec.ws) { try { rec.ws.close(); } catch (e) { } }
  },

  // ?name=value from the page address (empty string if missing). Used for share links like /?join=ABCD.
  KcWs_Query: function (namePtr) {
    var v = "";
    try { v = new URLSearchParams(location.search).get(UTF8ToString(namePtr)) || ""; } catch (e) { }
    var len = lengthBytesUTF8(v) + 1;
    var buf = _malloc(len);
    stringToUTF8(v, buf, len);
    return buf;
  },

  // http://host:port of the page (what a friend should open).
  KcWs_Origin: function () {
    var v = location.origin || "";
    var len = lengthBytesUTF8(v) + 1;
    var buf = _malloc(len);
    stringToUTF8(v, buf, len);
    return buf;
  },

  // ws://host:port/ws for the page this game was loaded from (so phones need no configuration).
  KcWs_DefaultUrl: function () {
    var proto = (location.protocol === "https:") ? "wss://" : "ws://";
    var s = proto + location.host + "/ws";
    var len = lengthBytesUTF8(s) + 1;
    var buf = _malloc(len);
    stringToUTF8(s, buf, len);
    return buf;
  },

  // Copies text to the clipboard. The modern navigator.clipboard API needs a "secure context"
  // (https, or localhost) - since phones on the same Wi-Fi reach this game over plain http on a LAN
  // IP, that API silently does nothing there. Always fall back to the older execCommand('copy') path
  // (a hidden, off-screen textarea), which works in any context as long as it's triggered from a real
  // click, which every caller here is.
  KcCopyText: function (textPtr) {
    var text = UTF8ToString(textPtr);
    function legacyCopy() {
      var ta = document.createElement("textarea");
      ta.value = text;
      ta.style.position = "fixed";
      ta.style.top = "-1000px";
      ta.style.left = "-1000px";
      document.body.appendChild(ta);
      ta.focus();
      ta.select();
      try { document.execCommand("copy"); } catch (e) { }
      document.body.removeChild(ta);
    }
    if (window.isSecureContext && navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(legacyCopy);
    } else {
      legacyCopy();
    }
  }
});
