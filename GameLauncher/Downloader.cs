using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Threading;
using System.Xml;

namespace GameLauncher
{
	public class Downloader
	{
		private const int LZMAOutPropsSize = 5;

		private const int LZMALengthSize = 8;

		private const int LZMAHeaderSize = 13;

		private const int HashThreads = 3;

		private const int DownloadThreads = 3;

		private const int DownloadChunks = 16;

		private ISynchronizeInvoke mFE;

		private Thread mThread;

		private DownloaderActionUpdated mDownloadProgressUpdated;

		private DownloaderActionFinished mDownloadFinished;

		private DownloaderActionFailed mDownloadFailed;

		private DownloaderActionUpdated mVerifyProgressUpdated;

		private DownloaderActionFinished mVerifyFinished;

		private DownloaderActionFailed mVerifyFailed;

		private DownloaderActionUpdated mDeleteProgressUpdated;

		private DownloaderActionFinished mDeleteFinished;

		private DownloaderActionFailed mDeleteFailed;

		private DownloaderActionUpdated mCleanDownloadProgressUpdated;

		private DownloaderActionFinished mCleanDownloadFinished;

		private DownloaderActionFailed mCleanDownloadFailed;

		private ShowMessage mShowMessage;

		private Telemetry mTelemetry;

		private DateTime mDownloaderStartTime;

		private static string mCurrentLocalVersion = string.Empty;

		private static string mCurrentServerVersion = string.Empty;

		private bool mDownloadQueueActive;

		private bool mDownloading;

		private bool mVerifying;

		private int mHashThreads;

		private DownloadManager mDownloadManager;

		private long mCompressedLength;

		private long mTotalToDownload;

		private static XmlDocument mIndexCached = null;

		private static bool mStopFlag = false;

		private static readonly ILog mLogger = LogManager.GetLogger(typeof(Downloader));

		private Queue<FileItem> mDownloadItemQueue;

		public bool DownloadQueueActive => mDownloadQueueActive;

		public bool Downloading => mDownloading;

		public bool Verifying => mVerifying;

		public DownloaderActionUpdated DownloadProgressUpdated
		{
			get
			{
				return mDownloadProgressUpdated;
			}
			set
			{
				mDownloadProgressUpdated = value;
			}
		}

		public DownloaderActionFinished DownloadFinished
		{
			get
			{
				return mDownloadFinished;
			}
			set
			{
				mDownloadFinished = value;
			}
		}

		public DownloaderActionFailed DownloadFailed
		{
			get
			{
				return mDownloadFailed;
			}
			set
			{
				mDownloadFailed = value;
			}
		}

		public DownloaderActionUpdated VerifyProgressUpdated
		{
			get
			{
				return mVerifyProgressUpdated;
			}
			set
			{
				mVerifyProgressUpdated = value;
			}
		}

		public DownloaderActionFinished VerifyFinished
		{
			get
			{
				return mVerifyFinished;
			}
			set
			{
				mVerifyFinished = value;
			}
		}

		public DownloaderActionFailed VerifyFailed
		{
			get
			{
				return mVerifyFailed;
			}
			set
			{
				mVerifyFailed = value;
			}
		}

		public DownloaderActionUpdated DeleteProgressUpdated
		{
			get
			{
				return mDeleteProgressUpdated;
			}
			set
			{
				mDeleteProgressUpdated = value;
			}
		}

		public DownloaderActionFinished DeleteFinished
		{
			get
			{
				return mDeleteFinished;
			}
			set
			{
				mDeleteFinished = value;
			}
		}

		public DownloaderActionFailed DeleteFailed
		{
			get
			{
				return mDeleteFailed;
			}
			set
			{
				mDeleteFailed = value;
			}
		}

		public DownloaderActionUpdated CleanProgressUpdated
		{
			get
			{
				return mCleanDownloadProgressUpdated;
			}
			set
			{
				mCleanDownloadProgressUpdated = value;
			}
		}

		public DownloaderActionFinished CleanDownloadFinished
		{
			get
			{
				return mCleanDownloadFinished;
			}
			set
			{
				mCleanDownloadFinished = value;
			}
		}

		public DownloaderActionFailed CleanDownloadFailed
		{
			get
			{
				return mCleanDownloadFailed;
			}
			set
			{
				mCleanDownloadFailed = value;
			}
		}

