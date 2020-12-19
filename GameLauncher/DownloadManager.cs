using DownloadProvider;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Cache;
using System.Threading;

namespace GameLauncher
{
	internal class DownloadManager
	{
		private class Limiter
		{
			private int mMaxActiveDownloads;

			private int mDownloadingFileCount;

			public Limiter(int maxActiveDownloads)
			{
				mMaxActiveDownloads = maxActiveDownloads;
				mDownloadingFileCount = 0;
			}

			public bool CanDoOneMore()
			{
				return mDownloadingFileCount < mMaxActiveDownloads;
			}

			public void DoingOneMore()
			{
				mDownloadingFileCount++;
			}

			public void DoingOneLess()
			{
				mDownloadingFileCount--;
			}

			public static bool CanDoOneMore(Limiter limiter)
			{
				return limiter?.CanDoOneMore() ?? true;
			}

			public static void DoingOneMore(Limiter limiter)
			{
				limiter?.DoingOneMore();
			}

			public static void DoingOneLess(Limiter limiter)
			{
				limiter?.DoingOneLess();
			}
		}

		private class WorkerContext
		{
			public Limiter mLimiter;

			public DownloadProvider.DownloadProvider mDownloader;

			public WorkerContext()
			{
				mLimiter = null;
				mDownloader = null;
			}
		}

		private class DownloadItem
		{
			public DownloadFile.Status Status;

			private byte[] _data;

			public byte[] Data
			{
				get
				{
					return _data;
				}
				set
				{
					_data = value;
				}
			}

			public DownloadItem()
			{
				Status = DownloadFile.Status.Queued;
			}
		}

		private class DownloaderArgs
		{
			public bool FullDownload;

			public string ServerURL;
		}

		private const int MaxDirectDownloads = 4;

		private const int MaxAkamaiDownloads = 4;

		private const double FileTimeOutInSeconds = 600.0;

		private const int FileMaxRetryCount = 1;

		private const int MaxWorkers = 3;

		private const int MaxActiveChunks = 16;

		private long AmountDownloaded;

		protected string _serverPath;

		private static int _workerCount = 0;

		private int _maxWorkers;

		private bool FullDownload;

		private string ServerURL;

		private Dictionary<string, DownloadItem> _downloadList;

		private LinkedList<string> _downloadQueue;

		private List<BackgroundWorker> _workers;

		private int _freeChunks;

		private object _freeChunksLock;

		private bool _managerRunning;

		private static readonly ILog mLogger = LogManager.GetLogger("DownloadManager");

		private Telemetry mTelemetry;

		public string ServerPath
		{
			get
			{
				return _serverPath;
			}
			set
			{
				_serverPath = value;
			}
		}

		public bool ManagerRunning => _managerRunning;

		public long GetAmountDownloaded()
		{
			return AmountDownloaded;
		}

		public DownloadManager(Telemetry telemetry)
			: this(3, 16, telemetry)
		{
		}

		public DownloadManager(int maxWorkers, int maxActiveChunks, Telemetry telemetry)
		{
			mTelemetry = telemetry;
			_maxWorkers = maxWorkers;
			_freeChunks = maxActiveChunks;
			_downloadList = new Dictionary<string, DownloadItem>();
			_downloadQueue = new LinkedList<string>();
			_workers = new List<BackgroundWorker>();
			_freeChunksLock = new object();
		}

		private bool AttemptToAddWorkerThread()
		{
			if (_managerRunning && _workerCount < _maxWorkers)
			{
				mLogger.DebugFormat("Adding new download worker ({0}/{1})", _workerCount, _maxWorkers);
				lock (_workers)
				{
					DownloaderArgs downloaderArgs = new DownloaderArgs();
					downloaderArgs.FullDownload = FullDownload;
					downloaderArgs.ServerURL = ServerURL;
					BackgroundWorker backgroundWorker = new BackgroundWorker();
					backgroundWorker.DoWork += BackgroundWorker_DoWork;
					backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerComplete;
					backgroundWorker.RunWorkerAsync(downloaderArgs);
					_workers.Add(backgroundWorker);
					_workerCount++;
				}
				return true;
			}
			return false;
		}

