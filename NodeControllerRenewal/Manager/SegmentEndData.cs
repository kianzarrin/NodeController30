using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using KianCommons;
using System;
using System.Runtime.Serialization;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using ModsCommon.Utilities;
using NodeController.Utilities;

namespace NodeController
{
    public class SegmentEndData : INetworkData, IOverlay
    {
        #region STATIC

        public static float CircleRadius => 2.5f;
        public static float DotRadius => 1f;
        public static float MinPossibleRotate => -80f;
        public static float MaxPossibleRotate => 80f;

        #endregion

        #region PROPERTIES

        public string Title => $"Segment #{Id}";

        public ushort NodeId { get; set; }
        public ushort Id { get; set; }

        public NetSegment Segment => Id.GetSegment();
        public NetInfo Info => Segment.Info;
        public NetNode Node => NodeId.GetNode();
        public NodeData NodeData => Manager.Instance[NodeId];
        public bool IsStartNode => Segment.IsStartNode(NodeId);
        public SegmentEndData Other => Manager.Instance[Segment.GetOtherNode(NodeId), Id, true];

        public BezierTrajectory RawSegmentBezier { get; private set; }
        public BezierTrajectory SegmentBezier { get; private set; }
        private SegmentSide LeftSide { get; }
        private SegmentSide RightSide { get; }
        public float AbsoluteAngle => RawSegmentBezier.StartDirection.AbsoluteAngle();

        public float SegmentMinT { get; private set; }
        public float SegmentMaxT { get; private set; }

        public float DefaultOffset => Mathf.Max(Info.m_minCornerOffset, Info.m_halfWidth < 4f ? 0f : 8f);
        public bool DefaultIsSlope => !Info.m_flatJunctions && !Node.m_flags.IsFlagSet(NetNode.Flags.Untouchable);
        public bool DefaultIsTwist => !DefaultIsSlope && !Node.m_flags.IsFlagSet(NetNode.Flags.Untouchable);
        public NetSegment.Flags DefaultFlags { get; set; }

        public int PedestrianLaneCount { get; set; }
        public float CachedSuperElevationDeg { get; set; }

        public bool NoCrossings { get; set; }
        public bool NoMarkings { get; set; }
        public bool IsSlope { get; set; }

        public bool IsDefault
        {
            get
            {
                var ret = SlopeAngle == 0f;
                ret &= TwistAngle == 0;
                ret &= IsSlope == DefaultIsSlope;

                ret &= NoCrossings == false;
                ret &= NoMarkings == false;
                return ret;
            }
        }
        private float _offsetValue;
        private float _rotateValue;
        private float _minOffset = 0f;
        private float _maxOffset = 100f;

        public float Offset
        {
            get => _offsetValue;
            set => SetOffset(value, true);
        }
        public float OffsetT
        {
            get
            {
                if (Offset == MinOffset)
                    return SegmentMinT;
                else if (Offset == MaxOffset)
                    return SegmentMaxT;
                else
                    return RawSegmentBezier.Travel(0f, Offset);
            }
        }
        public float MinPossibleOffset { get; private set; }
        public float MaxPossibleOffset { get; private set; }
        public float MinOffset
        {
            get => Mathf.Max(_minOffset, MinPossibleOffset);
            private set
            {
                _minOffset = value;
                SetOffset(Offset);
            }
        }
        public float MaxOffset
        {
            get => Mathf.Min(_maxOffset, MaxPossibleOffset);
            private set
            {
                _maxOffset = value;
                SetOffset(Offset);
            }
        }

        public float Shift { get; set; }
        public float RotateAngle
        {
            get => _rotateValue;
            set => _rotateValue = Mathf.Clamp(value, MinRotate, MaxRotate);
        }
        public float MinRotate { get; set; }
        public float MaxRotate { get; set; }
        public float SlopeAngle { get; set; }
        public float TwistAngle { get; set; }

        public bool IsBorderOffset => Offset == MinOffset;
        public bool IsBorderRotate => RotateAngle == MinRotate || RotateAngle == MaxRotate;
        public bool IsBorderT => LeftSide.RawT >= RightSide.RawT ? LeftSide.IsBorderT : RightSide.IsBorderT;

        public bool? ShouldHideCrossingTexture
        {
            get
            {
                if (NodeData != null && NodeData.Type == NodeStyleType.Stretch)
                    return false; // always ignore.
                else if (NoMarkings)
                    return true; // always hide
                else
                    return null; // default.
            }
        }

