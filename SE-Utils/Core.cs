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

using SEUtils.Exceptions;

namespace SEUtils
{
	public class SEUtils : PluginBase, IChatEventHandler, ICubeGridHandler
	{
		
		#region "Attributes"

		SEUtilsSettings settings = new SEUtilsSettings();

		Thread m_blockManager;
		Thread m_scriptManager;

		bool m_running = false;
		bool m_loading = true;
		bool m_debugging = false;
		bool m_newmethod = true;
		int m_debuglevel = 1;

		List<long> m_cubeGridsEntityID = new List<long>();

		#endregion

		#region "Constructors and Initializers"

		public void Core()
		{
			Console.WriteLine("SE Utils Plugin '" + Id.ToString() + "' constructed!");	
		}

		public override void Init()
		{
			m_loading = true;
			allowPos = true;
			resolution = 1000;
			Console.WriteLine("SE Utils Plugin '" + Id.ToString() + "' initialized!");
			loadXML();
			//start up ship manager
			m_running = true;
			m_blockManager = new Thread(beaconLoop);
			m_blockManager.Priority = ThreadPriority.BelowNormal;//make sure this thread isnt high priority
			m_blockManager.Start();

			m_scriptManager = new Thread(scriptloop);
			m_scriptManager.Priority = ThreadPriority.BelowNormal;
			m_scriptManager.Start();

			#region "Chat Command Registry"
			//Register Chat Commands
			ChatManager.ChatCommand command = new ChatManager.ChatCommand();
			command.callback = saveXML;
			command.command = "utils-save";
			command.requiresAdmin = true;
			ChatManager.Instance.RegisterChatCommand(command);

			command = new ChatManager.ChatCommand();
			command.callback = loadXML;
			command.command = "utils-load";
			command.requiresAdmin = true;
			ChatManager.Instance.RegisterChatCommand(command);

			command = new ChatManager.ChatCommand();
			command.callback = loadDefaults;
			command.command = "utils-loaddefaults";
			command.requiresAdmin = true;
			ChatManager.Instance.RegisterChatCommand(command);

			command = new ChatManager.ChatCommand();
			command.callback = UtilsCleanup;
			command.command = "utils-cleanup";
			command.requiresAdmin = true;
			ChatManager.Instance.RegisterChatCommand(command);

			//End Register Chat commands
			#endregion
		}

		#endregion

		#region "Properties"

