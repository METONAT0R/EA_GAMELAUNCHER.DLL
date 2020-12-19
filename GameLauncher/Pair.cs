namespace GameLauncher
{
	internal class Pair<T1, T2>
	{
		private T1 _first;

		private T2 _second;

		public T1 First => _first;

		public T2 Second => _second;

		public Pair(T1 first, T2 second)
		{
			_first = first;
			_second = second;
		}
	}
}