        public SegmentSide this[SideType side] => side switch
        {
            SideType.Left => LeftSide,
            SideType.Right => RightSide,
            _ => throw new NotImplementedException(),
        };

        public Vector3 Position { get; private set; }
        public Vector3 Direction { get; private set; }


        #endregion

        #region BASIC

        public SegmentEndData(ushort segmentId, ushort nodeId)
        {
            Id = segmentId;
            NodeId = nodeId;

            LeftSide = new SegmentSide(SideType.Left);
            RightSide = new SegmentSide(SideType.Right);

            DefaultFlags = Segment.m_flags;
            PedestrianLaneCount = Info.CountPedestrianLanes();

            CalculateSegmentBeziers(Id, out var bezier, out var leftBezier, out var rightBezier);
            if (IsStartNode)
            {
                RawSegmentBezier = bezier;
                LeftSide.RawBezier = leftBezier;
                RightSide.RawBezier = rightBezier;
            }
            else
            {
                RawSegmentBezier = bezier.Invert();
                LeftSide.RawBezier = rightBezier.Invert();
                RightSide.RawBezier = leftBezier.Invert();
            }
        }
        public void UpdateNode() => Manager.Instance.Update(NodeId);

        public void ResetToDefault(NodeStyle style, bool force)
        {
            MinPossibleOffset = style.MinOffset;
            MaxPossibleOffset = style.MaxOffset;

            if (!style.SupportShift || force)
                Shift = NodeStyle.DefaultShift;
            if (!style.SupportRotate || force)
                RotateAngle = NodeStyle.DefaultRotate;
            if (!style.SupportSlope || force)
                SlopeAngle = NodeStyle.DefaultSlope;
            if (!style.SupportTwist || force)
                TwistAngle = NodeStyle.DefaultTwist;
            if (!style.SupportNoMarking || force)
                NoMarkings = NodeStyle.DefaultNoMarking;
            if (!style.SupportSlopeJunction || force)
                IsSlope = NodeStyle.DefaultSlopeJunction;
            if (!style.SupportOffset || force)
                SetOffset(DefaultOffset);
            else
                SetOffset(Offset);
        }

        private void SetOffset(float value, bool changeRotate = false)
        {
            _offsetValue = Mathf.Clamp(value, MinOffset, MaxOffset);

            if (changeRotate && IsBorderT)
                SetRotate(0f);
        }
        public void SetRotate(float value)
        {
            CalculateMinMaxRotate();
            RotateAngle = value;
        }

        #endregion

        #region CALCULATE

        #region BEZIERS

