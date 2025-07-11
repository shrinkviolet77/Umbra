﻿using Terraria.DataStructures;
using Terraria.ID;
using Umbra.Content.Items;
using Umbra.Core.PassiveTreeSystem;

namespace Umbra.Core
{
	internal class UmbraDropNPC : GlobalNPC
	{
		public bool umbraDropDisabled;

		public override bool InstancePerEntity => true;

		public static float UmbraChance => 0.01f + 0.05f * MathF.Atan(TreeSystem.tree.difficulty * 0.001f);

		public override void OnSpawn(NPC npc, IEntitySource source)
		{
			if (source is EntitySource_Parent { Entity: NPC { boss: true } })
				umbraDropDisabled = true;
		}

		public override void OnKill(NPC npc)
		{
			if (umbraDropDisabled ||
				npc.friendly ||
				NPCID.Sets.CountsAsCritter[npc.type] ||
				npc.lifeMax <= 5 ||
				npc.SpawnedFromStatue ||
				npc.lastInteraction == 255)
			{
				return;
			}

			int rolls = npc.boss ? 5 : 1;

			for (int k = 0; k < rolls; k++)
			{
				if (Main.rand.NextFloat() <= UmbraChance)
					Item.NewItem(npc.GetSource_Death(), npc.Hitbox, ModContent.ItemType<UmbraPickup>());
			}

			// grant partial points
			Player lastPlayer = Main.player[npc.lastInteraction];
			lastPlayer.GetModPlayer<TreePlayer>().partialPoints += npc.boss ? 15 : 1;
		}
	}
}
