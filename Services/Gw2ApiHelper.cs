using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CharacterMapTracker.Models;

namespace CharacterMapTracker.Services {

    public static class Gw2ApiHelper {

        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<MapInfo> GetMapInfoAsync(int mapId) {
            var url = $"https://api.guildwars2.com/v2/maps/{mapId}";

            var json = await _httpClient.GetStringAsync(url);
            var mapInfo = JsonConvert.DeserializeObject<MapInfo>(json);

            return mapInfo;
        }

        public static async Task<MapDetails> GetMapDetailsAsync(int continentId, int floorId, int regionId, int mapId) {
            string url = $"https://api.guildwars2.com/v2/continents/{continentId}/floors/{floorId}/regions/{regionId}/maps/{mapId}";

            var json = await _httpClient.GetStringAsync(url);
            var mapDetails = JsonConvert.DeserializeObject<MapDetails>(json);

            return mapDetails;
        }

    }

}