        public static void UpdateBeziers(ushort segmentId)
        {
            CalculateSegmentBeziers(segmentId, out var bezier, out var leftBezier, out var rightBezier);
            Manager.Instance.GetSegmentData(segmentId, out var start, out var end);

            if (start != null)
            {
                start.RawSegmentBezier = bezier;
                start.LeftSide.RawBezier = leftBezier;
                start.RightSide.RawBezier = rightBezier;
            }
            if (end != null)
            {
                end.RawSegmentBezier = bezier.Invert();
                end.LeftSide.RawBezier = rightBezier.Invert();
                end.RightSide.RawBezier = leftBezier.Invert();
            }
        }
        public static void CalculateSegmentBeziers(ushort segmentId, out BezierTrajectory bezier, out BezierTrajectory leftSide, out BezierTrajectory rightSide)
        {
            var segment = segmentId.GetSegment();
            GetSegmentPosAndDir(segmentId, segment.m_startNode, out var startPos, out var startDir, out var endPos, out var endDir);

            Fix(segment.m_startNode, segmentId, ref startDir);
            Fix(segment.m_endNode, segmentId, ref endDir);

            bezier = new BezierTrajectory(startPos, startDir, endPos, endDir);

            var startNormal = startDir.MakeFlatNormalized().Turn90(false);
            var endNormal = endDir.MakeFlatNormalized().Turn90(true);
            GetSegmentHalfWidth(segmentId, out var startHalfWidth, out var endHalfWidth);

            leftSide = new BezierTrajectory(startPos + startNormal * startHalfWidth, startDir, endPos + endNormal * endHalfWidth, endDir);
            rightSide = new BezierTrajectory(startPos - startNormal * startHalfWidth, startDir, endPos - endNormal * endHalfWidth, endDir);

            static void Fix(ushort nodeId, ushort ignoreSegmentId, ref Vector3 dir)
            {
                if (Manager.Instance[nodeId] is NodeData startData && startData.IsMiddleNode)
                {
                    var startNearSegmentId = startData.SegmentIds.First(s => s != ignoreSegmentId);
                    GetSegmentPosAndDir(startNearSegmentId, nodeId, out _, out var nearDir, out _, out _);
                    dir = (dir - nearDir).normalized;
                }
            }
        }
        private static void GetSegmentPosAndDir(ushort segmentId, ushort startNodeId, out Vector3 startPos, out Vector3 startDir, out Vector3 endPos, out Vector3 endDir)
        {
            var segment = segmentId.GetSegment();
            var isStart = segment.IsStartNode(startNodeId);

            startPos = (isStart ? segment.m_startNode : segment.m_endNode).GetNode().m_position;
            startDir = isStart ? segment.m_startDirection : segment.m_endDirection;
            endPos = (isStart ? segment.m_endNode : segment.m_startNode).GetNode().m_position;
            endDir = isStart ? segment.m_endDirection : segment.m_startDirection;

            Manager.Instance.GetSegmentData(segmentId, out var start, out var end);
            var startShift = (isStart ? start : end)?.Shift ?? 0f;
            var endShift = (isStart ? end : start)?.Shift ?? 0f;

            if (startShift == 0f && endShift == 0f)
                return;

            var shift = (startShift + endShift) / 2;
            var dir = endPos - startPos;
            var sin = shift / dir.XZ().magnitude;
            var deltaAngle = Mathf.Asin(sin);
            var normal = dir.TurnRad(Mathf.PI / 2 + deltaAngle, true).normalized;

            startPos -= normal * startShift;
            endPos += normal * endShift;
            startDir = startDir.TurnRad(deltaAngle, true);
            endDir = endDir.TurnRad(deltaAngle, true);
        }
        private static void GetSegmentHalfWidth(ushort segmentId, out float startWidth, out float endWidth)
        {
            var segment = segmentId.GetSegment();

            Manager.Instance.GetSegmentData(segmentId, out var start, out var end);
            var startTwist = start?.TwistAngle ?? 0f;
            var endTwist = end?.TwistAngle ?? 0f;

            startWidth = segment.Info.m_halfWidth * Mathf.Cos(startTwist * Mathf.Deg2Rad);
            endWidth = segment.Info.m_halfWidth * Mathf.Cos(endTwist * Mathf.Deg2Rad);
        }

        #endregion

        #region LIMITS

        public static void UpdateMinLimits(NodeData data)
        {
            var endDatas = data.SegmentEndDatas.OrderBy(s => s.AbsoluteAngle).ToArray();
            var count = endDatas.Length;

            var leftMitT = new float[count];
            var rightMinT = new float[count];
            var isMiddle = data.IsMiddleNode;

            for (var i = 0; i < count; i += 1)
            {
                if (count == 1 || isMiddle)
                {
                    leftMitT[i] = 0f;
                    rightMinT[i] = 0f;
                }
                else
                {
                    var j = (i + 1) % count;

                    var intersect = Intersection.CalculateSingle(endDatas[i].LeftSide.RawBezier, endDatas[j].RightSide.RawBezier);
                    if (intersect.IsIntersect)
                    {
                        leftMitT[i] = Mathf.Max(leftMitT[i], intersect.FirstT);
                        rightMinT[j] = Mathf.Max(rightMinT[j], intersect.SecondT);
                    }
                    intersect = Intersection.CalculateSingle(endDatas[i].LeftSide.RawBezier, endDatas[j].LeftSide.RawBezier);
                    if (intersect.IsIntersect)
                    {
                        leftMitT[i] = Mathf.Max(leftMitT[i], intersect.FirstT);
                        leftMitT[j] = Mathf.Max(leftMitT[j], intersect.SecondT);
                    }
                    intersect = Intersection.CalculateSingle(endDatas[i].RightSide.RawBezier, endDatas[j].RightSide.RawBezier);
                    if (intersect.IsIntersect)
                    {
                        rightMinT[i] = Mathf.Max(rightMinT[i], intersect.FirstT);
                        rightMinT[j] = Mathf.Max(rightMinT[j], intersect.SecondT);
                    }
                }
            }

            for (var i = 0; i < count; i += 1)
            {
                endDatas[i].LeftSide.MinT = leftMitT[i];
                endDatas[i].RightSide.MinT = rightMinT[i];
            }
        }
        public static void UpdateMaxLimits(ushort segmentId)
        {
            Manager.Instance.GetSegmentData(segmentId, out var start, out var end);

            if (start == null)
                SetNoMaxLimits(end);
            else if (end == null)
                SetNoMaxLimits(start);
            else
            {
                SetMaxLimits(start, end, SideType.Left);
                SetMaxLimits(start, end, SideType.Right);
            }
        }
        private static void SetNoMaxLimits(SegmentEndData segmentEnd)
        {
            if (segmentEnd != null)
            {
                segmentEnd.LeftSide.MaxT = 1f;
                segmentEnd.RightSide.MaxT = 1f;
            }
        }
        private static void SetMaxLimits(SegmentEndData start, SegmentEndData end, SideType side)
        {
            var startSide = start[side];
            var endSide = end[side.Invert()];

            var startT = start.GetCornerOffset(startSide);
            var endT = end.GetCornerOffset(endSide);
            if (startT + endT > 1f)
            {
                var delta = (startT + endT - 1f) / 2;
                startT -= delta;
                endT -= delta;
            }
            startSide.MaxT = 1f - endT;
            endSide.MaxT = 1f - startT;
        }

