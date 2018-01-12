using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using System.Xml;
using System.Linq;
using System.Collections;

namespace Server.Engines.Doom
{
	public class DoomGuardianRegion : DungeonRegion
	{
		public static DoomGuardianRegion Instance { get; set; }
		
		public static void Initialize()
		{
            Instance.CheckDoors();
		}

        private Timer m_Timer;
	
		public bool Active { get; set; }
		public List<DarkGuardian> Guardians { get; set; }
		public BaseDoor DoorOne { get; set; }
		public BaseDoor DoorTwo { get; set; }
		public DateTime NextActivate { get; set; }
		
		public bool CanActivate { get { return NextActivate < DateTime.UtcNow; } }
	   
		private static Rectangle2D[] RegionBounds = new Rectangle2D[] { new Rectangle2D(355, 5, 20, 20) };
		private static Rectangle2D PentagramBounds = new Rectangle2D(364, 14, 2, 2);
		private static Point3D DoorOneLoc = new Point3D(355, 14, -1);
		private static Point3D DoorTwoLoc = new Point3D(355, 15, -1);
		private static Point3D KickLoc = new Point3D(344, 172, -1);
        private static Point3D PentagramLoc = new Point3D(365, 15, -1);

        public DoomGuardianRegion(XmlElement xml, Map map, Region parent)
            : base(xml, map, parent)
		{
            Instance = this;
		}
		
		public override bool AllowHousing( Mobile from, Point3D p )
		{
			return false;
		}
		
		public override void OnLocationChanged( Mobile m, Point3D oldLocation )
		{
			base.OnLocationChanged( m, oldLocation );
			
			if(!Active && CanActivate && m is PlayerMobile && m.AccessLevel == AccessLevel.Player && m.Alive)
			{
				for(int x = m.X - 3; x <= m.X + 3; x++)
				{
					for(int y = m.Y - 3; y <= m.Y + 3; y++)
					{
						if(!Active && PentagramBounds.Contains(new Point2D(x, y)))
						{
							Activate(m);
                            Active = true;
                            return;
						}
					}
				}
			}
		}
		
		public override void OnDeath( Mobile m )
		{
			if(Guardians != null)
			{
				if(m is DarkGuardian && Guardians.Contains((DarkGuardian)m))
					Guardians.Remove((DarkGuardian)m);

                if (Guardians.Count == 0)
                {
                    Reset();
                    Guardians.Clear();
                    Guardians.TrimExcess();
                }
			}
			
			if(m is PlayerMobile)
			{
                Timer.DelayCall(TimeSpan.FromSeconds(1), () => 
                { 
                    BaseCreature.TeleportPets(m, KickLoc, Map.Malas); 
                    m.MoveToWorld(KickLoc, Map.Malas);

                    if (m.Corpse != null)
                        m.Corpse.MoveToWorld(KickLoc, Map.Malas);

                    if (this.GetEnumeratedMobiles().FirstOrDefault(mob => mob is PlayerMobile && mob.Alive) == null)
                    {
                        Reset();
                    }
                });
			}
		}
		
		public void Activate(Mobile m)
		{
            CheckDoors();

            DoorOne.Open = false;
            DoorTwo.Open = false;
			DoorOne.Locked = true;
			DoorTwo.Locked = true;
			
            Effects.PlaySound(DoorOne.Location, DoorOne.Map, 0x241);
            Effects.PlaySound(DoorTwo.Location, DoorTwo.Map, 0x241);

            if (Guardians == null)
                Guardians = new List<DarkGuardian>();

            int count = 0;
            foreach (var mob in this.GetEnumeratedMobiles().Where(mob => mob is PlayerMobile || (mob is BaseCreature && ((BaseCreature)mob).GetMaster() != null && !mob.IsDeadBondedPet)))
            {
                if (mob.NetState != null)
                    mob.SendLocalizedMessage(1050000, "", 365); // The locks on the door click loudly and you begin to hear a faint hissing near the walls.

                if(mob.Alive)
                    count++;
            }

            count = Math.Min(1, count * 2);

			for(int i = 0; i < count; i++)
			{
				DarkGuardian guardian = new DarkGuardian();
				
				int x = Utility.RandomMinMax(PentagramBounds.X, PentagramBounds.X + PentagramBounds.Width);
				int y = Utility.RandomMinMax(PentagramBounds.Y, PentagramBounds.Y + PentagramBounds.Height);
				int z = Map.Malas.GetAverageZ(x, y);
				
				guardian.MoveToWorld(new Point3D(x, y, z), Map.Malas);
				Guardians.Add(guardian);
				
				guardian.Combatant = m;
			}

            if (m_Timer != null)
            {
                m_Timer.Stop();
                m_Timer = null;
            }

		    m_Timer = new InternalTimer(this);
			m_Timer.Start();
		}

