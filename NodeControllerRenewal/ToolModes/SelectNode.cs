﻿using ColossalFramework;
using ColossalFramework.Math;
using ModsCommon;
using ModsCommon.Utilities;
using UnityEngine;

namespace NodeController
{
    public class SelectNodeToolMode : BaseSelectToolMode<NodeControllerTool>, IToolModePanel, IToolMode<ToolModeType>
    {
        public bool ShowPanel => false;
        public ToolModeType Type => ToolModeType.Select;
        protected override Color32 NodeColor => Colors.Yellow;
        private bool IsPossibleInsertNode { get; set; }
        private Vector3 InsertPosition { get; set; }

        public override string GetToolInfo()
        {
            if (IsHoverNode)
                return string.Format(Localize.Tool_InfoClickNode, HoverNode.Id) + GetStepOverInfo();
            else if (IsHoverSegment)
            {
                if (!IsPossibleInsertNode)
                    return Localize.Tool_InfoTooCloseNode.AddErrorColor() + GetStepOverInfo();
                else if (HoverSegment.Id.GetSegment().Info.PedestrianLanes() >= 2)
                    return Localize.Tool_InfoInsertCrossingNode.AddActionColor() + GetStepOverInfo();
                else
                    return Localize.Tool_InfoInsertNode.AddActionColor() + GetStepOverInfo();
            }
            else
                return $"{Localize.Tool_InfoSelectNode}\n\n{string.Format(Localize.Tool_InfoUnderground, LocalizeExtension.Shift.AddInfoColor())}";
        }
        private string GetStepOverInfo() => NodeControllerTool.SelectionStepOverShortcut.NotSet ? string.Empty : "\n\n" + string.Format(CommonLocalize.Tool_InfoSelectionStepOver, NodeControllerTool.SelectionStepOverShortcut.AddInfoColor());

        protected override bool IsValidNode(ushort nodeId)
        {
            if (!base.IsValidNode(nodeId))
                return false;

            if (Settings.SelectMiddleNodes)
                return true;

            var node = nodeId.GetNode();
            return node.m_flags.CheckFlags(0, NetNode.Flags.Middle | NetNode.Flags.Outside) || node.m_flags.CheckFlags(0, NetNode.Flags.Moveable | NetNode.Flags.Outside);
        }
        protected override bool CheckSegment(ushort segmentId) => segmentId.GetSegment().m_flags.CheckFlags(0, NetSegment.Flags.Untouchable) && base.CheckSegment(segmentId);
        protected override bool CheckItemClass(ItemClass itemClass) => (itemClass.m_layer == ItemClass.Layer.Default || itemClass.m_layer == ItemClass.Layer.MetroTunnels) && itemClass switch
        {
            //{ m_service: ItemClass.Service.Road } => true,
            //{ m_service: ItemClass.Service.PublicTransport } => true,
            //{ m_service: ItemClass.Service.Beautification, m_level: >= ItemClass.Level.Level3 } => true,
            //{ m_service: ItemClass.Service.Beautification, m_subService: ItemClass.SubService.BeautificationParks } => true,
            //_ => false,
            { m_service: ItemClass.Service.Electricity} => false,
            { m_service: ItemClass.Service.Water } => false,
            _ => true,
        };

        public override void OnToolUpdate()
        {
            base.OnToolUpdate();

            if (!Underground && Utility.OnlyShiftIsPressed)
                Underground = true;
            else if (Underground && !Utility.OnlyShiftIsPressed)
                Underground = false;

            if (IsHoverSegment)
            {
                IsPossibleInsertNode = PossibleInsertNode(out var position, out _, out _);
                InsertPosition = position;
            }
        }

