using Microsoft.Extensions.DependencyInjection;
using RedBlacksmith.TrafficProcessing.Models.Abstracts;

namespace RedBlacksmith.TrafficProcessing.Application;

public static class ApplicationServicesExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services) => 
        services.AddSingleton<IPacketEventProcessor, PacketEventProcessor>();
}