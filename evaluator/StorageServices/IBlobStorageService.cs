using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IBlobStorageService
{
    Task<List<string>> ListGroundTruthUris(CancellationToken cancellationToken = default);
    Task<string> CreateInferenceBlob(string blobName, CancellationToken cancellationToken = default);
    Task<string> CreateEvaluationBlob(string blobName, CancellationToken cancellationToken = default);
}