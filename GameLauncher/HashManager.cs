using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

namespace GameLauncher
{
	internal class HashManager
	{
		private class HashTuple
		{
			public string Old;

			public string New;

			public bool Exists;

			public bool Downloaded;

			public long Ticks;

			public HashTuple(string oldHash, string newHash, bool exists, long ticks)
			{
				Old = oldHash;
				New = newHash;
				Exists = exists;
				Downloaded = false;
				Ticks = ticks;
			}

			public HashTuple(string oldHash, string newHash, bool exists)
				: this(oldHash, newHash, exists, 0L)
			{
			}
		}

		private const int MaxWorkers = 3;

		private const string HashFileName = "HashFile";

		private const string CryptoKey = "12345678";

		private Dictionary<string, HashTuple> _fileList;

		private Queue<string> _queueHash;

		private static readonly object _queueHashLock = new object();

		private static int _workerCount = 0;

		private bool _useCache = true;

		private bool _signalStop;

		private static readonly ILog mLogger = LogManager.GetLogger("HashManager");

		private static HashManager _instance = new HashManager();

		internal static HashManager Instance => _instance;

		private HashManager()
		{
			_useCache = true;
			_signalStop = false;
			_fileList = new Dictionary<string, HashTuple>();
			_queueHash = new Queue<string>();
		}

		public void Start(XmlDocument doc, string patchPath, string hashFileNameSuffix, int maxWorkers)
		{
			try
			{
				lock (_fileList)
				{
					_signalStop = false;
					string path = Path.Combine(Environment.CurrentDirectory, patchPath);
					int startIndex = Environment.CurrentDirectory.Length + 1;
					string[] files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
					string[] array = files;
					foreach (string text in array)
					{
						_fileList.Add(text.Substring(startIndex).Replace('\\', '/').ToLower(CultureInfo.InvariantCulture), null);
					}
					foreach (XmlNode item in doc.SelectNodes("/index/fileinfo"))
					{
						string innerText = item.SelectSingleNode("path").InnerText;
						string innerText2 = item.SelectSingleNode("file").InnerText;
						innerText = PatchPath(innerText, patchPath);
						string text2 = (innerText + "/" + innerText2).ToLower(CultureInfo.InvariantCulture);
						if (item.SelectSingleNode("hash") == null)
						{
							if (_fileList.ContainsKey(text2))
							{
								_fileList[text2] = new HashTuple(null, null, exists: true);
							}
							else
							{
								_fileList.Add(text2, new HashTuple(null, null, exists: false));
							}
							continue;
						}
						if (_fileList.ContainsKey(text2))
						{
							_fileList[text2] = new HashTuple(string.Empty, item.SelectSingleNode("hash").InnerText, exists: true);
						}
						else
						{
							_fileList.Add(text2, new HashTuple(string.Empty, item.SelectSingleNode("hash").InnerText, exists: false));
						}
						_queueHash.Enqueue(text2);
					}
					List<string> list = new List<string>(_fileList.Keys);
					int count = list.Count;
					for (int num = count - 1; num >= 0; num--)
					{
						string key = list[num];
						if (_fileList[key] == null)
						{
							_fileList.Remove(key);
						}
					}
					if (_useCache && File.Exists("HashFile" + hashFileNameSuffix))
					{
						mLogger.Debug("Using Cache");
						try
						{
							DESCryptoServiceProvider dESCryptoServiceProvider = new DESCryptoServiceProvider();
							dESCryptoServiceProvider.Key = Encoding.ASCII.GetBytes("12345678");
							dESCryptoServiceProvider.IV = Encoding.ASCII.GetBytes("12345678");
							ICryptoTransform transform = dESCryptoServiceProvider.CreateDecryptor();
							using (FileStream stream = new FileStream("HashFile" + hashFileNameSuffix, FileMode.Open))
							{
								using (CryptoStream stream2 = new CryptoStream(stream, transform, CryptoStreamMode.Read))
								{
									using (StreamReader streamReader = new StreamReader(stream2))
									{
										string text3 = null;
										while ((text3 = streamReader.ReadLine()) != null)
										{
											if (_signalStop)
											{
												return;
											}
											string[] array2 = text3.Split('\t');
											string text4 = array2[0].ToLower(CultureInfo.InvariantCulture);
											if (_fileList.ContainsKey(text4) && _fileList[text4].Exists)
											{
												long ticks = File.GetLastWriteTime(text4).Ticks;
												if (long.Parse(array2[2]) == ticks && !string.IsNullOrEmpty(array2[1]))
												{
													_fileList[text4].Old = array2[1];
												}
												_fileList[text4].Ticks = ticks;
											}
										}
									}
								}
							}
						}
						catch (CryptographicException ex)
						{
							mLogger.Error("Start Exception: " + ex.ToString());
						}
						catch (Exception ex2)
						{
							mLogger.Error("Start Exception: " + ex2.ToString());
						}
						finally
						{
							File.Delete("HashFile" + hashFileNameSuffix);
						}
					}
					_workerCount = 0;
					while (_workerCount < maxWorkers && _queueHash.Count > 0)
					{
						mLogger.DebugFormat("Adding new hash worker ({0}/{1})", _workerCount, maxWorkers);
						BackgroundWorker backgroundWorker = new BackgroundWorker();
						backgroundWorker.DoWork += BackgroundWorker_DoWork;
						backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerComplete;
						backgroundWorker.RunWorkerAsync();
						_workerCount++;
					}
				}
			}
			catch (Exception innerException)
			{
				throw new HashManagerException("Exception Starting HashManager", innerException);
			}
		}

