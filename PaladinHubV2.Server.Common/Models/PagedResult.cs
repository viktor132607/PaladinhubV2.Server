namespace PaladinHub.Models
{
	public class PagedResult<T>
	{
		public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
		public int Page { get; set; }
		public int PageSize { get; set; }
		public int TotalItems { get; set; }
		public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
		public bool HasPrevious => Page > 1;
		public bool HasNext => Page < TotalPages;
	}
}