		public void Start(bool fullDownload, string serverURL)
		{
			mLogger.Info("Starting download manager");
			FullDownload = fullDownload;
			ServerURL = serverURL;
			AmountDownloaded = 0L;
			_managerRunning = true;
			lock (_workers)
			{
				while (AttemptToAddWorkerThread())
				{
				}
			}
		}

		public void Stop()
		{
			mLogger.Info("Stopping download manager");
			_managerRunning = false;
		}

		public DownloadFile.Status? GetStatus(string fileName)
		{
			if (_downloadList.ContainsKey(fileName))
			{
				return _downloadList[fileName].Status;
			}
			return null;
		}

		public bool ScheduleFile(string fileName)
		{
			bool result = false;
			lock (_downloadList)
			{
				if (!_downloadList.ContainsKey(fileName))
				{
					_downloadList.Add(fileName, new DownloadItem());
					_downloadList[fileName].Status = DownloadFile.Status.Queued;
					lock (_downloadQueue)
					{
						_downloadQueue.AddLast(fileName);
						result = true;
					}
					mLogger.DebugFormat("Successfully scheduling the cabinet {0} for download", fileName);
				}
				else
				{
					mLogger.DebugFormat("The cabinet {0} is already scheduled", fileName);
				}
			}
			AttemptToAddWorkerThread();
			return result;
		}

		public void ReScheduleFile(string fileName)
		{
			lock (_downloadList)
			{
				if (!_downloadList.ContainsKey(fileName))
				{
					_downloadList.Add(fileName, new DownloadItem());
					lock (_downloadQueue)
					{
						_downloadQueue.AddFirst(fileName);
					}
				}
				else
				{
					DownloadFile.Status status = DownloadFile.Status.Queued;
					lock (_downloadList[fileName])
					{
						status = _downloadList[fileName].Status;
					}
					if (status != 0 && status != DownloadFile.Status.Cancelled)
					{
						mLogger.DebugFormat("The file '{0}' is already downloaded/downloading, skipping re-schedule", fileName);
						return;
					}
					lock (_downloadQueue)
					{
						if (_downloadQueue.Contains(fileName))
						{
							if (_downloadQueue.First.Value != fileName)
							{
								_downloadQueue.Remove(fileName);
								_downloadQueue.AddFirst(fileName);
							}
						}
						else
						{
							_downloadQueue.AddFirst(fileName);
						}
					}
					lock (_downloadList[fileName])
					{
						_downloadList[fileName].Status = DownloadFile.Status.Queued;
					}
				}
			}
			AttemptToAddWorkerThread();
		}

