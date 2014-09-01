namespace SEUtils.Exceptions
{
	class UtilsNoSteamIDException : System.ApplicationException
	{
		public UtilsNoSteamIDException() { }
		public UtilsNoSteamIDException(string message) { }
		public UtilsNoSteamIDException(string message, System.Exception inner) { }

		protected UtilsNoSteamIDException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) { }
	}
}
