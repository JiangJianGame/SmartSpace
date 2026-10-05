import { Schema, type, MapSchema } from "@colyseus/schema";

export class Player extends Schema {
  @type("string") id: string = "";
  @type("string") username: string = "Visitor";
  @type("number") x: number = 0;
  @type("number") y: number = 0;
  @type("number") z: number = 0;
  @type("number") rotY: number = 0;
  @type("int8") animState: number = 0; // 0: Idle, 1: Walk, 2: Run, 3: Jump, 4: Wave/Emote
  @type("string") chatMsg: string = "";
  @type("number") chatTime: number = 0;
  @type("int8") avatarId: number = 0;
  @type("string") gender: string = "secret"; // "secret" | "male" | "female"
  @type("int8") age: number = 0; // 0 = not specified / secret
  @type("string") bio: string = "";
}

export class PlazaState extends Schema {
  @type({ map: Player }) players = new MapSchema<Player>();
}
