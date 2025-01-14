﻿using Overlayer.MapParser.Types;
using JSON;
using static Overlayer.MapParser.Actions.ActionUtils;

namespace Overlayer.MapParser.Actions.Default
{
    public class MultiPlanet : Action
    {
        public int planets = 2;
        public MultiPlanet() : base(LevelEventType.MultiPlanet) { }
        public MultiPlanet(int planets, bool active) : base(LevelEventType.MultiPlanet, active)
            => this.planets = planets;
        public override JsonNode ToNode()
        {
            JsonNode node = InitNode(eventType, active);
            node["planets"] = planets;
            return node;
        }
    }
}
