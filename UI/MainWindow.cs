//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using CharacterMapTracker.Models;
using CharacterMapTracker.Services;
using Blish_HUD.Graphics.UI;
using Blish_HUD.Content;
using Blish_HUD.Modules.Managers;
using Microsoft.Xna.Framework.Content;

namespace CharacterMapTracker.UI {
    public class MainWindow : TabbedWindow2 {

        private Panel _leftPanel;
        private Panel _rightPanel;
       

        public MainWindow() : base(
            AsyncTexture2D.FromAssetId(155985),         // Background  155978
            new Rectangle(24, 30, 960, 700),            // Window Region (wider for both panels)
            new Rectangle(82, 30, 860, 660))            // Content Region
        {
            this.Title = "Map Completion Tracker";
            this.Subtitle = "Explore your progress";
            this.Location = new Point(500, 500);
            this.SavesPosition = true;
            this.Id = "002_MapTracker";

            this.Hide();
            BuildLayout();
        }

        private void BuildLayout() {
            var container = new Panel {
                Size = this.ContentRegion.Size,
                Location = Point.Zero,
                Parent = this,
                CanScroll = false
            };

            _leftPanel = new Panel {
                Size = new Point(200, container.Height),
                Location = new Point(0, 0),
                Parent = container,
                CanScroll = true,
                BackgroundColor = new Color(20, 20, 20, 100)
            };

            _rightPanel = new Panel {
                Size = new Point(container.Width - 200, container.Height),
                Location = new Point(200, 0),
                Parent = container,
                CanScroll = true,
                BackgroundColor = new Color(0, 0, 0, 80)
            };

            BuildExpansionSections();
        }
        
        private void BuildExpansionSections() {
            // Dummy collapsible sections for now — will be dynamic

            var coreButton = new StandardButton {
                Text = "Core Tyria",
                Size = new Point(_leftPanel.Width - 20, 40),
                Location = new Point(10, 10),
                Parent = _leftPanel
            };

            coreButton.Click += delegate {
                ShowMapsForExpansion("Core Tyria");
            };

            var hotButton = new StandardButton {
                Text = "Heart of Thorns",
                Size = new Point(_leftPanel.Width - 20, 40),
                Location = new Point(10, 60),
                Parent = _leftPanel
            };

            hotButton.Click += delegate {
                ShowMapsForExpansion("Heart of Thorns");
            };
        }

        private void ShowMapsForExpansion(string expansionName) {
            _rightPanel.ClearChildren();

            var label = new Label {
                Text = $"Showing maps for: {expansionName}",
                Location = new Point(10, 10),
                Parent = _rightPanel,
                AutoSizeWidth = true
            };

            // Later: dynamically add map controls here based on expansionName
        }
    }
}
