// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;

namespace TiltBrush
{
    public class StencilButton : BaseButton
    {
        [SerializeField] private StencilType m_Type;
        private bool m_PrimitivePropertyMode;
        private PrimitivePropertyAction m_PrimitivePropertyAction;

        public StencilType Type
        {
            get => m_Type;
            set => m_Type = value;
        }

        public void Configure(StencilType type, string description)
        {
            m_Type = type;
            m_LocalizedDescription = new UnityEngine.Localization.LocalizedString();
            SetDescriptionText(description);
            gameObject.name = "PanelButton_Primitive_" + type;
        }

        public void ConfigureProperty(
            PrimitivePropertyAction action, string description, string iconPath)
        {
            m_PrimitivePropertyMode = true;
            m_PrimitivePropertyAction = action;
            m_LocalizedDescription = new UnityEngine.Localization.LocalizedString();
            SetDescriptionText(description);

            Texture2D texture = Resources.Load<Texture2D>(iconPath);
            if (texture != null)
            {
                m_ButtonTexture = texture;
                m_CurrentButtonTexture = texture;
                ConfigureTextureAtlas();
            }

            gameObject.name = "PanelButton_PrimitiveProperty_" + action;
        }

        override protected void OnButtonPressed()
        {
            if (m_PrimitivePropertyMode)
            {
                PrimitivePropertiesPanelController.ApplyAction(m_PrimitivePropertyAction);
                SketchControlsScript.m_Instance.EatGazeObjectInput();
                return;
            }

            if (WidgetManager.m_Instance.StencilsDisabled)
            {
                WidgetManager.m_Instance.StencilsDisabled = false;
            }
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new CreateWidgetCommand(
                WidgetManager.m_Instance.GetStencilPrefab(m_Type), TrTransform.FromTransform(transform), null,
                false, SelectionManager.m_Instance.SnappingGridSize, SelectionManager.m_Instance.SnappingAngle
            ));
            SketchControlsScript.m_Instance.EatGazeObjectInput();
            SelectionManager.m_Instance.RemoveFromSelection(false);
        }
    }
} // namespace TiltBrush
