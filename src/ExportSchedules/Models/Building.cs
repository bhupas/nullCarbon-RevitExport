using Newtonsoft.Json;
using System.Collections.Generic;

namespace SCaddins.ExportSchedules.Models
{
    public class StructureDetails
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class Building
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("team")]
        public string Team { get; set; }

        [JsonProperty("structure_details")]
        public StructureDetails StructureDetails { get; set; }

        [JsonProperty("reference_area")]
        public double ReferenceArea { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; }

        [JsonProperty("is_deleted")]
        public bool IsDeleted { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("origin_of_building")]
        public string OriginOfBuilding { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("gross_floor_area")]
        public double GrossFloorArea { get; set; }

        [JsonProperty("commissioning_year")]
        public int CommissioningYear { get; set; }

        [JsonProperty("calculation_type")]
        public string CalculationType { get; set; }

        [JsonProperty("consideration_period")]
        public int ConsiderationPeriod { get; set; }

        [JsonProperty("building_type")]
        public string BuildingType { get; set; }

        [JsonProperty("project")]
        public string Project { get; set; }

        [JsonProperty("created_by")]
        public int CreatedBy { get; set; }

        [JsonProperty("structure")]
        public string Structure { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class BuildingsResponse
    {
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("next")]
        public object Next { get; set; }

        [JsonProperty("previous")]
        public object Previous { get; set; }

        [JsonProperty("results")]
        public List<Building> Results { get; set; }
    }
}