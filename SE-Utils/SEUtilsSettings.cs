using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SEUtils
{
	[Serializable()]
	public class SEUtilsSettings
	{
		private bool m_allowpos = true;
		private int m_resolution = 1000;
		private int m_minCleanupDistance = 10;
		private bool m_allowAntennaPos = true;
		private bool m_allowAntennaDir = true;
		private bool m_allowBeaconPos = true;
		private bool m_allowBeaconDir = true;
		private List<SEUtilsScript> m_scripts = new List<SEUtilsScript>();

		public int resolution
		{
			set { if (value > 100) m_resolution = value; }
			get { return m_resolution; }
		}
		public bool allowpos
		{
			set { m_allowpos = value; }
			get { return m_allowpos; }
		}
		public int minCleanupDistance
		{
			get { return m_minCleanupDistance; }
			set { if (value >= 0) m_minCleanupDistance = value; }
		}
		public bool allowAntennaPos
		{
			get { return m_allowAntennaPos; }
			set { m_allowAntennaPos = value; }
		}
		public bool allowAntennaDir
		{
			get { return m_allowAntennaDir; }
			set { m_allowAntennaDir = value; }
		}
		public bool allowBeaconPos
		{
			get { return m_allowBeaconPos; }
			set { m_allowBeaconPos = value; }
		}
		public bool allowBeaconDir
		{
			get { return m_allowBeaconDir; }
			set { m_allowBeaconDir = value; }
		}
		public List<SEUtilsScript> scripts
		{
			get { return m_scripts; }
			set { m_scripts = value; }
		}

	}
	[Serializable()]
	public class SEUtilsScript
	{
		private DateTime m_lastrun;
		private DateTime m_nextrun;
		private int m_interval = 86400;
		private string m_name = "script";
		private bool m_enabled = false;
		private List<SEUtilsChatCommands> m_commands = new List<SEUtilsChatCommands>();

		public List<SEUtilsChatCommands> commands
		{
			get { return m_commands; }
			set { m_commands = value; }
		}
		public string name
		{
			get { return m_name; }
			set { m_name = value; }
		}
		public int interval
		{
			get { return m_interval; }
			set { if(value > 0) m_interval = value; }
		}
		public DateTime lastrun
		{
			get { return m_lastrun; }
			set 
			{ 
				if(DateTime.UtcNow + TimeSpan.FromSeconds(m_interval) > value)
				{
					m_lastrun = value;
					m_nextrun = value + TimeSpan.FromSeconds(m_interval); 
				}

			}
		}
		public DateTime nextrun
		{
			get { return m_nextrun; }
			set 
			{
				if(value > DateTime.UtcNow) 
					m_nextrun = value; 
			}
		}
		public bool enabled
		{
			get { return m_enabled; }
			set { m_enabled = true; }
		}
		public override string ToString()
		{
			return m_name;
		}
	}
	[Serializable()]
	public class SEUtilsChatCommands
	{
		private string m_chatcommand = "";
		private int m_delay = 0;
		public override string ToString()
		{
			return m_chatcommand.ToString();
		}
		public string chatCommand
		{
			get { return m_chatcommand; }
			set { m_chatcommand = value; }
		}
		public int delay
		{
			get { return m_delay; }
			set {if(value > 0) m_delay = value;}
		}
	}
}
