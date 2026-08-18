namespace PaladinHubV2.Server.Domain.Services.Common
{
	public class OperationResult
	{
		public bool Ok { get; set; }
		public string Message { get; set; } = "";
		public string? Path { get; set; }
		public static OperationResult Success(string msg, string? path = null) => new OperationResult { Ok = true, Message = msg, Path = path };
		public static OperationResult Fail(string msg) => new OperationResult { Ok = false, Message = msg };
	}
}
