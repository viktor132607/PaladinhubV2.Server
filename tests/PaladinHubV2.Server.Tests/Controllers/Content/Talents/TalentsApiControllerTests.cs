using Microsoft.AspNetCore.Mvc;
using PaladinHub.Models.Talents;
using PaladinHubV2.Server.API.Controllers.Content.Talents;
using PaladinHubV2.Server.Data.Entities;
using PaladinHubV2.Server.Domain.Services.TalentTrees;
using Xunit;

namespace PaladinHubV2.Server.Tests.Controllers.Content.Talents;

public sealed class TalentsApiControllerTests
{
    [Fact]
    public async Task Save_WhenServiceSucceeds_ReturnsNoContentAndForwardsRequest()
    {
        var service = new FakeTalentTreeService
        {
            NextResult = new TalentTreeSaveResult(TalentTreeSaveError.None)
        };
        var request = new SaveTreeRequest("holy", new List<NodeState>());
        var controller = new TalentsApiController(service);

        IActionResult result = await controller.Save("holy", request);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("holy", service.CapturedKey);
        Assert.Same(request, service.CapturedRequest);
    }

    [Theory]
    [InlineData(TalentTreeSaveError.KeyRequired, "Talent tree key is required.")]
    [InlineData(TalentTreeSaveError.DataRequired, "Talent tree data is required.")]
    [InlineData(TalentTreeSaveError.KeyMismatch, "The route key does not match the request key.")]
    [InlineData(TalentTreeSaveError.NodesRequired, "Talent nodes are required.")]
    [InlineData(TalentTreeSaveError.InvalidNode, "Every talent node must contain a valid ID.")]
    public async Task Save_WhenValidationFails_ReturnsExpectedBadRequest(
        TalentTreeSaveError error,
        string expectedMessage)
    {
        var service = new FakeTalentTreeService
        {
            NextResult = new TalentTreeSaveResult(error)
        };
        var controller = new TalentsApiController(service);

        IActionResult result = await controller.Save("holy", null);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(expectedMessage, ReadMessage(badRequest.Value));
    }

    [Fact]
    public async Task Save_WhenDuplicateNodeExists_IncludesDuplicateNodeIdInMessage()
    {
        var service = new FakeTalentTreeService
        {
            NextResult = new TalentTreeSaveResult(
                TalentTreeSaveError.DuplicateNode,
                "node-42")
        };
        var controller = new TalentsApiController(service);

        IActionResult result = await controller.Save("holy", null);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            "Duplicate talent node ID: node-42.",
            ReadMessage(badRequest.Value));
    }

    [Fact]
    public async Task Save_WhenServiceReturnsUnknownError_ReturnsPlainBadRequest()
    {
        var service = new FakeTalentTreeService
        {
            NextResult = new TalentTreeSaveResult((TalentTreeSaveError)999)
        };
        var controller = new TalentsApiController(service);

        IActionResult result = await controller.Save("holy", null);

        Assert.IsType<BadRequestResult>(result);
    }

    private static string? ReadMessage(object? value)
    {
        return value?
            .GetType()
            .GetProperty("message")?
            .GetValue(value) as string;
    }

    private sealed class FakeTalentTreeService : ITalentTreeService
    {
        public TalentTreeSaveResult NextResult { get; init; } =
            new(TalentTreeSaveError.None);

        public string? CapturedKey { get; private set; }
        public SaveTreeRequest? CapturedRequest { get; private set; }

        public Task<TalentTreeSaveResult> ValidateAndSaveActiveStatesAsync(
            string? routeKey,
            SaveTreeRequest? request)
        {
            CapturedKey = routeKey;
            CapturedRequest = request;
            return Task.FromResult(NextResult);
        }

        public Task<Dictionary<string, TalentTreeViewModel>> GetTalentTrees(
            string section,
            List<Spell> spells) =>
            throw new NotSupportedException();

        public Task<TalentTreeViewModel?> GetTalentTree(
            string key,
            string section,
            List<Spell> spells) =>
            throw new NotSupportedException();

        public Task SaveActiveStatesAsync(
            string key,
            List<NodeState> nodes) =>
            throw new NotSupportedException();
    }
}
