using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Colyseus;
using Colyseus.Schema;
using SmartSpace.Network.Schema;
using SmartSpace.Character;

namespace SmartSpace.Network
{
    [Serializable]
    public class MoveMessage
    {
        public float x;
        public float y;
        public float z;
        public float rotY;
        public sbyte animState;
    }

    [Serializable]
    public class ChatMessagePayload
    {
        public string message;
    }

    [Serializable]
    public class ChatMessageBroadcast
    {
        public string senderId;
        public string username;
        public string message;
        public double timestamp;
    }

    [Serializable]
    public class PlayerEmoteBroadcast
    {
        public string senderId;
        public string username;
        public sbyte emoteId;
    }

    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Colyseus Settings")]
        [SerializeField] private string serverUrl = "ws://localhost:2567";
        [SerializeField] private string roomName = "plaza";
        [SerializeField] private bool autoConnect = true;

        [Header("Prefabs")]
        [SerializeField] private GameObject localPlayerPrefab;
        [SerializeField] private GameObject remotePlayerPrefab;
        [SerializeField] private Transform spawnPoint;

        private Client _client;
        private Room<PlazaState> _room;
        private readonly Dictionary<string, GameObject> _spawnedPlayers = new Dictionary<string, GameObject>();
        private static readonly List<NativeWebSocket.WebSocket> _activeWebSockets = new List<NativeWebSocket.WebSocket>();

        public bool IsConnected => _room != null;
        public string SessionId => _room != null ? _room.SessionId : "";
        public int PlayerCount => _spawnedPlayers.Count;

        public event Action<bool> OnConnectionStateChanged;
        public event Action<string, string, string> OnChatMessageReceived; // senderId, username, message
        public event Action<string, string, EmoteType> OnPlayerEmoteReceived; // senderId, username, emoteType
        public event Action<string, string> OnPlayerJoined; // sessionId, username
        public event Action<string> OnPlayerLeft; // sessionId

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ColyseusContext.RegisterWebSocketForDispatch = (ws) =>
            {
                lock (_activeWebSockets)
                {
                    if (!_activeWebSockets.Contains(ws))
                    {
                        _activeWebSockets.Add(ws);
                    }
                }
            };

