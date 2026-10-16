using System.Threading.Tasks;

namespace AcxiomCRM.Services
{
    public interface IAuditService
    {
        Task LogAsync(string action, string entityName, string? recordId = null, string? oldValue = null, string? newValue = null, string? details = null, string? result = "Success", string? specificUserId = null, string? specificUserName = null);
    }
}