		public ShowMessage ShowMessage
		{
			get
			{
				return mShowMessage;
			}
			set
			{
				mShowMessage = value;
			}
		}

		public SendTelemetry SendTelemetry
		{
			get
			{
				return mTelemetry.SendTelemetry;
			}
			set
			{
				mTelemetry.SendTelemetry = value;
			}
		}

		public static string ServerVersion => mCurrentServerVersion;

		public Downloader(ISynchronizeInvoke fe)
			: this(fe, 3, 3, 16)
		{
		}

		public Downloader(ISynchronizeInvoke fe, int hashThreads, int downloadThreads, int downloadChunks)
		{
			mHashThreads = hashThreads;
			mFE = fe;
			mTelemetry = new Telemetry(fe);
			mDownloadManager = new DownloadManager(downloadThreads, downloadChunks, mTelemetry);
			mDownloadItemQueue = new Queue<FileItem>();
			mDownloaderStartTime = DateTime.Now;
		}

		public void StartVerificationAndDownload(VerifyCommandArgument parameters)
		{
			mLogger.Info("Starting verify");
			mStopFlag = false;
			mThread = new Thread(Verify);
			mThread.Start(parameters);
			if (parameters.Download)
			{
				mDownloaderStartTime = DateTime.Now;
				mDownloadManager.ServerPath = parameters.ServerPath;
				DownloadCommandArgument parameter = new DownloadCommandArgument(parameters.Package, parameters.PatchPath);
				mThread = new Thread(Download);
				mThread.Start(parameter);
			}
		}

		public void StartDelete(DeleteCommandArgument parameters)
		{
			mLogger.Info("Starting delete");
			mStopFlag = false;
			mThread = new Thread(Delete);
			mThread.Start(parameters);
		}

		public void StartCleanDownload(CleanDownloadCommandArgument parameters)
		{
			mLogger.Info("Starting clean download");
			mStopFlag = false;
			mThread = new Thread(CleanDownload);
			mThread.Start(parameters);
		}

		public void Stop()
		{
			mStopFlag = true;
			if (mDownloadManager != null && mDownloadManager.ManagerRunning)
			{
				mDownloadManager.Reset();
			}
			while (mVerifying || mDownloading)
			{
				Thread.Sleep(100);
			}
		}

		private void Downloader_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
		{
			string arg = e.UserState.ToString();
			if (!e.Cancelled && e.Error == null)
			{
				mLogger.DebugFormat("File '{0}' downloaded", arg);
				return;
			}
			mLogger.ErrorFormat("Error downloading file '{0}'", arg);
			if (e.Error != null)
			{
				mLogger.Error("Downloader_DownloadFileCompleted Exception: " + e.Error.ToString());
			}
		}

		private XmlDocument GetIndexFile(string url, bool useCache)
		{
			try
			{
				if (useCache && mIndexCached != null)
				{
					mLogger.Info("Getting Index.xml from cache");
					return mIndexCached;
				}
				mLogger.Info("Downloading " + url);
				WebClient webClient = new WebClient();
				webClient.DownloadFileCompleted += Downloader_DownloadFileCompleted;
				string tempFileName = Path.GetTempFileName();
				webClient.DownloadFileAsync(new Uri(url), tempFileName, url);
				while (webClient.IsBusy)
				{
					if (mStopFlag)
					{
						mLogger.Info("Canceled while downloading " + url);
						webClient.CancelAsync();
						return null;
					}
					Thread.Sleep(100);
				}
				XmlDocument xmlDocument = new XmlDocument();
				xmlDocument.Load(tempFileName);
				mIndexCached = xmlDocument;
				mLogger.Info(url + " downloaded");
				return xmlDocument;
			}
			catch (Exception ex)
			{
				mLogger.Error("GetIndexFile Exception: " + ex.ToString());
				return null;
			}
		}

