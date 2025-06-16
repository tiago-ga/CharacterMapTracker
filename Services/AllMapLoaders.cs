//using Blish_HUD.Graphics.UI;
//using Blish_HUD.Controls;
//using Blish_HUD.Settings;

using Blish_HUD.Modules.Managers;
using Blish_HUD.Modules;
using Blish_HUD;
using CharacterMapTracker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.ComponentModel.Composition;

namespace CharacterMapTracker.Services {
    internal class AllMapLoaders  {


        public static AllMapLoaders Instance { get; } = new AllMapLoaders();

        //[ImportingConstructor]
        //public AllMapLoaders([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) {
        //    Instance = this;
        //}



        private JsonSerializerOptions _optionsJSON = new JsonSerializerOptions {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        private bool _isLoadingMapIds = false;



        // Use local .txt to load map Ids instead of the API which loads many maps not used from the .xml
        public async Task<List<int>> GetLocalMapIds() {
            string path = "markersMapIDs.txt"; // Path to your .txt file in 'ref' folder
            List<int> mapIds = new List<int>();
            try {
                // Use Task.Run to offload synchronous file I/O
                return await Task.Run(() => {
                    using (Stream stream = CharacterMapTrackerModule.Instance.ContentsManager.GetFileStream(path))
                    using (StreamReader reader = new StreamReader(stream)) {

                        string line;
                        while ((line = reader.ReadLine()) != null) {
                            if (int.TryParse(line.Trim(), out int id) && id > 0)
                                mapIds.Add(id);
                        }
                        return mapIds;
                    }
                });
            }
            catch (Exception ex) {
                CharacterMapTrackerModule.Logger.Warn($"Failed to read {path}. Error: {ex.Message}");
            }
            return mapIds;
        }

        public async Task<Dictionary<int, List<MarkerWithApiData>>> LoadMarkersFromJSON() {

            List<MarkerWithApiData> currMapWithApiData;
            var result = new Dictionary<int, List<MarkerWithApiData>>();

            var currCharacterName = GameService.Gw2Mumble.PlayerCharacter.Name ?? "UnknownCharacter";

            try {

                // Obtain the map IDs of all maps in the game 
                //var mapIds = await Gw2ApiHelper.GetAPIMapIds();   // from API
                var mapIds = await GetLocalMapIds();                // Using Local File markersMapIDs.txt

                foreach (var id in mapIds) {
                    var baseDir = CharacterMapTrackerModule.Instance.DirectoriesManager.GetFullDirectoryPath("map-tracker");
                    var jsonPath = Path.Combine(baseDir, $"Maps", $"{currCharacterName}", $"progress_map{id}.json");

                    if (!File.Exists(jsonPath)) {
                        //CharacterMapTrackerModule.Logger.Warn($"JSON file for map{id} hasn't been loaded yet, loading next");
                        continue;
                    }

                    // Read JSON file
                    var json = File.ReadAllText(jsonPath);

                    // Serialize from existing JSON
                    currMapWithApiData = JsonSerializer.Deserialize<List<MarkerWithApiData>>(json);


                    if (currMapWithApiData != null) result[id] = currMapWithApiData;
                    // Read only the markers category in the file
                    //markers = currMarkerWithApiData.Select(m => m.Marker).ToList();

                    //// Group JSON markers by expansion (includes API info)
                    //var instance = CharacterMapTrackerModule.Instance;
                    //currCore = currMarkerWithApiData.Where(m => m.Marker.expansion == instance.EXP_CORE).ToList(); ;
                    //currHot = currMarkerWithApiData.Where(m => m.Marker.expansion == instance.EXP_HOT).ToList(); ;
                    //currPof = currMarkerWithApiData.Where(m => m.Marker.expansion == instance.EXP_POF).ToList(); ;
                    //currEod = currMarkerWithApiData.Where(m => m.Marker.expansion == instance.EXP_EOD).ToList(); ;
                    //currSoto = currMarkerWithApiData.Where(m => m.Marker.expansion == instance.EXP_SOTO).ToList(); ;
                    //currJan = currMarkerWithApiData.Where(m => m.Marker.expansion == instance.EXP_JANTHIR).ToList(); ;
                    //currLw1 = currMarkerWithApiData.Where(m => m.Marker.expansion == instance.LWS1).ToList(); ;
                    //currLw2 = currMarkerWithApiData.Where(m => m.Marker.expansion == instance.LWS2).ToList(); ;
                    //currLw3 = currMarkerWithApiData.Where(m => m.Marker.expansion == instance.LWS3).ToList(); ;
                    //currLw4 = currMarkerWithApiData.Where(m => m.Marker.expansion == instance.LWS4).ToList(); ;
                    //currLw5 = currMarkerWithApiData.Where(m => m.Marker.expansion == instance.LWS5).ToList(); ;

                    //CharacterMapTrackerModule.Logger.Info($"Using existing JSON file at: {jsonPath}");
                    //CharacterMapTrackerModule.Logger.Info($"Total markers loaded: {currMapWithApiData.Count}");
                }
                    
            }
            catch (Exception ex) {
                CharacterMapTrackerModule.Logger.Warn($"Could not load JSON markers: {ex.Message}");
            }

            return result;

        }


        protected async Task LoadMarkersForAllMaps() {
            if (_isLoadingMapIds) return;   // Skips if already loading

            int currMapId = -1;
            MapInfo currMapInfo;
            List<MarkerWithApiData> currMarkerWithApiData;
            int countCreated = 0;
            var currCharacterName = GameService.Gw2Mumble.PlayerCharacter.Name ?? "UnknownCharacter";

            _isLoadingMapIds = true;

            //CharacterMapTrackerModule.Logger.Info("Testing before attempting to LoadMarkersForAllMaps");
            try {
                // Obtain the map IDs of all maps in the game 
                //var mapIds = await Gw2ApiHelper.GetAPIMapIds();   // from API
                var mapIds = await GetLocalMapIds();                // Using Local File markersMapIDs.txt

                if (mapIds.Count == 0 || mapIds == null) return;

                foreach (var id in mapIds) {
                    // Obtain the map info (name, continent, floor, region IDs) from the current mapId
                    try {
                        currMapInfo = await Gw2ApiHelper.GetMapInfoAsync(id);
                    }
                    catch (Exception ex) {
                        CharacterMapTrackerModule.Logger.Warn($"Skipped map {currMapId}: API request failed with Error:{ex.Message}");
                        continue;
                    }

                    if (currMapInfo.type != "Public") continue;   // Ignore instances

                    currMapId = id;  // To have info in case of error
                    var baseDir = CharacterMapTrackerModule.Instance.DirectoriesManager.GetFullDirectoryPath("map-tracker");
                    var jsonPath = Path.Combine(baseDir, $"Maps", $"{currCharacterName}", $"progress_map{currMapId}.json");

                    List<Marker> markers;
                    if (!File.Exists(jsonPath)) {
                        // Parse XML and create JSON with found = false (only happens for maps loaded for the first time)
                        try {
                            string xmlPath = $"markers/map{currMapId}.xml";
                            using (Stream stream = await Task.Run(() => CharacterMapTrackerModule.Instance.ContentsManager.GetFileStream(xmlPath))) {
                                markers = PathingMarkerLoader.LoadMarkersFromXml(stream, currMapId, currMapInfo.name);

                                foreach (var m in markers) {
                                    m.found = false;
                                    m.MapName = currMapInfo.name;
                                    m.region_id = currMapInfo.region_id;
                                    m.region_name = currMapInfo.region_name;

                                    // First check by map IDs to get the isolated ones
                                    var lws3MapIds = new HashSet<int> { 1178, 1185, 1203, 1165, 1175 };
                                    var lws4MapIds = new HashSet<int> { 1263, 1288, 1317, 1301, 1271, 1310 };
                                    var ibsMapIds = new HashSet<int> { 1343, 1371, 1330 };

                                    // Then check by region to get the rest in the proper expansion
                                    var coreMapRegion = new HashSet<int> { 1, 2, 3, 4, 5, 8 };
                                    var hotMapRegion = new HashSet<int> { 10 };
                                    var pofMapRegion = new HashSet<int> { 12 };
                                    var eodMapRegion = new HashSet<int> { 37 };
                                    var sotoMapRegion = new HashSet<int> { 48 };
                                    var janMapRegion = new HashSet<int> { 18 };

                                    if (currMapId == 873) {
                                        m.expansion = "LWS1";
                                    }
                                    else if (currMapInfo.region_id == 11) {
                                        m.expansion = "LWS2";
                                    }
                                    else if (currMapInfo.region_id == 20 || lws3MapIds.Contains(currMapId)) {
                                        m.expansion = "LWS3";
                                    }
                                    else if (lws4MapIds.Contains(currMapId)) {
                                        m.expansion = "LWS4";
                                    }
                                    else if (ibsMapIds.Contains(currMapId)) {
                                        m.expansion = "Icebrood Saga";
                                    }
                                    else if (coreMapRegion.Contains(currMapInfo.region_id)) {
                                        m.expansion = "Core";
                                    }
                                    else if (hotMapRegion.Contains(currMapInfo.region_id)) {
                                        m.expansion = "Heart of Thorns";
                                    }
                                    else if (pofMapRegion.Contains(currMapInfo.region_id)) {
                                        m.expansion = "Path of Fire";
                                    }
                                    else if (eodMapRegion.Contains(currMapInfo.region_id)) {
                                        m.expansion = "End of Dragons";
                                    }
                                    else if (sotoMapRegion.Contains(currMapInfo.region_id)) {
                                        m.expansion = "Secrets of the Obscure";
                                    }
                                    else if (janMapRegion.Contains(currMapInfo.region_id)) {
                                        m.expansion = "Janthir Wilds";
                                    }
                                }
                            }
                        }
                        catch (Exception) {
                            //Logger.Warn($"XML marker for map {currMapId} doesn't exist, skipped it and continue");
                            continue;
                        }

                        CharacterMapTrackerModule.Logger.Info($"Before markertWithApiData created");
                        // Populate MarkerWithApiData with only Marker field set, API fields null
                        currMarkerWithApiData = markers.Select(m => new MarkerWithApiData {
                            Marker = m,
                            ApiPoi = null,
                            ApiHeart = null,
                            ApiHeroPoint = null
                        }).ToList();

                        countCreated++;
                        CharacterMapTrackerModule.Logger.Info($"After markertWithApiData created");
                        try {
                            // Serialize to JSON and save
                            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));
                            File.WriteAllText(jsonPath, JsonSerializer.Serialize(currMarkerWithApiData, _optionsJSON));
                            CharacterMapTrackerModule.Logger.Info($"Succesfully created JSON file at: {jsonPath}");
                        }
                        catch (Exception) {
                            CharacterMapTrackerModule.Logger.Warn($"Attempting to save XML marker for map {currMapId}, but failed processing");
                            continue;
                        }
                    }
                    else {
                        continue;
                    }
                }
                CharacterMapTrackerModule.Logger.Info($"Total maps created that weren't loaded previously: {countCreated}");
            }
            catch (Exception ex) {
                CharacterMapTrackerModule.Logger.Warn($"Could not load XML markers for map {currMapId}: {ex.Message}");
            }
            finally {
                _isLoadingMapIds = false;
            }
        }

        // Function to load all maps from other modules
        public async Task GenerateAllMapMarkerFilesAsync() {
            await LoadMarkersForAllMaps();
        }
    }
}