        #endregion

        public void Calculate(bool isMain)
        {
            CalculateSegmentLimit();
            CalculateMinMaxRotate();

            LeftSide.RawT = GetCornerOffset(LeftSide);
            RightSide.RawT = GetCornerOffset(RightSide);

            LeftSide.Calculate(this, isMain);
            RightSide.Calculate(this, isMain);

            CalculatePositionAndDirection();
            UpdateCachedSuperElevation();
        }

        private void CalculateSegmentLimit()
        {
            var startLimitLine = new StraightTrajectory(LeftSide.Bezier.StartPosition, RightSide.Bezier.StartPosition);
            var startIntersect = Intersection.CalculateSingle(RawSegmentBezier, startLimitLine);

            SegmentMinT = startIntersect.IsIntersect ? startIntersect.FirstT : 0f;
            MinOffset = RawSegmentBezier.Cut(0f, SegmentMinT).Length;

            var endLimitLine = new StraightTrajectory(LeftSide.Bezier.EndPosition, RightSide.Bezier.EndPosition);
            var endIntersect = Intersection.CalculateSingle(RawSegmentBezier, endLimitLine);

            SegmentMaxT = endIntersect.IsIntersect ? endIntersect.FirstT : 1f;
            MaxOffset = RawSegmentBezier.Cut(0f, SegmentMaxT).Length;

            SegmentBezier = RawSegmentBezier.Cut(SegmentMinT, SegmentMaxT);
        }
        private void CalculateMinMaxRotate()
        {
            var t = OffsetT;
            var position = RawSegmentBezier.Position(t);
            var direction = RawSegmentBezier.Tangent(t).MakeFlatNormalized().Turn90(false);

            var startLeft = GetAngle(LeftSide.Bezier.StartPosition - position, direction);
            var endLeft = GetAngle(LeftSide.Bezier.EndPosition - position, direction);
            var startRight = GetAngle(position - RightSide.Bezier.StartPosition, direction);
            var endRight = GetAngle(position - RightSide.Bezier.EndPosition, direction);

            MinRotate = Mathf.Clamp(Mathf.Max(startLeft, endRight), MinPossibleRotate, MaxPossibleRotate);
            MaxRotate = Mathf.Clamp(Mathf.Min(endLeft, startRight), MinPossibleRotate, MaxPossibleRotate);

            RotateAngle = RotateAngle;

            static float GetAngle(Vector3 cornerDir, Vector3 segmentDir)
            {
                var angle = Vector3.Angle(segmentDir, cornerDir);
                var sign = Mathf.Sign(Vector3.Cross(segmentDir, cornerDir).y);
                return sign * angle;
            }
        }
        private float GetCornerOffset(SegmentSide side)
        {
            var t = OffsetT;
            var position = RawSegmentBezier.Position(t);
            var direction = RawSegmentBezier.Tangent(t).MakeFlatNormalized().TurnDeg(90 + RotateAngle, true);

            var line = new StraightTrajectory(position, position + direction, false);
            var intersection = Intersection.CalculateSingle(side.RawBezier, line);

            if (intersection.IsIntersect)
                return intersection.FirstT;
            else if (RotateAngle == 0f)
                return t <= 0.5f ? 0f : 1f;
            else
                return side.Type == SideType.Left ^ RotateAngle > 0f ? 0f : 1f;
        }
        private void CalculatePositionAndDirection()
        {
            var line = new StraightTrajectory(LeftSide.Position, RightSide.Position);
            var intersect = Intersection.CalculateSingle(line, RawSegmentBezier);
            var t = intersect.IsIntersect ? intersect.FirstT : 0.5f;

            Position = line.Position(t);
            Direction = VectorUtils.NormalizeXZ(LeftSide.Direction * t + RightSide.Direction * (1 - t));
        }
        private void UpdateCachedSuperElevation()
        {
            var diff = RightSide.Position - LeftSide.Position;
            var se = Mathf.Atan2(diff.y, VectorUtils.LengthXZ(diff));
            CachedSuperElevationDeg = se * Mathf.Rad2Deg;
        }

