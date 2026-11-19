using FastEndpoints;
using FluentValidation;
using TrafficApi.Models;

namespace TrafficApi;

public class GetSourceTrafficMinuteValidator : Validator<GetSourceTrafficMinuteRequest>
{
    public GetSourceTrafficMinuteValidator()
    {
        RuleFor(x => x.SourceId)
            .NotEmpty();

        RuleFor(x => x.FromUtc)
            .NotEmpty();

        RuleFor(x => x.ToUtc)
            .NotEmpty()
            .GreaterThan(x => x.FromUtc);
    }
}