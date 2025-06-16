//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Gw2Mumble;
using Blish_HUD.Modules.Managers;
using CharacterMapTracker;
using CharacterMapTracker.Models;
using CharacterMapTracker.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
//using System.Reflection.Emit;

namespace CharacterMapTracker.UI {
    public class MainWindow : TabbedWindow2 {

        private Panel _container;
        private Panel _leftPanel;
        private Panel _rightPanel;
        private Panel _rightTopPanel;
        private Panel _rightBottomPanel;

        private LoadingSpinner _loadingSpinner;
        private Label _label;
        //private List<int> _regionIds;
        //private List<RegionInfo> _regions;

        private bool _mapsLoading = false;
       

        public MainWindow() : base(
            AsyncTexture2D.FromAssetId(155985),
            new Rectangle(24, 30, 960, 700),            // Window Region (wider for both panels)
            new Rectangle(82, 30, 860, 660))            // Content Region
        {
            this.Title = "Map Completion Tracker";
            this.Subtitle = "Explore your progress";
            this.Location = new Point(500, 500);
            this.SavesPosition = true;
            this.Id = "002_MapTracker";

            this.Hide();


            // Building Layout of Window
            _container = new Panel {
                Size = this.ContentRegion.Size,
                Location = Point.Zero,
                Parent = this,
                CanScroll = false
            };

            _leftPanel = new Panel {
                Size = new Point(200, _container.Height),
                Location = new Point(0, 0),
                Parent = _container,
                CanScroll = true,
                BackgroundColor = new Color(20, 20, 20, 100)
            };

            _rightPanel = new Panel {
                Size = new Point(_container.Width - 200, _container.Height),
                Location = new Point(200, 0),
                Parent = _container,
                CanScroll = true,
                BackgroundColor = new Color(0, 0, 0, 80)
            };

            _rightTopPanel = new Panel {
                Parent = _rightPanel,
                Size = new Point(_rightPanel.Width, _rightPanel.Height / 4),
                Location = new Point(0, 0),
                CanScroll = true,
                BackgroundColor = new Color(0, 0, 0, 80)
            };

            _rightBottomPanel = new Panel {
                Parent = _rightPanel,
                Size = new Point(_rightPanel.Width, _rightPanel.Height / 4 * 3),
                Location = new Point(0, _rightTopPanel.Height),
                CanScroll = true,
                BackgroundColor = new Color(0, 0, 0, 80)
            };

            // Each time a POI is saved when exploring the panels reload Window (also with the first load up of a map)
            CharacterMapTrackerModule.Instance.MapProgressSaved += OnMapProgressSaved;

            // Initial load
            Task.Run(async () => {
                await BuildExpansionSections();
            });
        }
        private async void OnMapProgressSaved() {
            // Optionally show a loading spinner here
            _leftPanel.ClearChildren();
            _rightPanel.ClearChildren();
            await BuildExpansionSections();
        }

