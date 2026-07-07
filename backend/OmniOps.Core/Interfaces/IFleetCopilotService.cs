namespace OmniOps.Core.Interfaces;

public interface IFleetCopilotService
{
    Task<string> AskAsync(string question, Guid fleetId, CancellationToken cancellationToken = default);
}
