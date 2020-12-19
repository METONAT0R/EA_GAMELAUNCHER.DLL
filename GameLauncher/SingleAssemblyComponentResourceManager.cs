using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;

namespace GameLauncher
{
	internal class SingleAssemblyComponentResourceManager : ComponentResourceManager
	{
		private Type _contextTypeInfo;

		private CultureInfo _neutralResourcesCulture;

		public SingleAssemblyComponentResourceManager(Type t)
			: base(t)
		{
			_contextTypeInfo = t;
			ResourceSets = new Hashtable();
		}

		public SingleAssemblyComponentResourceManager(string baseName, Assembly assembly)
		{
			MainAssembly = assembly;
			_contextTypeInfo = null;
			BaseNameField = baseName;
			ResourceSets = new Hashtable();
		}

		protected override ResourceSet InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents)
		{
			ResourceSet rs = (ResourceSet)ResourceSets[culture];
			if (rs == null)
			{
				Stream stream = null;
				if (_neutralResourcesCulture == null)
				{
					_neutralResourcesCulture = ResourceManager.GetNeutralResourcesLanguage(MainAssembly);
				}
				if (_neutralResourcesCulture.Equals(culture))
				{
					culture = CultureInfo.InvariantCulture;
				}
				string resourceFileName = GetResourceFileName(culture);
				stream = MainAssembly.GetManifestResourceStream(resourceFileName);
				if (stream == null)
				{
					resourceFileName += ".custom";
					string[] manifestResourceNames = MainAssembly.GetManifestResourceNames();
					string[] array = manifestResourceNames;
					foreach (string text in array)
					{
						if (text.EndsWith(resourceFileName, StringComparison.InvariantCultureIgnoreCase))
						{
							stream = MainAssembly.GetManifestResourceStream(text);
							break;
						}
					}
				}
				if (stream != null)
				{
					rs = new ResourceSet(stream);
					AddResourceSet(ResourceSets, culture, ref rs);
				}
				else if (tryParents)
				{
					CultureInfo parent = culture.Parent;
					rs = InternalGetResourceSet(parent, createIfNotExists, tryParents);
					if (rs != null)
					{
						AddResourceSet(ResourceSets, culture, ref rs);
					}
				}
			}
			return rs;
		}

		private static void AddResourceSet(Hashtable localResourceSets, CultureInfo culture, ref ResourceSet rs)
		{
			lock (localResourceSets)
			{
				ResourceSet resourceSet = (ResourceSet)localResourceSets[culture];
				if (resourceSet != null)
				{
					if (!object.Equals(resourceSet, rs))
					{
						rs.Dispose();
						rs = resourceSet;
					}
				}
				else
				{
					localResourceSets.Add(culture, rs);
				}
			}
		}
	}
}
