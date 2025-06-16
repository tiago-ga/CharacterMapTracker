using Blish_HUD;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;                 
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using CharacterMapTracker.UI;
using System.Reflection.Emit;
using CharacterMapTracker.Models;
using CharacterMapTracker.Services;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using Blish_HUD.Content;
using SharpDX.Direct3D9;
using System.Text.Json;
using Blish_HUD.Gw2Mumble;
using Microsoft.Xna.Framework.Content;
//using Gw2Sharp.WebApi.V2.Models;
//using Blish_HUD.Gw2WebApi;

namespace CharacterMapTracker {
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class CharacterMapTrackerModule : Blish_HUD.Modules.Module {

        public static readonly Logger Logger = Logger.GetLogger<CharacterMapTrackerModule>();

        #region Service Managers
        internal SettingsManager SettingsManager => this.ModuleParameters.SettingsManager;
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion

        public static CharacterMapTrackerModule Instance { get; private set; }

        [ImportingConstructor]
        public CharacterMapTrackerModule([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) {
            Instance = this;
        }

        protected override void DefineSettings(SettingCollection settings) {

        }

        public string EXP_CORE = "Core";
        public string EXP_HOT = "Heart of Thorns";
        public string EXP_POF = "Path of Fire";
        public string EXP_EOD = "End of Dragons";
        public string EXP_SOTO = "Secrets of the Obscure";
        public string EXP_JANTHIR = "Janthir Wilds";

        public string LWS1 = "LWS1";
        public string LWS2 = "LWS2";
        public string LWS3 = "LWS3";
        public string LWS4 = "LWS4";
        public string LWS5 = "Icebrood Saga";

        public List<MarkerWithApiData> _markerWithApiData = new List<MarkerWithApiData>();
        public MapDetails _currentMapDetails;
        public List<int> _regionIds;
        public List<RegionInfo> _regions;
        //public string _accountName;
        public string _currentCharacterName;

        public event Action MapProgressSaved;

        //private Account _account;
        private TrackerWindow _trackerWindow;
        private MainWindow _mainWindow;
        private CornerIcon _cornerIcon;
        private MapInfo _currentMapInfo;
        private bool _isLoadingMapData = false;
        private int _lastMapId = -1;
        private List<Marker> _currentXmlMarkers;
        private bool _isLoading = false;
        private bool _isLoadingMapIds = false;
        private bool _isLoadingMapIdForRegions = false;

        private AsyncTexture2D _trackerBackground;
        private Texture2D _trackerBackgroundCropped;

        private List<PointOfInterest> _waypoints;
        private List<PointOfInterest> _landmarks;
        private List<PointOfInterest> _vistas;
        private List<TaskInfo> _hearts;
        private List<SkillChallenge> _heropoints;

        private List<Marker> _currentXmlwaypoints;
        private List<Marker> _currentXmllandmarks;
        private List<Marker> _currentXmlvistas;
        private List<Marker> _currentXmlheropoints;
        private List<Marker> _currentXmlhearts;

        private JsonSerializerOptions _optionsJSON = new JsonSerializerOptions {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        protected override void Initialize()
        {
            // Called once when module is loaded
            _trackerBackground = ContentsManager.GetTexture("UI/background156003.png");
            _trackerBackgroundCropped = _trackerBackground.Texture.GetRegion(30, 25, 300, 380);

            _trackerWindow = new TrackerWindow(this.ContentsManager, _trackerBackgroundCropped);
            _trackerWindow.Emblem = ContentsManager.GetTexture("UI/emblem156034.png"); ;
            _trackerWindow.Parent = GameService.Graphics.SpriteScreen;

            _mainWindow = new MainWindow();
            _mainWindow.Emblem = ContentsManager.GetTexture("UI/emblem156035.png");
            _mainWindow.Parent = GameService.Graphics.SpriteScreen;

            // Hide it until user opens it
            _trackerWindow.Hide();
            _mainWindow.Hide();
        }

        // Load map details
        protected async Task LoadMapDataAsync(int currentMapId) {
            if (_isLoadingMapData) return;          

            _isLoadingMapData = true;
            try {
                // Obtain the map info (continent, floor, region IDs) from the current map
                _currentMapInfo = await Gw2ApiHelper.GetMapInfoAsync(currentMapId);

                // Load current character's name
                _currentCharacterName = GameService.Gw2Mumble.PlayerCharacter.Name ?? "UnknownCharacter";

                // Load account name and player character name
                //await FetchAccountName();
                //var account = await Gw2ApiManager.Gw2ApiClient.V2.Account.GetAsync();
                //_accountName = account.Name ?? "UnkownAccount";

                int continentId = _currentMapInfo.continent_id;
                int regionId = _currentMapInfo.region_id;
                MapDetails mapDetails = null;

                // Go through each floor to make sure it finds a valid one to load the map details
                foreach (int floor in _currentMapInfo.floors) {
                    try {
                        // From the current map obtains the details, with this the coordinates of each POI 
                        mapDetails = await Gw2ApiHelper.GetMapDetailsAsync(continentId, floor, regionId, currentMapId);

                        if (mapDetails != null) {
                            Logger.Info($"Successfully loaded map details for floor {floor}.");
                            break; // Stop checking other floors
                        }
                    }
                    catch (Exception ex) {
                        Logger.Warn($"Failed to load floor {floor}: {ex.Message}");
                        continue; // Try next floor
                    }
                }

                _currentMapDetails = mapDetails; // Store it for later use

                // Group POIs by type
                _hearts = _currentMapDetails.tasks.Values.ToList();
                _heropoints = _currentMapDetails.skill_challenges;
                _waypoints = _currentMapDetails.points_of_interest.Values.Where(p => p.type == "waypoint").ToList();
                _landmarks = _currentMapDetails.points_of_interest.Values.Where(p => p.type == "landmark").ToList();
                _vistas = _currentMapDetails.points_of_interest.Values.Where(p => p.type == "vista").ToList();

                Logger.Info($"On current character: {_currentCharacterName} Loaded map: {_currentMapDetails.name}, " +
                    $"with Landmarks: {_landmarks?.Count ?? 0}, vistas: {_vistas?.Count ?? 0}, Waypoints:{_waypoints?.Count ?? 0}, " +
                    $"Tasks: {_currentMapDetails.tasks?.Count ?? 0}, and SkillChallenges: {_currentMapDetails.skill_challenges?.Count ?? 0}");
            }
            catch (Exception ex) {
                Logger.Error(ex, "Error loading map details.");
            }
            finally {
                _isLoadingMapData = false;
            }
        }
        // -----------------------------------------------------------------------------------------------TO FETCH ACCOUNT NAME, TEST
        //private async Task FetchAccountName() {
        //    if (!this.Gw2ApiManager.HasPermissions(new[]
        //        {
        //             TokenPermission.Account
        //         })) {
        //        return;
        //    }

        //    Account account = await this.Gw2ApiManager.Gw2ApiClient.V2.Account.GetAsync();
        //    _accountName = account.Name;
        //}


        // Load markers from .xml
        protected async Task LoadMarkersForCurrentMap(int mapId)
        {
            if (_currentMapDetails == null)
            {
                Logger.Info($"Waiting for {mapId} to load first");
                return;
            }
            try
            {

                if (_currentMapInfo.type != "Public" && _currentMapInfo.id != mapId) return;   // Ignore instances 
                
                var baseDir = ModuleParameters.DirectoriesManager.GetFullDirectoryPath("map-tracker");
                var jsonPath = Path.Combine(baseDir, $"Maps", $"{_currentCharacterName}", $"progress_map{mapId}.json");

                List<Marker> markers;
                if (!File.Exists(jsonPath))
                {
                    // Parse XML and create JSON with found = false (only happens for maps loaded for the first time)
                    string xmlPath = $"markers/map{mapId}.xml";
                    using (Stream stream = await Task.Run(() => ContentsManager.GetFileStream(xmlPath)))
                    {
                        markers = PathingMarkerLoader.LoadMarkersFromXml(stream, mapId, _currentMapDetails.name);

                        foreach (var m in markers) {
                            m.found = false;
                            m.MapName = _currentMapInfo.name;
                            m.region_id = _currentMapInfo.region_id;
                            m.region_name = _currentMapInfo.region_name;

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

                            if (mapId == 873) {
                                m.expansion = LWS1;
                            }
                            else if (_currentMapInfo.region_id == 11) {
                                m.expansion = LWS2;
                            }
                            else if (_currentMapInfo.region_id == 20 || lws3MapIds.Contains(mapId)) {
                                m.expansion = LWS3;
                            }
                            else if (lws4MapIds.Contains(mapId)) {
                                m.expansion = LWS4;
                            }
                            else if (ibsMapIds.Contains(mapId)) {
                                m.expansion = LWS5;
                            }
                            else if (coreMapRegion.Contains(_currentMapInfo.region_id)) {
                                m.expansion = EXP_CORE;
                            }
                            else if (hotMapRegion.Contains(_currentMapInfo.region_id)) {
                                m.expansion = EXP_HOT;
                            }
                            else if (pofMapRegion.Contains(_currentMapInfo.region_id)) {
                                m.expansion = EXP_POF;
                            }
                            else if (eodMapRegion.Contains(_currentMapInfo.region_id)) {
                                m.expansion = EXP_EOD;
                            }
                            else if (sotoMapRegion.Contains(_currentMapInfo.region_id)) {
                                m.expansion = EXP_SOTO;
                            }
                            else if (janMapRegion.Contains(_currentMapInfo.region_id)) {
                                m.expansion = EXP_JANTHIR;
                            }
                        }
                    }

                    // Populate MarkerWithApiData with only Marker field set, API fields null
                    _markerWithApiData = markers.Select(m => new MarkerWithApiData
                    {
                        Marker = m,
                        ApiPoi = null,
                        ApiHeart = null,
                        ApiHeroPoint = null
                    }).ToList();

                    // Serialize to JSON and save
                    Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));
                    File.WriteAllText(jsonPath, JsonSerializer.Serialize(_markerWithApiData, _optionsJSON));
                    Logger.Info($"Succesfully created JSON file at: {jsonPath}");
                }
                else
                {
                    var json = File.ReadAllText(jsonPath);

                    // Serialize from existing JSON
                    _markerWithApiData = JsonSerializer.Deserialize<List<MarkerWithApiData>>(json);

                    // Read only the markers category in the file
                    markers = _markerWithApiData.Select(m => m.Marker).ToList();

                    Logger.Info($"Using existing JSON file at: {jsonPath}");
                }

                _currentXmlMarkers = markers;

                // Group POIs by type from .xml markers
                _currentXmlwaypoints = _currentXmlMarkers.Where(m => m.Category == "waypoint").ToList();
                _currentXmllandmarks = _currentXmlMarkers.Where(m => m.Category == "landmark").ToList();
                _currentXmlvistas = _currentXmlMarkers.Where(m => m.Category == "vista").ToList();
                _currentXmlheropoints = _currentXmlMarkers.Where(m => m.Category == "heropoint").ToList();
                _currentXmlhearts = _currentXmlMarkers.Where(m => m.Category == "task").ToList();

                Logger.Info($"Total markers loaded: {_currentXmlMarkers.Count}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not load XML markers for map {mapId}: {ex.Message}");
                _currentXmlMarkers = new List<Marker>(); // fallback to empty
                _markerWithApiData = new List<MarkerWithApiData>();
            }

        }


        //protected async Task<List<Marker>> LoadMarkersForMapId(int mapId) {
        //    if (_isLoadingMapIdForRegions) return null;  // Skip if loading already
        //    List<Marker> markers;
        //    try {
        //        return await Task.Run(() => {
        //            var baseDir = ModuleParameters.DirectoriesManager.GetFullDirectoryPath("map-tracker");
        //            var jsonPath = Path.Combine(baseDir, $"Maps", $"{_currentCharacterName}", $"progress_map{mapId}.json");

        //            if (!File.Exists(jsonPath)) {
        //                Logger.Warn($"JSON file for map{mapId} hasn't been loaded yet");
        //                return null;
        //            }
        //            var json = File.ReadAllText(jsonPath);
        //            var markerWithApiDatas = new List<MarkerWithApiData>();
        //            // Serialize from existing JSON
        //            markerWithApiDatas = JsonSerializer.Deserialize<List<MarkerWithApiData>>(json);

        //            // Read only the markers category in the file
        //            markers = markerWithApiDatas.Select(m => m.Marker).ToList();

        //            Logger.Info($"Using existing JSON file at: {jsonPath}");

        //            //// Group POIs by type from .xml markers
        //            //_currentXmlwaypoints = markers.Where(m => m.Category == "waypoint").ToList();
        //            //_currentXmllandmarks = markers.Where(m => m.Category == "landmark").ToList();
        //            //_currentXmlvistas = markers.Where(m => m.Category == "vista").ToList();
        //            //_currentXmlheropoints = markers.Where(m => m.Category == "heropoint").ToList();
        //            //_currentXmlhearts = markers.Where(m => m.Category == "task").ToList();

        //            Logger.Info($"Total markers loaded: {markers.Count}");
        //            return markers;
        //        });
        //    }

        //    catch (Exception ex) {
        //        Logger.Warn($"Could not load XML markers for map {mapId}: {ex.Message}");
        //        return new List<Marker>();
        //    }
        //    finally {
        //        _isLoadingMapIdForRegions = false;

        //    }

        //}

        //public async Task<List<Marker>> GetLoadMarkersForMapId(int mapId) {
        //     return await LoadMarkersForMapId(mapId);
        //}

        //protected async Task LoadMarkersForAllMaps() {
        //    if (_isLoadingMapIds) return;   // Skips if already loading

        //    int currMapId = -1;
        //    MapInfo currMapInfo;
        //    List<MarkerWithApiData> currMarkerWithApiData;
        //    int countCreated = 0;

        //    _isLoadingMapIds = true;
        //    try {
        //        // Obtain the map IDs of all maps in the game 
        //        //var mapIds = await Gw2ApiHelper.GetAPIMapIds();   // from API
        //        var mapIds = await GetLocalMapIds();                // Using Local File markersMapIDs.txt

        //        if (mapIds.Count == 0 || mapIds == null) return;

        //        foreach (var id in mapIds) {
        //            // Obtain the map info (name, continent, floor, region IDs) from the current mapId
        //            try {
        //                currMapInfo = await Gw2ApiHelper.GetMapInfoAsync(id);
        //            }
        //            catch (Exception ex) {
        //                Logger.Warn($"Skipped map {currMapId}: API request failed with Error:{ex.Message}");
        //                continue;
        //            }

        //            if (currMapInfo.type != "Public") continue;   // Ignore instances

        //            currMapId = id;  // To have info in case of error
        //            var baseDir = ModuleParameters.DirectoriesManager.GetFullDirectoryPath("map-tracker");
        //            var jsonPath = Path.Combine(baseDir, $"Maps", $"{_currentCharacterName}", $"progress_map{currMapId}.json");

        //            List<Marker> markers;
        //            if (!File.Exists(jsonPath)) {
        //                // Parse XML and create JSON with found = false (only happens for maps loaded for the first time)
        //                try {
        //                    string xmlPath = $"markers/map{currMapId}.xml";
        //                    using (Stream stream = await Task.Run(() => ContentsManager.GetFileStream(xmlPath))) {
        //                        markers = PathingMarkerLoader.LoadMarkersFromXml(stream, currMapId, currMapInfo.name);

        //                        foreach (var m in markers) {
        //                            m.found = false;
        //                            m.MapName = currMapInfo.name;
        //                            m.region_id = currMapInfo.region_id;
        //                            m.region_name = currMapInfo.region_name;

        //                            // First check by map IDs to get the isolated ones
        //                            var lws3MapIds = new HashSet<int> { 1178, 1185, 1203, 1165, 1175 };
        //                            var lws4MapIds = new HashSet<int> { 1263, 1288, 1317, 1301, 1271, 1310 };
        //                            var ibsMapIds = new HashSet<int> { 1343, 1371, 1330 };

        //                            // Then check by region to get the rest in the proper expansion
        //                            var coreMapRegion = new HashSet<int> { 1, 2, 3, 4, 5, 8 };
        //                            var hotMapRegion = new HashSet<int> { 10 };
        //                            var pofMapRegion = new HashSet<int> { 12 };
        //                            var eodMapRegion = new HashSet<int> { 37 };
        //                            var sotoMapRegion = new HashSet<int> { 48 };
        //                            var janMapRegion = new HashSet<int> { 18 };

        //                            if (currMapId == 873) {
        //                                m.expansion = "LWS1";
        //                            }
        //                            else if (currMapInfo.region_id == 11) {
        //                                m.expansion = "LWS2";
        //                            }
        //                            else if (currMapInfo.region_id == 20 || lws3MapIds.Contains(currMapId)) {
        //                                m.expansion = "LWS3";
        //                            }
        //                            else if (lws4MapIds.Contains(currMapId)) {
        //                                m.expansion = "LWS4";
        //                            }
        //                            else if (ibsMapIds.Contains(currMapId)) {
        //                                m.expansion = "Icebrood Saga";
        //                            }
        //                            else if (coreMapRegion.Contains(currMapInfo.region_id)) {
        //                                m.expansion = "Core";
        //                            }
        //                            else if (hotMapRegion.Contains(currMapInfo.region_id)) {
        //                                m.expansion = "Heart of Thorns";
        //                            }
        //                            else if (pofMapRegion.Contains(currMapInfo.region_id)) {
        //                                m.expansion = "Path of Fire";
        //                            }
        //                            else if (eodMapRegion.Contains(currMapInfo.region_id)) {
        //                                m.expansion = "End of Dragons";
        //                            }
        //                            else if (sotoMapRegion.Contains(currMapInfo.region_id)) {
        //                                m.expansion = "Secrets of the Obscure";
        //                            }
        //                            else if (janMapRegion.Contains(currMapInfo.region_id)) {
        //                                m.expansion = "Janthir Wilds";
        //                            }
        //                        }
        //                    }
        //                }
        //                catch (Exception) {
        //                    //Logger.Warn($"XML marker for map {currMapId} doesn't exist, skipped it and continue");
        //                    continue;
        //                }

        //                Logger.Info($"Before markertWithApiData created");
        //                // Populate MarkerWithApiData with only Marker field set, API fields null
        //                currMarkerWithApiData = markers.Select(m => new MarkerWithApiData {
        //                    Marker = m,
        //                    ApiPoi = null,
        //                    ApiHeart = null,
        //                    ApiHeroPoint = null
        //                }).ToList();

        //                countCreated++;
        //                Logger.Info($"After markertWithApiData created");
        //                try {
        //                    // Serialize to JSON and save
        //                    Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));
        //                    File.WriteAllText(jsonPath, JsonSerializer.Serialize(currMarkerWithApiData, _optionsJSON));
        //                    Logger.Info($"Succesfully created JSON file at: {jsonPath}");
        //                }
        //                catch (Exception) {
        //                    Logger.Warn($"Attempting to save XML marker for map {currMapId}, but failed processing");
        //                    continue;
        //                }
        //            }
        //            else {
        //                continue;
        //            }
        //        }
        //        Logger.Info($"Total maps created that weren't loaded previously: {countCreated}");
        //    }
        //    catch (Exception ex) {
        //        Logger.Warn($"Could not load XML markers for map {currMapId}: {ex.Message}");
        //    }
        //    finally {
        //        _isLoadingMapIds = false;
        //    }
        //}
        //// Use local .txt to load map Ids instead of the API that loads a bunch of irrelevant maps (like instances for example)
        //private async Task<List<int>> GetLocalMapIds() {
        //    string path = "markersMapIDs.txt"; // Path to your .txt file in 'ref' folder
        //    List<int> mapIds = new List<int>();
        //    try {
        //        // Use Task.Run to offload synchronous file I/O
        //        return await Task.Run(() => {
        //            using (Stream stream = ContentsManager.GetFileStream(path))
        //            using (StreamReader reader = new StreamReader(stream)) {

        //                string line;
        //                while ((line = reader.ReadLine()) != null) {
        //                    if (int.TryParse(line.Trim(), out int id) && id > 0)
        //                        mapIds.Add(id);
        //                }
        //                return mapIds;
        //            }
        //        });
        //    }
        //    catch (Exception ex) {
        //        Logger.Warn($"Failed to read {path}. Error: {ex.Message}");
        //    }
        //    return mapIds;
        //}


        //// Function to load all maps from other modules
        //public async Task GenerateAllMapMarkerFilesAsync() {
        //    await LoadMarkersForAllMaps();
        //}

        protected override void OnModuleLoaded(EventArgs e) {

            // Base handler must be called
            base.OnModuleLoaded(e);
                       
            // Create a corner icon for opening the tracker
            _cornerIcon = new CornerIcon() {
                Icon = ContentsManager.GetTexture("UI/emblem156035.png"),  //AsyncTexture2D.FromAssetId(156035), // Optional: replace with null or actual icon
                BasicTooltipText = "Open Character Map Tracker"
            };

            _cornerIcon.Click += delegate {
                _trackerWindow?.ToggleWindow();

            };

            _cornerIcon.RightMouseButtonPressed += delegate {
                _mainWindow?.ToggleWindow();
            };
        }

        protected override void Update(GameTime gameTime) {
            // Make sure other processes withous issues
            if (_isLoading) return;
            if (_isLoadingMapIds) return;
            if (_isLoadingMapData) return;

            int currentMapId = GameService.Gw2Mumble.CurrentMap.Id;
            //int currentFloor = GameService.Gw2Mumble.CurrentMap.;

            if (_lastMapId != currentMapId && currentMapId != 0) {
                _lastMapId = currentMapId;

                _isLoading = true;

                // Loads in correct sequence
                Task.Run(async () => {
                    await LoadMapDataAsync(currentMapId);
                    await LoadMarkersForCurrentMap(currentMapId);
                    _isLoading = false;

                    // Invoke the Progress Saved event to update the MainWindow UI after loading the current map
                    MapProgressSaved?.Invoke();
                });

                
            }
            TrackPlayerPosition();                          // This method will handle objective proximity tracking
        }

        private void TrackPlayerPosition() {
            if (_currentMapDetails == null) return;
            
            // Use map-based UI coordinates, not 3D world position
            var playerCoordRaw = GameService.Gw2Mumble.UI.MapPosition;                                      // returns continent coords of player
            var avatarPos = GameService.Gw2Mumble.PlayerCharacter.Position;                                 // returns Avatar Position of player 
            var playerCoord = new Vector2((float)playerCoordRaw.X, (float)playerCoordRaw.Y);                // Casts coords to float to match coords obtained in game API

            _trackerWindow?.UpdatePOIDisplay(avatarPos, playerCoord, _currentMapInfo, null, null, null, null, null, null, null, null, null, null, -1, -1, -1, -1, -1);                              // Keeps updated the position of the player

            // Initialize nearest POIs and markers
            PointOfInterest nearestPOI = null;
            PointOfInterest nearestWp = null;
            PointOfInterest nearestVista = null;
            SkillChallenge nearestHP = null;
            TaskInfo nearestHeart = null;
            Marker nearestPoiXML = null;
            Marker nearestWpXML = null;
            Marker nearestVistaXML = null;
            Marker nearestHpXML = null;
            Marker nearestHeartXML = null;


            // Live distances
            float distPOI = 999f;
            float distWp = 999f;
            float distVista = 999f;
            float distHp = 999f;
            float distHeart = 999f;

            // Track landmarks by player coords
            foreach (var poi in _landmarks) {
                var poiCoord = new Vector2(poi.coord[0], poi.coord[1]);
                float distTemp = Vector2.Distance(poiCoord, playerCoord);
                if (distTemp < 80f) {
                    nearestPOI = poi;   // Update nearest landmark with API

                    if (_currentXmllandmarks != null) {
                        foreach (var landmarkXML in _currentXmllandmarks) {
                            var landmarkXMLPos = new Vector3(landmarkXML.X, landmarkXML.Y, landmarkXML.Z);
                            distPOI = Vector3.Distance(avatarPos, landmarkXMLPos);

                            if (distPOI < 50f) {
                                nearestPoiXML = landmarkXML;   // Update nearest POI from .xml (more relevance due to Z position)
                                break;
                            }
                        }
                    }
                    break;
                }
            }

            // Track waypoints by player coords
            foreach (var waypoint in _waypoints) {
                var wpCoord = new Vector2(waypoint.coord[0], waypoint.coord[1]);
                float distTemp = Vector2.Distance(wpCoord, playerCoord);
                if (distTemp < 80f) {
                    nearestWp = waypoint;   // Update nearest waypoint with API

                    if (_currentXmlwaypoints != null) {
                        foreach (var waypointXML in _currentXmlwaypoints) {
                            var landmarkXMLPos = new Vector3(waypointXML.X, waypointXML.Y, waypointXML.Z);
                            distWp = Vector3.Distance(avatarPos, landmarkXMLPos);

                            if (distWp < 50f) {
                                nearestWpXML = waypointXML;   // Update nearest POI from .xml (more relevance due to Z position)
                                break;
                            }
                        }
                    }

                    break;
                }
            }
            // Track vistas by player coords
            foreach (var vista in _vistas) {
                var vistaCoord = new Vector2(vista.coord[0], vista.coord[1]);
                float distTemp = Vector2.Distance(vistaCoord, playerCoord);
                if (distTemp < 80f) {
                    nearestVista = vista;   // Update nearest vista with API

                    if (_currentXmlvistas != null) {
                        foreach (var vistaXML in _currentXmlvistas) {
                            var landmarkXMLPos = new Vector3(vistaXML.X, vistaXML.Y, vistaXML.Z);
                            distVista = Vector3.Distance(avatarPos, landmarkXMLPos);

                            if (distVista < 50f) {
                                nearestVistaXML = vistaXML;   // Update nearest POI from .xml (more relevance due to Z position)
                                break;
                            }
                        }
                    }

                    break;
                }
            }

            // Track hearts by boundaries and check if player inside boundary
            foreach (var heart in _hearts) {
                if (IsPointInPolygon(playerCoord, heart.bounds.Select(b => new Vector2(b[0], b[1])).ToList())) {
                    nearestHeart = heart;

                    if (_currentXmlhearts != null) {
                        foreach (var heartXML in _currentXmlhearts) {
                            var heartXMLPos = new Vector3(heartXML.X, heartXML.Y, heartXML.Z);
                            distHeart = Vector3.Distance(avatarPos, heartXMLPos);

                            if (distHeart < 600f) {
                                nearestHeartXML = heartXML;   // Update nearest POI from .xml (more relevance due to Z position)
                                break;
                            }
                        }
                    }
                    break;
                }
            }

            // Track hero points by player coords
            foreach (var heroP in _heropoints) {
                var heroCoord = new Vector2(heroP.coord[0], heroP.coord[1]);
                float distTemp = Vector2.Distance(heroCoord, playerCoord);
                if (distTemp < 80f) {
                    nearestHP = heroP;   // Update nearest hero point with .xml

                    if (_currentXmlheropoints != null) {
                        foreach (var heroPointXML in _currentXmlheropoints) {
                            var hpXMLPos = new Vector3(heroPointXML.X, heroPointXML.Y, heroPointXML.Z);
                            distHp = Vector3.Distance(avatarPos, hpXMLPos);

                            if (distHp < 50f) {
                                nearestHpXML = heroPointXML;   // Update nearest POI from .xml (more relevance due to Z position)
                                break;
                            }
                        }
                    }

                    break;
                }
            }


            _trackerWindow?.UpdatePOIDisplay(avatarPos, playerCoord, _currentMapInfo, nearestHeart, nearestHeartXML, nearestPOI, nearestPoiXML, 
                nearestWp, nearestWpXML, nearestVista, nearestVistaXML, nearestHP, nearestHpXML, distPOI, distWp, distVista, distHp, distHeart);       // Update position of player with the position of the POIs, hearts and hero points

        }

        // Helper function to determine the boundary of a task (hearts)
        private bool IsPointInPolygon(Vector2 playerPoint, List<Vector2> polygon) {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++) {
                if (((polygon[i].Y > playerPoint.Y) != (polygon[j].Y > playerPoint.Y)) &&
                    (playerPoint.X < (polygon[j].X - polygon[i].X) * (playerPoint.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)) {
                    inside = !inside;
                }
            }
            return inside;
        }
        
        public void SaveMarkersToJson() {
            //Logger.Info($"Inside SaveMarkersToJson: Attempts to save for map ID: {_currentMapInfo.id}"); // For debugging

            string baseDir = ModuleParameters.DirectoriesManager.GetFullDirectoryPath("map-tracker");
            string jsonPath = Path.Combine(baseDir, $"Maps", $"{_currentCharacterName}", $"progress_map{_currentMapInfo.id}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));

            // If you have a list of MarkerWithApiData, save it directly
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(_markerWithApiData, _optionsJSON));
            Logger.Info($"Progress for map {_currentMapInfo.id} saved to {jsonPath}");

            // Invoke the Progress Saved event to update the MainWindow UI
            MapProgressSaved?.Invoke();
        }

        protected override void Unload()
        {
            // Unload here
            _trackerWindow?.Dispose();
            _mainWindow?.Dispose();
            _cornerIcon?.Dispose();

            // All static members must be manually unset
        }

    }

}
