using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Repositories;
using IepAssistant.Domain.Storage;

namespace IepAssistant.Domain;

public static class DependencyInjection
{
    public static IServiceCollection AddDomain(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IChildProfileRepository, ChildProfileRepository>();
        services.AddScoped<IIepDocumentRepository, IepDocumentRepository>();
        services.AddScoped<IParentAdvocacyGoalRepository, ParentAdvocacyGoalRepository>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        var blobConnectionString = configuration.GetConnectionString("BlobStorage")
            ?? throw new InvalidOperationException("ConnectionStrings:BlobStorage is not configured.");
        services.AddSingleton(new BlobServiceClient(blobConnectionString));
        services.AddSingleton<IBlobStorageService>(sp =>
            new AzureBlobStorageService(sp.GetRequiredService<BlobServiceClient>()));

        return services;
    }
}
