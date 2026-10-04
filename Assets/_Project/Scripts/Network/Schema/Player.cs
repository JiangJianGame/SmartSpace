// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 5.0.35
// 

namespace SmartSpace.Network.Schema {
	public partial class Player : global::Colyseus.Schema.Schema {
#if UNITY_5_3_OR_NEWER
		[global::UnityEngine.Scripting.Preserve]
#endif
		public Player() { }

		[global::Colyseus.Schema.Type(0, "string")]
		public string id = "";

		[global::Colyseus.Schema.Type(1, "string")]
		public string username = "Visitor";

		[global::Colyseus.Schema.Type(2, "number")]
		public double x = 0;

		[global::Colyseus.Schema.Type(3, "number")]
		public double y = 0;

		[global::Colyseus.Schema.Type(4, "number")]
		public double z = 0;

		[global::Colyseus.Schema.Type(5, "number")]
		public double rotY = 0;

		[global::Colyseus.Schema.Type(6, "int8")]
		public sbyte animState = 0;

		[global::Colyseus.Schema.Type(7, "string")]
		public string chatMsg = "";

		[global::Colyseus.Schema.Type(8, "number")]
		public double chatTime = 0;
	}
}