        #endregion

        #region UTILITIES

        public void GetCorner(bool isLeft, out Vector3 position, out Vector3 direction)
        {
            var side = isLeft ? LeftSide : RightSide;

            position = side.Position;
            direction = side.Direction;
        }
        public override string ToString() => $"segment:{Id} node:{NodeId}";

        #endregion

        #region RENDER

        public void Render(OverlayData data) => Render(data, data, data);
        public void Render(OverlayData contourData, OverlayData outterData, OverlayData innerData)
        {
            var data = Manager.Instance[NodeId];

            RenderŅontour(contourData);
            if (data.IsMoveableEnds)
            {
                RenderEnd(contourData, (LeftSide.Position - Position).magnitude + CircleRadius, 0f);
                RenderEnd(contourData, 0f, (RightSide.Position - Position).magnitude + CircleRadius);
                RenderOutterCircle(outterData);
                RenderInnerCircle(innerData);
            }
            else
                RenderEnd(contourData);
        }
        public void RenderAlign(OverlayData contourData, OverlayData? leftData = null, OverlayData? rightData = null)
        {
            var leftCut = leftData != null ? DotRadius : 0f;
            var rightCut = rightData != null ? DotRadius : 0f;

            RenderŅontour(contourData, leftCut, rightCut);
            RenderEnd(contourData, leftCut, rightCut);

            if (leftData != null)
                LeftSide.Position.RenderCircle(leftData.Value, DotRadius * 2, 0f);
            if (rightData != null)
                RightSide.Position.RenderCircle(rightData.Value, DotRadius * 2, 0f);
        }

        public void RenderSides(OverlayData dataAllow, OverlayData dataForbidden)
        {
            LeftSide.Render(dataAllow, dataForbidden);
            RightSide.Render(dataAllow, dataForbidden);
        }
        public void RenderEnd(OverlayData data, float? leftCut = null, float? rightCut = null)
        {
            var line = new StraightTrajectory(LeftSide.Position, RightSide.Position);
            var startT = (leftCut ?? 0f) / line.Length;
            var endT = (rightCut ?? 0f) / line.Length;
            line = line.Cut(startT, 1 - endT);
            line.Render(data);
        }
        public void RenderŅontour(OverlayData data, float? leftCut = null, float? rightCut = null)
        {
            RenderSide(LeftSide, data, leftCut);
            RenderSide(RightSide, data, rightCut);

            var endSide = new StraightTrajectory(LeftSide.Bezier.EndPosition, RightSide.Bezier.EndPosition);
            endSide.Render(data);
        }
        private void RenderSide(SegmentSide side, OverlayData data, float? cut)
        {
            var bezier = new BezierTrajectory(side.Position, side.Direction, side.Bezier.EndPosition, side.Bezier.EndDirection);
            if (cut != null)
            {
                var t = bezier.Travel(0f, cut.Value);
                bezier = bezier.Cut(t, 1f);
            }
            bezier.Render(data);
        }

        public void RenderInnerCircle(OverlayData data) => Position.RenderCircle(data, DotRadius * 2, 0f);
        public void RenderOutterCircle(OverlayData data) => Position.RenderCircle(data, CircleRadius * 2 + 0.5f, CircleRadius * 2 - 0.5f);

        #endregion