        public override void OnPrimaryMouseClicked(Event e)
        {
            if (IsHoverNode)
                Set(SingletonManager<Manager>.Instance[HoverNode.Id, true]);
            else if (IsHoverSegment && IsPossibleInsertNode)
            {
                var controlPoint = new NetTool.ControlPoint() { m_segment = HoverSegment.Id, m_position = InsertPosition };
                var newNode = SingletonManager<Manager>.Instance.InsertNode(controlPoint);
                Set(newNode);
            }
        }
        private void Set(NodeData data)
        {
            if (data != null)
            {
                Tool.SetData(data);
                Tool.SetDefaultMode();
            }
        }
        public bool PossibleInsertNode(out Vector3 position, out Vector3 direction, out float halfWidth)
        {
            position = Vector3.zero;
            direction = Vector3.zero;
            halfWidth = 0f;

            if (!IsHoverSegment)
                return false;

            SegmentEndData.CalculateSegmentBeziers(HoverSegment.Id, out var bezier, out _, out _);
            bezier.Trajectory.GetHitPosition(Tool.Ray, out _, out var t, out position);
            direction = bezier.Tangent(t).MakeFlatNormalized();
            halfWidth = SegmentEndData.GetSegmentWidth(HoverSegment.Id, t);
            var line = new StraightTrajectory(position, position + direction.Turn90(true), false);

            ref var segment = ref HoverSegment.Id.GetSegment();
            foreach (var nodeId in segment.NodeIds())
            {
                ref var node = ref nodeId.GetNode();
                if (!Settings.SelectMiddleNodes && node.m_flags.CheckFlags(NetNode.Flags.Moveable, NetNode.Flags.End))
                    continue;

                var isStart = segment.IsStartNode(nodeId);
                segment.CalculateCorner(HoverSegment.Id, true, isStart, true, out var leftPos, out var leftDir, out _);
                segment.CalculateCorner(HoverSegment.Id, true, isStart, false, out var rightPos, out var rightDir, out _);

                if (Check(line, leftPos, leftDir) || Check(line, rightPos, rightDir))
                    return false;

                static bool Check(StraightTrajectory line, Vector3 pos, Vector3 dir)
                    => Intersection.CalculateSingle(line, new StraightTrajectory(pos, pos + dir, false), out _, out var leftT) && leftT < 8f;
            }

            return true;
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            var otherOverlay = new OverlayData(cameraInfo) { Color = new Color(1f, 1f, 1f, 0.3f), RenderLimit = Underground };
            if (IsHoverSegment)
            {
                if (Settings.RenderNearNode)
                {
                    var segment = HoverSegment.Id.GetSegment();

                    if (!Underground ^ segment.m_startNode.GetNode().m_flags.IsSet(NetNode.Flags.Underground))
                        new NodeSelection(segment.m_startNode).Render(otherOverlay);

                    if (!Underground ^ segment.m_endNode.GetNode().m_flags.IsSet(NetNode.Flags.Underground))
                        new NodeSelection(segment.m_endNode).Render(otherOverlay);
                }

                var isPossibleInsert = PossibleInsertNode(out var position, out var direction, out var halfWidth);
                var overlayData = new OverlayData(cameraInfo) { Width = halfWidth * 2, Color = isPossibleInsert ? Colors.Green : Colors.Red, AlphaBlend = false, Cut = true, RenderLimit = Underground };

                var middle = new Bezier3()
                {
                    a = position + direction,
                    b = position,
                    c = position,
                    d = position - direction,
                };
                middle.RenderBezier(overlayData);

                overlayData.Width = Mathf.Min(halfWidth * 2, Selection.BorderOverlayWidth);
                overlayData.Cut = false;

                var normal = direction.MakeFlatNormalized().Turn90(true);
                RenderBorder(overlayData, position + direction, normal, halfWidth);
                RenderBorder(overlayData, position - direction, normal, halfWidth);
            }
            else
                base.RenderOverlay(cameraInfo);
        }
        private void RenderBorder(OverlayData overlayData, Vector3 position, Vector3 normal, float halfWidth)
        {
            var delta = Mathf.Max(halfWidth - Selection.BorderOverlayWidth / 2, 0f);
            var bezier = new Bezier3
            {
                a = position + normal * delta,
                b = position,
                c = position,
                d = position - normal * delta,
            };
            bezier.RenderBezier(overlayData);
        }
    }
}
