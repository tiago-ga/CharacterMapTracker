//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection.Emit;
//using System.Text;
//using System.Threading.Tasks;

using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CharacterMapTracker;
using CharacterMapTracker.Models;
using CharacterMapTracker.Services;
using Blish_HUD.Content;
using Blish_HUD.Modules.Managers;
using System.Collections.Generic;
using System.Linq;

namespace CharacterMapTracker.UI {
    public class TrackerWindow : StandardWindow {

        private readonly ContentsManager _contents;

        public TrackerWindow(ContentsManager contents, Texture2D background): base(
            background, //AsyncTexture2D.FromAssetId(155985)
            new Rectangle(0, 0, background.Width, background.Height+15),
            new Rectangle(20, 25, background.Width - 10, background.Height))
        {
            this.Title = "Live Tracker";
            this.Subtitle = " ";
            this.Id = "001_MapTracker";
            this.Location = new Point(300,300);
            this.SavesPosition = true;
            
            _contents = contents;
            // Start hidden — will show when icon clicked
            this.Hide();
            this.BuildWindow();

        }

        private Label _avatarLabel;
        private Label _mapLabel;

        private int xPanelLoc, yPanelLoc;
        private readonly float minDist = 15f;
        private Panel _nearWaypointPanel;
        private Panel _nearVistaPanel;
        private Panel _nearPoiPanel;
        private Panel _nearHeroPointPanel;
        private Panel _nearHeartPanel;

        private Checkbox _checkBoxWp;
        private Checkbox _checkBoxPoi;
        private Checkbox _checkBoxVista;
        private Checkbox _checkBoxHp;
        private Checkbox _checkBoxHeart;

        private Marker _currentWaypointMarker;
        private Marker _currentPoiMarker;
        private Marker _currentVistaMarker;
        private Marker _currentHpMarker;
        private Marker _currentHeartMarker;

        private PointOfInterest _apiWp;
        private PointOfInterest _apiPoi;
        private PointOfInterest _apiVista;
        private TaskInfo _apiHeart;
        private SkillChallenge _apiHp;

