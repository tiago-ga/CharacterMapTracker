// File: Services/PathingMarkerLoader.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;                 // For Stream
using System.Linq;
using System.Xml.Linq;
using Blish_HUD;
using Blish_HUD.Modules.Managers;
using Gw2Sharp.WebApi.V2.Models;
//using Microsoft.VisualBasic;
//using SharpDX.XAudio2;

namespace CharacterMapTracker.Services{
    public class Marker{
        public string PoiType { get; set; }
        public int MapId { get; set; }
        public string MapName { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string Guid { get; set; }
        public string Category { get; set; }    // vista, waypoint, hero point, heart
        public int? Number { get; set; }        // For numbered markers like vista01, wp02, etc.
        public bool found { get; set; }
        public string expansion { get; set; }   // Added to properly generate the MainWindow
        public int region_id { get; set; }
        public string region_name { get; set; }
    }

    public static class PathingMarkerLoader{
        public static List<Marker> LoadMarkersFromXml(Stream xmlStream, int mapId, string mapName){

            //CharacterMapTrackerModule.Logger.Info($"Before loading file for map{mapId}");
            var doc = XDocument.Load(xmlStream);
            var markers = new List<Marker>();
            string normalizedMapName = mapName.ToLowerInvariant().Replace(" ", "");

            //CharacterMapTrackerModule.Logger.Info($"Before loading each poi type");
            foreach (var poi in doc.Descendants("poi"))
            {
                // Skip if not for current map
                if (!int.TryParse(poi.Attribute("mapid")?.Value, out int currentMapId) || currentMapId != mapId){
                    continue;
                }

                string poitype = poi.Attribute("type")?.Value;
                if (string.IsNullOrEmpty(poitype)){
                    continue;
                }

                // Extract category and number from poitype
                var (category, number) = ParseType(poitype, normalizedMapName);

                // Skip if not a recognized category
                if (category == "unknown"){
                    continue;
                }

                // Flipping Z value with Y values because (X,Y,Z) in marker = (X,Z,Y) in avatar
                if (number!=null && category != null) {
                    markers.Add(new Marker {
                        PoiType = poitype,
                        MapId = mapId,
                        X = float.Parse(poi.Attribute("xpos")?.Value ?? "0", CultureInfo.InvariantCulture),
                        Z = float.Parse(poi.Attribute("ypos")?.Value ?? "0", CultureInfo.InvariantCulture),
                        Y = float.Parse(poi.Attribute("zpos")?.Value ?? "0", CultureInfo.InvariantCulture),
                        Guid = poi.Attribute("guid")?.Value,
                        Category = category,
                        Number = number
                    });
                }

            }

            //CharacterMapTrackerModule.Logger.Info($"Finished parsing map{mapId}");
            return markers;
        }

        // Parse the poi type to obtain the category in question
        private static (string category, int? number) ParseType(string poitype, string normalizedMapName){
            // Split poitype string by dots
            var parts = poitype.Split('.');
            if (parts.Length < 2){
                return ("unknown", null);
            }

            // Get the last part which contains the marker poitype (something like: "tw_mc_vistas_region_normalizedMapName_vista03")
            string lastPart = parts.Last();

            // Split last part by underscores (e.g: [tw, mc, vistas_region, normalizedMapName, vista03])
            var subParts = lastPart.Split('_');
            if (subParts.Length < 2) {
                return("unknown", null);
            }

            // after PoF the hearts have this element at the end, remove it to keep consistency
            if (subParts.Last() == "contibutionchart") {
                // Remove the last element
                subParts = subParts.Take(subParts.Length - 1).ToArray();
            }
            // Get category and number of it
            string categoryPart = subParts[subParts.Length - 1];
            return ParseMarkerType(categoryPart);
        }

        // Helper function
        private static (string category, int? number) ParseMarkerType(string markerPart){
            if (markerPart.StartsWith("vista")){
                return ("vista", ExtractNumber(markerPart, "vista".Length));
            }
            if (markerPart.StartsWith("wp")) {
                return ("waypoint", ExtractNumber(markerPart, "wp".Length));
            }
            if (markerPart.StartsWith("waypoint")) {
                return ("waypoint", ExtractNumber(markerPart, "waypoint".Length));
            }
            if (markerPart.StartsWith("pointsofinterest")) {
                return ("landmark", ExtractNumber(markerPart, "pointsofinterest".Length));
            }
            if (markerPart.StartsWith("poi")) {
                return ("landmark", ExtractNumber(markerPart, "poi".Length));
            }
            if (markerPart.StartsWith("heart")) {
                return ("task", ExtractNumber(markerPart, "heart".Length));
            }
            if (markerPart.StartsWith("task")) {
                return ("task", ExtractNumber(markerPart, "task".Length));
            }
            if (markerPart.StartsWith("hp")) {
                return ("heropoint", ExtractNumber(markerPart, "hp".Length));
            }
            if (markerPart.StartsWith("herochallenge")) {
                return ("heropoint", ExtractNumber(markerPart, "herochallenge".Length));
            }
            return ("unknown", null);
        }

        // Helper function
        private static int? ExtractNumber(string input, int prefixLength){
            if (int.TryParse(input.Substring(prefixLength), out int number)){
                return number;
            }
            return null;
        }
    }
}
