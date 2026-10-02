using System;
using KingsCourt.Core;
using UnityEngine;

namespace KingsCourt.UnityLayer
{
    /// <summary>Envelope the relay server understands. Field meaning depends on t (see Server/server.js).</summary>
    [Serializable]
    public class RelayMsg
    {
        public string t;
        public string code;
        public string name;
        public int id;
        public int from;
        public string data;
        public string reason;
        /// <summary>Only on "hosted": the server's own best guess at its LAN-reachable address:port
        /// (e.g. "192.168.1.5:8000"), since the host's own browser URL (location.origin) could be
        /// "localhost" or similar, which is useless to anyone else. Empty if the server couldn't tell.</summary>
        public string lan;
    }

    // Game messages, carried as JSON text inside RelayMsg.data. Every one starts with k (its kind).
    [Serializable] public class KindMsg { public string k; }
    [Serializable] public class HelloMsg { public string k = "hello"; public string name; }
    [Serializable] public class SubmitMsg { public string k = "submit"; public int decisionId; public int index; }
    [Serializable] public class MemberDto { public int id; public string name; public bool isHost; public int playerId = -1; public bool online = true; }
    [Serializable] public class LobbyMsg { public string k = "lobby"; public MemberDto[] members = new MemberDto[0]; public int seats; public bool bots; public string code; public bool inProgress; }
    [Serializable] public class StartMsg { public string k = "start"; public int playerId; public string code; }
    [Serializable] public class SnapMsg { public string k = "snap"; public SnapshotDto snap; }
    [Serializable] public class RefusedMsg { public string k = "refused"; public string reason; }
    [Serializable] public class DeniedMsg { public string k = "denied"; public string reason; }
    /// <summary>Host -> every phone: the game just ended and everyone is back in the lobby, same room code.</summary>
    [Serializable] public class BackToLobbyMsg { public string k = "backlobby"; }

    public static class Json
    {
        public static string Relay(RelayMsg m) { return JsonUtility.ToJson(m); }

        public static RelayMsg ParseRelay(string text)
        {
            try { return JsonUtility.FromJson<RelayMsg>(text); } catch (Exception) { return null; }
        }

        public static string Kind(string data)
        {
            try { var k = JsonUtility.FromJson<KindMsg>(data); return k != null ? k.k : null; } catch (Exception) { return null; }
        }

        public static T Parse<T>(string data) where T : class
        {
            try { return JsonUtility.FromJson<T>(data); } catch (Exception) { return null; }
        }
    }

    /// <summary>One game, seen from one device. The local game and the phone-to-host game both look like this to the UI.</summary>
    public interface IPlayerSession
    {
        string JoinCode { get; }
        event Action Updated;
        /// <summary>The host refused a move (out of turn, second card...). Argument is the reason to show the player.</summary>
        event Action<string> Refused;
        /// <summary>The game ended because the connection to the host was lost.</summary>
        event Action<string> Ended;
        GameSnapshot Snapshot(bool spectator = false);
        SubmitResult Submit(int decisionId, int index);
        void LeaveTable();
    }
}
