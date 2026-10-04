import { Client } from "colyseus.js";
import WebSocket from "ws";

(global as any).WebSocket = WebSocket;

async function main() {
  console.log("[Bot] Connecting to Colyseus server...");
  const client = new Client("http://localhost:2567");

  try {
    const room = await client.joinOrCreate("plaza", { username: "Bot_Alice" });
    console.log(`[Bot] Joined room: ${room.name} with SessionId: ${room.sessionId}`);

    // Wait 1.5 second then send a greeting chat
    setTimeout(() => {
      console.log("[Bot] Sending greeting chat...");
      room.send("chat", { message: "Hello! 欢迎来到智慧空间广场！👋" });
      room.send("emote", 4); // Wave
    }, 1500);

    // Simulate moving around in a circular patrol path at 20Hz (50ms interval)
    let angle = 0;
    const radius = 5.0;
    const centerX = 0;
    const centerZ = 4.0;

    setInterval(() => {
      angle += 0.04;
      const x = centerX + Math.cos(angle) * radius;
      const z = centerZ + Math.sin(angle) * radius;
      const rotY = ((-angle + Math.PI / 2) * 180) / Math.PI;

      room.send("move", {
        x,
        y: 0,
        z,
        rotY,
        animState: 1 // Walk
      });
    }, 50);

    // Periodically send chat and wave
    setInterval(() => {
      room.send("chat", { message: "大空间漫游同步测试进行中 ✨" });
      room.send("emote", 4);
    }, 8000);

  } catch (err) {
    console.error("[Bot] Failed to connect:", err);
  }
}

main();