            ColyseusContext.UnregisterWebSocketForDispatch = (ws) =>
            {
                lock (_activeWebSockets)
                {
                    _activeWebSockets.Remove(ws);
                }
            };
        }

        private void Start()
        {
            if (autoConnect)
            {
                Connect("Player_" + UnityEngine.Random.Range(1000, 9999));
            }
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            lock (_activeWebSockets)
            {
                for (int i = 0; i < _activeWebSockets.Count; i++)
                {
                    _activeWebSockets[i]?.DispatchMessageQueue();
                }
            }
#endif
        }

        public async void Connect(string username)
        {
            if (IsConnected) return;

            try
            {
                Debug.Log($"[NetworkManager] Connecting to {serverUrl}...");
                _client = new Client(serverUrl);

                var options = new Dictionary<string, object>
                {
                    { "username", username }
                };

                _room = await _client.JoinOrCreate<PlazaState>(roomName, options);
                Debug.Log($"[NetworkManager] Successfully joined room: {_room.Name} with SessionId: {_room.SessionId}");

                OnConnectionStateChanged?.Invoke(true);

                // Register room leave callback
                _room.OnLeave += (code) =>
                {
                    Debug.Log($"[NetworkManager] Left room with code: {code}");
                    CleanupPlayers();
                    _room = null;
                    OnConnectionStateChanged?.Invoke(false);
                };

                // Register chat broadcast message
                _room.OnMessage<ChatMessageBroadcast>("chatMessage", (chatMsg) =>
                {
                    Debug.Log($"[Chat] {chatMsg.username}: {chatMsg.message}");
                    OnChatMessageReceived?.Invoke(chatMsg.senderId, chatMsg.username, chatMsg.message);
                });

                // Register player emote message
                _room.OnMessage<PlayerEmoteBroadcast>("playerEmote", (emoteMsg) =>
                {
                    EmoteType type = (EmoteType)emoteMsg.emoteId;
                    Debug.Log($"[Emote] {emoteMsg.username} performed {type}");
                    if (emoteMsg.senderId != _room.SessionId)
                    {
                        if (_spawnedPlayers.TryGetValue(emoteMsg.senderId, out GameObject playerObj))
                        {
                            var remote = playerObj.GetComponent<RemotePlayerController>();
                            if (remote != null)
                            {
                                remote.TriggerRemoteEmote(type);
                            }
                        }
                    }
                    OnPlayerEmoteReceived?.Invoke(emoteMsg.senderId, emoteMsg.username, type);
                });

                // Register Schema Callbacks (Colyseus 0.18+)
                var callbacks = Callbacks.Get(_room);

                // When a player is added
                callbacks.OnAdd(state => state.players, (key, player) =>
                {
                    Debug.Log($"[NetworkManager] Player added: {key} ({player.username})");
                    SpawnPlayer(key, player);
                    OnPlayerJoined?.Invoke(key, player.username);

                    // Track changes on this player
                    callbacks.OnChange(player, () =>
                    {
                        if (_spawnedPlayers.TryGetValue(key, out GameObject playerObj))
                        {
                            var remote = playerObj.GetComponent<RemotePlayerController>();
                            if (remote != null)
                            {
                                remote.UpdateFromSchema(player);
                            }
                        }
                    });
                });

                // When a player is removed
                callbacks.OnRemove(state => state.players, (key, player) =>
                {
                    Debug.Log($"[NetworkManager] Player removed: {key}");
                    OnPlayerLeft?.Invoke(key);
                    DespawnPlayer(key);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Failed to connect to server: {ex.Message}");
                OnConnectionStateChanged?.Invoke(false);
            }
        }

        private void SpawnPlayer(string key, Player playerSchema)
        {
            if (_spawnedPlayers.ContainsKey(key)) return;

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            spawnPos.x += (float)playerSchema.x;
            spawnPos.y += (float)playerSchema.y;
            spawnPos.z += (float)playerSchema.z;

            GameObject playerObj = null;

            if (key == _room.SessionId)
            {
                // Local Player
                if (localPlayerPrefab != null)
                {
                    playerObj = Instantiate(localPlayerPrefab, spawnPos, Quaternion.Euler(0, (float)playerSchema.rotY, 0));
                    var controller = playerObj.GetComponent<LocalPlayerController>();
                    if (controller != null)
                    {
                        controller.SessionId = key;
                        controller.Username = playerSchema.username;
                    }
                }
            }
            else
            {
                // Remote Player
                if (remotePlayerPrefab != null)
                {
                    playerObj = Instantiate(remotePlayerPrefab, spawnPos, Quaternion.Euler(0, (float)playerSchema.rotY, 0));
                    var remote = playerObj.GetComponent<RemotePlayerController>();
                    if (remote != null)
                    {
                        remote.Initialize(playerSchema);
                    }
                }
            }

            if (playerObj != null)
            {
                playerObj.name = $"Player_{playerSchema.username}_{key.Substring(0, 4)}";
                _spawnedPlayers[key] = playerObj;
            }
        }

        private void DespawnPlayer(string key)
        {
            if (_spawnedPlayers.TryGetValue(key, out GameObject playerObj))
            {
                Destroy(playerObj);
                _spawnedPlayers.Remove(key);
            }
        }

        private void CleanupPlayers()
        {
            foreach (var kvp in _spawnedPlayers)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            _spawnedPlayers.Clear();
        }

        public void SendMove(float x, float y, float z, float rotY, sbyte animState)
        {
            if (!IsConnected) return;

            _room.Send("move", new MoveMessage
            {
                x = x,
                y = y,
                z = z,
                rotY = rotY,
                animState = animState
            });
        }

        public void SendChat(string message)
        {
            if (!IsConnected || string.IsNullOrEmpty(message)) return;

            _room.Send("chat", new ChatMessagePayload
            {
                message = message
            });

            // Also show bubble on local player immediately for responsive feel
            if (_spawnedPlayers.TryGetValue(_room.SessionId, out GameObject localObj))
            {
                var localCtrl = localObj.GetComponent<LocalPlayerController>();
                if (localCtrl != null)
                {
                    localCtrl.ShowChatBubble(message);
                }
            }
        }

        public void SendEmote(sbyte emoteId)
        {
            if (!IsConnected) return;
            _room.Send("emote", emoteId);
        }

        private readonly List<Room<PlazaState>> _botRooms = new List<Room<PlazaState>>();

        public async void SpawnNetworkBot(string botName = null)
        {
            if (string.IsNullOrEmpty(botName))
            {
                botName = "Guest_" + UnityEngine.Random.Range(10, 99);
            }

            try
            {
                var botClient = new Client(serverUrl);
                var options = new Dictionary<string, object> { { "username", botName } };
                var botRoom = await botClient.JoinOrCreate<PlazaState>(roomName, options);
                _botRooms.Add(botRoom);

                // Ignore incoming broadcasts on bot client
                botRoom.OnMessage<object>("chatMessage", _ => { });
                botRoom.OnMessage<object>("playerEmote", _ => { });

                Debug.Log($"[NetworkBot] Bot '{botName}' joined with SessionId: {botRoom.SessionId}");

                StartCoroutine(BotPatrolRoutine(botRoom, botName));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkBot] Failed to spawn bot: {ex.Message}");
            }
        }

        private System.Collections.IEnumerator BotPatrolRoutine(Room<PlazaState> botRoom, string botName)
        {
            // Initial greeting after 1.2s
            yield return new WaitForSeconds(1.2f);
            if (botRoom != null)
            {
                botRoom.Send("chat", new ChatMessagePayload { message = $"Hi! I am {botName}! 👋" });
                botRoom.Send("emote", (sbyte)EmoteType.Wave);
            }

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            float radius = UnityEngine.Random.Range(4f, 8f);
            Vector3 center = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            center += new Vector3(UnityEngine.Random.Range(-4f, 4f), 0, UnityEngine.Random.Range(-4f, 4f));

            float chatTimer = 0f;
            var waitStep = new WaitForSeconds(0.05f); // 20Hz update rate
            EmoteType[] pool = new EmoteType[]
            {
                EmoteType.Wave,
                EmoteType.Heart,
                EmoteType.Clap,
                EmoteType.Bow,
                EmoteType.Dance,
                EmoteType.HighFive
            };

            while (botRoom != null)
            {
                angle += 0.04f;
                float x = center.x + Mathf.Cos(angle) * radius;
                float z = center.z + Mathf.Sin(angle) * radius;
                float rotY = (-angle + Mathf.PI / 2) * Mathf.Rad2Deg;

                botRoom.Send("move", new MoveMessage
                {
                    x = x,
                    y = 0,
                    z = z,
                    rotY = rotY,
                    animState = 1 // Walk
                });

                chatTimer += 0.05f;
                if (chatTimer >= 6.5f)
                {
                    chatTimer = 0f;
                    EmoteType picked = pool[UnityEngine.Random.Range(0, pool.Length)];
                    botRoom.Send("chat", new ChatMessagePayload { message = EmoteHelper.GetChatText(picked) });
                    botRoom.Send("emote", (sbyte)picked);
                }

                yield return waitStep;
            }
        }

        private async void OnDestroy()
        {
            foreach (var b in _botRooms)
            {
                if (b != null) await b.Leave();
            }
            _botRooms.Clear();

            if (_room != null)
            {
                await _room.Leave();
                _room = null;
            }
        }
    }
}
