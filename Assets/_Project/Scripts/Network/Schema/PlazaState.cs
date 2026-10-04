// 
// THIS FILE HAS BEEN GENERATED AUTOMATICALLY
// DO NOT CHANGE IT MANUALLY UNLESS YOU KNOW WHAT YOU'RE DOING
// 
// GENERATED USING @colyseus/schema 5.0.35
// 

namespace SmartSpace.Network.Schema {
	public partial class PlazaState : global::Colyseus.Schema.Schema {
#if UNITY_5_3_OR_NEWER
		[global::UnityEngine.Scripting.Preserve]
#endif
		public PlazaState() { }

		[global::Colyseus.Schema.Type(0, "map", typeof(global::Colyseus.Schema.MapSchema<Player>))]
		public global::Colyseus.Schema.MapSchema<Player> players = new global::Colyseus.Schema.MapSchema<Player>();
	}
}
