using GestorEquipos.Models.ViewModels.CasoTI;

namespace Gestor_Equipos.Services
{
    public interface ICasoTIService
    {
        Task<int> CreateAsync(CasoTICreateViewModel vm, int actingUserSystemId, int actingUsersId, string actingUserRole, CancellationToken ct = default);
        Task<CasoTIDetailViewModel?> GetDetailAsync(int id, int actingUserSystemId, int actingUsersId, string actingUserRole, CancellationToken ct = default);

        Task<List<CasoTIListItemViewModel>> GetIndexAsync(
            int actingUserSystemId, int actingUsersId, string actingUserRole, CasoTIIndexFilterViewModel filter, CancellationToken ct = default);

        Task ResolveAsync(int casoId, string notasFinal, int actingUserSystemId, string actingUserRole, Func<Guid, string> buildRatingUrl, CancellationToken ct = default);
        Task DeleteAsync(int casoId, int actingUsersId, CancellationToken ct = default);
        Task<CasoTIRatingPromptViewModel?> GetRatingPromptAsync(Guid token, CancellationToken ct = default);
        Task<CasoTIRatingSubmitResult> SubmitRatingAsync(Guid token, int rating, string? comment, CancellationToken ct = default);

        Task<CasoTIStatsViewModel> GetStatsAsync(
            int actingUserSystemId, int actingUsersId, string actingUserRole, int? tecnicoFilterUserSystemId,
            CasoTIGranularidad granularidad, int? mes, int? anio, CancellationToken ct = default);

        Task<CasoTIRatingsViewModel> GetRatingsAsync(int actingUserSystemId, int actingUsersId, string actingUserRole, int? tecnicoFilterUserSystemId, CancellationToken ct = default);
    }
}