		private void Download(object param)
		{
			DownloadCommandArgument downloadCommandArgument = (DownloadCommandArgument)param;
			mDownloadQueueActive = true;
			mDownloading = true;
			byte[] array = null;
			byte[] array2 = null;
			byte[] array3 = null;
			string package = downloadCommandArgument.Package;
			string patchPath = downloadCommandArgument.PatchPath;
			mLogger.Info("Download Starting " + package);
			mTelemetry.Call($"download_{package}_start");
			bool flag = false;
			object[] array4 = new object[1];
			object[] args = array4;
			try
			{
				long num = 0L;
				long num2 = 0L;
				int num3 = 1;
				int num4 = 0;
				array3 = null;
				int num5 = 0;
				array2 = new byte[13];
				while (true)
				{
					if (mDownloadItemQueue.Count == 0 && !mStopFlag && mDownloadQueueActive)
					{
						Thread.Sleep(100);
						continue;
					}
					if (mStopFlag || (!mDownloadQueueActive && mDownloadItemQueue.Count == 0))
					{
						break;
					}
					FileItem fileItem;
					lock (mDownloadItemQueue)
					{
						fileItem = mDownloadItemQueue.Dequeue();
						mLogger.DebugFormat("Dequeue {0}/{1} for uncompressing", fileItem.Path, fileItem.File);
					}
					int compressed = fileItem.Compressed;
					num5 = ((compressed > num5) ? compressed : num5);
					string text = fileItem.Path;
					if (!string.IsNullOrEmpty(patchPath))
					{
						int num6 = text.IndexOf("/");
						text = ((num6 < 0) ? patchPath : text.Replace(text.Substring(0, num6), patchPath));
					}
					string file = fileItem.File;
					string text2 = text + "/" + file;
					if (array == null || (array != null && num5 > array.Length))
					{
						array = new byte[num5];
					}
					if (!string.IsNullOrEmpty(patchPath))
					{
						int num7 = text.IndexOf("/");
						text = ((num7 < 0) ? patchPath : text.Replace(text.Substring(0, num7), patchPath));
					}
					int length = fileItem.Length;
					int num8 = 0;
					string text3 = null;
					Directory.CreateDirectory(text);
					FileStream fileStream = File.Create(text2);
					int compressed2 = fileItem.Compressed;
					int num9 = 0;
					if (fileItem.FromSection >= num3)
					{
						array3 = null;
					}
					num3 = Math.Max(num3, fileItem.FromSection);
					int num10 = 13;
					num4 = fileItem.Offset;
					while (num9 < compressed2)
					{
						if (array3 == null || num4 >= array3.Length)
						{
							if (array3 != null && num4 >= array3.Length)
							{
								num4 = 0;
							}
							text3 = ((!string.IsNullOrEmpty(package)) ? $"/{package}/section{num3}.dat" : $"/section{num3}.dat");
							array3 = null;
							mLogger.Debug("Forcing GC collection in loop");
							GC.Collect();
							array3 = mDownloadManager.GetFile(text3, UpdateProgress, num);
							if (array3 == null)
							{
								mLogger.FatalFormat("DownloadManager returned a null buffer downloading '{0}', aborting", text3);
								if (mDownloadFailed != null)
								{
									if (!mStopFlag)
									{
										flag = true;
										args = new object[1]
										{
											new Exception("DownloadManager returned a null buffer")
										};
										mTelemetry.SendTelemetry("download_null_buffer");
									}
									else
									{
										flag = true;
									}
								}
								return;
							}
							num2 += array3.Length;
							num3++;
						}
						else
						{
							int num11 = Math.Min(array3.Length - num4, compressed2 - num9);
							if (num10 != 0)
							{
								if (length != compressed2)
								{
									int num12 = Math.Min(num10, num11);
									Buffer.BlockCopy(array3, num4, array2, 13 - num10, num12);
									Buffer.BlockCopy(array3, num4 + num12, array, 0, num11 - num12);
									num10 -= num12;
								}
								else
								{
									Buffer.BlockCopy(array3, num4, array, 0, num11);
									num10 = 0;
								}
							}
							else
							{
								Buffer.BlockCopy(array3, num4, array, num9 - ((length != compressed2) ? 13 : 0), num11);
							}
							num4 += num11;
							num9 += num11;
							num += num11;
						}
						UpdateProgress(num);
					}
					if (length != compressed2)
					{
						if (!IsLzma(array2))
						{
							mLogger.Fatal("Compression algorithm not recognized " + BitConverter.ToString(array2));
							mTelemetry.Call("decompress_algorithm_error");
							throw new DownloaderException(string.Format(ResourceWrapper.Instance.GetString("GameLauncher.LanguageStrings", "DOWNLOADER00003"), $"{text2} ({text3})"));
						}
						fileStream.Close();
						fileStream.Dispose();
						_ = (IntPtr)length;
						IntPtr outPropsSize = new IntPtr(5);
						byte[] array5 = new byte[5];
						for (int i = 0; i < 5; i++)
						{
							array5[i] = array2[i];
						}
						long num13 = 0L;
						for (int j = 0; j < 8; j++)
						{
							num13 += array2[j + 5] << 8 * j;
						}
						if (num13 != length)
						{
							mLogger.FatalFormat("Compression data length in header '{0}' != than in metadata '{1}'", num13, length);
							mTelemetry.Call("decompress_header_error");
							throw new DownloaderException(string.Format(ResourceWrapper.Instance.GetString("GameLauncher.LanguageStrings", "DOWNLOADER00001"), length, num13));
						}
						int num14 = compressed2;
						compressed2 -= 13;
						IntPtr srcLen = new IntPtr(compressed2);
						IntPtr destLen = new IntPtr(num13);
						mLogger.DebugFormat("Writing compressed file '{0}', compSize: {1}, uncompSize: {2}", text2, num14, length);
						int num15 = UnsafeNativeMethods.LzmaUncompressBuf2File(text2, ref destLen, array, ref srcLen, array5, outPropsSize);
						if (num15 != 0)
						{
							mLogger.FatalFormat("Decompression returned {0} for {1}", num15, text2);
							mTelemetry.Call($"decompress_error_{num15}");
							throw new UncompressionException(num15, string.Format(ResourceWrapper.Instance.GetString("GameLauncher.LanguageStrings", "DOWNLOADER00002"), num15));
						}
						if (destLen.ToInt32() != length)
						{
							mLogger.FatalFormat("Decompression returned different size '{0}' than metadata '{1}' for {2}", destLen.ToInt32(), length, text2);
							mTelemetry.Call("decompress_size_error");
							throw new DownloaderException(ResourceWrapper.Instance.GetString("GameLauncher.LanguageStrings", "DOWNLOADER00006"));
						}
						num8 += (int)destLen;
					}
					else
					{
						mLogger.DebugFormat("Writing uncompressed file '{0}', uncompSize: {1}", text2, length);
						fileStream.Write(array, 0, length);
						num8 += length;
					}
					if (fileStream != null)
					{
						fileStream.Close();
						fileStream.Dispose();
					}
					HashManager.Instance.UpdateTicks(text2, File.GetLastWriteTime(text2).Ticks);
				}
				if (mStopFlag)
				{
					flag = true;
				}
			}
			catch (DownloaderException ex)
			{
				mLogger.Error("Download DownloaderException: " + ex.ToString());
				flag = true;
				mStopFlag = true;
				args = new object[1]
				{
					ex
				};
			}
			catch (Exception ex2)
			{
				mLogger.Error("Download Exception: " + ex2.ToString());
				flag = true;
				mStopFlag = true;
				args = new object[1]
				{
					ex2
				};
			}
			finally
			{
				mDownloadManager.Reset();
				array = null;
				array2 = null;
				array3 = null;
				HashManager.Instance.WriteHashCache(package + ".hsh");
				mLogger.Debug("Forcing GC Collection closing download");
				GC.Collect();
				mDownloadQueueActive = false;
				if (flag || mStopFlag)
				{
					if (mDownloadFailed != null)
					{
						mFE.BeginInvoke(mDownloadFailed, args);
					}
				}
				else if (mDownloadFinished != null)
				{
					mFE.BeginInvoke(mDownloadFinished, null);
				}
				mDownloading = false;
			}
		}

