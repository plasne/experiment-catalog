public interface IStorageService
{
    public Task<List<string>> ListGroundTruthUris(CancellationToken cancellationToken = default);
}