		private string PatchPath(string basePath, string patchPath)
		{
			if (!string.IsNullOrEmpty(patchPath))
			{
				int num = basePath.IndexOf("/");
				basePath = ((num < 0) ? patchPath : basePath.Replace(basePath.Substring(0, num), patchPath));
			}
			return basePath;
		}

		public string GetHashOld(string fileName)
		{
			string text = string.Empty;
			fileName = fileName.ToLower(CultureInfo.InvariantCulture);
			while (true)
			{
				lock (_fileList[fileName])
				{
					text = _fileList[fileName].Old;
				}
				if (text != string.Empty)
				{
					break;
				}
				Thread.Sleep(100);
			}
			return text;
		}

		public bool HashesMatch(string fileName)
		{
			fileName = fileName.ToLower(CultureInfo.InvariantCulture);
			try
			{
				while (true)
				{
					lock (_fileList[fileName])
					{
						HashTuple hashTuple = _fileList[fileName];
						if (hashTuple.Old != string.Empty)
						{
							return hashTuple.New == hashTuple.Old;
						}
					}
					Thread.Sleep(100);
				}
			}
			catch (Exception ex)
			{
				mLogger.Error("Retrieving hash for " + fileName);
				mLogger.Error("HashesMatch Exception: " + ex.ToString());
			}
			return false;
		}

		public void WriteHashCache(string hashFileNameSuffix)
		{
			lock (_fileList)
			{
				mLogger.Debug("Writing Hash cache HashFile" + hashFileNameSuffix);
				_signalStop = true;
				try
				{
					using (FileStream stream = new FileStream("HashFile" + hashFileNameSuffix, FileMode.Create))
					{
						DESCryptoServiceProvider dESCryptoServiceProvider = new DESCryptoServiceProvider();
						byte[] array2 = dESCryptoServiceProvider.Key = (dESCryptoServiceProvider.IV = Encoding.ASCII.GetBytes("12345678"));
						ICryptoTransform transform = dESCryptoServiceProvider.CreateEncryptor();
						using (CryptoStream stream2 = new CryptoStream(stream, transform, CryptoStreamMode.Write))
						{
							using (StreamWriter streamWriter = new StreamWriter(stream2))
							{
								foreach (string key in _fileList.Keys)
								{
									string empty = string.Empty;
									HashTuple hashTuple = _fileList[key];
									empty = ((!hashTuple.Downloaded) ? hashTuple.Old : hashTuple.New);
									try
									{
										if (!string.IsNullOrEmpty(empty))
										{
											streamWriter.WriteLine($"{key}\t{empty}\t{hashTuple.Ticks}");
										}
										else
										{
											streamWriter.WriteLine($"{key}\t\t{hashTuple.Ticks}");
										}
									}
									catch (Exception)
									{
										mLogger.WarnFormat("The file {0} does not exist so we cannot calculate the date", key);
									}
								}
							}
						}
					}
				}
				catch (Exception ex2)
				{
					mLogger.Error("WriteHashCache Exception: " + ex2.ToString());
				}
			}
		}

		public void UpdateTicks(string fileName, long ticks)
		{
			fileName = fileName.ToLower(CultureInfo.InvariantCulture);
			lock (_fileList)
			{
				_fileList[fileName].Ticks = ticks;
				_fileList[fileName].Exists = true;
				_fileList[fileName].Downloaded = true;
			}
		}

		public void Clear()
		{
			mLogger.Debug("Clearing the HashManager");
			lock (_fileList)
			{
				_signalStop = true;
				lock (_queueHash)
				{
					_queueHash.Clear();
				}
				while (_workerCount > 0)
				{
					Thread.Sleep(100);
				}
				_fileList.Clear();
			}
		}

		private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs args)
		{
			while (true)
			{
				string text = null;
				lock (_queueHashLock)
				{
					if (_queueHash.Count > 0)
					{
						text = _queueHash.Dequeue();
					}
					if (string.IsNullOrEmpty(text) || _signalStop)
					{
						mLogger.DebugFormat("Stopping hash worker ({0}/{1})", _workerCount, 3);
						_workerCount--;
						return;
					}
				}
				string old = null;
				bool flag = false;
				string text2 = null;
				lock (_fileList[text])
				{
					HashTuple hashTuple = _fileList[text];
					flag = hashTuple.Exists;
					text2 = hashTuple.Old;
				}
				if (flag)
				{
					if (!string.IsNullOrEmpty(text2))
					{
						old = text2;
					}
					else
					{
						try
						{
							using (FileStream inputStream = File.OpenRead(text))
							{
								using (MD5 mD = MD5.Create())
								{
									old = Convert.ToBase64String(mD.ComputeHash(inputStream));
								}
							}
						}
						catch (Exception ex)
						{
							mLogger.Error("BackgroundWorker_DoWork Exception: " + ex.ToString());
						}
					}
				}
				lock (_fileList[text])
				{
					_fileList[text].Old = old;
					_fileList[text].Ticks = File.GetLastWriteTime(text).Ticks;
				}
			}
		}

		private void BackgroundWorker_RunWorkerComplete(object sender, RunWorkerCompletedEventArgs e)
		{
			if (e.Error != null)
			{
				mLogger.Error("BackgroundWorker_RunWorkerComplete Exception: " + e.Error.ToString());
			}
		}
	}
}
