using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



using MediatR;
using OmniOps.Core.Entities;

namespace OmniOps.Core.Telemetry
{
    public record ProcessTelemetryCommand(VehicleTelemetry Telemetry) : IRequest<bool>;
}
