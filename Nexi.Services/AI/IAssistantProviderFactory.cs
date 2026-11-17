using Nexi.Data.Models;

namespace Nexi.Services.AI;

public interface IAssistantProviderFactory
{
    IAssistantProvider Create(AppSettings settings);
}