        private void BuildWindow() {

            _mapLabel = new Label {
                Parent = this,
                Location = new Point(20, 10),
                AutoSizeWidth = true,
                AutoSizeHeight = true
            };

            _avatarLabel = new Label {
                Parent = this,
                Location = new Point(20, 30),
                AutoSizeWidth = true,
                AutoSizeHeight = true
            };

            // Waypoint Panel
            _nearWaypointPanel = new Panel() {
                Size = new Point(270, 80),
                Parent = this,
                Icon = _contents.GetTexture("icons/emptyWaypoint.png"),
                BackgroundColor = new Color(0, 0, 0, 0.5f)
            };
            _checkBoxWp = new Checkbox {
                Checked = false,
                Size = new Point(80, 25),
                Location = new Point(0, 10),
                Text = "It fills if <15 Units (can check it manually)",
                Parent = _nearWaypointPanel
            };
            _checkBoxWp.CheckedChanged += (s, e) => {
                if (_checkBoxWp.Checked) {
                    _nearWaypointPanel.Icon = _contents.GetTexture("icons/filledWaypoint.png");

                    _currentWaypointMarker.found = true;
                    _apiWp.found = true;
                    if (_currentWaypointMarker != null && _currentWaypointMarker.found) {
                        SetMarkerFound(_currentWaypointMarker);
                        _checkBoxWp.Checked = false; // Returns to empty state after saving
                    }
                }
                else {
                    _nearWaypointPanel.Icon = _contents.GetTexture("icons/emptyWaypoint.png");
                }
            };

            // POI Panel (landmark)
            _nearPoiPanel = new Panel() {
                Size = new Point(270, 80),
                Parent = this,
                Icon = _contents.GetTexture("icons/emptyLandmark.png"),
                BackgroundColor = new Color(0, 0, 0, 0.5f)
            };
            _checkBoxPoi = new Checkbox {
                Checked = false,
                Size = new Point(80, 25),
                Location = new Point(0, 10),
                Text = "It fills if <15 Units (can check it manually)",
                Parent = _nearPoiPanel
            };
            _checkBoxPoi.CheckedChanged += (s, e) => {
                if (_checkBoxPoi.Checked) {
                    _nearPoiPanel.Icon = _contents.GetTexture("icons/filledLandmark.png");

                    _currentPoiMarker.found = true;
                    _apiPoi.found = true;
                    if (_currentPoiMarker != null && _currentPoiMarker.found) {
                        SetMarkerFound(_currentPoiMarker);
                        _checkBoxPoi.Checked = false; // Returns to empty state after saving
                    }
                }
                else {
                    _nearPoiPanel.Icon = _contents.GetTexture("icons/emptyLandmark.png");
                }
            };

            // Vista Panel
            _nearVistaPanel = new Panel() {
                Size = new Point(270, 80),
                Parent = this,
                Icon = _contents.GetTexture("icons/emptyVista.png"),
                BackgroundColor = new Color(0, 0, 0, 0.5f)
            };
            _checkBoxVista = new Checkbox {
                Checked = false,
                Size = new Point(80, 25),
                Location = new Point(0, 10),
                Text = "It fills if <15 Units (can check it manually)",
                Parent = _nearVistaPanel
            };
            _checkBoxVista.CheckedChanged += (s, e) => {
                if (_checkBoxVista.Checked) {
                    _nearVistaPanel.Icon = _contents.GetTexture("icons/filledVista.png");

                    _currentVistaMarker.found = true;
                    _apiVista.found = true;
                    if (_currentVistaMarker != null && _currentVistaMarker.found) {
                        SetMarkerFound(_currentVistaMarker);
                        _checkBoxVista.Checked = false; // Returns to empty state after saving
                    }
                }
                else {
                    _nearVistaPanel.Icon = _contents.GetTexture("icons/emptyVista.png");
                }
            };

            // Hero Point Panel
            _nearHeroPointPanel = new Panel() {
                Size = new Point(270, 80),
                Parent = this,
                Icon = _contents.GetTexture("icons/incompleteHeroPoint.png"),
                BackgroundColor = new Color(0, 0, 0, 0.5f)
            };
            _checkBoxHp = new Checkbox {
                Checked = false,
                Size = new Point(80, 25),
                Location = new Point(0, 10),
                Text = "It fills if <15 Units (can check it manually)",
                Parent = _nearHeroPointPanel
            };
            _checkBoxHp.CheckedChanged += (s, e) => {
                if (_checkBoxHp.Checked) {
                    _nearHeroPointPanel.Icon = _contents.GetTexture("icons/completeHeroPoint.png");

                    _currentHpMarker.found = true;
                    _apiHp.found = true;
                    if (_currentHpMarker != null && _currentHpMarker.found) {
                        SetMarkerFound(_currentHpMarker);
                        _checkBoxHp.Checked = false; // Returns to empty state after saving
                    }
                }
                else {
                    _nearHeroPointPanel.Icon = _contents.GetTexture("icons/incompleteHeroPoint.png");
                }
            };

            // Heart Panel
            _nearHeartPanel = new Panel() {
                Size = new Point(270, 80),
                Parent = this,
                Icon = _contents.GetTexture("icons/emptyHeart.png"),
                BackgroundColor = new Color(0, 0, 0, 0.5f)
            };
            _checkBoxHeart = new Checkbox {
                Checked = false,
                Size = new Point(80, 25),
                Location = new Point(0, 10),
                Text = "Check if finished (manually)",
                Parent = _nearHeartPanel
            };
            _checkBoxHeart.CheckedChanged += (s, e) => {
                if (_checkBoxHeart.Checked) {
                    _nearHeartPanel.Icon = _contents.GetTexture("icons/filledHeart.png");

                    _currentHeartMarker.found = true;
                    _apiHeart.finished = true;
                    //CharacterMapTrackerModule.Logger.Info($"Attempting to save for: {_currentHeartMarker.Category}{_currentHeartMarker.Number} found?: {_currentHeartMarker.found}");
                    if (_currentHeartMarker!=null && _currentHeartMarker.found) {
                        SetMarkerFound(_currentHeartMarker);
                        _checkBoxHeart.Checked = false; // Returns to empty state after saving
                    } 
                }
                else {
                    _nearHeartPanel.Icon = _contents.GetTexture("icons/emptyHeart.png");
                }
            };

            
            // Wait for them to be called in update
            _nearWaypointPanel.Hide();
            _nearPoiPanel.Hide();
            _nearVistaPanel.Hide();
            _nearHeroPointPanel.Hide();
            _nearHeartPanel.Hide();

        }

        // When a checkbox is checked, update the found property in the main list and save

        private void SetMarkerFound(Marker marker) {
            var module = CharacterMapTrackerModule.Instance;
            if (module == null) return;

            CharacterMapTrackerModule.Logger.Info($"SetMarkerFound: Attempting to save for: {marker.Category}{marker.Number} with Guid: {marker.Guid}");
            // Find the MarkerWithApiData that wraps this marker (by unique property, e.g., Guid)
            var entry = module._markerWithApiData.FirstOrDefault(mwad => mwad.Marker.Guid == marker.Guid);
            if (entry != null) {
                entry.Marker.found = true;

                CharacterMapTrackerModule.Logger.Info($"SetMarkerFound: checking if entry not null for {marker.Category}{marker.Number}");
                if (marker.Category == "landmark") {
                    entry.ApiPoi = _apiPoi;
                }
                else if (marker.Category == "waypoint") {
                    entry.ApiPoi = _apiWp;
                }
                else if (marker.Category == "vista") {
                    entry.ApiPoi = _apiVista;
                }
                else if (marker.Category == "heropoint") {
                    entry.ApiHeroPoint = _apiHp;
                }
                else if (marker.Category == "task") {
                    entry.ApiHeart = _apiHeart;
                }
                CharacterMapTrackerModule.Logger.Info($"SetMarkerFound: Right before calling SaveMarkersToJson");
                module.SaveMarkersToJson();
            }
        }