		private void UpdateProgress(long downloadCurrent)
		{
			if (mDownloadProgressUpdated != null && !mVerifying)
			{
				mDownloadManager.GetAmountDownloaded();
				double num = (double)downloadCurrent / (double)mCompressedLength;
				if (num > 1.0)
				{
					num = 1.0;
				}
				double num2 = num;
				long num3 = (long)((num + num2) * 0.5 * (double)mCompressedLength);
				object[] args = new object[5]
				{
					0,
					num3,
					mCompressedLength,
					null,
					mDownloaderStartTime
				};
				mFE.BeginInvoke(mDownloadProgressUpdated, args);
			}
		}

		private void Verify(object param)
		{
			mVerifying = true;
			VerifyCommandArgument verifyCommandArgument = (VerifyCommandArgument)param;
			string text = verifyCommandArgument.ServerPath;
			string text2 = verifyCommandArgument.Package;
			if (!string.IsNullOrEmpty(text2))
			{
				text = text + "/" + text2;
			}
			string patchPath = verifyCommandArgument.PatchPath;
			mLogger.Info("Verify Starting " + text2);
			mTelemetry.Call($"verify_{text2}_start");
			XmlDocument xmlDocument = null;
			XmlNodeList xmlNodeList = null;
			bool stopOnFail = verifyCommandArgument.StopOnFail;
			bool clearHashes = verifyCommandArgument.ClearHashes;
			_ = verifyCommandArgument.WriteHashes;
			bool download = verifyCommandArgument.Download;
			bool fullDownload = false;
			bool flag = false;
			object[] args = null;
			mCompressedLength = 0L;
			mTotalToDownload = 0L;
			mDownloadItemQueue.Clear();
			try
			{
				xmlDocument = GetIndexFile(text + "/index.xml", useCache: false);
				if (xmlDocument == null)
				{
					mStopFlag = true;
					mLogger.Error("Error retrieving the index.xml file");
					mFE.BeginInvoke(mVerifyFailed, new object[1]
					{
						new DownloaderException("Exception retrieving index from " + text + "/index.xml")
					});
					return;
				}
				long num = long.Parse(xmlDocument.SelectSingleNode("/index/header/length").InnerText);
				int num2 = int.Parse(xmlDocument.SelectSingleNode("/index/header/firstcab").InnerText);
				int.Parse(xmlDocument.SelectSingleNode("/index/header/lastcab").InnerText);
				xmlNodeList = xmlDocument.SelectNodes("/index/fileinfo");
				string text3 = Path.Combine(Environment.CurrentDirectory, patchPath);
				string path = text3;
				if (text2.Length == 2)
				{
					path = text3 + "\\Sound\\Speech";
				}
				else if (text2.Length > 0)
				{
					path = text3 + '\\' + text2;
				}
				if (!Directory.Exists(path))
				{
					mTelemetry.Call($"full_download_{text2}");
					fullDownload = true;
				}
				if (!Directory.Exists(text3))
				{
					Directory.CreateDirectory(text3);
				}
				HashManager.Instance.Clear();
				HashManager.Instance.Start(xmlDocument, patchPath, text2 + ".hsh", mHashThreads);
				if (!string.IsNullOrEmpty(text2))
				{
					text2 = "/" + text2;
				}
				long num3 = 0L;
				long num4 = 0L;
				long num5 = 0L;
				FileItem fileItem = null;
				int num6 = 1;
				int i = 1;
				bool flag2 = false;
				foreach (XmlNode item in xmlNodeList)
				{
					string text4 = item.SelectSingleNode("path").InnerText;
					string innerText = item.SelectSingleNode("file").InnerText;
					if (!string.IsNullOrEmpty(patchPath))
					{
						int num7 = text4.IndexOf("/");
						text4 = ((num7 < 0) ? patchPath : text4.Replace(text4.Substring(0, num7), patchPath));
					}
					string text5 = text4 + "/" + innerText;
					int num8 = int.Parse(item.SelectSingleNode("length").InnerText);
					if (item.SelectSingleNode("hash") != null)
					{
						num6 = int.Parse(item.SelectSingleNode("section").InnerText);
						if (!HashManager.Instance.HashesMatch(text5))
						{
							num4 += int.Parse(item.SelectSingleNode("length").InnerText);
							int num9 = 0;
							XmlNode xmlNode2 = item.SelectSingleNode("compressed");
							num9 = ((xmlNode2 == null) ? num8 : int.Parse(xmlNode2.InnerText));
							num5 += num9;
							mLogger.DebugFormat("Failed verification for {0}, compSize={1}, uncompSize={2}", text5, num9, num8);
							if (stopOnFail)
							{
								flag = true;
								args = new object[1]
								{
									new VerificationException((ulong)num4, (ulong)num5, text2)
								};
								return;
							}
							flag = true;
							if (download)
							{
								int num10 = int.Parse(item.SelectSingleNode("offset").InnerText);
								if (fileItem != null)
								{
									i = Math.Max(fileItem.FromSection, i);
									if (num10 == 0 && num6 > 1)
									{
										fileItem.ToSection = num6 - 1;
									}
									else
									{
										fileItem.ToSection = num6;
									}
									for (; i <= fileItem.ToSection; i++)
									{
										if (mDownloadManager.ScheduleFile($"{text2}/section{i}.dat"))
										{
											mTotalToDownload += num2;
										}
									}
									if (!flag2)
									{
										mDownloadManager.Start(fullDownload, verifyCommandArgument.ServerPath);
										flag2 = true;
									}
									lock (mDownloadItemQueue)
									{
										mLogger.DebugFormat("Enqueueing {0}/{1} for download [hash not match]", fileItem.Path, fileItem.File);
										mDownloadItemQueue.Enqueue(fileItem);
									}
									mCompressedLength += fileItem.Compressed;
									fileItem = null;
								}
								fileItem = new FileItem(text4, innerText, string.Empty, num6, num6, num10, num8, num9);
							}
						}
						else if (download && fileItem != null)
						{
							i = Math.Max(fileItem.FromSection, i);
							if (int.Parse(item.SelectSingleNode("offset").InnerText) == 0)
							{
								num6--;
							}
							fileItem.ToSection = num6;
							for (; i <= num6; i++)
							{
								if (mDownloadManager.ScheduleFile($"{text2}/section{i}.dat"))
								{
									mTotalToDownload += num2;
								}
							}
							if (!flag2)
							{
								mDownloadManager.Start(fullDownload, verifyCommandArgument.ServerPath);
								flag2 = true;
							}
							lock (mDownloadItemQueue)
							{
								mLogger.DebugFormat("Enqueueing {0}/{1} for download [hash match]", fileItem.Path, fileItem.File);
								mDownloadItemQueue.Enqueue(fileItem);
							}
							mCompressedLength += fileItem.Compressed;
							fileItem = null;
						}
					}
					else
					{
						if (stopOnFail)
						{
							mLogger.Error("Without hash in the metadata I cannot verify the download");
							mTelemetry.Call("index_nohash_error");
							throw new DownloaderException("Without hash in the metadata I cannot verify the download");
						}
						flag = true;
					}
					if (mStopFlag)
					{
						flag = true;
						object[] array = new object[1];
						args = array;
						return;
					}
					num3 += num8;
					object[] args2 = new object[5]
					{
						num,
						num3,
						0,
						innerText,
						mDownloaderStartTime
					};
					if (mVerifyProgressUpdated != null)
					{
						mFE.BeginInvoke(mVerifyProgressUpdated, args2);
					}
				}
				if (download && fileItem != null)
				{
					i = Math.Max(fileItem.FromSection, i);
					fileItem.ToSection = num6;
					for (; i <= num6; i++)
					{
						if (mDownloadManager.ScheduleFile($"{text2}/section{i}.dat"))
						{
							mTotalToDownload += num2;
						}
					}
					if (!flag2)
					{
						mDownloadManager.Start(fullDownload, verifyCommandArgument.ServerPath);
					}
					lock (mDownloadItemQueue)
					{
						mLogger.DebugFormat("Enqueueing {0}/{1} for download [last item]", fileItem.Path, fileItem.File);
						mDownloadItemQueue.Enqueue(fileItem);
					}
					mCompressedLength += fileItem.Compressed;
					fileItem = null;
				}
				if (flag)
				{
					args = new object[1]
					{
						new VerificationException((ulong)num4, (ulong)num5, text2)
					};
				}
			}
			catch (DownloaderException ex)
			{
				mLogger.Error("Verify DownloaderException: " + ex.ToString());
				flag = true;
				args = new object[1]
				{
					ex
				};
				if (download)
				{
					mDownloadItemQueue.Clear();
					mDownloadManager.Reset();
				}
			}
			catch (Exception ex2)
			{
				mLogger.Error("Verify Exception: " + ex2.ToString());
				flag = true;
				args = new object[1]
				{
					ex2
				};
				if (download)
				{
					mDownloadItemQueue.Clear();
					mDownloadManager.Reset();
				}
			}
			finally
			{
				if (clearHashes)
				{
					HashManager.Instance.Clear();
				}
				xmlDocument = null;
				xmlNodeList = null;
				mLogger.Debug("Forcing GC Collection closing verification");
				GC.Collect();
				mDownloadQueueActive = false;
				if (stopOnFail && download)
				{
					mDownloadItemQueue.Clear();
					mDownloadManager.Reset();
				}
				if (flag)
				{
					if (mVerifyFailed != null)
					{
						mFE.BeginInvoke(mVerifyFailed, args);
					}
				}
				else if (mVerifyFinished != null)
				{
					mFE.BeginInvoke(mVerifyFinished, null);
				}
				mVerifying = false;
			}
		}