		public void Reset()
		{
            if (m_Timer != null)
            {
                m_Timer.Stop();
                m_Timer = null;
            }
			
			DoorOne.Locked = false;
			DoorTwo.Locked = false;

            Active = false;
			NextActivate = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(15, 60));
		}
		
		public void CheckDoors()
		{
			if(DoorOne == null || DoorOne.Deleted)
			{
				if(!CheckDoor(DoorOneLoc, 1))
				{
					DoorOne = new MetalDoor2(DoorFacing.NorthCW);
					DoorOne.MoveToWorld(DoorOneLoc, Map.Malas);
					DoorOne.KeyValue = 0;
				}
			}
			
			if(DoorTwo == null || DoorTwo.Deleted)
			{
				if(!CheckDoor(DoorTwoLoc, 2))
				{
					DoorTwo = new MetalDoor2(DoorFacing.SouthCCW);
					DoorTwo.MoveToWorld(DoorTwoLoc, Map.Malas);
					DoorTwo.KeyValue = 0;
				}
			}
			
			if(DoorOne != null && DoorOne.Link != DoorTwo)
				DoorOne.Link = DoorTwo;
				
			if(DoorTwo != null && DoorTwo.Link != DoorOne)
				DoorTwo.Link = DoorOne;

            CheckPentagram();
		}
		
		public bool CheckDoor(Point3D p, int door)
		{
			IPooledEnumerable eable = Map.Malas.GetItemsInRange(p, 0);
			
			foreach(Item item in eable)
			{
				if(item is BaseDoor)
				{
					eable.Free();

                    if (door == 1)
                        DoorOne = item as BaseDoor;
                    else
                        DoorTwo = item as BaseDoor;

					return true;
				}
			}
			
			eable.Free();
			return false;
		}

        private void CheckPentagram()
        {
            IPooledEnumerable eable = Map.Malas.GetItemsInRange(PentagramLoc, 0);

            foreach (Item item in eable)
            {
                if (item is PentagramAddon)
                {
                    eable.Free();
                    return;
                }
            }

            eable.Free();

            var addon = new PentagramAddon();
            addon.MoveToWorld(PentagramLoc, Map.Malas);
        }
		
		private class InternalTimer : Timer
		{
			public DoomGuardianRegion Region { get; private set; }
			
			public InternalTimer(DoomGuardianRegion reg) : base(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3))
			{
				Region = reg;
			}
			
			protected override void OnTick()
			{
				for(int i = 0; i < Utility.RandomMinMax(5, 12); i++)
				{
					Point3D p = Region.RandomSpawnLocation(0, true, false, Point3D.Zero, 0);
					Effects.SendLocationEffect( p, Map.Malas, Utility.RandomList(0x113C, 0x1147, 0x11A8) - 2, 16, 3, 0, 0 );
				}

                List<Mobile> list = Region.GetEnumeratedMobiles().Where(mob => mob is PlayerMobile && mob.Alive).ToList();

                if (list.Count == 0)
                {
                    Region.Reset();
                }
                else
                {
                    foreach (var m in list.Where(m => m.AccessLevel == AccessLevel.Player && m.Poison == null))
                    {
                        m.ApplyPoison(m, Poison.Deadly);
                        m.SendSound(0x231);
                    }
                }

				list.Clear();
                list.TrimExcess();
			}
		}
	}
}
