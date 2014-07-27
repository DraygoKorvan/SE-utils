using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Timers;
using System.Reflection;
using System.Threading;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.VRageData;

using SEModAPIExtensions.API.Plugin;
using SEModAPIExtensions.API.Plugin.Events;
using SEModAPIExtensions.API;

using SEModAPIInternal.API.Common;
using SEModAPIInternal.API.Entity;
using SEModAPIInternal.API.Entity.Sector.SectorObject;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid;
using SEModAPIInternal.API.Entity.Sector.SectorObject.CubeGrid.CubeBlock;
using SEModAPIInternal.API.Server;
using SEModAPIInternal.Support;

using SEModAPI.API;

using VRageMath;
using VRage.Common.Utils;



namespace SEUtils
{
	[Serializable()]
	public class SEUtilssettings
	{
		public bool allowpos = true;
	}

	public class SEUtils : PluginBase, IChatEventHandler
	{
		
		#region "Attributes"
		SEUtilssettings settings = new SEUtilssettings();
		#endregion

		#region "Constructors and Initializers"

		public void Core()
		{
			Console.WriteLine("SE Utils Plugin '" + Id.ToString() + "' constructed!");	
		}

		public override void Init()
		{

			allowPos = true;
			Console.WriteLine("SE Utils Plugin '" + Id.ToString() + "' initialized!");
			loadXML();

		}

		#endregion

		#region "Properties"