		private void Delete(object param)
		{
			DeleteCommandArgument deleteCommandArgument = (DeleteCommandArgument)param;
			string serverPath = deleteCommandArgument.ServerPath;
			string text = deleteCommandArgument.Package;
			if (!string.IsNullOrEmpty(text))
			{
				text = text + "/" + text;
			}
			string patchPath = deleteCommandArgument.PatchPath;
			mTelemetry.Call($"delete_{text}_start");
			XmlDocument xmlDocument = null;
			XmlNodeList xmlNodeList = null;
			bool flag = false;
			try
			{
				xmlDocument = GetIndexFile(serverPath + "/delete.xml", useCache: false);
				if (xmlDocument == null)
				{
					mLogger.Error("Error retrieving the delete.xml file");
					mFE.BeginInvoke(mDeleteFinished, null);
					return;
				}
				xmlNodeList = xmlDocument.SelectNodes("/index/fileinfo");
				foreach (XmlNode item in xmlNodeList)
				{
					string text2 = item.SelectSingleNode("path").InnerText;
					string innerText = item.SelectSingleNode("file").InnerText;
					if (!string.IsNullOrEmpty(patchPath))
					{
						int num = text2.IndexOf("/");
						text2 = ((num < 0) ? patchPath : text2.Replace(text2.Substring(0, num), patchPath));
					}
					string text3 = text2 + "/" + innerText;
					if (innerText.Contains("*") || innerText.Contains("?"))
					{
						if (!Directory.Exists(text2))
						{
							continue;
						}
						string[] files = Directory.GetFiles(text2, innerText, SearchOption.AllDirectories);
						string[] array = files;
						foreach (string text4 in array)
						{
							try
							{
								File.SetAttributes(text4, FileAttributes.Normal);
								File.Delete(text4);
							}
							catch (Exception)
							{
								mLogger.Warn("Failed to delete the file " + text4);
							}
						}
						continue;
					}
					try
					{
						if (File.Exists(text3))
						{
							File.SetAttributes(text3, FileAttributes.Normal);
							File.Delete(text3);
						}
					}
					catch (Exception)
					{
						mLogger.Warn("Failed to delete the file " + text3);
					}
				}
				if (flag)
				{
					ISynchronizeInvoke synchronizeInvoke = mFE;
					DownloaderActionFailed method = mDeleteFailed;
					object[] args = new object[1];
					synchronizeInvoke.BeginInvoke(method, args);
				}
				else
				{
					mFE.BeginInvoke(mDeleteFinished, null);
				}
			}
			catch (DownloaderException ex3)
			{
				mLogger.Error("Delete DownloaderException: " + ex3.ToString());
				mFE.BeginInvoke(mDeleteFailed, new object[1]
				{
					ex3
				});
			}
			catch (Exception ex4)
			{
				mLogger.Error("Delete Exception: " + ex4.ToString());
				mFE.BeginInvoke(mDeleteFailed, new object[1]
				{
					ex4
				});
			}
			finally
			{
				xmlDocument = null;
				xmlNodeList = null;
			}
		}

