﻿using ColossalFramework.UI;
using ModsCommon.UI;
using ModsCommon.Utilities;
using NodeController.UI;
using NodeController.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace NodeController
{
    public enum NodeStyleType
    {
        [Description(nameof(Localize.NodeStyle_Middle))]
        Middle,

        [Description(nameof(Localize.NodeStyle_Bend))]
        Bend,

        [Description(nameof(Localize.NodeStyle_Stretch))]
        Stretch,

        [Description(nameof(Localize.NodeStyle_Crossing))]
        Crossing,

        [Description(nameof(Localize.NodeStyle_UTurn))]
        UTurn,

        [Description(nameof(Localize.NodeStyle_Custom))]
        Custom,

        [Description(nameof(Localize.NodeStyle_End))]
        End,
    }
    public static class NodeStyleTypeExtension
    {
        public static NodeStyle GetStyle(this NodeStyleType type, NodeData data) => type switch
        {
            NodeStyleType.Middle => new MiddleNode(data),
            NodeStyleType.Bend => new BendNode(data),
            NodeStyleType.Stretch => new StretchNode(data),
            NodeStyleType.Crossing => new CrossingNode(data),
            NodeStyleType.UTurn => new UTurnNode(data),
            NodeStyleType.Custom => new CustomNode(data),
            NodeStyleType.End => new EndNode(data),
            _ => throw new NotImplementedException(),
        };
    }
    public abstract class NodeStyle
    {
        public abstract NodeStyleType Type { get; }

        public static float MaxShift => 64f;
        public static float MinShift => -64f;
        public static float MaxRoadSlope => 60f;
        public static float MinRoadSlope => -60f;
        public static float MaxSlope => 85f;
        public static float MinSlope => -85f;
        public static float MaxRotate => 89f;
        public static float MinRotate => -89f;
        public static float MaxRoadTwist => 60f;
        public static float MinRoadTwist => -60f;
        public static float MaxTwist => 85f;
        public static float MinTwist => -85f;
        public static float MaxRoadStretch => 500f;
        public static float MaxStretch => 1000f;
        public static float MinStretch => 1f;
        public static float MaxOffset => 1000f;
        public static float MinOffset => 0f;
        private static bool HideCrosswalksEnable { get; } = DependencyUtilities.HideCrossings?.isEnabled == true;

        public virtual float AdditionalOffset => 0f;

        public virtual SupportOption SupportOffset => SupportOption.None;
        public virtual SupportOption SupportShift => SupportOption.None;
        public virtual SupportOption SupportRotate => SupportOption.None;
        public virtual SupportOption SupportSlope => SupportOption.None;
        public virtual SupportOption SupportTwist => SupportOption.None;
        public virtual SupportOption SupportMarking => SupportOption.None;
        public virtual SupportOption SupportSlopeJunction => SupportOption.None;
        public virtual SupportOption SupportStretch => SupportOption.None;
        public virtual SupportOption SupportCollision => SupportOption.None;
        public virtual SupportOption SupportForceNodeless => SupportOption.None;
        public virtual bool SupportTrafficLights => false;
        public virtual bool OnlyKeepDefault => false;
        public virtual bool NeedFixDirection => true;

        public SupportOption TotalSupport => (SupportOffset | SupportShift | SupportRotate | SupportSlope | SupportTwist | SupportStretch | SupportCollision | SupportForceNodeless) & SupportOption.All;
        private bool OnlyOnSlope => SupportSlopeJunction != SupportOption.None || DefaultSlopeJunction;

        public virtual float DefaultOffset => 0f;
        public virtual float DefaultShift => 0f;
        public virtual float DefaultRotate => 0f;
        public virtual float DefaultSlope => 0f;
        public virtual float DefaultTwist => 0f;
        public virtual bool DefaultNoMarking => false;
        public virtual bool GetDefaultCollision(SegmentEndData segmentEnd) => !segmentEnd.IsTrack;
        public virtual bool DefaultForceNodeLess => false;
        public virtual bool DefaultSlopeJunction => Settings.NodeIsSlopedByDefault;
        public virtual float DefaultStretch => 1f;

        public virtual bool IsMoveable => false;

        public bool IsDefault
        {
            get
            {
                if (Mathf.Abs(GetShift() - DefaultShift) > 0.001f)
                    return false;

                else if (Mathf.Abs(GetRotate() - DefaultRotate) > 0.1f)
                    return false;

                else if (Mathf.Abs(GetSlope() - DefaultSlope) > 0.1f)
                    return false;

                else if (Mathf.Abs(GetTwist() - DefaultTwist) > 0.1f)
                    return false;

                else if (Mathf.Abs(GetStretch() - DefaultStretch) > 0.1f)
                    return false;

                else if (GetNoMarkings() != DefaultNoMarking)
                    return false;

                else if (!Data.SegmentEndDatas.Any(s => s.Collision == GetDefaultCollision(s)))
                    return false;

                else if (GetIsSlopeJunctions() != DefaultSlopeJunction)
                    return false;

                else if (Data.ForceNodeLess != DefaultForceNodeLess)
                    return false;

                else
                    return true;
            }
        }

        public NodeData Data { get; }
        public IEnumerable<SegmentEndData> TouchableDatas => GetDatas(TouchablePredicate);
        public IEnumerable<SegmentEndData> IsRoadDatas => GetDatas(IsRoadPredicate);

        public NodeStyle(NodeData data)
        {
            Data = data;
        }

        private IEnumerable<SegmentEndData> GetDatas(Func<SegmentEndData, bool> predicate) => Data.SegmentEndDatas.Where(predicate);

        public virtual float GetOffset() => TouchableDatas.AverageOrDefault(s => s.Offset, DefaultOffset);
        public virtual void SetOffset(float value)
        {
            foreach (var segmentData in TouchableDatas)
            {
                if (AllowOffsetPredicate(segmentData))
                    segmentData.Offset = value;
            }
        }

        public virtual float GetShift()
        {
            if (Data.IsTwoRoads)
            {
                var first = Data.FirstMainSegmentEnd;
                var second = Data.SecondMainSegmentEnd;

                if (first.IsUntouchable)
                    return second.IsUntouchable ? 0f : second.Shift;
                else if (second.IsUntouchable)
                    return first.IsUntouchable ? 0f : -first.Shift;
                else
                    return (first.Shift - second.Shift) / 2;
            }
            else
                return TouchableDatas.AverageOrDefault(s => s.Shift, DefaultShift);
        }
        public virtual void SetShift(float value)
        {
            if (Data.IsTwoRoads)
            {
                var first = Data.FirstMainSegmentEnd;
                var second = Data.SecondMainSegmentEnd;
                var firstAllow = AllowShiftPredicate(first);
                var secondAllow = AllowShiftPredicate(second);

                if (firstAllow && secondAllow)
                {
                    first.Shift = value;
                    second.Shift = -value;
                }
                else if (firstAllow)
                    first.Shift = value;
                else if (secondAllow)
                    second.Shift = -value;
            }
            else
            {
                foreach (var segmentData in TouchableDatas)
                {
                    if (AllowShiftPredicate(segmentData))
                        segmentData.Shift = value;
                }
            }
        }

        public virtual float GetRotate() => TouchableDatas.AverageOrDefault(s => s.RotateAngle, DefaultRotate);
        public virtual void SetRotate(float value)
        {
            foreach (var segmentData in TouchableDatas)
            {
                if (AllowRotatePredicate(segmentData))
                    segmentData.RotateAngle = value;
            }
        }

        public virtual float GetSlope() => TouchableDatas.AverageOrDefault(s => s.SlopeAngle, DefaultSlope);
        public virtual void SetSlope(float value)
        {
            foreach (var segmentData in TouchableDatas)
                segmentData.SlopeAngle = value;
        }

        public virtual float GetTwist() => TouchableDatas.AverageOrDefault(s => s.TwistAngle, DefaultTwist);
        public virtual void SetTwist(float value)
        {
            foreach (var segmentData in TouchableDatas)
                segmentData.TwistAngle = value;
        }

        public virtual float GetStretch() => TouchableDatas.AverageOrDefault(s => s.Stretch, DefaultStretch);
        public virtual void SetStretch(float value)
        {
            foreach (var segmentData in TouchableDatas)
                segmentData.Stretch = value;
        }

        public virtual bool? GetNoMarkings()
        {
            if (IsRoadDatas.All(s => s.NoMarkings == true))
                return true;
            else if (IsRoadDatas.All(s => s.NoMarkings == false))
                return false;
            else
                return null;
        }
        public virtual void SetNoMarkings(bool? value)
        {
            if (value != null)
            {
                foreach (var segmentData in IsRoadDatas)
                    segmentData.NoMarkings = value;
            }
        }
        public virtual bool? GetCollision()
        {
            if (TouchableDatas.All(s => s.Collision == true))
                return true;
            else if (TouchableDatas.All(s => s.Collision == false))
                return false;
            else
                return null;
        }
        public virtual void SetCollision(bool? value)
        {
            if (value != null)
            {
                foreach (var segmentData in TouchableDatas)
                    segmentData.Collision = value;
            }
        }
        public virtual bool? GetForceNodeLess()
        {
            if (GetDatas(AllowNodeLessPredicate).All(s => s.ForceNodeLess == true))
                return true;
            else if (GetDatas(AllowNodeLessPredicate).All(s => s.ForceNodeLess == false))
                return false;
            else
                return null;
        }
        public virtual void SetForceNodeLess(bool? value)
        {
            if (value != null)
            {
                foreach (var segmentData in GetDatas(AllowNodeLessPredicate))
                    segmentData.ForceNodeLess = value;
            }
        }

        public virtual bool GetIsSlopeJunctions() => TouchableDatas.Any(s => s.IsSlope);
        public virtual void SetIsSlopeJunctions(bool value)
        {
            foreach (var segmentData in TouchableDatas)
                segmentData.IsSlope = value;
        }

        #region UICOMPONENTS

        public List<EditorItem> GetUIComponents(UIComponent parent)
        {
            var components = new List<EditorItem>();
            var optionPanels = new Dictionary<Options, EditorPropertyPanel>();
            var totalSupport = TotalSupport;

            var junctionStyle = GetJunctionButtons(parent);
            if (GetMainRoadButtons(parent) is EditorPropertyPanel mainRoad)
                optionPanels.Add(Options.MainRoad, mainRoad);

            if (totalSupport == SupportOption.All)
            {
                var space = ComponentPool.Get<SpacePanel>(parent);
                space.Init(20f);
                components.Add(space);

                var titles = ComponentPool.Get<TextOptionPanel>(parent);
                titles.Init(Data, SupportOption.All, SupportOption.All);
                components.Add(titles);
            }

            foreach (var option in EnumExtension.GetEnumValues<Options>(i => true))
            {
                if (Settings.GetOptionVisibility(option) == OptionVisibility.Visible)
                {
                    if(GetOptionPanel(parent, option, totalSupport) is EditorPropertyPanel optionPanel)
                        optionPanels.Add(option, optionPanel);
                }    
            }

            bool hiddenExist = false;
            foreach (var option in EnumExtension.GetEnumValues<Options>(i => true))
            {
                if (Settings.GetOptionVisibility(option) == OptionVisibility.Hidden)
                {
                    if (GetOptionPanel(parent, option, totalSupport) is EditorPropertyPanel optionPanel)
                    {
                        optionPanels.Add(option, optionPanel);
                        hiddenExist = true;
                    }
                }
            }

            foreach(var optionPanel in optionPanels.Values)
            {
                components.Add(optionPanel);
            }


            if (optionPanels.TryGetValue(Options.Offset, out var offset) && offset is IOptionPanel offsetPanel)
            {
                if (optionPanels.TryGetValue(Options.Shift, out var shift) && shift is FloatOptionPanel shiftPanel)
                    shiftPanel.OnChanged += (_, _) => offsetPanel.Refresh();

                if (optionPanels.TryGetValue(Options.Nodeless, out var nodeless) && nodeless is BoolOptionPanel nodelessPanel)
                    nodelessPanel.OnChanged += (_, _) => offsetPanel.Refresh();
            }

            if (optionPanels.TryGetValue(Options.Rotate, out var rotate) && rotate is IOptionPanel rotatePanel)
            {
                if (optionPanels.TryGetValue(Options.Shift, out var shift) && shift is FloatOptionPanel shiftPanel)
                    shiftPanel.OnChanged += (_, _) => rotatePanel.Refresh();

                if (optionPanels.TryGetValue(Options.Nodeless, out var nodeless) && nodeless is BoolOptionPanel nodelessPanel)
                    nodelessPanel.OnChanged += (_, _) => rotatePanel.Refresh();
            }

            var moreOptionsButton = ComponentPool.Get<ButtonPanel>(parent);
            components.Add(moreOptionsButton);
            moreOptionsButton.Text = $"▼ {Localize.Option_MoreOptions} ▼";
            moreOptionsButton.Init();
            moreOptionsButton.isVisible = hiddenExist;
            moreOptionsButton.OnButtonClick += () =>
            {
                moreOptionsButton.isVisible = false;
                UpdateVisible(optionPanels, !moreOptionsButton.isVisibleSelf);
            };

            if (junctionStyle != null)
            {
                components.Add(junctionStyle);
                junctionStyle.OnSelectObjectChanged += (_) => UpdateVisible(optionPanels, !moreOptionsButton.isVisibleSelf);
            }

            UpdateVisible(optionPanels, !moreOptionsButton.isVisibleSelf);

            return components;

            
        }
        private void UpdateVisible(Dictionary<Options, EditorPropertyPanel> optionPanels, bool showHidden)
        {
            foreach (var option in EnumExtension.GetEnumValues<Options>(i => true))
            {
                if (optionPanels.TryGetValue(option, out var optionPanel))
                {
                    bool isVisible = optionPanel.isVisibleSelf;
                    if (isVisible != IsVisible(option, showHidden))
                    {
                        optionPanel.isVisible = !isVisible;
                        if (!isVisible && optionPanel is IOptionPanel refreshPanel)
                            refreshPanel.Refresh();
                    }
                }
            }
        }
        private bool IsVisible(Options option, bool showHidden)
        {
            var visibility = Settings.GetOptionVisibility(option);
            var visible = visibility == OptionVisibility.Visible || (visibility == OptionVisibility.Hidden && showHidden);

            switch (option)
            {
                case Options.Slope:
                case Options.Twist:
                    return visible && Data.IsSlopeJunctions;
                case Options.MainRoad:
                    return Data.IsSlopeJunctions;
                default:
                    return visible;
            }
        }

        private EditorPropertyPanel GetOptionPanel(UIComponent parent, Options option, SupportOption support) => option switch
        {
            Options.Offset => GetOffsetOption(parent, support),
            Options.Rotate => GetRotateOption(parent, support),
            Options.Shift => GetShiftOption(parent, support),
            Options.Slope => GetSlopeOption(parent, support),
            Options.Twist => GetTwistOption(parent, support),
            Options.Stretch => GetStretchOption(parent, support),
            Options.Marking => GetMarkingsOption(parent, support),
            Options.Collision => GetCollisionOption(parent, support),
            Options.Nodeless => GetForceNodeLessOption(parent, support),
            _ => null,
        };

        private BoolListPropertyPanel GetJunctionButtons(UIComponent parent)
        {
            if (SupportSlopeJunction != SupportOption.None)
            {
                var flatJunctionProperty = ComponentPool.Get<BoolListPropertyPanel>(parent);
                flatJunctionProperty.Text = Localize.Option_Style;
                flatJunctionProperty.Init(Localize.Option_StyleFlat, Localize.Option_StyleSlope, false);
                flatJunctionProperty.SelectedObject = Data.IsSlopeJunctions;
                flatJunctionProperty.OnSelectObjectChanged += (value) =>
                    {
                        Data.IsSlopeJunctions = value;
                        Data.UpdateNode();
                    };

                return flatJunctionProperty;
            }
            else
                return null;
        }
        private BoolListPropertyPanel GetMainRoadButtons(UIComponent parent)
        {
            if (Data.IsJunction && !Data.IsDecoration)
            {
                var mainRoadProperty = ComponentPool.Get<BoolListPropertyPanel>(parent);
                mainRoadProperty.Text = Localize.Option_MainSlopeDirection;
                mainRoadProperty.Init(Localize.Option_MainSlopeDirectionManually, Localize.Option_MainSlopeDirectionAuto);
                mainRoadProperty.SelectedObject = Data.MainRoad.Auto;
                mainRoadProperty.OnSelectObjectChanged += (value) =>
                {
                    Data.MainRoad.Auto = value;
                    Data.UpdateNode();
                };

                return mainRoadProperty;
            }
            else
                return null;
        }
        private FloatOptionPanel GetOffsetOption(UIComponent parent, SupportOption totalSupport)
        {
            if (SupportOffset != SupportOption.None && Data.SegmentEndDatas.Any(s => AllowOffsetPredicate(s)))
            {
                var offset = ComponentPool.Get<FloatOptionPanel>(parent);
                offset.Text = Localize.Option_Offset;
                offset.Format = Localize.Option_OffsetFormat;
                offset.NumberFormat = "0.##";
                offset.Init(Data, SupportOffset, totalSupport, OffsetGetter, OffsetSetter, MinMaxOffset, HasNodePredicate);

                return offset;
            }
            else
                return null;
        }
        private FloatOptionPanel GetShiftOption(UIComponent parent, SupportOption totalSupport)
        {
            if (SupportShift != SupportOption.None && Data.SegmentEndDatas.Any(s => AllowShiftPredicate(s)))
            {
                var shift = ComponentPool.Get<FloatOptionPanel>(parent);
                shift.Text = Localize.Option_Shift;
                shift.Format = Localize.Option_ShiftFormat;
                shift.NumberFormat = "0.##";
                shift.Init(Data, SupportShift, totalSupport, ShiftGetter, ShiftSetter, MinMaxShift, AllowShiftPredicate);

                return shift;
            }
            else
                return null;
        }
        private FloatOptionPanel GetRotateOption(UIComponent parent, SupportOption totalSupport)
        {
            if (SupportRotate != SupportOption.None && Data.SegmentEndDatas.Any(s => AllowRotatePredicate(s)))
            {
                var rotate = ComponentPool.Get<FloatOptionPanel>(parent);
                rotate.Text = Localize.Option_Rotate;
                rotate.Format = Localize.Option_RotateFormat;
                rotate.NumberFormat = "0.#";
                rotate.Init(Data, SupportRotate, totalSupport, RotateGetter, RotateSetter, MinMaxRotate, HasNodePredicate);

                return rotate;
            }
            else
                return null;
        }
        private FloatOptionPanel GetStretchOption(UIComponent parent, SupportOption totalSupport)
        {
            if (SupportStretch != SupportOption.None && Data.SegmentEndDatas.Any(s => TouchablePredicate(s)))
            {
                var stretch = ComponentPool.Get<FloatOptionPanel>(parent);
                stretch.Text = Localize.Option_Stretch;
                stretch.Format = Localize.Option_StretchFormat;
                stretch.NumberFormat = "0.#";
                stretch.Init(Data, SupportStretch, totalSupport, StretchGetter, StretchSetter, MinMaxStretch, TouchablePredicate);

                return stretch;
            }
            else
                return null;
        }
        private FloatOptionPanel GetSlopeOption(UIComponent parent, SupportOption totalSupport)
        {
            if (SupportSlope != SupportOption.None && OnlyOnSlope)
            {
                var slope = ComponentPool.Get<FloatOptionPanel>(parent);
                slope.Text = Localize.Option_Slope;
                slope.Format = Localize.Option_SlopeFormat;
                slope.NumberFormat = "0.#";
                slope.Init(Data, SupportSlope, totalSupport, SlopeGetter, SlopeSetter, MinMaxSlope, MainRoadPredicate);

                return slope;
            }
            else
                return null;
        }
        private FloatOptionPanel GetTwistOption(UIComponent parent, SupportOption totalSupport)
        {
            if (SupportTwist != SupportOption.None && OnlyOnSlope)
            {
                var twist = ComponentPool.Get<FloatOptionPanel>(parent);
                twist.Text = Localize.Option_Twist;
                twist.Format = Localize.Option_TwistFormat;
                twist.NumberFormat = "0.#";
                twist.Init(Data, SupportTwist, totalSupport, TwistGetter, TwistSetter, MinMaxTwist, MainRoadPredicate);

                return twist;
            }
            else
                return null;
        }
        private BoolOptionPanel GetMarkingsOption(UIComponent parent, SupportOption totalSupport)
        {
            if (SupportMarking != SupportOption.None && Data.SegmentEndDatas.Any(s => IsRoadPredicate(s)) && HideCrosswalksEnable)
            {
                var hideMarking = ComponentPool.Get<BoolOptionPanel>(parent);
                hideMarking.Text = Localize.Option_Marking;
                hideMarking.Init(Data, SupportMarking, totalSupport, MarkingsGetter, MarkingsSetter, IsRoadPredicate);

                return hideMarking;
            }
            else
                return null;
        }
        private BoolOptionPanel GetCollisionOption(UIComponent parent, SupportOption totalSupport)
        {
            if (SupportCollision != SupportOption.None && Data.SegmentEndDatas.Any(s => TouchablePredicate(s)))
            {
                var collision = ComponentPool.Get<BoolOptionPanel>(parent);
                collision.Text = Localize.Option_Collision;
                collision.Init(Data, SupportCollision, totalSupport, CollisionGetter, CollisionSetter, TouchablePredicate);

                return collision;
            }
            else
                return null;
        }
        private BoolOptionPanel GetForceNodeLessOption(UIComponent parent, SupportOption totalSupport)
        {
            if (SupportForceNodeless != SupportOption.None && Data.SegmentEndDatas.Any(s => TouchablePredicate(s)))
            {
                var forceNodeLess = ComponentPool.Get<BoolOptionPanel>(parent);
                forceNodeLess.Text = Localize.Option_NodeLess;
                forceNodeLess.Init(Data, SupportForceNodeless, totalSupport, ForceNodeLessGetter, ForceNodeLessSetter, AllowNodeLessPredicate);

                return forceNodeLess;
            }
            else
                return null;
        }

        private void MinMaxOffset(INetworkData data, out float min, out float max)
        {
            if (data is SegmentEndData segmentEnd)
            {
                min = segmentEnd.MinOffset;
                max = segmentEnd.MaxOffset;
            }
            else if(data is NodeData nodeData)
            {
                min = nodeData.SegmentEndDatas.Min(s => s.MinOffset);
                max = nodeData.SegmentEndDatas.Max(s => s.MaxOffset);
            }
            else
            {
                min = MinOffset;
                max = MaxOffset;
            }
        }
        private void MinMaxShift(INetworkData data, out float min, out float max)
        {
            min = MinShift;
            max = MaxShift;
        }
        private void MinMaxRotate(INetworkData data, out float min, out float max)
        {
            if (data is SegmentEndData segmentEnd)
            {
                min = segmentEnd.MinRotate;
                max = segmentEnd.MaxRotate;
            }
            else if (data is NodeData nodeData)
            {
                min = nodeData.SegmentEndDatas.Min(s => s.MinRotate);
                max = nodeData.SegmentEndDatas.Max(s => s.MaxRotate);
            }
            else
            {
                min = MinRotate;
                max = MaxRotate;
            }
        }
        private bool IsNotDecoration(INetworkData data) => (data is SegmentEndData endData && !IsDecorationPredicate(endData)) || (data is NodeData nodeData && !nodeData.SegmentEndDatas.Any(IsDecorationPredicate));
        private void MinMaxStretch(INetworkData data, out float min, out float max)
        {
            min = MinStretch;

            if(!IsNotDecoration(data))
                max = MaxStretch;
            else if (data is SegmentEndData endData)
                max = Math.Max(4000f / endData.Id.GetSegment().Info.m_halfWidth, MaxRoadStretch);
            else
                max = MaxRoadStretch;
        }
        private void MinMaxSlope(INetworkData data, out float min, out float max)
        {
            if (IsNotDecoration(data))
            {
                min = MinRoadSlope;
                max = MaxRoadSlope;
            }
            else
            {
                min = MinSlope;
                max = MaxSlope;
            }
        }
        private void MinMaxTwist(INetworkData data, out float min, out float max)
        {
            if (IsNotDecoration(data))
            {
                min = MinRoadTwist;
                max = MaxRoadTwist;
            }
            else
            {
                min = MinTwist;
                max = MaxTwist;
            }
        }

        private static void OffsetSetter(INetworkData data, float value) => data.Offset = value;
        private static void ShiftSetter(INetworkData data, float value) => data.Shift = value;
        private static void RotateSetter(INetworkData data, float value) => data.RotateAngle = value;
        private static void SlopeSetter(INetworkData data, float value) => data.SlopeAngle = value;
        private static void TwistSetter(INetworkData data, float value) => data.TwistAngle = value;
        private static void StretchSetter(INetworkData data, float value) => data.StretchPercent = value;
        private static void MarkingsSetter(INetworkData data, bool? value) => data.NoMarkings = value == null ? null : !value.Value;
        private static void CollisionSetter(INetworkData data, bool? value) => data.Collision = value;
        private static void ForceNodeLessSetter(INetworkData data, bool? value) => data.ForceNodeLess = value;

        private static float OffsetGetter(INetworkData data) => data.Offset;
        private static float ShiftGetter(INetworkData data) => data.Shift;
        private static float RotateGetter(INetworkData data) => data.RotateAngle;
        private static float SlopeGetter(INetworkData data) => data.SlopeAngle;
        private static float TwistGetter(INetworkData data) => data.TwistAngle;
        private static float StretchGetter(INetworkData data) => data.StretchPercent;
        private static bool? MarkingsGetter(INetworkData data)
        {
            var value = data.NoMarkings;
            return value == null ? null : !value.Value;
        }
        private static bool? CollisionGetter(INetworkData data) => data.Collision;
        private static bool? ForceNodeLessGetter(INetworkData data) => data.ForceNodeLess;

        protected static bool HasNodePredicate(SegmentEndData data) => TouchablePredicate(data) && !data.FinalNodeLess;
        protected static bool TouchablePredicate(SegmentEndData data) => !data.IsUntouchable;
        protected static bool IsRoadPredicate(SegmentEndData data) => data.IsRoad;
        protected static bool IsTrackPredicate(SegmentEndData data) => data.IsTrack;
        protected static bool IsPathPredicate(SegmentEndData data) => data.IsPath;
        protected static bool IsDecorationPredicate(SegmentEndData data) => data.IsDecoration;

        protected static bool AllowOffsetPredicate(SegmentEndData data) => TouchablePredicate(data);
        protected static bool AllowShiftPredicate(SegmentEndData data) => TouchablePredicate(data) && !IsDecorationPredicate(data);
        protected static bool AllowRotatePredicate(SegmentEndData data) => TouchablePredicate(data);
        protected static bool MainRoadPredicate(SegmentEndData data) => TouchablePredicate(data) && (data.IsMainRoad || data.IsDecoration);
        protected static bool AllowNodeLessPredicate(SegmentEndData data) => TouchablePredicate(data) && !data.IsNodeLess;

        #endregion
    }

    public class MiddleNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.Middle;
        public override SupportOption SupportSlope => Data.SegmentEndDatas.All(TouchablePredicate) ? SupportOption.All : SupportOption.None;
        private SupportOption Support
        {
            get
            {
                if (!Data.SegmentEndDatas.All(TouchablePredicate))
                    return SupportOption.None;
                else if (Data.SegmentEndDatas.Any(IsDecorationPredicate))
                    return SupportOption.All;
                else
                    return SupportOption.Group;
            }
        }
        public override SupportOption SupportTwist => Support;
        public override SupportOption SupportShift => Support;
        public override SupportOption SupportStretch => Support;

        public override bool DefaultSlopeJunction => true;
        public override bool NeedFixDirection => false;

        public MiddleNode(NodeData data) : base(data) { }

        public override float GetSlope() => (Data.FirstMainSegmentEnd.SlopeAngle - Data.SecondMainSegmentEnd.SlopeAngle) / 2;
        public override void SetSlope(float value)
        {
            Data.FirstMainSegmentEnd.SlopeAngle = value;
            Data.SecondMainSegmentEnd.SlopeAngle = -value;
        }

        public override float GetTwist() => (Data.FirstMainSegmentEnd.TwistAngle - Data.SecondMainSegmentEnd.TwistAngle) / 2;
        public override void SetTwist(float value)
        {
            Data.FirstMainSegmentEnd.TwistAngle = value;
            Data.SecondMainSegmentEnd.TwistAngle = -value;
        }

        public override float GetStretch() => (Data.FirstMainSegmentEnd.Stretch + Data.SecondMainSegmentEnd.Stretch) / 2;
        public override void SetStretch(float value)
        {
            Data.FirstMainSegmentEnd.Stretch = value;
            Data.SecondMainSegmentEnd.Stretch = value;
        }
    }
    public class BendNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.Bend;

        public override float DefaultOffset => 8f;
        public override float AdditionalOffset => 2f;

        public override SupportOption SupportOffset => SupportOption.All;
        public override SupportOption SupportRotate => SupportOption.All;
        public override SupportOption SupportTwist => SupportOption.All;
        public override SupportOption SupportShift => SupportOption.All;
        public override SupportOption SupportStretch => SupportOption.All;
        public override SupportOption SupportSlopeJunction => SupportOption.Group;
        public override bool IsMoveable => true;

        public BendNode(NodeData data) : base(data) { }

        public override float GetTwist() => (Data.FirstMainSegmentEnd.TwistAngle - Data.SecondMainSegmentEnd.TwistAngle) / 2;
        public override void SetTwist(float value)
        {
            Data.FirstMainSegmentEnd.TwistAngle = value;
            Data.SecondMainSegmentEnd.TwistAngle = -value;
        }
    }
    public class StretchNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.Stretch;

        public override float DefaultOffset => 8f;
        public override float AdditionalOffset => 2f;

        public override SupportOption SupportOffset => SupportOption.All;
        public override SupportOption SupportRotate => SupportOption.All;
        public override SupportOption SupportTwist => SupportOption.All;
        public override SupportOption SupportShift => SupportOption.All;
        public override SupportOption SupportStretch => SupportOption.All;
        public override SupportOption SupportSlopeJunction => SupportOption.Group;
        public override bool IsMoveable => true;

        public StretchNode(NodeData data) : base(data) { }

        public override float GetTwist() => (Data.FirstMainSegmentEnd.TwistAngle - Data.SecondMainSegmentEnd.TwistAngle) / 2;
        public override void SetTwist(float value)
        {
            Data.FirstMainSegmentEnd.TwistAngle = value;
            Data.SecondMainSegmentEnd.TwistAngle = -value;
        }
    }
    public class CrossingNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.Crossing;
        public override float DefaultOffset => 2f;

        public override SupportOption SupportShift => SupportOption.Group;
        public override SupportOption SupportTwist => SupportOption.Group;
        public override SupportOption SupportStretch => SupportOption.Group;
        public override SupportOption SupportMarking => SupportOption.All;
        public override SupportOption SupportSlopeJunction => SupportOption.Group;
        public override bool OnlyKeepDefault => true;
        public override bool SupportTrafficLights => true;
        public override bool NeedFixDirection => false;

        public CrossingNode(NodeData data) : base(data) { }

        public override float GetTwist() => (Data.FirstMainSegmentEnd.TwistAngle - Data.SecondMainSegmentEnd.TwistAngle) / 2;
        public override void SetTwist(float value)
        {
            Data.FirstMainSegmentEnd.TwistAngle = value;
            Data.SecondMainSegmentEnd.TwistAngle = -value;
        }
    }
    public class UTurnNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.UTurn;
        public override float DefaultOffset => 8f;
        public override bool DefaultNoMarking => true;

        public override SupportOption SupportShift => SupportOption.Group;
        public override SupportOption SupportTwist => SupportOption.Group;
        public override SupportOption SupportStretch => SupportOption.Group;
        public override SupportOption SupportMarking => SupportOption.All;
        public override SupportOption SupportSlopeJunction => SupportOption.Group;
        public override bool SupportTrafficLights => true;
        public override bool OnlyKeepDefault => true;
        public override bool NeedFixDirection => false;

        public UTurnNode(NodeData data) : base(data) { }

        public override float GetTwist() => (Data.FirstMainSegmentEnd.TwistAngle - Data.SecondMainSegmentEnd.TwistAngle) / 2;
        public override void SetTwist(float value)
        {
            Data.FirstMainSegmentEnd.TwistAngle = value;
            Data.SecondMainSegmentEnd.TwistAngle = -value;
        }
    }
    public class EndNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.End;

        public override SupportOption SupportOffset => SupportOption.None;
        public override SupportOption SupportRotate => SupportOption.Group;
        public override SupportOption SupportShift => SupportOption.Group;
        public override SupportOption SupportSlope => SupportOption.Group;
        public override SupportOption SupportTwist => SupportOption.Group;
        public override SupportOption SupportStretch => SupportOption.Group;
        public override SupportOption SupportSlopeJunction => SupportOption.Group;
        public override bool SupportTrafficLights => true;
        public override bool IsMoveable => true;

        public EndNode(NodeData data) : base(data) { }
    }
    public class CustomNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.Custom;

        public override float DefaultOffset => 8f;
        public override float AdditionalOffset => 2f;

        public override SupportOption SupportOffset => SupportOption.All;
        public override SupportOption SupportShift => SupportOption.All;
        public override SupportOption SupportRotate => SupportOption.All;
        public override SupportOption SupportSlope => SupportOption.All;
        public override SupportOption SupportTwist => SupportOption.All;
        public override SupportOption SupportStretch => SupportOption.All;
        public override SupportOption SupportMarking => SupportOption.All;
        public override SupportOption SupportSlopeJunction => SupportOption.Group;
        public override SupportOption SupportCollision => SupportOption.All;
        public override SupportOption SupportForceNodeless => SupportOption.All;
        public override bool IsMoveable => true;
        public override bool SupportTrafficLights => true;

        public CustomNode(NodeData data) : base(data) { }

        public override float GetTwist()
        {
            if (Data.IsDecoration)
                return base.GetTwist();
            else
            {
                var first = Data.FirstMainSegmentEnd;
                var second = Data.SecondMainSegmentEnd;

                if (first.IsUntouchable)
                    return second.IsUntouchable ? 0f : second.TwistAngle;
                else if (second.IsUntouchable)
                    return first.IsUntouchable ? 0f : first.TwistAngle;
                else
                    return (first.TwistAngle - second.TwistAngle) / 2;
            }
        }
        public override void SetTwist(float value)
        {
            if (Data.IsDecoration)
                base.SetTwist(value);
            else
            {
                var first = Data.FirstMainSegmentEnd;
                var second = Data.SecondMainSegmentEnd;

                if (!first.IsUntouchable && !second.IsUntouchable)
                {
                    first.TwistAngle = value;
                    second.TwistAngle = -value;
                }
                else
                {
                    if (!first.IsUntouchable)
                        first.TwistAngle = value;
                    if (!second.IsUntouchable)
                        second.TwistAngle = value;
                }
            }
        }
    }

    [Flags]
    public enum SupportOption
    {
        None = 0,
        Individually = 1,
        Group = 2,
        All = Individually | Group,
    }

    [Flags]
    public enum Options
    {
        [NotVisible]
        [Description(nameof(Localize.Option_MainSlopeDirection))]
        MainRoad = 1 << 0,

        [NotVisible]
        [Description(nameof(Localize.Option_Offset))]
        Offset = 1 << 1,

        [NotVisible]
        [Description(nameof(Localize.Option_Rotate))]
        Rotate = 1 << 2,

        [NotVisible]
        [Description(nameof(Localize.Option_Shift))]
        Shift = 1 << 3,

        [Description(nameof(Localize.Option_Slope))]
        Slope = 1 << 4,

        [Description(nameof(Localize.Option_Twist))]
        Twist = 1 << 5,

        [Description(nameof(Localize.Option_Stretch))]
        Stretch = 1 << 6,

        [Description(nameof(Localize.Option_Marking))]
        Marking = 1 << 7,

        [Description(nameof(Localize.Option_Collision))]
        Collision = 1 << 8,

        [Description(nameof(Localize.Option_NodeLess))]
        Nodeless = 1 << 9,
    }

    public enum OptionVisibility
    {
        [Description(nameof(Localize.Settings_Option_Visible))]
        Visible,

        [Description(nameof(Localize.Settings_Option_Hidden))]
        Hidden,

        [Description(nameof(Localize.Settings_Option_Disabled))]
        Disabled,
    }
}
