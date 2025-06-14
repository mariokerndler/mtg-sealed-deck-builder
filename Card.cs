﻿namespace SealedDeckBuilder
{
    internal class Card
    {
        public string oracle_id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string mana_cost { get; set; } = string.Empty;
        public float cmc { get; set; } = 0.0f;
        public string type_line { get; set; } = string.Empty;
        public string oracle_text { get; set; } = string.Empty;
        public string[] colors { get; set; } = [];
        public string rarity { get; set; } = string.Empty;
        public string power { get; set; } = string.Empty;
        public string toughness { get; set; } = string.Empty;
        public string loyalty { get; set; } = string.Empty;
        public string[] keywords { get; set; } = [];

        public string GetCardColor()
        {
            if (colors.Length == 0)
                return "C";
            return string.Join(", ", colors.Select(c => c.ToUpperInvariant()));
        }
    }
}
