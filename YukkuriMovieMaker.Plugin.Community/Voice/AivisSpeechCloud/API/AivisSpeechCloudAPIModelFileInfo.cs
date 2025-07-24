using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisSpeechCloud.API
{
    public record AivisSpeechCloudAPIModelFileInfo(
        [property: JsonProperty("aivm_model_uuid")] string AivmModelUuid,
        [property: JsonProperty("manifest_version")] string ManifestVersion,
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("description")] string Description,
        [property: JsonProperty("creators")] List<string> Creators,
        [property: JsonProperty("license_type")] string LicenseType,
        [property: JsonProperty("license_text")] string? LicenseText,
        [property: JsonProperty("model_type")] string ModelType,
        [property: JsonProperty("model_architecture")] string ModelArchitecture,
        [property: JsonProperty("model_format")] string ModelFormat,
        [property: JsonProperty("training_epochs")] int? TrainingEpochs,
        [property: JsonProperty("training_steps")] int? TrainingSteps,
        [property: JsonProperty("version")] string Version,
        [property: JsonProperty("file_size")] long FileSize,
        [property: JsonProperty("checksum")] string Checksum,
        [property: JsonProperty("download_count")] int DownloadCount,
        [property: JsonProperty("created_at")] DateTime CreatedAt,
        [property: JsonProperty("updated_at")] DateTime UpdatedAt
        );
}