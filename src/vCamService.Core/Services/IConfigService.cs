using vCamService.Core.Models;

namespace vCamService.Core.Services;

public interface IConfigService
{
    AppConfig Load();
    void Save(AppConfig config);
}
