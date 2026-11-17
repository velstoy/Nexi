using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Nexi.Data;

namespace Nexi.UI.Tests;

public static class TestStore
{
    public static INexiStore Create()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<NexiDbContext>(options =>
            options.UseInMemoryDatabase("nexi-ui-tests-" + Guid.NewGuid()));
        services.AddSingleton<INexiStore, NexiStore>();
        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<INexiStore>();
        store.EnsureCreatedAsync().GetAwaiter().GetResult();
        return store;
    }
}
