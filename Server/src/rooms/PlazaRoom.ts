import { Room, Client } from "colyseus";
import { PlazaState, Player } from "./schema/PlazaState";

interface MoveData {
  x: number;
  y: number;
  z: number;
  rotY: number;
  animState: number;
}

interface ChatData {
  message: string;
}

export interface ChatHistoryItem {
  senderId: string;
  username: string;
  avatarId: number;
  message: string;
  timestamp: number;
  channel?: string;
  title?: string;
}

export class PlazaRoom extends Room<{ state: PlazaState }> {
  maxClients = 100;
  private chatHistory: ChatHistoryItem[] = [];
  private maxChatHistory: number = 50;

  onCreate(options: any) {
    console.log("[PlazaRoom] Room created with options:", options);
    this.setState(new PlazaState());

    // 20Hz update rate (50ms) for high-grade responsive sync
    this.setPatchRate(50);

    // Register movement handler
    this.onMessage("move", (client: Client, data: MoveData) => {
      const player = this.state.players.get(client.sessionId);
      if (player) {
        player.x = data.x;
        player.y = data.y;
        player.z = data.z;
        player.rotY = data.rotY;
        player.animState = data.animState;
      }
    });

    // Register chat handler
    this.onMessage("chat", (client: Client, data: ChatData) => {
      const player = this.state.players.get(client.sessionId);
      if (player && data && data.message) {
        const cleanMsg = data.message.trim().substring(0, 100);
        player.chatMsg = cleanMsg;
        player.chatTime = Date.now();

        const historyItem: ChatHistoryItem = {
          senderId: client.sessionId,
          username: player.username,
          avatarId: player.avatarId,
          message: cleanMsg,
          timestamp: Date.now(),
          channel: "world"
        };

        this.chatHistory.push(historyItem);
        if (this.chatHistory.length > this.maxChatHistory) {
          this.chatHistory.shift();
        }

        // Also broadcast as explicit chat event for chat window log
        this.broadcast("chatMessage", historyItem);
      }
    });

    // Register horn / loudspeaker handler
    this.onMessage("horn", (client: Client, data: { message: string }) => {
      const player = this.state.players.get(client.sessionId);
      if (player && data && data.message) {
        const cleanMsg = data.message.trim().substring(0, 100);
        const hornItem: ChatHistoryItem = {
          senderId: client.sessionId,
          username: player.username,
          avatarId: player.avatarId,
          message: cleanMsg,
          timestamp: Date.now(),
          channel: "horn"
        };

        this.chatHistory.push(hornItem);
        if (this.chatHistory.length > this.maxChatHistory) {
          this.chatHistory.shift();
        }

        this.broadcast("hornMessage", hornItem);
      }
    });

    // Register achievement broadcast handler
    this.onMessage("achievement", (client: Client, data: { title: string; desc: string }) => {
      const player = this.state.players.get(client.sessionId);
      if (player && data && data.title) {
        const achItem: ChatHistoryItem = {
          senderId: client.sessionId,
          username: player.username,
          avatarId: player.avatarId,
          message: data.desc || "",
          title: data.title,
          timestamp: Date.now(),
          channel: "achievement"
        };

        this.chatHistory.push(achItem);
        if (this.chatHistory.length > this.maxChatHistory) {
          this.chatHistory.shift();
        }

        this.broadcast("achievementMessage", achItem);
      }
    });

    // Client requests full chat history
    this.onMessage("getChatHistory", (client: Client) => {
      if (this.chatHistory.length > 0) {
        client.send("chatHistory", this.chatHistory);
      }
    });

    // Register whisper / private chat handler
    this.onMessage("whisper", (client: Client, data: { targetId: string; message: string }) => {
      const sender = this.state.players.get(client.sessionId);
      if (!sender || !data || !data.message) return;

      const cleanMsg = data.message.trim().substring(0, 100);
      const target = this.state.players.get(data.targetId);
      const targetClient = this.clients.find(c => c.sessionId === data.targetId);

      if (target && targetClient) {
        const payload = {
          senderId: client.sessionId,
          senderName: sender.username,
          senderAvatarId: sender.avatarId,
          targetId: data.targetId,
          targetName: target.username,
          targetAvatarId: target.avatarId,
          message: cleanMsg,
          isError: false,
          timestamp: Date.now()
        };

        // Send to target recipient
        targetClient.send("whisperMessage", payload);

        // Echo back to sender
        if (targetClient.sessionId !== client.sessionId) {
          client.send("whisperMessage", payload);
        }
      } else {
        client.send("whisperMessage", {
          senderId: "system",
          senderName: "系统",
          senderAvatarId: 0,
          targetId: client.sessionId,
          targetName: sender.username,
          targetAvatarId: 0,
          message: "目标玩家当前不在线或已离开广场。",
          isError: true,
          timestamp: Date.now()
        });
      }
    });

    // Register updateProfile handler
    this.onMessage("updateProfile", (client: Client, data: { username?: string; avatarId?: number; gender?: string; age?: number; bio?: string }) => {
      const player = this.state.players.get(client.sessionId);
      if (player && data) {
        if (typeof data.username === "string" && data.username.trim().length > 0) {
          player.username = data.username.trim().substring(0, 16);
        }
        if (typeof data.avatarId === "number") {
          player.avatarId = data.avatarId;
        }
        if (typeof data.gender === "string") {
          player.gender = data.gender;
        }
        if (typeof data.age === "number") {
          player.age = data.age;
        }
        if (typeof data.bio === "string") {
          player.bio = data.bio.trim().substring(0, 60);
        }

        console.log(`[PlazaRoom] Profile updated for ${client.sessionId}: username=${player.username}, avatarId=${player.avatarId}, gender=${player.gender}, age=${player.age}`);

        // Broadcast profile update event
        this.broadcast("playerProfileUpdated", {
          sessionId: client.sessionId,
          username: player.username,
          avatarId: player.avatarId,
          gender: player.gender,
          age: player.age,
          bio: player.bio
        });
      }
    });

    // Register emote / greeting handler
    this.onMessage("emote", (client: Client, emoteId: number) => {
      const player = this.state.players.get(client.sessionId);
      if (player) {
        player.animState = emoteId;
        this.broadcast("playerEmote", {
          senderId: client.sessionId,
          username: player.username,
          emoteId: emoteId
        });
      }
    });
  }

