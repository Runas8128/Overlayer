﻿using Overlayer.MapParser.Types;
using JSON;
using static Overlayer.MapParser.Actions.ActionUtils;

namespace Overlayer.MapParser.Actions.Default
{
    public class Hide : Action
    {
        public Toggle hideJudgment = Toggle.Disabled;
        public Toggle hideTileIcon = Toggle.Disabled;
        public Hide() : base(LevelEventType.Hide) { }
        public Hide(Toggle hideJudgment, Toggle hideTileIcon, bool active) : base(LevelEventType.Hide, active)
        {
            this.hideJudgment = hideJudgment;
            this.hideTileIcon = hideTileIcon;
        }
        public override JsonNode ToNode()
        {
            JsonNode node = InitNode(eventType, active);
            node["hideJudgment"] = hideJudgment.ToString();
            node["hideTileIcon"] = hideTileIcon.ToString();
            return node;
        }
    }
}