		[Browsable(true)]
		[ReadOnly(true)]
		public string DefaultLocation
		{
			get { return System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\"; }

		}
		[Browsable(true)]
		[ReadOnly(true)]
		public string Location
		{
			get { return SandboxGameAssemblyWrapper.Instance.GetServerConfig().LoadWorld + "\\"; }

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
		[Category("SE-Utils")]
		[Description("Resolution in milliseconds of the beacon update, 1000 = 1 second")]
		[Browsable(true)]
		[ReadOnly(false)]
		public int resolution
		{
			get { return settings.resolution; }
			set { settings.resolution = value; }
		}

		[Category("SE-Utils")]
		[Description("Minimum cleanup distance")]
		[Browsable(true)]
		[ReadOnly(false)]
		public int minCleanupDistance
		{
			get { return settings.minCleanupDistance; }
			set { settings.minCleanupDistance = value; }
		}

		[Category("SE-Utils")]
		[Description("Allow Beacon Pos Updates.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool allowBeaconPos
		{
			get { return settings.allowBeaconPos; }
			set { settings.allowBeaconPos = value; }
		}
		[Category("SE-Utils")]
		[Description("Allow Beacon Directional Updates.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool allowBeaconDir
		{
			get { return settings.allowBeaconDir; }
			set { settings.allowBeaconDir = value; }
		}

		[Category("SE-Utils")]
		[Description("Allow Antenna Pos Updates.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool allowAntennaPos
		{
			get { return settings.allowAntennaPos; }
			set { settings.allowAntennaPos = value; }
		}
		[Category("SE-Utils")]
		[Description("Allow Antenna Directional Updates.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool allowAntennaDir
		{
			get { return settings.allowAntennaDir; }
			set { settings.allowAntennaDir = value; }
		}

		[Category("Scripts")]
		[Description("Create chat scripts triggered on an interval.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public List<SEUtilsScript> script
		{
			get { return settings.scripts; }
			set { settings.scripts = value; }
		}

		[Category("Plugin Status")]
		[Description("Beacon Thread state")]
		[Browsable(true)]
		[ReadOnly(true)]
		public string BeaconLoopThreadState
		{
			get { return m_blockManager.ThreadState.ToString(); }
		}
		[Category("Plugin Status")]
		[Description("Script Thread state")]
		[Browsable(true)]
		[ReadOnly(true)]
		public string ScriptLoopThreadState
		{
			get { return m_scriptManager.ThreadState.ToString(); }
		}
		[Category("Plugin Status")]
		[Description("Running")]
		[Browsable(true)]
		[ReadOnly(true)]
		public bool isrunning
		{
			get { return m_running; }
		}
		[Category("Plugin Status")]
		[Description("is loaded")]
		[Browsable(true)]
		[ReadOnly(true)]
		public bool isloaded
		{
			get { return !m_loading; }
		}
		[Category("Plugin Status")]
		[Description("is debugging")]
		[Browsable(true)]
		[ReadOnly(true)]
		public bool isdebugging
		{
			get { return m_debugging || SandboxGameAssemblyWrapper.IsDebugging; }
		}
		[Category("Plugin Status")]
		[Description("Debug Output")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool debugging
		{
			get { return m_debugging; }
			set { m_debugging = value; }
		}
		[Category("Plugin Status")]
		[Description("Debug Level")]
		[Browsable(true)]
		[ReadOnly(false)]
		public int debugLevel
		{
			get { return m_debuglevel; }
			set { m_debuglevel = value; }
		}
		[Category("Plugin Status")]
		[Description("Entity ID list")]
		[Browsable(true)]
		[ReadOnly(false)]
		public List<long> watchedEntityIDs
		{
			get { return m_cubeGridsEntityID; }
			set { m_cubeGridsEntityID = value; }
		}
		[Category("Plugin Status")]
		[Description("Use new update method, should be less lag inducing.")]
		[Browsable(true)]
		[ReadOnly(false)]
		public bool newmethod
		{
			get { return m_newmethod; }
			set { m_newmethod = value; }
		}
		#endregion

		#region "Methods"
		#region "Core"
		public void saveXML()
		{

			XmlSerializer x = new XmlSerializer(typeof(SEUtilsSettings));
			TextWriter writer = new StreamWriter(Location + "SEUtils-Settings.xml");
			x.Serialize(writer, settings);
			writer.Close();

		}
		public void loadXML(bool l_default = false)
		{
			try
			{
				if (File.Exists(Location + "SEUtils-Settings.xml") && !l_default)
				{

					XmlSerializer x = new XmlSerializer(typeof(SEUtilsSettings));
					TextReader reader = new StreamReader(Location + "SEUtils-Settings.xml");
					SEUtilsSettings obj = (SEUtilsSettings)x.Deserialize(reader);
					settings = obj;
					reader.Close();
					return;
				}
			}
			catch (Exception ex)
			{
				LogManager.APILog.WriteLineAndConsole("Could not load configuration: " + ex.ToString());
			}
			try
			{
				if (File.Exists(DefaultLocation + "SEUtils-Settings.xml"))
				{
					XmlSerializer x = new XmlSerializer(typeof(SEUtilsSettings));
					TextReader reader = new StreamReader(DefaultLocation + "SEUtils-Settings.xml");
					SEUtilsSettings obj = (SEUtilsSettings)x.Deserialize(reader);
					settings = obj;
					reader.Close();
				}
			}
			catch (Exception ex)
			{
				LogManager.APILog.WriteLineAndConsole("Could not load configuration: " + ex.ToString());
			}

		}
		private void beaconLoop()
		{
			Thread.Sleep(10000);
			List<CubeGridEntity> ships = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
			foreach (CubeGridEntity ship in ships)
			{
				if(debugging)
					LogManager.APILog.WriteLineAndConsole("Added " + ship.EntityId.ToString());
				m_cubeGridsEntityID.Add(ship.EntityId);
			}
			m_loading = false;
			while(m_running)
			{
				if(newmethod)
				{
					#region "beacon/antenna update loop"
					List<long> _entityIDList = new List<long>(m_cubeGridsEntityID);
					foreach (long shipEntityId in _entityIDList)
					{
						try
						{
							CubeGridEntity ship = (CubeGridEntity)GameEntityManager.GetEntity(shipEntityId);
							if (ship == null)
							{
								if (isdebugging)
									LogManager.APILog.WriteLineAndConsole("Removed " + shipEntityId.ToString());
								throw new NullReferenceException();

							}
							if (ship.IsLoading)
							{
								if (isdebugging)
									LogManager.APILog.WriteLineAndConsole("Ship is loading: " + shipEntityId.ToString());
								continue;
							}

							Thread.Yield();//could be an expensive function, lets yeild to the OS every iteration. 
							try
							{
								foreach (CubeBlockEntity cubeBlock in ship.CubeBlocks)
								{
									if (cubeBlock is AntennaEntity)
									{
										if (isdebugging && m_debuglevel > 1)
										{
											LogManager.APILog.WriteLineAndConsole("Antenna found getting name - Entityid: " + shipEntityId.ToString());
										}
										AntennaEntity antenna = (AntennaEntity)cubeBlock;
										if (isdebugging && m_debuglevel > 1)
										{
											LogManager.APILog.WriteLineAndConsole(antenna.CustomName.ToString());
										}
										if (antenna.CustomName != null)
										{
											string name = antenna.CustomName;
											if (name.Substring(0, 4) == "Pos:" && allowAntennaPos)
											{
												Thread.Sleep(10);
												if (isdebugging && m_debuglevel > 1)
												{
													LogManager.APILog.WriteLineAndConsole("Updating antenna name");
												}
												name = "Pos: " + ship.Position.ToString();
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Antenna name before:" + antenna.CustomName.ToString());
												antenna.CustomName = name;
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Antenna name after:" + antenna.CustomName.ToString());
											}
											if (name.Substring(0, 4) == "Dir:" && allowAntennaDir)
											{
												Thread.Sleep(10);
												if (isdebugging && m_debuglevel > 1)
												{
													LogManager.APILog.WriteLineAndConsole("Updating antenna name");
												}
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Antenna name before:" + antenna.CustomName.ToString());
												name = "Dir: " + ship.Forward.ToString();
												antenna.CustomName = name;
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Antenna name after:" + antenna.CustomName.ToString());
											}
										}
									}
									else if (cubeBlock is BeaconEntity)
									{

										if (isdebugging && m_debuglevel > 1)
										{
											LogManager.APILog.WriteLineAndConsole("Beacon found getting name - Entityid: " + shipEntityId.ToString());
										}
										BeaconEntity beacon = (BeaconEntity)cubeBlock;
										if (isdebugging && m_debuglevel > 1)
										{
											LogManager.APILog.WriteLineAndConsole(beacon.CustomName.ToString());
										}
										if (beacon.CustomName != null)
										{
											string name = beacon.CustomName;
											if (name.Substring(0, 4) == "Pos:" && allowBeaconPos)
											{
												Thread.Sleep(10);
												if (isdebugging && m_debuglevel > 1)
												{
													LogManager.APILog.WriteLineAndConsole("Updating beacon name");
												}
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Beacon name before:" + beacon.CustomName.ToString());
												name = "Pos: " + ship.Position.ToString();
												beacon.CustomName = name;
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Beacon name after:" + beacon.CustomName.ToString());
											}
											if (name.Substring(0, 4) == "Dir:" && allowBeaconDir)
											{
												Thread.Sleep(10);
												if (isdebugging && m_debuglevel > 1)
												{
													LogManager.APILog.WriteLineAndConsole("Updating beacon name");
												}
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Beacon name before:" + beacon.CustomName.ToString());
												name = "Dir: " + ship.Forward.ToString();
												beacon.CustomName = name;
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Beacon name after:" + beacon.CustomName.ToString());
											}
										}
									}
								}
							}
							catch (Exception ex)
							{
								if (isdebugging)
								{
									LogManager.APILog.WriteLineAndConsole("Exception thrown when setting beacons/antenna on " + shipEntityId.ToString());
									LogManager.APILog.WriteLineAndConsole(ex.ToString());
								}
								continue;
							}
						}
						catch (Exception ex)
						{
							//could not pull remove it
							if (isdebugging)
								LogManager.APILog.WriteLineAndConsole("Game entity manager returned null. EntityID " + shipEntityId + " error: " + ex.ToString());
							m_cubeGridsEntityID.Remove(shipEntityId);
							continue;
						}
					}
					#endregion
				}
				else
				{
					#region "beacon/antenna update loop OLD method"
					List<CubeGridEntity> _entityList = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
					foreach (CubeGridEntity ship in _entityList)
					{
						try
						{
							//CubeGridEntity ship = (CubeGridEntity)GameEntityManager.GetEntity(shipEntityId);
							long shipEntityId = ship.EntityId;
							if (ship.IsLoading)
							{
								if (isdebugging)
									LogManager.APILog.WriteLineAndConsole("Ship is loading: " + shipEntityId.ToString());
								continue;
							}

							Thread.Yield();//could be an expensive function, lets yeild to the OS every iteration. 
							try
							{
								foreach (CubeBlockEntity cubeBlock in ship.CubeBlocks)
								{
									if (cubeBlock is AntennaEntity)
									{
										if (isdebugging && m_debuglevel > 1)
										{
											LogManager.APILog.WriteLineAndConsole("Antenna found getting name - Entityid: " + shipEntityId.ToString());
										}
										AntennaEntity antenna = (AntennaEntity)cubeBlock;
										if (isdebugging && m_debuglevel > 1)
										{
											LogManager.APILog.WriteLineAndConsole(antenna.CustomName.ToString());
										}
										if (antenna.CustomName != null)
										{
											string name = antenna.CustomName;
											if (name.Substring(0, 4) == "Pos:" && allowAntennaPos)
											{
												if (isdebugging && m_debuglevel > 1)
												{
													LogManager.APILog.WriteLineAndConsole("Updating antenna name");
												}
												name = "Pos: " + ship.Position.ToString();
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Antenna name before:" + antenna.CustomName.ToString());
												antenna.CustomName = name;
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Antenna name after:" + antenna.CustomName.ToString());
											}
											if (name.Substring(0, 4) == "Dir:" && allowAntennaDir)
											{
												if (isdebugging && m_debuglevel > 1)
												{
													LogManager.APILog.WriteLineAndConsole("Updating antenna name");
												}
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Antenna name before:" + antenna.CustomName.ToString());
												name = "Dir: " + ship.Forward.ToString();
												antenna.CustomName = name;
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Antenna name after:" + antenna.CustomName.ToString());
											}
										}
									}
									else if (cubeBlock is BeaconEntity)
									{

										if (isdebugging && m_debuglevel > 1)
										{
											LogManager.APILog.WriteLineAndConsole("Beacon found getting name - Entityid: " + shipEntityId.ToString());
										}
										BeaconEntity beacon = (BeaconEntity)cubeBlock;
										if (isdebugging && m_debuglevel > 1)
										{
											LogManager.APILog.WriteLineAndConsole(beacon.CustomName.ToString());
										}
										if (beacon.CustomName != null)
										{
											string name = beacon.CustomName;
											if (name.Substring(0, 4) == "Pos:" && allowBeaconPos)
											{
												if (isdebugging && m_debuglevel > 1)
												{
													LogManager.APILog.WriteLineAndConsole("Updating beacon name");
												}
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Beacon name before:" + beacon.CustomName.ToString());
												name = "Pos: " + ship.Position.ToString();
												beacon.CustomName = name;
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Beacon name after:" + beacon.CustomName.ToString());
											}
											if (name.Substring(0, 4) == "Dir:" && allowBeaconDir)
											{
												if (isdebugging && m_debuglevel > 1)
												{
													LogManager.APILog.WriteLineAndConsole("Updating beacon name");
												}
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Beacon name before:" + beacon.CustomName.ToString());
												name = "Dir: " + ship.Forward.ToString();
												beacon.CustomName = name;
												if (isdebugging && m_debuglevel > 1)
													LogManager.APILog.WriteLineAndConsole("Beacon name after:" + beacon.CustomName.ToString());
											}
										}
									}
								}
							}
							catch (Exception ex)
							{
								if (isdebugging)
								{
									LogManager.APILog.WriteLineAndConsole("Exception thrown when setting beacons/antenna on " + shipEntityId.ToString());
									LogManager.APILog.WriteLineAndConsole(ex.ToString());
								}
								continue;
							}
						}
						catch (Exception ex)
						{
							//could not pull remove it
							if (isdebugging)
								LogManager.APILog.WriteLineAndConsole("SEUTils beacon loop error: " + ex.ToString());
							//m_cubeGridsEntityID.Remove(shipEntityId);
							continue;
						}
					}
					#endregion
				}

				Thread.Sleep(resolution);//resolution of this loop
			}
		}
		private void scriptloop()
		{
			foreach (SEUtilsScript _script in script)
			{

				while (DateTime.UtcNow > _script.nextrun)
				{
					if (_script.interval <= 0) break;
					_script.lastrun = _script.nextrun;
					_script.nextrun = _script.nextrun + TimeSpan.FromSeconds(_script.interval);
				}
			}

			while (m_running)
			{
				foreach(SEUtilsScript _script in script)
				{

					if(DateTime.UtcNow > _script.nextrun)
					{
						if (_script.interval <= 0) continue;
						_script.lastrun = _script.nextrun;
						_script.nextrun = _script.nextrun + TimeSpan.FromSeconds(_script.interval);
						if (_script.enabled)
						{
							foreach ( SEUtilsChatCommands _command in _script.commands)
							{
								Thread.Sleep(_command.delay * 1000);
								ChatManager.Instance.SendPublicChatMessage(_command.ToString());
							}
						}
					}
				}
				Thread.Sleep(1000);
			}
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
			if (steamid == 0) 
				throw new UtilsNoSteamIDException("Server GUI cannot issue this command.");
			Vector3Wrapper myPos = getCharacter(steamid).Position;
			int movedist = 50;
			List<CubeGridEntity> shipList = SectorObjectManager.Instance.GetTypedInternalData<CubeGridEntity>();
			int maxsize = dist * 1000;
			if (maxsize < minCleanupDistance * 1000) throw new Exception("Distance is less than " + minCleanupDistance.ToString() + "KM, aborting cleanup.");
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
		#endregion
		#region "EventHandlers"
		public override void Update()
		{
			return;
		}
		public override void Shutdown()
		{
			m_running = false;
			saveXML();
			return;
		}
		public void OnChatReceived(SEModAPIExtensions.API.ChatManager.ChatEvent obj)
		{
			//PlayerMap.Instance.GetSteamId(long entityId)
			//PlayerMap.Instance.GetPlayerId(ulong steamId)
			if (obj.sourceUserId == 0)
				return;
			bool isadmin = PlayerManager.Instance.IsUserAdmin(obj.sourceUserId);
			
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


			}
			return; 
		}
		public void OnChatSent(SEModAPIExtensions.API.ChatManager.ChatEvent obj)
		{
			return; //no handling for motd right now
		}
		public void OnCubeGridLoaded(CubeGridEntity grid)
		{

		}
		public void OnCubeGridDeleted(CubeGridEntity grid)
		{
			if(m_cubeGridsEntityID.Exists(x => x == grid.EntityId))
			{
				m_cubeGridsEntityID.Remove(grid.EntityId);
			}
		}
		public void OnCubeGridCreated(CubeGridEntity grid)
		{
			if (!m_loading)
			{
				m_cubeGridsEntityID.Add(grid.EntityId);
				if(debugging)
					LogManager.APILog.WriteLineAndConsole("OnCubeGridCreated Added " + grid.EntityId.ToString());
			}
			
		}
		public void OnCubeGridMoved(CubeGridEntity grid)
		{

		}
		#endregion
		#region "Chat Callbacks"
		public void saveXML(ChatManager.ChatEvent _event)
		{
			saveXML();
			try
			{
				if (_event.remoteUserId > 0)
					ChatManager.Instance.SendPrivateChatMessage(_event.remoteUserId, "Utils configuration saved.");
				else
					Console.WriteLine("Utils configuration saved.");
			}
			catch
			{
				//donothing
			}

		}
		public void loadXML(ChatManager.ChatEvent _event)
		{
			loadXML(false);
			try
			{
				if (_event.remoteUserId > 0)
					ChatManager.Instance.SendPrivateChatMessage(_event.remoteUserId, "Utils configuration loaded.");
				else
					Console.WriteLine("Utils configuration loaded.");
			}
			catch
			{
				//donothing
			}
		}
		public void loadDefaults(ChatManager.ChatEvent _event)
		{
			loadXML(true);
			try
			{
				if (_event.remoteUserId > 0)
					ChatManager.Instance.SendPrivateChatMessage(_event.remoteUserId, "Utils configuration defaults loaded.");
				else
					Console.WriteLine("Utils configuration defaults loaded.");
			}
			catch
			{
				//donothing
			}
		}
		public void UtilsCleanup(ChatManager.ChatEvent _event)
		{
			try
			{
				string[] words = _event.message.Split(' ');
				if (words.Count() >= 2)
				{
					if (words[1].ToLower() == "fo" || words[1].ToLower() == "floating-object")
					{
						bool force = false;
						if (words.Count() >= 3)
							if (words[2].ToLower() == "force")
							{
								force = true;
							}
						try
						{
							floatingObjectCleanup(force);
							ChatManager.Instance.SendPrivateChatMessage(_event.sourceUserId, "Floating object cleanup suceeded.");
						}
						catch (Exception ex)
						{
							ChatManager.Instance.SendPrivateChatMessage(_event.sourceUserId, "Floating object cleanup failed: " + ex.Message.ToString());
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
							ChatManager.Instance.SendPrivateChatMessage(_event.sourceUserId, "Faction cleanup suceeded.");
						}
						catch (Exception ex)
						{
							ChatManager.Instance.SendPrivateChatMessage(_event.sourceUserId, "Faction cleanup failed: " + ex.Message.ToString());
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
									shipCleanup(force, _event.sourceUserId, dist);
									ChatManager.Instance.SendPrivateChatMessage(_event.sourceUserId, "Ship cleanup suceeded.");
								}
								catch (Exception ex)
								{
									ChatManager.Instance.SendPrivateChatMessage(_event.sourceUserId, "Ship cleanup failed: " + ex.Message.ToString());
								}
							}

						}
						else
							ChatManager.Instance.SendPrivateChatMessage(_event.sourceUserId, "Must specify 'force' or 'rescue', and a distance. ");
					}
				}
			}
			catch (Exception ex)
			{
				LogManager.APILog.WriteLine("UtilsCleanup: " + ex.ToString());
			}

		}
		#endregion
		#endregion
	}
}
