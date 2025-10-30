// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using PampelGames.Shared.Editor;
using PampelGames.Shared.Utility;
using UnityEditor;
using UnityEngine.UIElements;

namespace PampelGames.Shared.Construction.Editor
{
    public static class ConstructionEditorUtility
    {
        public static void CreateIntegrationsEditor(SerializedObject serializedObject, VisualElement IntegrationsParent,
            List<ConstructionBase> integrations, SerializedProperty settingsProperty)
        {
            var integrationsListView = new ListView();
            integrationsListView.name = nameof(integrationsListView);
            var integrationsProperty = serializedObject.FindProperty(nameof(integrations));
            integrationsListView.PGSetupObjectListView(integrationsProperty, integrations);
            integrationsListView.PGObjectListViewStyle();
            integrationsListView.PGSetupBindProperty(serializedObject, nameof(integrations));

            var integrationActive = new Toggle("Active");
            integrationActive.name = nameof(integrationActive);
            integrationActive.PGToggleStyleDefault();
            integrationActive.PGSetupBindPropertyRelative(settingsProperty, nameof(integrationActive));
            integrationActive.tooltip = "Enables or disables the integrations.";
            
            var integrationDetectOverlap = new Toggle("Detect Overlap");
            integrationDetectOverlap.name = nameof(integrationDetectOverlap);
            integrationDetectOverlap.PGToggleStyleDefault();
            integrationDetectOverlap.PGSetupBindPropertyRelative(settingsProperty, nameof(integrationDetectOverlap));
            integrationDetectOverlap.tooltip = "Detects overlaps to objects created by integrations which can cause construction failures.";
            
            IntegrationsParent.Add(integrationActive);
            IntegrationsParent.Add(integrationDetectOverlap);
            IntegrationsParent.Add(integrationsListView);
        }
    }
}