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
