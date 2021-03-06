#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Mods.RA.Activities;
using OpenRA.Mods.RA.Traits;
using OpenRA.Mods.RA.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	[Desc("This unit can spawn and eject other actors while flying.")]
	public class ParaDropInfo : ITraitInfo, Requires<CargoInfo>
	{
		[Desc("Distance around the drop-point to unload troops.")]
		public readonly WRange DropRange = WRange.FromCells(4);

		[Desc("Sound to play when dropping.")]
		public readonly string ChuteSound = "chute1.aud";

		public object Create(ActorInitializer init) { return new ParaDrop(init.self, this); }
	}

	public class ParaDrop : ITick, INotifyRemovedFromWorld
	{
		readonly ParaDropInfo info;
		readonly Actor self;
		readonly Cargo cargo;
		readonly HashSet<CPos> droppedAt = new HashSet<CPos>();

		public event Action<Actor> OnRemovedFromWorld = self => { };
		public event Action<Actor> OnEnteredDropRange = self => { };
		public event Action<Actor> OnExitedDropRange = self => { };

		[Sync] bool inDropRange;
		[Sync] Target target;

		bool checkForSuitableCell;

		public ParaDrop(Actor self, ParaDropInfo info)
		{
			this.info = info;
			this.self = self;
			cargo = self.Trait<Cargo>();
		}

		public void SetLZ(CPos lz, bool checkLandingCell)
		{
			droppedAt.Clear();
			target = Target.FromCell(self.World, lz);
			checkForSuitableCell = checkLandingCell;
		}

		public void Tick(Actor self)
		{
			var wasInDropRange = inDropRange;
			inDropRange = target.IsInRange(self.CenterPosition, info.DropRange);

			if (inDropRange && !wasInDropRange)
				OnEnteredDropRange(self);

			if (!inDropRange && wasInDropRange)
				OnExitedDropRange(self);

			// Are we able to drop the next trooper?
			if (!inDropRange || cargo.IsEmpty(self))
				return;

			if (droppedAt.Contains(self.Location) || checkForSuitableCell && !IsSuitableCell(cargo.Peek(self), self.Location))
				return;

			if (!self.World.Map.Contains(self.Location))
				return;

			// unload a dude here
			droppedAt.Add(self.Location);

			var a = cargo.Unload(self);
			self.World.AddFrameEndTask(w => w.Add(new Parachute(a, self.CenterPosition)));
			Sound.Play(info.ChuteSound, self.CenterPosition);
		}

		static bool IsSuitableCell(Actor actorToDrop, CPos p)
		{
			return actorToDrop.Trait<IPositionable>().CanEnterCell(p);
		}

		public void RemovedFromWorld(Actor self)
		{
			OnRemovedFromWorld(self);
		}
	}
}