        private async Task BuildExpansionSections()
        {
            // Dummy collapsible sections for now — will be dynamic

            var loadMapsButton = new StandardButton
            {
                Text = "Load all Maps",
                Size = new Point(90, 40),
                Location = new Point(_leftPanel.Width/2 - 40, 10),
                Parent = _leftPanel
            };


            loadMapsButton.Click += async (s, e) =>
            {
                LoadAllMaps();
                try {
                    await AllMapLoaders.Instance.GenerateAllMapMarkerFilesAsync();
                }
                catch (System.Exception ex) {
                    CharacterMapTrackerModule.Logger.Warn($"error loading all markers: {ex.Message}");
                    throw;
                }
                

                _mapsLoading = false;
                _loadingSpinner.Visible = false;
                _label.Visible = false;

                // Clear panels and rebuild expansions/regions/maps
                _leftPanel.ClearChildren();
                _rightPanel.ClearChildren();
                await BuildExpansionSections();

            };

 
            
            //CharacterMapTrackerModule.Logger.Warn($"Before catching the localMapIds");
            var mapIds = await AllMapLoaders.Instance.GetLocalMapIds();
            var mapsDict = await AllMapLoaders.Instance.LoadMarkersFromJSON();

            // Group maps by expansion and then by region
            var expansionRegionMap = new Dictionary<string, Dictionary<string, List<int>>>();
            foreach (var id in mapIds){
                if (!mapsDict.ContainsKey(id) || mapsDict[id].Count == 0) continue;
                var marker = mapsDict[id][0].Marker;
                var expansion = marker.expansion ?? "Unknown";
                var region = marker.region_name ?? "Unknown Region";

                if (!expansionRegionMap.ContainsKey(expansion))
                    expansionRegionMap[expansion] = new Dictionary<string, List<int>>();
                if (!expansionRegionMap[expansion].ContainsKey(region))
                    expansionRegionMap[expansion][region] = new List<int>();
                expansionRegionMap[expansion][region].Add(id);
            }

            // Create menuitems for each expansion in left panel and start calling the maps per region in right panel
            var menu = new Menu {
                Parent = _leftPanel,
                Location = new Point(0, 60),
                Size = new Point(_leftPanel.Width, _leftPanel.Height - 60),
                CanSelect = true
            };

            var expansionMenuItems = new List<MenuItem>();

            foreach (var expKvp in expansionRegionMap) {
                string expansion = expKvp.Key;
                var expMenuItem = menu.AddMenuItem(expansion);
                expansionMenuItems.Add(expMenuItem);

                foreach (var regKvp in expKvp.Value) {
                    string region = regKvp.Key;
                    var regMenuItem = new MenuItem {
                        Text = "    " + region,
                        Parent = expMenuItem                        
                    };
                    
                    expMenuItem.Children.Add(regMenuItem);

                    var mapIdsForRegion = regKvp.Value.ToList();
                    regMenuItem.Click += (s, e) => ShowMapsForRegion(region, mapIdsForRegion, mapsDict);
                }

                // Only one expansion expanded at a time
                expMenuItem.Click += (s, e) => {
                    if (!expMenuItem.Collapsed) {
                        foreach (var other in expansionMenuItems) {
                            if (other != expMenuItem) other.Collapsed = true;
                        }
                    }
                };
            }
        }

        private void LoadAllMaps() {
            _rightPanel.ClearChildren();
            _mapsLoading = true;

            _label = new Label {
                Text = $"Loading, this may take few minutes ...",
                Location = new Point((_rightPanel.Width / 2)-60, (_rightPanel.Height / 2)+33),
                Parent = _rightPanel,
                AutoSizeWidth = true,
                Visible = false
            };

            _loadingSpinner = new LoadingSpinner {
                Size = new Point(64, 64),
                Location = new Point((_rightPanel.Width / 2) - 32, (_rightPanel.Height / 2) - 32),
                Visible = false,
                Parent = _rightPanel

            };

            //CharacterMapTrackerModule.Logger.Info($"During the LoadAllMaps function: {_mapsLoading}");
            if (_mapsLoading) {
                //CharacterMapTrackerModule.Logger.Info($"Just pressed the loading button");
                _label.Visible = true;
                _loadingSpinner.Visible = true;
            }
        }

