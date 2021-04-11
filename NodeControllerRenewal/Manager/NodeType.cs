﻿using ColossalFramework.UI;
using ModsCommon.UI;
using ModsCommon.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NodeController
{
    public enum NodeStyleType
    {
        Middle,
        Bend,
        Stretch,
        Crossing, // change dataMatrix.w to render crossings in the middle.
        UTurn, // set offset to 5.
        Custom,
        End,
    }
    public abstract class NodeStyle
    {
        public abstract NodeStyleType Type { get; }

        public virtual bool ResetOffset => false;
        public virtual float DefaultOffset => 0f;

        public virtual bool ResetShift => false;
        public virtual float DefaultShift => 0f;

        public virtual bool ResetRotate => false;
        public virtual float DefaultRotate => 0f;

        public virtual bool ResetSlope => false;
        public virtual float DefaultSlope => 0f;

        public virtual bool ResetTwist => false;
        public virtual float DefaultTwist => 0f;

        public virtual bool ResetNoMarking => false;
        public virtual bool DefaultNoMarking => false;

        public virtual bool ResetFlatJunction => false;
        public virtual bool DefaultFlatJunction => true;

        public bool IsDefault
        {
            get
            {
                if (Mathf.Abs(Data.Offset - DefaultOffset) > 0.001f)
                    return false;
                else if (Mathf.Abs(Data.Shift - DefaultShift) > 0.001f)
                    return false;
                else if (Mathf.Abs(Data.RotateAngle - DefaultRotate) > 0.001f)
                    return false;
                else if (Mathf.Abs(Data.SlopeAngle - DefaultSlope) > 0.001f)
                    return false;
                else if (Mathf.Abs(Data.TwistAngle - DefaultTwist) > 0.001f)
                    return false;
                else if (Data.NoMarkings != DefaultNoMarking)
                    return false;
                else if (Data.IsFlatJunctions != DefaultFlatJunction)
                    return false;
                else
                    return true;
            }
        }

        public NodeData Data { get; }

        public NodeStyle(NodeData data)
        {
            Data = data;
        }

        public virtual void GetUIComponents(UIComponent parent, Action refresh) { }

        protected void GetOffsetUIComponents(UIComponent parent) => GetUIComponents(parent, GetOffsetProperty, GetSegmentOffsetProperty, (data) => data.Offset, (data, value) => data.Offset = value);
        protected void GetShiftUIComponents(UIComponent parent) => GetUIComponents(parent, GetShiftProperty, GetSegmentShiftProperty, (data) => data.Shift, (data, value) => data.Shift = value);
        protected void GetRotateUIComponents(UIComponent parent) => GetUIComponents(parent, GetRotateProperty, GetSegmentRotateProperty, (data) => data.RotateAngle, (data, value) => data.RotateAngle = value);
        protected void GetSlopeUIComponents(UIComponent parent) => GetUIComponents(parent, GetSlopeProperty, null, (data) => data.SlopeAngle, (data, value) => data.SlopeAngle = value);
        protected void GetTwistUIComponents(UIComponent parent) => GetUIComponents(parent, GetTwistProperty, null, (data) => data.TwistAngle, (data, value) => data.TwistAngle = value);
        protected void GetUIComponents(UIComponent parent, Func<UIComponent, FloatPropertyPanel> getNodeProperty, Func<UIComponent, SegmentEndData, FloatPropertyPanel> getSegmentProperty, Func<INetworkData, float> getValue, Action<INetworkData, float> setValue)
        {
            var nodeProperty = getNodeProperty(parent);
            nodeProperty.Value = getValue(Data);

            var segmentProperties = new List<FloatPropertyPanel>();
            if (getSegmentProperty != null)
            {
                foreach (var segmentData in Data.SegmentEndDatas)
                {
                    var segmentProperty = getSegmentProperty(parent, segmentData);
                    segmentProperty.Value = getValue(segmentData);
                    segmentProperty.OnValueChanged += (newValue) =>
                    {
                        setValue(segmentData, newValue);
                        nodeProperty.Value = getValue(Data);
                        Data.UpdateNode();
                    };
                    segmentProperties.Add(segmentProperty);
                }
            }

            nodeProperty.OnValueChanged += (float newValue) =>
            {
                setValue(Data, newValue);
                foreach (var segmentProperty in segmentProperties)
                    segmentProperty.Value = newValue;
            };
        }

        protected FloatPropertyPanel GetOffsetProperty(UIComponent parent)
        {
            var offsetProperty = GetNodeProperty(parent, "Offset");
            offsetProperty.MinValue = 0;
            offsetProperty.MaxValue = 100;

            return offsetProperty;
        }
        protected FloatPropertyPanel GetShiftProperty(UIComponent parent)
        {
            var offsetProperty = GetNodeProperty(parent, "Shift");
            offsetProperty.MinValue = -32;
            offsetProperty.MaxValue = 32;

            return offsetProperty;
        }
        protected FloatPropertyPanel GetRotateProperty(UIComponent parent)
        {
            var rotateProperty = GetNodeProperty(parent, "Rotate");
            rotateProperty.MinValue = -60;
            rotateProperty.MaxValue = 60;

            return rotateProperty;
        }
        protected FloatPropertyPanel GetSlopeProperty(UIComponent parent)
        {
            var slopeProperty = GetNodeProperty(parent, "Slope");
            slopeProperty.MinValue = -60;
            slopeProperty.MaxValue = 60;

            return slopeProperty;
        }
        protected FloatPropertyPanel GetTwistProperty(UIComponent parent)
        {
            var twistProperty = GetNodeProperty(parent, "Twist");
            twistProperty.MinValue = -60;
            twistProperty.MaxValue = 60;

            return twistProperty;
        }

        private FloatPropertyPanel GetNodeProperty(UIComponent parent, string name)
        {
            var property = ComponentPool.Get<FloatPropertyPanel>(parent, name);
            property.Text = name;
            property.CheckMin = true;
            property.CheckMax = true;
            property.UseWheel = true;
            property.WheelStep = 1f;
            property.Init();

            return property;
        }

        protected FloatPropertyPanel GetSegmentOffsetProperty(UIComponent parent, SegmentEndData segmentData)
        {
            var offsetProperty = GetSegmentProperty(parent, $"Segment #{segmentData.Id} offset");
            offsetProperty.MinValue = 0;
            offsetProperty.MaxValue = 100;

            return offsetProperty;
        }
        protected FloatPropertyPanel GetSegmentShiftProperty(UIComponent parent, SegmentEndData segmentData)
        {
            var offsetProperty = GetSegmentProperty(parent, $"Segment #{segmentData.Id} shift");
            offsetProperty.MinValue = -32;
            offsetProperty.MaxValue = 32;

            return offsetProperty;
        }
        protected FloatPropertyPanel GetSegmentRotateProperty(UIComponent parent, SegmentEndData segmentData)
        {
            var rotateProperty = GetSegmentProperty(parent, $"Segment #{segmentData.Id} rotate");
            rotateProperty.MinValue = -60;
            rotateProperty.MaxValue = 60;

            return rotateProperty;
        }
        protected FloatPropertyPanel GetSegmentSlopeProperty(UIComponent parent, SegmentEndData segmentData)
        {
            var slopeProperty = GetSegmentProperty(parent, $"Segment #{segmentData.Id} slope");
            slopeProperty.MinValue = -60;
            slopeProperty.MaxValue = 60;

            return slopeProperty;
        }
        protected FloatPropertyPanel GetSegmentTwistProperty(UIComponent parent, SegmentEndData segmentData)
        {
            var twistProperty = GetSegmentProperty(parent, $"Segment #{segmentData.Id} twist");
            twistProperty.MinValue = -60;
            twistProperty.MaxValue = 60;

            return twistProperty;
        }
        private FloatPropertyPanel GetSegmentProperty(UIComponent parent, string name)
        {
            var property = ComponentPool.Get<FloatPropertyPanel>(parent, name);
            property.Text = name;
            property.CheckMin = true;
            property.CheckMax = true;
            property.UseWheel = true;
            property.WheelStep = 1f;
            property.Init();

            return property;
        }
        protected BoolListPropertyPanel GetActionButtons(UIComponent parent)
        {
            var flatJunctionProperty = ComponentPool.Get<BoolListPropertyPanel>(parent);
            flatJunctionProperty.Text = "Style";
            flatJunctionProperty.Init("Slope", "Flat");
            flatJunctionProperty.SelectedObject = Data.IsFlatJunctions;
            flatJunctionProperty.OnSelectObjectChanged += (value) => Data.IsFlatJunctions = value;

            return flatJunctionProperty;

            //var actionButtons = ComponentPool.Get<ButtonsPanel>(parent);
            //var slopeIndex = actionButtons.AddButton("Make slope");
            //var flatIndex = actionButtons.AddButton("Make flat");
            //actionButtons.Init();
            //actionButtons.OnButtonClick += OnButtonClick;

            //return actionButtons;

            //void OnButtonClick(int index)
            //{
            //    if (index == slopeIndex)
            //        Data.IsFlatJunctions = false;
            //    else if (index == flatIndex)
            //        Data.IsFlatJunctions = true;
            //}
        }
        protected BoolListPropertyPanel GetHideMarkingProperty(UIComponent parent)
        {
            var hideMarkingProperty = ComponentPool.Get<BoolListPropertyPanel>(parent);
            hideMarkingProperty.Text = "Hide crosswalk marking";
            hideMarkingProperty.Init("No", "Yes");
            hideMarkingProperty.SelectedObject = Data.NoMarkings;
            hideMarkingProperty.OnSelectObjectChanged += (value) => Data.NoMarkings = value;

            return hideMarkingProperty;
        }
        protected ButtonPanel GetResetButton(UIComponent parent, Action refresh)
        {
            var resetButton = ComponentPool.Get<ButtonPanel>(parent);
            resetButton.Text = "Reset to default";
            resetButton.Init();
            resetButton.OnButtonClick += () =>
            {
                Data.Refresh();
                refresh();
            };

            return resetButton;
        }
    }
    public class MiddleNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.Middle;
        public override bool ResetOffset => true;
        public override bool ResetShift => true;
        public override bool ResetRotate => true;
        public override bool ResetNoMarking => true;
        public override bool ResetFlatJunction => true;

        public MiddleNode(NodeData data) : base(data) { }

        public override void GetUIComponents(UIComponent parent, Action refresh)
        {
            GetSlopeUIComponents(parent);
            GetTwistUIComponents(parent);
            GetResetButton(parent, refresh);
        }
    }
    public class BendNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.Bend;
        public override bool ResetSlope => true;
        public override bool ResetTwist => true;

        public BendNode(NodeData data) : base(data) { }

        public override void GetUIComponents(UIComponent parent, Action refresh)
        {
            GetOffsetUIComponents(parent);
            GetShiftUIComponents(parent);
            GetRotateUIComponents(parent);
            GetResetButton(parent, refresh);
        }
    }
    public class StretchNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.Stretch;
        public override bool ResetShift => true;
        public override bool ResetSlope => true;
        public override bool ResetTwist => true;

        public StretchNode(NodeData data) : base(data) { }

        public override void GetUIComponents(UIComponent parent, Action refresh)
        {
            GetOffsetUIComponents(parent);
            GetRotateUIComponents(parent);
            GetResetButton(parent, refresh);
        }
    }
    public class CrossingNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.Crossing;
        public override bool ResetOffset => true;
        public override bool ResetShift => true;
        public override bool ResetRotate => true;
        public override bool ResetSlope => true;
        public override bool ResetTwist => true;
        public override bool ResetNoMarking => true;

        public CrossingNode(NodeData data) : base(data) { }

        public override void GetUIComponents(UIComponent parent, Action refresh)
        {
            GetActionButtons(parent);
            GetHideMarkingProperty(parent);
            GetResetButton(parent, refresh);
        }
    }
    public class UTurnNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.UTurn;
        public override bool ResetOffset => true;
        public override bool ResetShift => true;
        public override bool ResetRotate => true;
        public override bool ResetSlope => true;
        public override bool ResetTwist => true;

        public override float DefaultOffset => 8f;

        public UTurnNode(NodeData data) : base(data) { }

        public override void GetUIComponents(UIComponent parent, Action refresh)
        {
            GetActionButtons(parent);
            GetHideMarkingProperty(parent);
            GetResetButton(parent, refresh);
        }
    }
    public class EndNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.End;
        public override bool ResetOffset => true;
        public override bool ResetShift => true;
        public override bool ResetRotate => true;

        public EndNode(NodeData data) : base(data) { }

        public override void GetUIComponents(UIComponent parent, Action refresh)
        {
            GetActionButtons(parent);
            GetSlopeUIComponents(parent);
            GetTwistUIComponents(parent);
            GetResetButton(parent, refresh);
        }
    }
    public class CustomNode : NodeStyle
    {
        public override NodeStyleType Type => NodeStyleType.Custom;
        public override bool ResetRotate => true;
        public override bool ResetSlope => true;
        public override bool ResetTwist => true;

        public CustomNode(NodeData data) : base(data) { }

        public override void GetUIComponents(UIComponent parent, Action refresh)
        {
            GetActionButtons(parent);
            GetOffsetUIComponents(parent);
            GetShiftUIComponents(parent);
            GetRotateUIComponents(parent);
            GetHideMarkingProperty(parent);
            GetResetButton(parent, refresh);
        }
    }
}