		private void CleanDownload(object param)
		{
			CleanDownloadCommandArgument cleanDownloadCommandArgument = (CleanDownloadCommandArgument)param;
			string serverPath = cleanDownloadCommandArgument.ServerPath;
			string text = cleanDownloadCommandArgument.Package;
			if (!string.IsNullOrEmpty(text))
			{
				text = "/" + text;
			}
			_ = cleanDownloadCommandArgument.PatchPath;
			bool useIndexCache = cleanDownloadCommandArgument.UseIndexCache;
			mTelemetry.Call($"clean_{text}_start");
			XmlDocument xmlDocument = null;
			XmlNodeList xmlNodeList = null;
			try
			{
				xmlDocument = GetIndexFile(serverPath + text + "/index.xml", useIndexCache);
				if (xmlDocument == null)
				{
					mLogger.Error("Error retrieving the index.xml file");
					mFE.BeginInvoke(mDownloadFinished, null);
					return;
				}
				xmlNodeList = xmlDocument.SelectNodes("/index/fileinfo");
				List<string> list = null;
				string a = string.Empty;
				foreach (XmlNode item in xmlNodeList)
				{
					string innerText = item.SelectSingleNode("path").InnerText;
					string innerText2 = item.SelectSingleNode("file").InnerText;
					if (innerText.Split('/').Length == 1)
					{
						mLogger.Debug("Reached base folder, do not delete anything here. Stopping");
						break;
					}
					if (a != innerText)
					{
						if (list != null)
						{
							foreach (string item2 in list)
							{
								try
								{
									mLogger.Debug("Deleting the file " + item2);
									if (File.Exists(item2))
									{
										File.SetAttributes(item2, FileAttributes.Normal);
										File.Delete(item2);
									}
									else
									{
										mLogger.WarnFormat("Cannot delete the file {0}, it does not exist", item2);
									}
								}
								catch (Exception ex)
								{
									mLogger.ErrorFormat("CleanDownload Exception deleting the file {0}: {1}", item2, ex.ToString());
								}
							}
						}
						a = innerText;
						list = new List<string>(Directory.GetFiles(innerText, "*.*", SearchOption.TopDirectoryOnly));
					}
					list.Remove(innerText + "\\" + innerText2);
				}
				mFE.BeginInvoke(mCleanDownloadFinished, null);
			}
			catch (Exception ex2)
			{
				mLogger.Error("CleanDownload Exception: " + ex2.ToString());
				mFE.BeginInvoke(mCleanDownloadFailed, new object[1]
				{
					ex2
				});
			}
			finally
			{
				xmlDocument = null;
				xmlNodeList = null;
			}
		}