        #region UI COMPONENTS

        public void GetUIComponents(UIComponent parent, Action refresh)
        {
            throw new NotImplementedException();
        }

        #endregion

    }
    public class SegmentSide
    {
        private BezierTrajectory _rawBezier;
        private float _minT = 0f;
        private float _maxT = 1f;

        public SideType Type { get; }

        public BezierTrajectory RawBezier
        {
            get => _rawBezier;
            set
            {
                _rawBezier = value;
                Bezier = value;
            }
        }
        public float MinT
        {
            get => _minT;
            set
            {
                _minT = value;
                Update();
            }
        }
        public float MaxT
        {
            get => _maxT;
            set
            {
                _maxT = value;
                Update();
            }
        }

        public BezierTrajectory Bezier { get; private set; }
        public float RawT { get; set; }
        public float DeltaT => 0.05f / RawBezier.Length;

        public Vector3 Position { get; private set; }
        public Vector3 Direction { get; private set; }

        public bool IsBorderT => RawT - 0.001f <= MinT;

        public SegmentSide(SideType type)
        {
            Type = type;
        }
        private void Update() => Bezier = RawBezier.Cut(MinT, MaxT);
        public void Calculate(SegmentEndData data, bool isMain)
        {
            var nodeData = data.NodeData;

            var delta = !nodeData.IsMiddleNode ? DeltaT : 0f;
            var t = Mathf.Clamp(RawT, MinT + delta, MaxT);
            var position = RawBezier.Position(t);
            var direction = RawBezier.Tangent(t).normalized;

            if (nodeData.IsMiddleNode || nodeData.IsEndNode)
            {
                var quaternion = Quaternion.AngleAxis(data.SlopeAngle, direction.MakeFlat().Turn90(true));
                direction = quaternion * direction;

                position.y += (Type == SideType.Left ? -1 : 1) * data.Info.m_halfWidth * Mathf.Sin(data.TwistAngle * Mathf.Deg2Rad);
            }
            else if (!data.IsSlope)
            {
                position.y = data.Node.m_position.y;
                direction = direction.MakeFlatNormalized();
            }
            else if (!isMain)
            {
                GetClosest(nodeData, position, out var closestPos, out var closestDir);
                position.y = closestPos.y;

                var closestLine = new StraightTrajectory(closestPos, closestPos + closestDir, false);
                var line = new StraightTrajectory(position, position - direction, false);
                var intersect = Intersection.CalculateSingle(closestLine, line);
                var intersectPos = closestPos + intersect.FirstT * closestDir;
                direction = (position - intersectPos).normalized;
            }

            Position = position;
            Direction = VectorUtils.NormalizeXZ(direction);
        }
        private void GetClosest(NodeData nodeData, Vector3 position, out Vector3 closestPos, out Vector3 closestDir)
        {
            nodeData.LeftMainBezier.Trajectory.ClosestPositionAndDirection(position, out var leftClosestPos, out var leftClosestDir, out _);
            nodeData.RightMainBezier.Trajectory.ClosestPositionAndDirection(position, out var rightClosestPos, out var rightClosestDir, out _);

            if ((leftClosestPos - position).sqrMagnitude < (rightClosestPos - position).sqrMagnitude)
            {
                closestPos = leftClosestPos;
                closestDir = leftClosestDir;
            }
            else
            {
                closestPos = rightClosestPos;
                closestDir = rightClosestDir;
            }
        }

        public void Render(OverlayData dataAllow, OverlayData dataForbidden)
        {
            if (MinT == 0f)
                RawBezier.Cut(0f, RawT).Render(dataAllow);
            else
            {
                dataForbidden.CutEnd = true;
                dataAllow.CutStart = true;
                RawBezier.Cut(0f, Math.Min(RawT, MinT)).Render(dataForbidden);
                if (RawT - MinT >= 0.2f / RawBezier.Length)
                    RawBezier.Cut(MinT, RawT).Render(dataAllow);
            }
        }

        public override string ToString() => $"{Type}: {nameof(RawT)}={RawT}; {nameof(MinT)}={MinT}; {nameof(MaxT)}={MaxT}; {nameof(Position)}={Position};";
    }
    public enum SideType : byte
    {
        Left,
        Right
    }
    public static class SideTypeExtension
    {
        public static SideType Invert(this SideType side) => side == SideType.Left ? SideType.Right : SideType.Left;
    }
}
