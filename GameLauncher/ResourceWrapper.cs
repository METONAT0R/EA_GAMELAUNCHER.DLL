using log4net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace GameLauncher
{
	public class ResourceWrapper
	{
		private static ResourceWrapper _instance = new ResourceWrapper();

		private Dictionary<string, SingleAssemblyComponentResourceManager> _resourceManagers;

		private static readonly ILog mLogger = LogManager.GetLogger(typeof(ResourceWrapper));

		public static ResourceWrapper Instance => _instance;

		private ResourceWrapper()
		{
			_resourceManagers = new Dictionary<string, SingleAssemblyComponentResourceManager>();
		}

		public string GetString(string baseName, string id, CultureInfo cInfo)
		{
			try
			{
				if (!_resourceManagers.ContainsKey(baseName))
				{
					_resourceManagers.Add(baseName, new SingleAssemblyComponentResourceManager(baseName, Assembly.GetCallingAssembly()));
				}
				string @string = _resourceManagers[baseName].GetString(id, cInfo);
				if (@string == null)
				{
					mLogger.Warn($"Missing resource string: <{id}-{cInfo.ToString()}>");
					return $"<{id}-{cInfo.ToString()}>";
				}
				return @string;
			}
			catch (Exception)
			{
				mLogger.Warn($"Missing resource string: <{id}-{cInfo.ToString()}>");
				return $"<{id}-{cInfo.ToString()}>";
			}
		}

		public string GetString(string baseName, string id)
		{
			try
			{
				if (!_resourceManagers.ContainsKey(baseName))
				{
					_resourceManagers.Add(baseName, new SingleAssemblyComponentResourceManager(baseName, Assembly.GetCallingAssembly()));
				}
				string @string = _resourceManagers[baseName].GetString(id);
				if (@string == null)
				{
					mLogger.Warn($"Missing resource string: <{id}>");
					return $"<{id}>";
				}
				return @string;
			}
			catch (Exception)
			{
				mLogger.Warn($"Missing resource string: <{id}>");
				return $"<{id}>";
			}
		}
	}
}
