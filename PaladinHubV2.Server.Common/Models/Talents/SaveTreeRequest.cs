namespace PaladinHub.Models.Talents
{
	public record SaveTreeRequest(string Key, List<NodeState> Nodes);
	public record NodeState(string Id, bool Active);
}