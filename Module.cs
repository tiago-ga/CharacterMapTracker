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

        public List<MarkerWithApiData> _markerWithApiData = new List<MarkerWithApiData>();
        public MapDetails _currentMapDetails;

        private TrackerWindow _trackerWindow;
        private MainWindow _mainWindow;
        private CornerIcon _cornerIcon;
        private MapInfo _currentMapInfo;
        private bool _isLoadingMapData = false;
        private int _lastMapId = -1;
        private List<Marker> _currentXmlMarkers;
        private bool _isLoading = false;

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

                Logger.Info($"Loaded map: {mapDetails.name}, Landmarks: {_landmarks?.Count ?? 0}, vistas: {_vistas?.Count ?? 0}, Waypoints:{_waypoints?.Count ?? 0}, " +
                    $"Tasks: {mapDetails.tasks?.Count ?? 0}, SkillChallenges: {mapDetails.skill_challenges?.Count ?? 0}");
            }
            catch (Exception ex) {
                Logger.Error(ex, "Error loading map details.");
            }
            finally {
                _isLoadingMapData = false;
            }
        }

        // Load markers from .xml
        protected async Task LoadMarkersForCurrentMap(int mapId) {
            if (_currentMapDetails == null) {
                Logger.Info($"Waiting for {mapId} to load first");
                return;
            }
            try {
                var baseDir = ModuleParameters.DirectoriesManager.GetFullDirectoryPath("map-tracker");
                var jsonPath = Path.Combine(baseDir, "Maps", $"progress_map{mapId}.json");
                List<Marker> markers;
                if (!File.Exists(jsonPath)) {
                    // Parse XML and create JSON with found = false (only happens for maps loaded for the first time)
                    string xmlPath = $"markers/map{mapId}.xml";
                    using (Stream stream = await Task.Run(() => ContentsManager.GetFileStream(xmlPath))) {
                        markers = PathingMarkerLoader.LoadMarkersFromXml(stream, mapId, _currentMapDetails.name);
                        foreach (var m in markers) m.found = false;
                    }

                    // Populate MarkerWithApiData with only Marker field set, API fields null
                    _markerWithApiData = markers.Select(m => new MarkerWithApiData {
                        Marker = m,
                        ApiPoi = null,
                        ApiHeart = null,
                        ApiHeroPoint = null
                    }).ToList();

                    // Serialize to JSON and save
                    File.WriteAllText(jsonPath, JsonSerializer.Serialize(_markerWithApiData, _optionsJSON));
                    Logger.Info($"Succesfully created JSON file at: {jsonPath}");
                }
                else {
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
            catch (Exception ex) {
                Logger.Warn($"Could not load XML markers for map {mapId}: {ex.Message}");
                _currentXmlMarkers = new List<Marker>(); // fallback to empty
                _markerWithApiData = new List<MarkerWithApiData>();
            }

        }

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
            if (_isLoading) return;
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

                            if (distHeart < 100f) {
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
            Logger.Info($"Inside SaveMarkersToJson: Attempts to save for map ID: {_currentMapInfo.id}"); // ELIMINATE THIS

            string baseDir = ModuleParameters.DirectoriesManager.GetFullDirectoryPath("map-tracker");
            string jsonPath = Path.Combine(baseDir, "Maps", $"progress_map{_currentMapInfo.id}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));

            // If you have a list of MarkerWithApiData, save it directly
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(_markerWithApiData, _optionsJSON));
            Logger.Info($"Progress for map {_currentMapInfo.id} saved to {jsonPath}");
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
