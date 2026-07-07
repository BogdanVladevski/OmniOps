using MediatR;
using OmniOps.Application.Commands;
using OmniOps.Application.Dtos;
using OmniOps.Core.Interfaces;

namespace OmniOps.Application.Commands.Handlers;

public class CopilotAskCommandHandler : IRequestHandler<CopilotAskCommand, CopilotResponseDto>
{
    private readonly IFleetCopilotService _copilot;

    public CopilotAskCommandHandler(IFleetCopilotService copilot) => _copilot = copilot;

    public async Task<CopilotResponseDto> Handle(CopilotAskCommand request, CancellationToken cancellationToken)
    {
        var answer = await _copilot.AskAsync(request.Question, request.FleetId, cancellationToken);
        var mode = answer.StartsWith("[Copilot — offline mode]", StringComparison.Ordinal) ? "offline" : "llm";
        return new CopilotResponseDto(answer, mode);
    }
}
