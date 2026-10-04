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

export class PlazaRoom extends Room<{ state: PlazaState }> {
  maxClients = 100;

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

        // Also broadcast as explicit chat event for chat window log
        this.broadcast("chatMessage", {
          senderId: client.sessionId,
          username: player.username,
          message: cleanMsg,
          timestamp: Date.now()
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
    console.log(`[PlazaRoom] Player joined: ${client.sessionId}`);

    const player = new Player();
    player.id = client.sessionId;
    player.username = options.username || `User_${client.sessionId.substring(0, 4)}`;

    // Slight random offset around origin for spawn point
    player.x = (Math.random() - 0.5) * 4;
    player.y = 0;
    player.z = (Math.random() - 0.5) * 4;
    player.rotY = 0;
    player.animState = 0;

    this.state.players.set(client.sessionId, player);
  }

  onLeave(client: Client, code?: number) {
    console.log(`[PlazaRoom] Player left: ${client.sessionId}`);
    this.state.players.delete(client.sessionId);
  }

  onDispose() {
    console.log("[PlazaRoom] Room disposed");
  }
}
