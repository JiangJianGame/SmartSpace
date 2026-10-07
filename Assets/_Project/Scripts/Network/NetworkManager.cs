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
        public sbyte avatarId;
        public string message;
        public double timestamp;
    }

    [Serializable]
    public class WhisperPayload
    {
        public string targetId;
        public string message;
    }

    [Serializable]
    public class WhisperMessageBroadcast
    {
        public string senderId;
        public string senderName;
        public sbyte senderAvatarId;
        public string targetId;
        public string targetName;
        public sbyte targetAvatarId;
        public string message;
        public bool isError;
        public double timestamp;
    }

    [Serializable]
    public class ProfileUpdateBroadcast
    {
        public string sessionId;
        public string username;
        public sbyte avatarId;
        public string gender;
        public sbyte age;
        public string bio;
    }

    [Serializable]
    public class UpdateProfilePayload
    {
        public string username;
        public int avatarId;
        public string gender;
        public int age;
        public string bio;
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

        public readonly Dictionary<string, string> OnlinePlayers = new Dictionary<string, string>();
        public readonly Dictionary<string, UserProfile> OnlineProfiles = new Dictionary<string, UserProfile>();

        public UserProfile LocalProfile { get; set; }

        public bool IsConnected => _room != null;
        public string SessionId => _room != null ? _room.SessionId : "";
        public int PlayerCount => _spawnedPlayers.Count;

        public event Action<bool> OnConnectionStateChanged;
        public event Action<string, string, string> OnChatMessageReceived; // senderId, username, message
        public event Action<string, string, int, string> OnChatMessageWithAvatarReceived; // senderId, username, avatarId, message
        public event Action<ChatMessageBroadcast[]> OnChatHistoryReceived; // historical messages from server
        public event Action<WhisperMessageBroadcast> OnWhisperMessageReceived;
        public event Action<string, string, EmoteType> OnPlayerEmoteReceived; // senderId, username, emoteType
        public event Action<string, string> OnPlayerJoined; // sessionId, username
        public event Action<string> OnPlayerLeft; // sessionId
        public event Action<string, UserProfile> OnPlayerProfileChanged; // sessionId, profile

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LocalProfile = UserProfile.LoadFromPrefs();

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
                // If user has already saved profile, connect immediately. Otherwise UI will show setup dialog first.
                if (UserProfile.HasSavedProfile())
                {
                    Connect(LocalProfile);
                }
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

        public void Connect(string username)
        {
            if (LocalProfile == null) LocalProfile = UserProfile.LoadFromPrefs();
            LocalProfile.username = username;
            Connect(LocalProfile);
        }

        public async void Connect(UserProfile profile = null)
        {
            if (IsConnected) return;

            if (profile != null)
            {
                LocalProfile = profile;
            }
            else if (LocalProfile == null)
            {
                LocalProfile = UserProfile.LoadFromPrefs();
            }

            try
            {
                Debug.Log($"[NetworkManager] Connecting to {serverUrl} with profile '{LocalProfile.username}' (Avatar:{LocalProfile.avatarId})...");
                _client = new Client(serverUrl);

                var options = new Dictionary<string, object>
                {
                    { "username", LocalProfile.username },
                    { "avatarId", LocalProfile.avatarId },
                    { "gender", LocalProfile.gender },
                    { "age", LocalProfile.age },
                    { "bio", LocalProfile.bio }
                };

                _room = await _client.JoinOrCreate<PlazaState>(roomName, options);
                Debug.Log($"[NetworkManager] Successfully joined room: {_room.Name} with SessionId: {_room.SessionId}");

                OnlinePlayers[_room.SessionId] = LocalProfile.username;
                OnlineProfiles[_room.SessionId] = LocalProfile;

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
                    OnChatMessageWithAvatarReceived?.Invoke(chatMsg.senderId, chatMsg.username, chatMsg.avatarId, chatMsg.message);
                });

                // Register chat history broadcast
                _room.OnMessage<ChatMessageBroadcast[]>("chatHistory", (historyList) =>
                {
                    Debug.Log($"[Chat] Received {historyList?.Length ?? 0} historical messages from server.");
                    if (historyList != null && historyList.Length > 0)
                    {
                        OnChatHistoryReceived?.Invoke(historyList);
                    }
                });

                // Register whisper / private chat message
                _room.OnMessage<WhisperMessageBroadcast>("whisperMessage", (whisperMsg) =>
                {
                    Debug.Log($"[Whisper] {whisperMsg.senderName} -> {whisperMsg.targetName}: {whisperMsg.message}");
                    OnWhisperMessageReceived?.Invoke(whisperMsg);
                });

                // Register profile update broadcast
                _room.OnMessage<ProfileUpdateBroadcast>("playerProfileUpdated", (pMsg) =>
                {
                    Debug.Log($"[Profile] Updated for {pMsg.sessionId}: {pMsg.username} (Avatar:{pMsg.avatarId})");

                    if (!OnlineProfiles.TryGetValue(pMsg.sessionId, out var prof))
                    {
                        prof = new UserProfile();
                        OnlineProfiles[pMsg.sessionId] = prof;
                    }
                    prof.username = pMsg.username;
                    prof.avatarId = pMsg.avatarId;
                    prof.gender = pMsg.gender;
                    prof.age = pMsg.age;
                    prof.bio = pMsg.bio;

                    OnlinePlayers[pMsg.sessionId] = pMsg.username;

                    if (pMsg.sessionId == _room.SessionId)
                    {
                        LocalProfile.username = pMsg.username;
                        LocalProfile.avatarId = pMsg.avatarId;
                        LocalProfile.gender = pMsg.gender;
                        LocalProfile.age = pMsg.age;
                        LocalProfile.bio = pMsg.bio;
                        LocalProfile.SaveToPrefs();

                        if (_spawnedPlayers.TryGetValue(pMsg.sessionId, out GameObject localObj))
                        {
                            var localCtrl = localObj.GetComponent<LocalPlayerController>();
                            if (localCtrl != null) localCtrl.SetProfile(pMsg.username, pMsg.avatarId);
                        }
                    }

                    OnPlayerProfileChanged?.Invoke(pMsg.sessionId, prof);
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
                    Debug.Log($"[NetworkManager] Player added: {key} ({player.username}, Avatar:{player.avatarId})");
                    OnlinePlayers[key] = player.username;
                    OnlineProfiles[key] = new UserProfile
                    {
                        username = player.username,
                        avatarId = player.avatarId,
                        gender = player.gender,
                        age = player.age,
                        bio = player.bio
                    };

                    SpawnPlayer(key, player);
                    OnPlayerJoined?.Invoke(key, player.username);

                    // Track changes on this player
                    callbacks.OnChange(player, () =>
                    {
                        if (OnlineProfiles.TryGetValue(key, out var p))
                        {
                            p.username = player.username;
                            p.avatarId = player.avatarId;
                            p.gender = player.gender;
                            p.age = player.age;
                            p.bio = player.bio;
                        }
                        OnlinePlayers[key] = player.username;

                        if (_spawnedPlayers.TryGetValue(key, out GameObject playerObj))
                        {
                            var remote = playerObj.GetComponent<RemotePlayerController>();
                            if (remote != null)
                            {
                                remote.UpdateFromSchema(player);
                            }
                        }

                        OnPlayerProfileChanged?.Invoke(key, OnlineProfiles[key]);
                    });
                });

                // When a player is removed
                callbacks.OnRemove(state => state.players, (key, player) =>
                {
                    Debug.Log($"[NetworkManager] Player removed: {key}");
                    OnlinePlayers.Remove(key);
                    OnlineProfiles.Remove(key);
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

        public void UpdateProfile(UserProfile newProfile)
        {
            if (newProfile == null) return;
            LocalProfile = newProfile;
            LocalProfile.SaveToPrefs();

            if (IsConnected)
            {
                OnlineProfiles[SessionId] = LocalProfile;
                OnlinePlayers[SessionId] = LocalProfile.username;

                if (_spawnedPlayers.TryGetValue(SessionId, out GameObject localObj))
                {
                    var localCtrl = localObj.GetComponent<LocalPlayerController>();
                    if (localCtrl != null)
                    {
                        localCtrl.SetProfile(newProfile.username, newProfile.avatarId);
                    }
                }

                _room.Send("updateProfile", new UpdateProfilePayload
                {
                    username = newProfile.username,
                    avatarId = newProfile.avatarId,
                    gender = newProfile.gender,
                    age = newProfile.age,
                    bio = newProfile.bio
                });
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
                        controller.SetProfile(playerSchema.username, playerSchema.avatarId);
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
            OnlinePlayers.Clear();
            OnlineProfiles.Clear();
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

        public void SendWhisper(string targetId, string message)
        {
            if (!IsConnected || string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(message)) return;

            _room.Send("whisper", new WhisperPayload
            {
                targetId = targetId,
                message = message
            });
        }

        public void SendEmote(sbyte emoteId)
        {
            if (!IsConnected) return;
            _room.Send("emote", emoteId);
        }

        public void RequestChatHistory()
        {
            if (IsConnected && _room != null)
            {
                _room.Send("getChatHistory");
            }
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

                // Auto reply to whispers sent to this bot
                botRoom.OnMessage<WhisperMessageBroadcast>("whisperMessage", (wMsg) =>
                {
                    if (wMsg.targetId == botRoom.SessionId && !wMsg.isError && wMsg.senderId != botRoom.SessionId)
                    {
                        StartCoroutine(BotWhisperReplyRoutine(botRoom, wMsg.senderId, wMsg.senderName, botName));
                    }
                });

                Debug.Log($"[NetworkBot] Bot '{botName}' joined with SessionId: {botRoom.SessionId}");

                StartCoroutine(BotPatrolRoutine(botRoom, botName));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkBot] Failed to spawn bot: {ex.Message}");
            }
        }

        private System.Collections.IEnumerator BotWhisperReplyRoutine(Room<PlazaState> botRoom, string targetId, string targetName, string botName)
        {
            yield return new WaitForSeconds(1.2f);
            if (botRoom != null)
            {
                string[] replies = new string[]
                {
                    $"收到你的私信啦！很高兴和你单独聊天~ 😊",
                    $"哈哈，我也觉得这里很好逛！要一起漫游拍照吗？✨",
                    $"收到！有空记得常来广场找我玩呀～👋"
                };
                string reply = replies[UnityEngine.Random.Range(0, replies.Length)];
                botRoom.Send("whisper", new WhisperPayload { targetId = targetId, message = reply });
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

            float emoteTimer = 0f;
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

                emoteTimer += 0.05f;
                if (emoteTimer >= 15f)
                {
                    emoteTimer = 0f;
                    EmoteType picked = pool[UnityEngine.Random.Range(0, pool.Length)];
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
