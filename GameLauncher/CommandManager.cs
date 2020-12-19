using log4net;
using System.Collections.Generic;

namespace GameLauncher
{
	public class CommandManager
	{
		private static readonly ILog mLogger = LogManager.GetLogger(typeof(CommandManager));

		private List<Pair<DownloaderCommand, ICommandArgument>> _queue;

		private Pair<DownloaderCommand, ICommandArgument> _currentItem;

		public int Count => _queue.Count;

		public DownloaderCommand CurrentCommand
		{
			get
			{
				if (_currentItem == null)
				{
					return null;
				}
				return _currentItem.First;
			}
		}

		public ICommandArgument CurrentParameters
		{
			get
			{
				if (_currentItem == null)
				{
					return null;
				}
				return _currentItem.Second;
			}
		}

		public CommandManager()
		{
			_queue = new List<Pair<DownloaderCommand, ICommandArgument>>();
		}

		public void AddBack(DownloaderCommand command, ICommandArgument parameters)
		{
			mLogger.Debug("Adding command to the queue: " + command.GetType());
			lock (_queue)
			{
				_queue.Add(new Pair<DownloaderCommand, ICommandArgument>(command, parameters));
			}
		}

		public void AddFront(DownloaderCommand command, ICommandArgument parameters)
		{
			mLogger.Debug("Adding command to the queue: " + command.GetType());
			lock (_queue)
			{
				_queue.Insert(0, new Pair<DownloaderCommand, ICommandArgument>(command, parameters));
			}
		}

		public void ExecuteNext()
		{
			mLogger.Debug("Executing next item in the queue");
			lock (_queue)
			{
				if (_queue.Count > 0)
				{
					_currentItem = _queue[0];
					_queue.RemoveAt(0);
					_currentItem.First.Execute(_currentItem.Second);
				}
				else
				{
					_currentItem = null;
				}
			}
		}

		public void Clear()
		{
			mLogger.Debug("Cleaning the queue");
			lock (_queue)
			{
				_queue.Clear();
			}
		}
	}
}