		public static string GetXml(string url)
		{
			byte[] data = GetData(url);
			if (IsLzma(data))
			{
				return DecompressLZMA(data);
			}
			return Encoding.UTF8.GetString(data).Trim();
		}

		public static byte[] GetData(string url)
		{
			WebClient webClient = new WebClient();
			webClient.Headers.Add("Accept", "text/html,text/xml,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
			webClient.Headers.Add("Accept-Language", "en-us,en;q=0.5");
			webClient.Headers.Add("Accept-Encoding", "gzip");
			webClient.Headers.Add("Accept-Charset", "utf-8;q=0.7,*;q=0.7");
			webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
			byte[] result = webClient.DownloadData(url);
			webClient.Dispose();
			return result;
		}

		public static bool IsLzma(byte[] arr)
		{
			if (arr.Length >= 2 && arr[0] == 93)
			{
				return arr[1] == 0;
			}
			return false;
		}

		public static string DecompressLZMA(byte[] compressedFile)
		{
			IntPtr srcLen = new IntPtr(compressedFile.Length - 13);
			byte[] array = new byte[srcLen.ToInt64()];
			IntPtr outPropsSize = new IntPtr(5);
			byte[] array2 = new byte[5];
			compressedFile.CopyTo(array, 13);
			for (int i = 0; i < 5; i++)
			{
				array2[i] = compressedFile[i];
			}
			int num = 0;
			for (int j = 0; j < 8; j++)
			{
				num += compressedFile[j + 5] << 8 * j;
			}
			IntPtr destLen = new IntPtr(num);
			byte[] array3 = new byte[num];
			int num2 = UnsafeNativeMethods.LzmaUncompress(array3, ref destLen, array, ref srcLen, array2, outPropsSize);
			if (num2 != 0)
			{
				mLogger.Fatal("Decompression returned " + num2);
				throw new UncompressionException(num2, string.Format(ResourceWrapper.Instance.GetString("GameLauncher.LanguageStrings", "DOWNLOADER00002"), num2));
			}
			array = null;
			return new string(Encoding.UTF8.GetString(array3).ToCharArray());
		}
	}
}