		[Browsable(true)]
		[ReadOnly(true)]
		public string Location
		{
			get { return System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\"; }
		
		}

		[Category("SE-Utils")]
		[Description("Allow non-admin players to query position.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool allowPos
		{
			get { return settings.allowpos; }
			set { settings.allowpos = value; }
		}

		#endregion

		#region "Methods"

		public void saveXML()
		{

			XmlSerializer x = new XmlSerializer(typeof(SEUtilssettings));
			TextWriter writer = new StreamWriter(Location + "Configuration.xml");
			x.Serialize(writer, settings);
			writer.Close();

		}
		public void loadXML(bool defaults)
		{
			try
			{
				if (File.Exists(Location + "Configuration.xml"))
				{
					XmlSerializer x = new XmlSerializer(typeof(SEUtilssettings));
					TextReader reader = new StreamReader(Location + "Configuration.xml");
					settings = (SEUtilssettings)x.Deserialize(reader);
					reader.Close();
				}
			}
			catch (Exception ex)
			{
				LogManager.APILog.WriteLineAndConsole("Could not load configuration: " + ex.ToString());
			}

		}
		public void loadXML()
		{
			loadXML(false);
		}
		private CharacterEntity getCharacter(ulong steamid)
		{
			List<CharacterEntity> characterlist = SectorObjectManager.Instance.GetTypedInternalData<CharacterEntity>();
			//long playerid = PlayerMap.Instance.GetPlayerEntityId(steamid);
			foreach (CharacterEntity character in characterlist)
			{
				if (character.SteamId == steamid)
				{
					//ChatManager.Instance.SendPrivateChatMessage(steamid, "Your position: " + character.Position.ToString());
					return character;
				}
			}
			throw new Exception("No Character found");

		}
		private void sendPlayerPosition(ulong steamid)
		{
			List<CharacterEntity> characterlist = SectorObjectManager.Instance.GetTypedInternalData<CharacterEntity>();
			//long playerid = PlayerMap.Instance.GetPlayerEntityId(steamid);
			foreach (CharacterEntity character in characterlist)
			{
				if (character.SteamId == steamid)
				{
					ChatManager.Instance.SendPrivateChatMessage(steamid, "Your position: " + character.Position.ToString());
					return;
				}
			}
			ChatManager.Instance.SendPrivateChatMessage(steamid, "Could not find your position, you may be in a cockpit.");
		}

		public void floatingObjectCleanup(bool force)
		{
			//get worldsize information
			int maxsize = SandboxGameAssemblyWrapper.Instance.GetServerConfig().SessionSettings.WorldSizeKm * 1000;
			if (maxsize == 0 && !force) throw new Exception("Worldsize is 0, aborting cleanup. Specify force if you want all floating objects deleted. ");
			foreach (FloatingObject obj in SectorObjectManager.Instance.GetTypedInternalData<FloatingObject>())
			{
				//currently crashes client boo hoo
				if (Math.Abs(obj.Position.X) > maxsize || Math.Abs(obj.Position.Y) > maxsize || Math.Abs(obj.Position.Z) > maxsize || force)
					obj.Dispose();
			}
		}

		public void factionCleanup(bool force)
		{
			List<Faction> factionList = FactionsManager.Instance.Factions;
			foreach (Faction f_faction in factionList)
			{
				if(	f_faction.Members.Count == 0)
				{
					//empty faction - delete
					FactionsManager.Instance.RemoveFaction(f_faction.Id);
					continue;
				}

			}
		}

		public void shipCleanup(bool force, ulong steamid, int dist)
		{
			Vector3Wrapper myPos = getCharacter(steamid).Position;
			int movedist = 50;
			List<CubeGridEntity> shipList = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
			int maxsize = dist * 1000;
			if (maxsize < 10000) throw new Exception("Distance is less than 10km, aborting cleanup.");
			foreach (CubeGridEntity grid in shipList)
			{
				if (Math.Abs(grid.Position.X) > maxsize || Math.Abs(grid.Position.Y) > maxsize || Math.Abs(grid.Position.Z) > maxsize)
				{
					if (!force)
					{
						//kill ship movement
						grid.LinearVelocity = new Vector3Wrapper(0, 0, 0);
						grid.AngularVelocity = new Vector3Wrapper(0, 0, 0);

						grid.Position = Vector3.Add(myPos, new Vector3Wrapper(movedist, 0, 0));
						movedist = movedist + 50;
						continue;
					}
					else
						grid.Dispose();
				}
				
			}
			
		}
		#region "EventHandlers"

		public override void Update()
		{
			return;
		}

		public override void Shutdown()
		{
			saveXML();
			return;
		}

		public void OnChatReceived(SEModAPIExtensions.API.ChatManager.ChatEvent obj)
		{
			//PlayerMap.Instance.GetSteamId(long entityId)
			//PlayerMap.Instance.GetPlayerId(ulong steamId)
			if (obj.sourceUserId == 0)
				return;
			bool isadmin = SandboxGameAssemblyWrapper.Instance.IsUserAdmin(obj.sourceUserId);
			
			if( obj.message[0] == '/' )
			{

				string[] words = obj.message.Split(' ');
				//string rem;
				//proccess
				if (words[0] == "/pos" && (isadmin || allowPos ) )
				{
					sendPlayerPosition(obj.sourceUserId);
				}
				
				if (isadmin && words[0] == "/util-allowpos-enable")
				{
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Position reporting enabled.");
					allowPos = true;
				}
				if (isadmin && words[0] == "/util-allowpos-disable")
				{
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Position reporting disabled.");
					allowPos = false;
				}

				if (isadmin && words[0].ToLower() == "/util-cleanup")
				{
					if (words.Count() >= 2)
					{
						if(words[1].ToLower() == "fo" || words[1].ToLower() == "floating-object")
						{
							bool force = false;
							if(words.Count() >= 3)
								if(words[2].ToLower() == "force")
								{
									force = true;
								}
							try
							{
								floatingObjectCleanup(force);
								ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Out of bounds floating object cleanup suceeded.");
							}
							catch (Exception ex)
							{
								ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Floating object cleanup failed: " + ex.Message.ToString());
							}
						}
						if (words[1].ToLower() == "fa" || words[1].ToLower() == "faction")
						{
							bool force = false;
							if (words.Count() >= 3)
								if (words[2].ToLower() == "force")
								{
									force = true;
								}
							try
							{
								factionCleanup(force);
								ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Faction cleanup suceeded.");
							}
							catch (Exception ex)
							{
								ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Faction cleanup failed: " + ex.Message.ToString());
							}
						}
						if (words[1].ToLower() == "ship" || words[1].ToLower() == "ships")
						{
							bool force = false;
							int dist = 0;
							if (words.Count() >= 4)
							{
								if (words[2].ToLower() == "force")
								{
									force = true;
								}
								if (words[2].ToLower() == "force" || words[2].ToLower() == "rescue")
								{
									
									try
									{
										dist = Convert.ToInt32(words[3]);
										if (dist < 10) throw new Exception("distance must be greater than 10km");
										shipCleanup(force, obj.sourceUserId, dist);
										ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Ship cleanup suceeded.");
									}
									catch (Exception ex)
									{
										ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Ship cleanup failed: " + ex.Message.ToString());
									}									
								}

							}
							else
								ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "Must specify 'force' or 'rescue', and a distance. ");
						}
					}
					return;
				}
				if (isadmin && words[0] == "/util-save")
				{
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "SE-utils - saved");
					saveXML();
				}
				if (isadmin && words[0] == "/util-load")
				{
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "SE-utils - loaded");
					loadXML();
				}
				if (isadmin && words[0] == "/util-loaddefaults")
				{
					ChatManager.Instance.SendPrivateChatMessage(obj.sourceUserId, "SE-utils - loaded defaults");
					loadXML(true);
				}
			}
			return; 
		}

		public void OnChatSent(SEModAPIExtensions.API.ChatManager.ChatEvent obj)
		{
			return; //no handling for motd right now
		}
		#endregion



		#endregion
	}
}
