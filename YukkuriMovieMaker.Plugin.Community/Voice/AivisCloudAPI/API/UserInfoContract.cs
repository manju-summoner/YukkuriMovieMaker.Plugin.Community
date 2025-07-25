using Newtonsoft.Json;
namespace YukkuriMovieMaker.Plugin.Community.Voice.AivisCloudAPI.API
{
    public record UserInfoContract(
        [property: JsonProperty("handle")] string Handle,
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("description")] string Description,
        [property: JsonProperty("icon_url")] string IconUrl,
        [property: JsonProperty("account_type")] string AccountType,
        [property: JsonProperty("account_status")] string AccountStatus,
        [property: JsonProperty("social_links")] List<SocialLinkContract> SocialLinks
        );
}