        private void ShowMapsForRegion(string region, List<int> mapIds, Dictionary<int, List<MarkerWithApiData>> mapsDict)
        {
            _rightPanel.ClearChildren();

            //private Panel _rightTopPanel;
            //private Panel _rightBottomPanel;

            _rightTopPanel = new Panel {
                Parent = _rightPanel,
                Size = new Point(_rightPanel.Width, _rightPanel.Height/4),
                Location = new Point(0, 0),
                CanScroll = true,
                BackgroundColor = new Color(0, 0, 0, 80)
            };

            _rightBottomPanel = new Panel {
                Parent = _rightPanel,
                Size = new Point(_rightPanel.Width, _rightPanel.Height / 4*3),
                Location = new Point(0, _rightTopPanel.Height),
                CanScroll = true,
                BackgroundColor = new Color(0, 0, 0, 80)
            };

            //  --------------Top Panel--------------
            //var markersRegion = mapsDict.Keys.ToList();

            int wpRegionTotal = 0;
            int wpRegionFound = 0;
            int lmRegionTotal = 0;
            int lmRegionFound = 0;
            int vistaRegionTotal = 0;
            int vistaRegionFound = 0;
            int hpRegionTotal = 0;
            int hpRegionFound = 0;
            int heartRegionTotal = 0;
            int heartRegionFound = 0;

            foreach (var id in mapIds) {
                var markers = mapsDict[id];
                var currMap = markers.Find(m => m.Marker.MapId == id);

                wpRegionFound += markers.Count(m => m.Marker.Category == "waypoint" && m.Marker.found);
                wpRegionTotal += markers.Count(m => m.Marker.Category == "waypoint");
                lmRegionFound += markers.Count(m => m.Marker.Category == "landmark" && m.Marker.found);
                lmRegionTotal += markers.Count(m => m.Marker.Category == "landmark");
                vistaRegionFound += markers.Count(m => m.Marker.Category == "vista" && m.Marker.found);
                vistaRegionTotal += markers.Count(m => m.Marker.Category == "vista");
                hpRegionFound += markers.Count(m => m.Marker.Category == "heropoint" && m.Marker.found);
                hpRegionTotal += markers.Count(m => m.Marker.Category == "heropoint");
                heartRegionFound += markers.Count(m => m.Marker.Category == "task" && m.Marker.found);
                heartRegionTotal += markers.Count(m => m.Marker.Category == "task");

                
            }

            int totalRegionMarkers = wpRegionTotal + lmRegionTotal + vistaRegionTotal + hpRegionTotal + heartRegionTotal;
            int totalRegionFound = wpRegionFound + lmRegionFound + vistaRegionFound + hpRegionFound + heartRegionFound;

            float completionRegion = totalRegionMarkers > 0 ? (float)totalRegionFound / totalRegionMarkers : 0f;

            // Map name
            var regionNameLabel = new Label {
                Text = region,
                Font = GameService.Content.DefaultFont16,
                Location = new Point(10, 10),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = _rightTopPanel
            };

            //// Completion bar
            //var completionBar = new ProgressBar {
            //    Location = new Point(10, 40),
            //    Size = new Point(_rightTopPanel.Width - 120, 24),
            //    Value = completion,
            //    Parent = _rightTopPanel
            //};
            var completionLabel = new Label {
                Text = $"Completion: {(completionRegion * 100):F1}%",
                Location = new Point(20, 42),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = _rightTopPanel
            };

            // Icons and found/total for each category
            int iconY = 80;
            int iconX = 10;
            int iconSpacing = 90;

            void AddIcon(string iconName, int found, int total) {
                var icon = new Image {
                    Texture = CharacterMapTrackerModule.Instance.ContentsManager.GetTexture(iconName),
                    Location = new Point(iconX, iconY),
                    Size = new Point(32, 32),
                    Parent = _rightTopPanel
                };
                var label = new Label {
                    Text = $" {found}/{total}",
                    Location = new Point(iconX + 36, iconY + 8),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Parent = _rightTopPanel
                };
                iconX += iconSpacing;
            }

            AddIcon("icons/filledWaypoint.png", wpRegionFound, wpRegionTotal);
            AddIcon("icons/filledLandmark.png", lmRegionFound, lmRegionTotal);
            AddIcon("icons/filledVista.png", vistaRegionFound, vistaRegionTotal);
            AddIcon("icons/completeHeroPoint.png", hpRegionFound, hpRegionTotal);
            AddIcon("icons/filledHeart.png", heartRegionFound, heartRegionTotal);

            // --------------Bottom Panel--------------

            int yLoc = 10;
            foreach (var id in mapIds){
                var markers = mapsDict[id];
                var currMap = markers.Find(m => m.Marker.MapId == id);
                var mapName = currMap.Marker.MapName;

                int wpFound = markers.Count(m => m.Marker.Category == "waypoint" && m.Marker.found);
                int wpTotal = markers.Count(m => m.Marker.Category == "waypoint");
                int lmFound = markers.Count(m => m.Marker.Category == "landmark" && m.Marker.found);
                int lmTotal = markers.Count(m => m.Marker.Category == "landmark");
                int vistaFound = markers.Count(m => m.Marker.Category == "vista" && m.Marker.found);
                int vistaTotal = markers.Count(m => m.Marker.Category == "vista");
                int hpFound = markers.Count(m => m.Marker.Category == "heropoint" && m.Marker.found);
                int hpTotal = markers.Count(m => m.Marker.Category == "heropoint");
                int heartFound = markers.Count(m => m.Marker.Category == "task" && m.Marker.found);
                int heartTotal = markers.Count(m => m.Marker.Category == "task");

                int totalPois = wpTotal + lmTotal + vistaTotal + hpTotal + heartTotal;
                int currentPois = wpFound + lmFound + vistaFound + hpFound + heartFound;

                // Detail button for the current map
                var currMapDetailButton = new DetailsButton {
                    Parent = _rightBottomPanel,
                    Location = new Point(10, yLoc),
                    Size = new Point((_rightPanel.Width - 40) - 20, 80),
                    Text = mapName,
                    MaxFill = totalPois,
                    CurrentFill = currentPois,
                    ShowFillFraction = true,
                    //ShowToggleButton = true,
                    FillColor = Color.LightBlue,
                    CanCollapse = true,
                    BasicTooltipText = $"Waypoints: {wpFound}/{wpTotal} | Landmarks: {lmFound}/{lmTotal} | Vistas: {vistaFound}/{vistaTotal} | Hero Points: {hpFound}/{hpTotal} | Hearts: {heartFound}/{heartTotal}"
                };

                // Icons of POIs and current progress of each per map

                // Waypoints
                var mapWpsIcon = new Image {
                    Texture = CharacterMapTrackerModule.Instance.ContentsManager.GetTexture("icons/filledWaypoint.png"),
                    Location = new Point((currMapDetailButton.Width - 40) / 5, 10),
                    Size = new Point(32, 32),
                    Parent = currMapDetailButton
                };
                var mapWpsText = new Label {
                    Text = $" {wpFound}/{wpTotal}",
                    Location = new Point((currMapDetailButton.Width - 40) / 5 + 5, 45),
                    Parent = currMapDetailButton,

                };

                // Landmarks (Points of Interests)
                var mapLandmarksIcon = new Image {
                    Texture = CharacterMapTrackerModule.Instance.ContentsManager.GetTexture("icons/filledLandmark.png"),
                    Location = new Point((currMapDetailButton.Width - 40) / 5 * 2, 10),
                    Size = new Point(32,32),
                    Parent = currMapDetailButton
                };
                var mapLandmarksText = new Label {
                    Text = $" {lmFound}/{lmTotal}",
                    Location = new Point((currMapDetailButton.Width - 40) / 5 * 2 + 5, 45),
                    Parent = currMapDetailButton,

                };

                // Vistas
                var mapVistasIcon = new Image {
                    Texture = CharacterMapTrackerModule.Instance.ContentsManager.GetTexture("icons/filledVista.png"),
                    Location = new Point((currMapDetailButton.Width - 40) / 5 * 3, 10),
                    Size = new Point(32, 32),
                    Parent = currMapDetailButton
                };
                var mapVistasText = new Label {
                    Text = $" {vistaFound}/{vistaTotal}",
                    Location = new Point((currMapDetailButton.Width - 40) / 5 * 3 + 5, 45),
                    Parent = currMapDetailButton,

                };

                // Hero Points
                var mapHpIcon = new Image {
                    Texture = CharacterMapTrackerModule.Instance.ContentsManager.GetTexture("icons/completeHeroPoint.png"),
                    Location = new Point((currMapDetailButton.Width - 40) / 5 * 4, 10),
                    Size = new Point(32, 32),
                    Parent = currMapDetailButton
                };
                var mapHpText = new Label {
                    Text = $" {hpFound}/{hpTotal}",
                    Location = new Point((currMapDetailButton.Width - 40) / 5 * 4 + 5, 45),
                    Parent = currMapDetailButton,

                };

                // Hearts (tasks)
                var mapHeartIcon = new Image {
                    Texture = CharacterMapTrackerModule.Instance.ContentsManager.GetTexture("icons/filledHeart.png"),
                    Location = new Point((currMapDetailButton.Width - 40), 10),
                    Size = new Point(32, 32),
                    Parent = currMapDetailButton
                };
                var mapHeartText = new Label {
                    Text = $" {heartFound}/{heartTotal}",
                    Location = new Point((currMapDetailButton.Width - 40) + 5, 45),
                    Parent = currMapDetailButton,

                };

                // Optionally, on click, show a more detailed panel for this map
                currMapDetailButton.Click += (s, e) => ShowMapDetails(id, markers);

                yLoc += 85;
            }
        }

