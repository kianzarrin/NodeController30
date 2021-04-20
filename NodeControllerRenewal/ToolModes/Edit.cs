﻿using ModsCommon.Utilities;
using UnityEngine;

namespace NodeController
{
    public class EditToolMode : NodeControllerToolMode
    {
        public override ToolModeType Type => ToolModeType.Edit;
        public SegmentEndData HoverSegmentEndCenter { get; set; }
        private bool IsHoverSegmentEndCenter => HoverSegmentEndCenter != null;
        public SegmentEndData HoverSegmentEndCircle { get; set; }
        private bool IsHoverSegmentEndCircle => HoverSegmentEndCircle != null;

        public override void OnToolUpdate()
        {
            if (Tool.Data.IsJunction && !Tool.Panel.IsHover && InputExtension.OnlyAltIsPressed)
                Tool.SetMode(ToolModeType.ChangeMain);
            else if (!Tool.Data.IsMiddleNode && !Tool.Panel.IsHover && InputExtension.OnlyShiftIsPressed)
                Tool.SetMode(ToolModeType.Aling);
            else if (Tool.MouseRayValid && Tool.Data.IsMoveableEnds)
            {
                foreach (var segmentData in Tool.Data.SegmentEndDatas)
                {
                    var hitPos = Tool.Ray.GetRayPosition(segmentData.Position.y, out _);

                    var magnitude = (segmentData.Position - hitPos).magnitude;
                    if (magnitude < SegmentEndData.DotRadius)
                    {
                        HoverSegmentEndCenter = segmentData;
                        HoverSegmentEndCircle = null;
                        return;
                    }
                    else if (magnitude < SegmentEndData.CircleRadius + 1f && magnitude > SegmentEndData.CircleRadius - 0.5f)
                    {
                        HoverSegmentEndCenter = null;
                        HoverSegmentEndCircle = segmentData;
                        return;
                    }
                }
            }

            HoverSegmentEndCenter = null;
            HoverSegmentEndCircle = null;
        }

        public override void OnSecondaryMouseClicked()
        {
            Tool.SetData(null);
            Tool.SetMode(ToolModeType.Select);
        }
        public override void OnMouseDown(Event e)
        {
            if (IsHoverSegmentEndCenter)
                Tool.SetMode(ToolModeType.Drag);
            else if (IsHoverSegmentEndCircle)
                Tool.SetMode(ToolModeType.Rotate);
        }
        public override string GetToolInfo() => $"Hold Shift to alignment roads\nHold Alt to change main road";

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            var hover = new OverlayData(cameraInfo);
            var green = new OverlayData(cameraInfo) { Color = Colors.Green };
            var yellow = new OverlayData(cameraInfo) { Color = Colors.Yellow };
            foreach (var segmentData in Tool.Data.SegmentEndDatas)
            {
                segmentData.Render(green, segmentData == HoverSegmentEndCircle ? hover : yellow, segmentData == HoverSegmentEndCenter ? hover : yellow);
            }
        }
    }
}
