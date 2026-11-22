using FastEndpoints;
using FluentValidation;
using TrafficApi.Models;

namespace TrafficApi;

public class GetTrafficMinuteAnalyticsValidator : Validator<GetTrafficMinuteAnalyticsRequest>
{
    public GetTrafficMinuteAnalyticsValidator()
    {
        RuleFor(x => x.FromUtc)
            .NotEmpty();

        RuleFor(x => x.ToUtc)
            .NotEmpty()
            .GreaterThan(x => x.FromUtc);
    }
}