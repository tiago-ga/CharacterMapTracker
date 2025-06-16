using CharacterMapTracker.Services;
using System.Collections.Generic;


namespace CharacterMapTracker.Models {

    // Using only the map ID to obtain the continent, floor and region IDs to obtain the MapDetails
    public class MapInfo {
        public int id { get; set; }
        public string name { get; set; }
        public int min_level { get; set; }
        public int max_level { get; set; }
        public string type { get; set; }
        public int default_floor { get; set; }
        public int[] floors { get; set; }
        public int region_id { get; set; }
        public string region_name { get; set; }
        public int continent_id { get; set; }
        public string continent_name { get; set; }
    }

    // Details for maps using the region and continent IDs
    public class MapDetails {
        public string name { get; set; }
        public int min_level { get; set; }
        public int max_level { get; set; }
        public float[] label_coord { get; set; }
        public float[][] map_rect { get; set; }
        public float[][] continent_rect { get; set; }

        public Dictionary<string, PointOfInterest> points_of_interest { get; set; }
        public Dictionary<string, TaskInfo> tasks { get; set; }
        public List<SkillChallenge> skill_challenges { get; set; }
    }

    public class PointOfInterest {
        public int id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public int floor { get; set; }
        public float[] coord { get; set; }
        public string chat_link { get; set; }
        public bool found { get; set; }
    }

    public class TaskInfo {
        public int id { get; set; }
        public string objective { get; set; }
        public int level { get; set; }
        public float[] coord { get; set; }
        public float[][] bounds { get; set; }
        public string chat_link { get; set; }
        public bool finished { get; set; }
    }

    public class SkillChallenge {
        public string id { get; set; } // NOTE: it's a string like "0-7"
        public float[] coord { get; set; }
        public bool found { get; set; }
    }

    public class RegionInfo {
        public int id { get; set; }
        public string name { get; set; }
        public float[] label_coord { get; set; }
        public float[] continent_rect { get; set; }
        public List<MapInfo> maps { get; set; }
    }

    public class MarkerWithApiData {
        public Marker Marker { get; set; }
        public PointOfInterest ApiPoi { get; set; }
        public TaskInfo ApiHeart { get; set; }
        public SkillChallenge ApiHeroPoint { get; set; }
    }
}