		public byte[] GetFile(string fileName, UpdateProgressHandler updateProgress, long downloadCurrent)
		{
			mLogger.DebugFormat("File '{0}' requested", fileName);
			byte[] array = null;
			if (!_downloadList.ContainsKey(fileName))
			{
				mLogger.WarnFormat("The file {0} is not scheduled for download, scheduling", fileName);
				ReScheduleFile(fileName);
			}
			DownloadFile.Status status;
			lock (_downloadList[fileName])
			{
				status = _downloadList[fileName].Status;
			}
			mLogger.DebugFormat("Initial Status of {0} is {1}", fileName, status.ToString());
			DateTime now = DateTime.Now;
			int num = 0;
			int num2 = 0;
			while (status != DownloadFile.Status.Downloaded && status != DownloadFile.Status.Cancelled)
			{
				updateProgress(downloadCurrent);
				TimeSpan timeSpan = DateTime.Now - now;
				int num3 = (int)timeSpan.TotalMinutes;
				if (num3 > num2)
				{
					num2 = num3;
				}
				if (timeSpan.TotalSeconds > 600.0)
				{
					num++;
					if (num > 1)
					{
						mLogger.FatalFormat("Could not get the file {0} : too many retries already, cancelling it ", fileName, num);
						mTelemetry.Call("download_max_retries");
						lock (_downloadList[fileName])
						{
							_downloadList[fileName].Status = DownloadFile.Status.Timeout;
						}
						Thread.Sleep(1000);
						num = 0;
					}
					else
					{
						CancelDownload(fileName);
						Thread.Sleep(100);
						ReScheduleFile(fileName);
						lock (_downloadList[fileName])
						{
							status = _downloadList[fileName].Status;
						}
						mLogger.DebugFormat("Could not get the file {0} in time, file rescheduled: retryCount = {1}", fileName, num);
						mTelemetry.Call($"download_retry_{num}");
					}
					now = DateTime.Now;
					num2 = 0;
				}
				else
				{
					Thread.Sleep(100);
				}
				lock (_downloadList[fileName])
				{
					status = _downloadList[fileName].Status;
				}
			}
			mTelemetry.Call($"file_download_time_{(double)num2 + (double)num * 600.0 / 60.0}");
			if (_downloadList[fileName].Status == DownloadFile.Status.Downloaded)
			{
				lock (_downloadList[fileName])
				{
					array = _downloadList[fileName].Data;
					_downloadList[fileName].Data = null;
					lock (_freeChunksLock)
					{
						_freeChunks++;
					}
				}
			}
			if (array == null)
			{
				mTelemetry.Call("download_data_null");
			}
			return array;
		}

		private void MarkDownloadEntryAsCancelled(string fileName)
		{
			lock (_downloadList[fileName])
			{
				if (_downloadList[fileName].Data != null)
				{
					lock (_freeChunksLock)
					{
						_freeChunks++;
					}
				}
				_downloadList[fileName].Data = null;
				_downloadList[fileName].Status = DownloadFile.Status.Cancelled;
			}
		}

		public void CancelDownload(string fileName)
		{
			mLogger.DebugFormat("File '{0}' Cancelled", fileName);
			lock (_downloadQueue)
			{
				if (_downloadQueue.Contains(fileName))
				{
					_downloadQueue.Remove(fileName);
				}
			}
			if (_downloadList.ContainsKey(fileName))
			{
				MarkDownloadEntryAsCancelled(fileName);
			}
		}

		public void Shutdown()
		{
			mLogger.Debug("Cancel all downloads");
			Stop();
			lock (_downloadQueue)
			{
				_downloadQueue.Clear();
			}
			lock (_downloadList)
			{
				foreach (string key in _downloadList.Keys)
				{
					MarkDownloadEntryAsCancelled(key);
				}
			}
		}

		public void Reset()
		{
			Shutdown();
			while (_workerCount > 0)
			{
				Thread.Sleep(100);
			}
			lock (_downloadList)
			{
				_downloadList.Clear();
			}
		}

		private void RemoveBackgroundWorker(BackgroundWorker workerToRemove, string reason)
		{
			mLogger.DebugFormat("Shutting down download worker ({0}/{1}), reason: {2}", _workerCount, _maxWorkers, reason);
			lock (_workers)
			{
				_workers.Remove(workerToRemove);
			}
			_workerCount--;
		}