        // Display position of POI and Avatar
        public void UpdatePOIDisplay(Vector3 avatarPos, Vector2 playerCoord, MapInfo mapInfo, TaskInfo nearestHeart, Marker nearestHeartXML,
            PointOfInterest nearestPoi, Marker nearestPoiXml, PointOfInterest nearestWp, Marker nearestWpXml, PointOfInterest nearestVista, 
            Marker nearestVistaXml, SkillChallenge nearestHp, Marker nearesthpXml, float distPoi, float distWp, float distVista, float distHp, float distHeart) {
            _mapLabel.Text = $"Current map: {mapInfo.name}";
            //_avatarLabel.Text = $"Avatar: X={avatarPos.X:F2} Y={avatarPos.Y:F2} Z={avatarPos.Z:F2}";
            _avatarLabel.Text = "";
            if (nearestHeartXML!=null) { 
                _avatarLabel.Text = $"{nearestHeartXML.Category}: Found?{nearestHeartXML.found}\nGUID:   {nearestHeartXML.Guid}";
            }
            

            xPanelLoc = 0;
            yPanelLoc = 60;

            _apiWp = nearestWp;
            _apiPoi = nearestPoi;
            _apiVista = nearestVista;
            _apiHeart = nearestHeart;
            _apiHp = nearestHp;
            
            // Loads Hero Point or Waypoint in panel 1 depending which is closer
            if (distPoi < distWp) {
                // Point of Interest
                _nearWaypointPanel.Hide();  // Hide Waypoint when showing POI
                if (nearestPoiXml != null && !nearestPoiXml.found) {
                    _currentPoiMarker = nearestPoiXml;
                    _nearPoiPanel.Title = $"{_apiPoi.type} At {distPoi:F1} Units";
                    _nearPoiPanel.Location = new Point(xPanelLoc, yPanelLoc);
                    _checkBoxPoi.BasicTooltipText = $"POI name: [{_apiPoi.name}]";

                    if (distPoi<minDist) _checkBoxPoi.Checked = true ;
                    
                    _nearPoiPanel.Show();
                }
                else {
                    _nearPoiPanel.Hide();
                }           
            }
            else {
                // Waypoint
                _nearPoiPanel.Hide();   // Hide Poi when showing Waypoint
                if (nearestWpXml != null && !nearestWpXml.found) {
                    _currentWaypointMarker = nearestWpXml;
                    _nearWaypointPanel.Title = $"{_apiWp.type} At {distWp:F1} Units";
                    _nearWaypointPanel.Location = new Point(xPanelLoc, yPanelLoc);
                    _checkBoxWp.BasicTooltipText = $"Wp name: [{_apiWp.name}]";

                    if (distWp < minDist) _checkBoxWp.Checked = true;
                    
                    _nearWaypointPanel.Show();
                }
                else {
                    _nearWaypointPanel.Hide();
                }   
            }
            

            if (distVista<distHp) {
                // Vista
                _nearHeroPointPanel.Hide();
                if (nearestVistaXml != null && !nearestVistaXml.found) {
                    _currentVistaMarker = nearestVistaXml;
                    _nearVistaPanel.Title = $"{_apiVista.type} At {distVista:F1} Units";
                    _nearVistaPanel.Location = new Point(xPanelLoc, yPanelLoc + 90);

                    if (distVista < minDist) _checkBoxVista.Checked = true;

                    _nearVistaPanel.Show();
                }
                else {
                    _nearVistaPanel.Hide();
                }

            }
            else {
                // Hero Point
                _nearVistaPanel.Hide();
                if (nearesthpXml != null && !nearesthpXml.found) {
                    _currentHpMarker = nearesthpXml;
                    _nearHeroPointPanel.Title = $"{nearesthpXml.Category} At {distHp:F1} Units";
                    _nearHeroPointPanel.Location = new Point(xPanelLoc, yPanelLoc+90);

                    if (distHp< minDist) _checkBoxHp.Checked = true;

                    _nearHeroPointPanel.Show();
                }
                else {
                    _nearHeroPointPanel.Hide();
                }
            }




            // Always show heart within boundaries
            if (nearestHeartXML != null && !nearestHeartXML.found) {
                _currentHeartMarker = nearestHeartXML;
                _nearHeartPanel.Title = $"Heart (inside boundary)";
                _nearHeartPanel.Location = new Point(xPanelLoc, yPanelLoc+180);
                _checkBoxHeart.BasicTooltipText = $"Objective: [{_apiHeart.objective}]";

                _nearHeartPanel.Show();
            }
            else {
                _nearHeartPanel.Hide();
            }
        }
    }
}