  onJoin(client: Client, options: any) {
    console.log(`[PlazaRoom] Player joined: ${client.sessionId}, options:`, options);

    const player = new Player();
    player.id = client.sessionId;
    player.username = (options && options.username && options.username.trim().length > 0)
      ? options.username.trim().substring(0, 16)
      : `User_${client.sessionId.substring(0, 4)}`;

    player.avatarId = (options && typeof options.avatarId === "number") ? options.avatarId : 0;
    player.gender = (options && typeof options.gender === "string") ? options.gender : "secret";
    player.age = (options && typeof options.age === "number") ? options.age : 0;
    player.bio = (options && typeof options.bio === "string") ? options.bio.trim().substring(0, 60) : "";

    // Slight random offset around origin for spawn point
    player.x = (Math.random() - 0.5) * 4;
    player.y = 0;
    player.z = (Math.random() - 0.5) * 4;
    player.rotY = 0;
    player.animState = 0;

    this.state.players.set(client.sessionId, player);

    // Send recent room chat history to newly joined player
    if (this.chatHistory.length > 0) {
      client.send("chatHistory", this.chatHistory);
    }
  }

  onLeave(client: Client, code?: number) {
    console.log(`[PlazaRoom] Player left: ${client.sessionId}`);
    this.state.players.delete(client.sessionId);
  }

  onDispose() {
    console.log("[PlazaRoom] Room disposed");
  }
}