		private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs args)
		{
			DownloaderArgs downloaderArgs = (DownloaderArgs)args.Argument;
			try
			{
				using (WebClient webClient = new WebClient())
				{
					webClient.DownloadDataCompleted += DownloadManager_DownloadDataCompleted;
					webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
					while (true)
					{
						if (_freeChunks <= 0)
						{
							Thread.Sleep(100);
							continue;
						}
						lock (_downloadQueue)
						{
							if (_downloadQueue.Count == 0)
							{
								mLogger.DebugFormat("Shutting down download worker ({0}/{1}), reason: no more work to do", _workerCount, _maxWorkers);
								lock (_workers)
								{
									_workers.Remove((BackgroundWorker)sender);
								}
								_workerCount--;
								return;
							}
						}
						string text = null;
						lock (_downloadQueue)
						{
							text = _downloadQueue.First.Value;
							_downloadQueue.RemoveFirst();
							lock (_freeChunksLock)
							{
								_freeChunks--;
							}
						}
						lock (_downloadList[text])
						{
							if (_downloadList[text].Status != DownloadFile.Status.Cancelled)
							{
								_downloadList[text].Status = DownloadFile.Status.Downloading;
							}
						}
						while (webClient.IsBusy)
						{
							Thread.Sleep(100);
						}
						webClient.DownloadDataAsync(new Uri(downloaderArgs.ServerURL + text), text);
						DownloadFile.Status status = DownloadFile.Status.Downloading;
						while (status == DownloadFile.Status.Downloading)
						{
							status = _downloadList[text].Status;
							if (status == DownloadFile.Status.Cancelled)
							{
								break;
							}
							Thread.Sleep(100);
						}
						if (status == DownloadFile.Status.Cancelled)
						{
							webClient.CancelAsync();
						}
						mLogger.DebugFormat("File {0} is {1}", text, status.ToString());
						lock (_workers)
						{
							if (_workerCount > _maxWorkers || !_managerRunning)
							{
								mLogger.DebugFormat("Shutting down download worker ({0}/{1}), reason: {2}", _workerCount, _maxWorkers, (_workerCount > _maxWorkers) ? "Too many workers" : "Manager Stopped");
								_workers.Remove((BackgroundWorker)sender);
								_workerCount--;
								return;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				mLogger.Error("BackgroundWorker_DoWork Exception: " + ex.ToString());
				lock (_workers)
				{
					_workers.Remove((BackgroundWorker)sender);
					_workerCount--;
				}
				args.Result = ex;
			}
		}

		private void DownloadManager_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
		{
			string text = e.UserState.ToString();
			if (!e.Cancelled && e.Error == null)
			{
				mLogger.DebugFormat("File '{0}' downloaded", text);
				lock (_downloadList[text])
				{
					if (_downloadList[text].Status != DownloadFile.Status.Downloaded)
					{
						_downloadList[text].Data = new byte[e.Result.Length];
						Buffer.BlockCopy(e.Result, 0, _downloadList[text].Data, 0, e.Result.Length);
						_downloadList[text].Status = DownloadFile.Status.Downloaded;
					}
				}
				return;
			}
			mLogger.ErrorFormat("Error downloading file '{0}'", text);
			if (e.Error != null)
			{
				mLogger.Error("DownloadManager_DownloadDataCompleted Exception: " + e.Error.ToString());
				if (_downloadList.ContainsKey(text))
				{
					lock (_downloadList[text])
					{
						if (_downloadList[text].Status != DownloadFile.Status.Cancelled && _maxWorkers > 1)
						{
							_downloadList[text].Data = null;
							_downloadList[text].Status = DownloadFile.Status.Queued;
							lock (_downloadQueue)
							{
								_downloadQueue.AddLast(text);
							}
							lock (_workers)
							{
								_maxWorkers--;
							}
						}
						else
						{
							_downloadList[text].Data = null;
							_downloadList[text].Status = DownloadFile.Status.Cancelled;
						}
					}
				}
			}
			lock (_freeChunksLock)
			{
				_freeChunks++;
			}
		}

		private void BackgroundWorker_RunWorkerComplete(object sender, RunWorkerCompletedEventArgs e)
		{
			if (e.Error != null)
			{
				mLogger.Error("BackgroundWorker_RunWorkerComplete Exception: " + e.Error.ToString());
			}
			else if (e.Cancelled)
			{
				mLogger.Error("BackgroundWorker_RunWorkerComplete Cancelled");
			}
			else if (e.Result != null)
			{
				mLogger.Debug("BackgroundWorker_RunWorkerComplete Completed: " + e.Result.ToString());
			}
			else
			{
				mLogger.Debug("BackgroundWorker_RunWorkerComplete Completed");
			}
		}
	}
}
