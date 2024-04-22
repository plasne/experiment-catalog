public interface IStorageService
{
    Task<List<string>> ListGroundTruthUris(CancellationToken cancellationToken = default);
    Task<string> CreateInferenceBlob(string blobName, CancellationToken cancellationToken = default);
    Task<string> CreateEvaluationBlob(string blobName, CancellationToken cancellationToken = default);
}