namespace PaladinHub.Helpers.Exceptions
{
	public class AppException : Exception
	{
		public int StatusCode { get; set; } = 500;

		public List<object> Args { get; } = new();

		public AppException(string message) : base(message) { }

		public AppException SetStatusCode(int statusCode)
		{
			StatusCode = statusCode;
			return this;
		}

		public AppException AddArgs(params object?[]? args)
		{
			if (args is { Length: > 0 })
			{
				Args.AddRange(args.OfType<object>());
			}
			return this;
		}
	}
}