        private void ShowMapDetails(int mapId, List<MarkerWithApiData> markers) {
            _rightTopPanel.ClearChildren();
            _rightBottomPanel.ClearChildren();

            var mapName = markers.FirstOrDefault()?.Marker.MapName ?? $"Map {mapId}";

            // Count found/total for each category
            int wpFound = markers.Count(m => m.Marker.Category == "waypoint" && m.Marker.found);
            int wpTotal = markers.Count(m => m.Marker.Category == "waypoint");
            int lmFound = markers.Count(m => m.Marker.Category == "landmark" && m.Marker.found);
            int lmTotal = markers.Count(m => m.Marker.Category == "landmark");
            int vistaFound = markers.Count(m => m.Marker.Category == "vista" && m.Marker.found);
            int vistaTotal = markers.Count(m => m.Marker.Category == "vista");
            int hpFound = markers.Count(m => m.Marker.Category == "heropoint" && m.Marker.found);
            int hpTotal = markers.Count(m => m.Marker.Category == "heropoint");
            int heartFound = markers.Count(m => m.Marker.Category == "task" && m.Marker.found);
            int heartTotal = markers.Count(m => m.Marker.Category == "task");

            int totalFound = wpFound + lmFound + vistaFound + hpFound + heartFound;
            int totalMarkers = wpTotal + lmTotal + vistaTotal + hpTotal + heartTotal;
            float completion = totalMarkers > 0 ? (float)totalFound / totalMarkers : 0f;

            // Map name
            var mapNameLabel = new Label {
                Text = mapName,
                Font = GameService.Content.DefaultFont16,
                Location = new Point(10, 10),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = _rightTopPanel
            };

            //// Completion bar
            //var completionBar = new ProgressBar {
            //    Location = new Point(10, 40),
            //    Size = new Point(_rightTopPanel.Width - 120, 24),
            //    Value = completion,
            //    Parent = _rightTopPanel
            //};
            var completionLabel = new Label {
                Text = $"Completion: {(completion * 100):F1}%",
                Location = new Point(20, 42),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = _rightTopPanel
            };

            // Save Markers button
            var saveButton = new StandardButton {
                Text = "Save Markers",
                Size = new Point(100, 32),
                Location = new Point(_rightTopPanel.Width - 110, 10),
                Parent = _rightTopPanel
            };
            saveButton.Click += (s, e) => {
                //CharacterMapTrackerModule.Instance.SaveMarkersToJson();
            };

            // Icons and found/total for each category
            int iconY = 80;
            int iconX = 10;
            int iconSpacing = 90;

            void AddIcon(string iconName, int found, int total) {
                var icon = new Image {
                    Texture = CharacterMapTrackerModule.Instance.ContentsManager.GetTexture(iconName),
                    Location = new Point(iconX, iconY),
                    Size = new Point(32, 32),
                    Parent = _rightTopPanel
                };
                var label = new Label {
                    Text = $" {found}/{total}",
                    Location = new Point(iconX + 36, iconY + 8),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Parent = _rightTopPanel
                };
                iconX += iconSpacing;
            }

            AddIcon("icons/filledWaypoint.png", wpFound, wpTotal);
            AddIcon("icons/filledLandmark.png", lmFound, lmTotal);
            AddIcon("icons/filledVista.png", vistaFound, vistaTotal);
            AddIcon("icons/completeHeroPoint.png", hpFound, hpTotal);
            AddIcon("icons/filledHeart.png", heartFound, heartTotal);

            // --- POI FlowPanels in _rightBottomPanel ---

            var categories = new[] {
                new { Name = "Waypoints", Icon = "icons/filledWaypoint.png", Cat = "waypoint", Found = wpFound, Total = wpTotal },
                new { Name = "Landmarks", Icon = "icons/filledLandmark.png", Cat = "landmark", Found = lmFound, Total = lmTotal },
                new { Name = "Vistas", Icon = "icons/filledVista.png", Cat = "vista", Found = vistaFound, Total = vistaTotal },
                new { Name = "Hero Points", Icon = "icons/completeHeroPoint.png", Cat = "heropoint", Found = hpFound, Total = hpTotal },
                new { Name = "Hearts", Icon = "icons/filledHeart.png", Cat = "task", Found = heartFound, Total = heartTotal }
            };

            var outerPanels = new List<Panel>();

            int panelY = 10;
            foreach (var cat in categories) {
                var outerPanel = new Panel {
                    Parent = _rightBottomPanel,
                    Icon = CharacterMapTrackerModule.Instance.ContentsManager.GetTexture(cat.Icon),
                    Title = $" {cat.Name}: {cat.Found}/{cat.Total}",
                    Location = new Point(10, panelY),
                    Size = new Point(_rightBottomPanel.Width - 20, 60), // header height
                    ShowBorder = true,
                    CanCollapse = true,
                    CanScroll = false
                };
                outerPanels.Add(outerPanel);

                //var icon = new Image {
                //    Texture = CharacterMapTrackerModule.Instance.ContentsManager.GetTexture(cat.Icon),
                //    Size = new Point(32, 32),
                //    Location = new Point(5, 4),
                //    Parent = outerPanel
                //};
                //var title = new Label {
                //    Text = $" {cat.Name}: {cat.Found}/{cat.Total}",
                //    Font = GameService.Content.DefaultFont14,
                //    Location = new Point(45, 10),
                //    AutoSizeWidth = true,
                //    AutoSizeHeight = true,
                //    Parent = outerPanel
                //};


                var contentPanel = new FlowPanel {
                    Parent = outerPanel,
                    Location = new Point(20, 20),
                    Size = new Point(outerPanel.Width / 2, 0), // Start collapsed
                    FlowDirection = ControlFlowDirection.SingleTopToBottom,
                    ControlPadding = new Vector2(0, 5)
                };

                var contentPanelRight = new FlowPanel {
                    Parent = outerPanel,
                    Location = new Point(outerPanel.Width / 4 * 3, 20),
                    Size = new Point(outerPanel.Width / 4, 0), // Start collapsed
                    FlowDirection = ControlFlowDirection.SingleTopToBottom,
                    ControlPadding = new Vector2(0, 5)
                };

                int yWithCheckBox = 0;
                foreach (var m in markers.Where(m => m.Marker.Category == cat.Cat)) {
                    string textCheckBox = "";
                    if (cat.Cat == "landmark" || cat.Cat == "waypoint") {
                        textCheckBox = $"{m.Marker.Number?.ToString():F2}: {m.ApiPoi?.name ?? "Not discovered"}";
                        if (m.ApiPoi != null) {
                            textCheckBox += $"          X: {m.ApiPoi?.coord[0]} Y: {m.ApiPoi?.coord[1]}";
                        }
                    }else if (cat.Cat == "vista") {
                        textCheckBox = $"{m.Marker.Number?.ToString():F2}: Vista ({m.ApiPoi?.id.ToString() ?? "Not discovered"})";
                    }
                    else if (cat.Cat == "heropoint") {
                        textCheckBox = $"{m.Marker.Number?.ToString():F2}: Hero Point ({m.ApiHeroPoint?.id ?? "Not discovered"})";
                    }
                    else if (cat.Cat == "task") {
                        textCheckBox = $"{m.Marker.Number?.ToString():F2}: {m.ApiHeart?.objective ?? "Not discovered"}";
                    }

                    var cb = new Checkbox {
                        Checked = m.Marker.found,
                        Text = textCheckBox ?? "",
                        Parent = contentPanel,
                        BasicTooltipText = $"Coords X: {m.Marker.X.ToString():F2} Y: {m.Marker.Y.ToString():F2} Z: {m.Marker.Z.ToString():F2}"
                    };
                    cb.CheckedChanged += (s, e) => {
                        m.Marker.found = cb.Checked;
                    };

                    var cbMap = new Checkbox {
                        //Checked = m.Marker.found,
                        Text = "Show on Map",
                        Parent = contentPanelRight,
                        BasicTooltipText = $"Shows a light blue circle around the objective in the world map"
                    };
                    cbMap.CheckedChanged += (s, e) => {
                        //m.Marker.found = cb.Checked;
                    };

                    contentPanel.Size = new Point(outerPanel.Width / 2, yWithCheckBox);
                    contentPanelRight.Size = new Point(outerPanel.Width / 4, yWithCheckBox);
                    yWithCheckBox += 27;
                }
                // Initial layout
                UpdatePanelLayout();

                // Collapsed by default
                outerPanel.Collapsed = true;

                // Update the layout for second time after collapsing (to avoid visual bug)
                UpdatePanelLayout();



                // Only one POI expanded at a time
                outerPanel.Click += (s, e) => {
                    //if (!outerPanel.Collapsed) {
                    foreach (var other in outerPanels) {
                        if (other != outerPanel) {
                            other.Collapsed = true;
                        }
                    }
                    //}
                    UpdatePanelLayout();
                    UpdatePanelLayout();
                };


                // Helper to update panel heights and re-layout
                void UpdatePanelLayout() {
                    int contentHeight = outerPanel.Collapsed ? 0 : yWithCheckBox;
                    outerPanel.Size = new Point(outerPanel.Width, 45 + contentHeight);

                    // Re-layout all panels
                    int y = 10;
                    foreach (var p in outerPanels) {
                        p.Location = new Point(10, y);
                        y += p.Height + 10;
                    }
                }
            }


        }

    }
}
