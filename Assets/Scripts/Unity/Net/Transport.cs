using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace KingsCourt.UnityLayer
{
#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>Copy-to-clipboard that actually works on a plain-http LAN page (see KcWebSocket.jslib for why
    /// GUIUtility.systemCopyBuffer alone isn't enough in WebGL: it needs a "secure context").</summary>
    public static class Clipboard
    {
        [DllImport("__Internal")] private static extern void KcCopyText(string text);
        public static void Copy(string text) { KcCopyText(text ?? ""); }
    }
#else
    public static class Clipboard
    {
        public static void Copy(string text) { UnityEngine.GUIUtility.systemCopyBuffer = text ?? ""; }
    }
#endif

    /// <summary>A text-message connection to the relay server. Events are delivered from Tick() on the main thread.</summary>
    public interface ITransport
    {
        bool IsOpen { get; }
        event Action Opened;
        event Action<string> Received;
        event Action<string> Closed;   // reason
        void Connect(string url);
        void Send(string text);
        void Close();
        void Tick();
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>Browser WebSocket, through Assets/Plugins/WebGL/KcWebSocket.jslib (polled, no callbacks).</summary>
    public sealed class WebSocketTransport : ITransport
    {
        [DllImport("__Internal")] private static extern int KcWs_Open(string url);
        [DllImport("__Internal")] private static extern void KcWs_Send(int id, string message);
        [DllImport("__Internal")] private static extern IntPtr KcWs_Next(int id);
        [DllImport("__Internal")] private static extern void KcWs_Free(IntPtr ptr);
        [DllImport("__Internal")] private static extern void KcWs_Close(int id);
        [DllImport("__Internal")] private static extern IntPtr KcWs_DefaultUrl();
        [DllImport("__Internal")] private static extern IntPtr KcWs_Query(string name);
        [DllImport("__Internal")] private static extern IntPtr KcWs_Origin();

        public bool IsOpen { get; private set; }
        public event Action Opened;
        public event Action<string> Received;
        public event Action<string> Closed;

        private int id = -1;
        private bool finished;

        public static string DefaultUrl()
        {
            IntPtr p = KcWs_DefaultUrl();
            string s = Marshal.PtrToStringUTF8(p);
            KcWs_Free(p);
            return s;
        }

        /// <summary>A value from the page address, for example ?join=ABCD. Empty if absent.</summary>
        public static string QueryParam(string name)
        {
            IntPtr p = KcWs_Query(name);
            string s = Marshal.PtrToStringUTF8(p);
            KcWs_Free(p);
            return s;
        }

        /// <summary>The address this page was loaded from, such as http://192.168.1.5:8000. Empty in the editor.</summary>
        public static string PageOrigin()
        {
            IntPtr p = KcWs_Origin();
            string s = Marshal.PtrToStringUTF8(p);
            KcWs_Free(p);
            return s;
        }

        public void Connect(string url) { id = KcWs_Open(url); }

        public void Send(string text) { if (id >= 0 && IsOpen) KcWs_Send(id, text); }

        public void Close()
        {
            if (id >= 0) KcWs_Close(id);
        }

        public void Tick()
        {
            if (id < 0 || finished) return;
            IntPtr p;
            while ((p = KcWs_Next(id)) != IntPtr.Zero)
            {
                string s = Marshal.PtrToStringUTF8(p);
                KcWs_Free(p);
                if (s.Length == 0) continue;
                switch (s[0])
                {
                    case 'O': IsOpen = true; if (Opened != null) Opened(); break;
                    case 'M': if (Received != null) Received(s.Substring(1)); break;
                    case 'C':
                        IsOpen = false; finished = true;
                        if (Closed != null) Closed(s.Substring(1));
                        return;
                }
            }
        }
    }
#else
    /// <summary>System WebSocket, used in the editor and standalone builds.</summary>
    public sealed class WebSocketTransport : ITransport
    {
        public bool IsOpen { get; private set; }
        public event Action Opened;
        public event Action<string> Received;
        public event Action<string> Closed;

        private ClientWebSocket ws;
        private CancellationTokenSource cts;
        private readonly ConcurrentQueue<Action> inbox = new ConcurrentQueue<Action>();
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private bool closedRaised;

        public static string DefaultUrl() { return "ws://localhost:8000/ws"; }
        public static string QueryParam(string name) { return ""; }
        public static string PageOrigin() { return ""; }

        public async void Connect(string url)
        {
            cts = new CancellationTokenSource();
            ws = new ClientWebSocket();
            try
            {
                await ws.ConnectAsync(new Uri(url), cts.Token);
            }
            catch (Exception e)
            {
                inbox.Enqueue(() => RaiseClosed("Could not reach the server (" + e.Message + ")"));
                return;
            }
            inbox.Enqueue(() => { IsOpen = true; if (Opened != null) Opened(); });
            await ReceiveLoop();
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[16 * 1024];
            var text = new StringBuilder();
            try
            {
                while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
                {
                    text.Length = 0;
                    WebSocketReceiveResult r;
                    do
                    {
                        r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                        if (r.MessageType == WebSocketMessageType.Close) { inbox.Enqueue(() => RaiseClosed("closed")); return; }
                        text.Append(Encoding.UTF8.GetString(buffer, 0, r.Count));
                    } while (!r.EndOfMessage);
                    string msg = text.ToString();
                    inbox.Enqueue(() => { if (Received != null) Received(msg); });
                }
            }
            catch (Exception e)
            {
                inbox.Enqueue(() => RaiseClosed(e.Message));
                return;
            }
            inbox.Enqueue(() => RaiseClosed("closed"));
        }

        public async void Send(string textToSend)
        {
            if (ws == null || ws.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(textToSend);
            await sendLock.WaitAsync();
            try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token); }
            catch (Exception) { /* the receive loop reports the drop */ }
            finally { sendLock.Release(); }
        }

        public void Close()
        {
            IsOpen = false;
            try { if (cts != null) cts.Cancel(); } catch (Exception) { }
            try { if (ws != null) ws.Abort(); } catch (Exception) { }
            closedRaised = true;   // a deliberate close is not reported as a drop
        }

        public void Tick()
        {
            Action a;
            while (inbox.TryDequeue(out a)) a();
        }

        private void RaiseClosed(string reason)
        {
            if (closedRaised) return;
            closedRaised = true;
            IsOpen = false;
            if (Closed != null) Closed(reason);
        }
    }
#endif
}
