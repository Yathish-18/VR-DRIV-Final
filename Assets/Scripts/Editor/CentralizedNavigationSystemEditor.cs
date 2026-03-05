#if UNITY_EDITOR
// ============================================================================
//  CENTRALIZED NAVIGATION SYSTEM EDITOR  v8.0
//  ============================================================================
//  PANELS (top to bottom):
//    1. ⚡ Route Cache Baking    — Phase 1 + Phase 2 bake, status, stale check
//    2. ✅ Connection Validator  — broken + duplicate detection / cleanup
//    3. 🔍 Search Node           — per-node connection inspector
//    4. 📋 All Connections       — scrollable full connection list
//    5. 🏔️ Snap Nodes            — raycast-snap nodes to road surface
//    6. 🚗 Node Creation         — quick-create buttons
//    7. 🔧 Graph Tools           — auto-connect, demo, test path, debug
//    8. Default Inspector        — all serialized fields
// ============================================================================

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(CentralizedNavigationSystem))]
public class CentralizedNavigationSystemEditor : Editor
{
    // ── Foldout state ─────────────────────────────────────────────────────────
    private bool _showBakePanel = true;
    private bool _showValidatorPanel = true;
    private bool _showSearchPanel = false;
    private bool _showAllConnsPanel = false;
    private bool _showSnapPanel = true;

    // ── Search state ──────────────────────────────────────────────────────────
    private int _searchNodeID = 0;
    private Vector2 _searchScrollPos = Vector2.zero;
    private Vector2 _allConnsScrollPos = Vector2.zero;

    // =========================================================================
    public override void OnInspectorGUI()
    {
        var nav = (CentralizedNavigationSystem)target;
        serializedObject.Update();

        DrawBakePanel(nav);
        Space(6);
        DrawValidatorPanel(nav);
        Space(6);
        DrawSearchPanel(nav);
        Space(6);
        DrawAllConnectionsPanel(nav);
        Space(6);
        DrawSnapPanel(nav);
        Space(6);
        DrawNodeCreationButtons(nav);
        Space(6);
        DrawGraphToolButtons(nav);
        Space(10);
        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }

    // =========================================================================
    //  1. ROUTE CACHE BAKING PANEL
    // =========================================================================

    private void DrawBakePanel(CentralizedNavigationSystem nav)
    {
        GUI.backgroundColor = new Color(0.45f, 0.9f, 0.45f);
        _showBakePanel = EditorGUILayout.BeginFoldoutHeaderGroup(_showBakePanel, "⚡  ROUTE CACHE BAKING  (Phase 1 + Phase 2)");
        GUI.backgroundColor = Color.white;

        if (!_showBakePanel) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUILayout.BeginVertical(Box());

        // ── Info ──────────────────────────────────────────────────────────────
        EditorGUILayout.HelpBox(
            "Bakes the full navigation cache in the editor — once.\n\n" +
            "Phase 1 — NavMesh.CalculatePath() for every connected node pair\n" +
            "          → dense road-surface waypoints saved to asset\n\n" +
            "Phase 2 — A* + segment stitch for every source node\n" +
            "          → complete NPC routes saved to asset\n\n" +
            "At runtime both caches load in ~10ms → NPCs spawn immediately.\n" +
            "Edge-case pool misses are handled by live compute + auto-cache.\n\n" +
            "SETUP:\n" +
            "  1. Assets → Create → Navigation → Nav Route Cache Asset\n" +
            "  2. Assign .asset to 'Route Cache Asset' below\n" +
            "  3. Bake scene NavMesh  (Window → AI → Navigation → Bake)\n" +
            "  4. Press 'Bake & Save Full Route Cache'\n" +
            "  5. Press Play — NPCs spawn with zero delay\n\n" +
            "RE-BAKE WHEN:\n" +
            "  • NavMesh rebuilt (road geometry changed)\n" +
            "  • Major graph changes (30%+ connections changed)\n" +
            "  • maxWaypointSpacing / heightOffset / routesPerNode changed\n\n" +
            "NOT needed for:\n" +
            "  • NPC speed / detection / spawn count changes\n" +
            "  • Minor node moves or adding a few connections",
            MessageType.Info);

        Space(5);

        // ── Asset status ──────────────────────────────────────────────────────
        NavRouteCacheAsset asset = nav.routeCacheAsset;

        if (asset == null)
        {
            GUI.backgroundColor = new Color(1f, 0.85f, 0.35f, 0.4f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("⚠️  No cache asset assigned", Bold());
            EditorGUILayout.LabelField(
                "Create: Assets → Create → Navigation → Nav Route Cache Asset",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
        }
        else
        {
            DrawCacheStatusBox(nav, asset);
        }

        Space(8);

        // ── Bake button ───────────────────────────────────────────────────────
        bool isBaking = CentralizedNavigationSystem.IsBakeRunning;
        bool canBake = asset != null && nav.nodes.Count >= 2 && !isBaking;
        GUI.enabled = canBake;

        if (isBaking)
        {
            GUI.backgroundColor = new Color(1f, 0.75f, 0f);
            GUILayout.Button("⏳  Baking in progress — check progress bar above...", BigBtn(52));
            GUI.backgroundColor = Color.white;
            // Force inspector to repaint so the label stays live
            EditorUtility.SetDirty(nav);
        }
        else
        {
            GUI.backgroundColor = new Color(0.1f, 0.82f, 0.1f);
            if (GUILayout.Button("⚡  Bake & Save Full Route Cache  (Phase 1 + Phase 2)", BigBtn(52)))
                nav.EditorBakeFullCache();
            GUI.backgroundColor = Color.white;
        }

        GUI.enabled = true;

        if (!canBake)
        {
            if (asset == null)
                EditorGUILayout.HelpBox("Assign a NavRouteCacheAsset to enable baking.", MessageType.Warning);
            else
                EditorGUILayout.HelpBox("Need at least 2 nodes.", MessageType.Warning);
        }

        // ── Clear button ──────────────────────────────────────────────────────
        if (asset != null && asset.isValid)
        {
            Space(3);
            GUI.backgroundColor = new Color(1f, 0.38f, 0.38f);
            if (GUILayout.Button("🗑️  Clear Cache  (mark invalid — does not delete .asset file)", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Clear Route Cache",
                    "Mark the cache as invalid?\n\n" +
                    "NPCs will fall back to runtime baking until you re-bake.\n" +
                    "The .asset file is NOT deleted.",
                    "Clear", "Cancel"))
                    nav.EditorClearCache();
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawCacheStatusBox(CentralizedNavigationSystem nav, NavRouteCacheAsset asset)
    {
        bool valid = asset.isValid;

        // Check for stale state
        string staleWarning = valid ? asset.GetStaleWarning(
            nav.nodes.Count,
            nav.connectionDefinitions.Count,
            nav.maxWaypointSpacing,
            nav.waypointHeightOffset,
            nav.routesPerSourceNode) : "";

        bool isStale = !string.IsNullOrEmpty(staleWarning);

        // Status color
        if (!valid) GUI.backgroundColor = new Color(1f, 0.35f, 0.25f, 0.3f);
        else if (isStale) GUI.backgroundColor = new Color(1f, 0.85f, 0.25f, 0.3f);
        else GUI.backgroundColor = new Color(0.2f, 1f, 0.2f, 0.25f);

        EditorGUILayout.BeginVertical(GUI.skin.box);

        // Header label
        var hdr = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        if (!valid) hdr.normal.textColor = new Color(0.85f, 0.15f, 0.1f);
        else if (isStale) hdr.normal.textColor = new Color(0.75f, 0.55f, 0.05f);
        else hdr.normal.textColor = new Color(0.05f, 0.65f, 0.05f);

        EditorGUILayout.LabelField(
            !valid ? "⚠️  NOT BAKED YET" :
            isStale ? "⚠️  CACHE MAY BE STALE" :
                          "✅  CACHE VALID",
            hdr);

        if (valid)
        {
            Space(3);
            EditorGUILayout.LabelField($"   Scene:        {asset.sceneName}");
            EditorGUILayout.LabelField($"   Nodes:        {asset.nodeCount}");
            EditorGUILayout.LabelField($"   Connections:  {asset.connectionCount}");
            EditorGUILayout.LabelField($"   Segments:     {asset.segmentCount}");
            EditorGUILayout.LabelField($"   Routes:       {asset.routeCount}  ({asset.routesPerNode} per node)");
            EditorGUILayout.LabelField($"   Baked at:     {asset.bakedAt}");

            // Estimated asset size
            float estimatedMB = (asset.segmentCount * 360f + asset.routeCount * 1800f) / (1024f * 1024f);
            EditorGUILayout.LabelField($"   Est. size:    ~{estimatedMB:F1} MB in memory");
        }

        // Stale details
        if (isStale)
        {
            Space(3);
            GUI.backgroundColor = new Color(1f, 0.9f, 0.4f, 0.4f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("Changes since last bake:", EditorStyles.boldLabel);
            foreach (var line in staleWarning.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
                EditorGUILayout.LabelField("  " + line.Trim(), EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "Edge cases will use live computation. Re-bake for full performance.",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;
    }

    // =========================================================================
    //  2. CONNECTION VALIDATOR
    // =========================================================================

    private void DrawValidatorPanel(CentralizedNavigationSystem nav)
    {
        var broken = new List<ConnectionDefinition>();
        var duplicates = new List<ConnectionDefinition>();

        foreach (var c in nav.connectionDefinitions)
            if (!nav.nodeMap.ContainsKey(c.fromNodeID) || !nav.nodeMap.ContainsKey(c.toNodeID))
                broken.Add(c);

        for (int i = 0; i < nav.connectionDefinitions.Count; i++)
        {
            var a = nav.connectionDefinitions[i];
            for (int j = i + 1; j < nav.connectionDefinitions.Count; j++)
            {
                var b = nav.connectionDefinitions[j];
                if ((a.fromNodeID == b.fromNodeID && a.toNodeID == b.toNodeID) ||
                    (a.fromNodeID == b.toNodeID && a.toNodeID == b.fromNodeID))
                    if (!duplicates.Contains(b)) duplicates.Add(b);
            }
        }

        bool issues = broken.Count > 0 || duplicates.Count > 0;

        GUI.backgroundColor = issues
            ? new Color(1f, 0.25f, 0.25f, 0.25f)
            : new Color(0.25f, 1f, 0.25f, 0.25f);

        EditorGUILayout.BeginVertical(Box());

        var hdr = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        hdr.normal.textColor = issues ? Color.red : new Color(0.05f, 0.65f, 0.05f);
        EditorGUILayout.LabelField(
            issues ? "⚠️  CONNECTION VALIDATOR — ISSUES FOUND"
                   : "✅  CONNECTION VALIDATOR — ALL GOOD", hdr);

        Space(3);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Connections: {nav.connectionDefinitions.Count}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Valid Nodes: {nav.nodeMap.Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        if (broken.Count > 0)
        {
            Space(4);
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f, 0.5f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"❌  Broken Connections: {broken.Count}", Bold());
            foreach (var c in broken.Take(5))
            {
                string f = nav.nodeMap.ContainsKey(c.fromNodeID) ? $"✓{c.fromNodeID}" : $"❌{c.fromNodeID}(MISSING)";
                string t = nav.nodeMap.ContainsKey(c.toNodeID) ? $"✓{c.toNodeID}" : $"❌{c.toNodeID}(MISSING)";
                EditorGUILayout.LabelField($"  {f}  {(c.bidirectional ? "⟷" : "→")}  {t}", EditorStyles.miniLabel);
            }
            if (broken.Count > 5) EditorGUILayout.LabelField($"  …and {broken.Count - 5} more", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.25f, 0.25f);
            if (GUILayout.Button($"🗑️  Delete {broken.Count} Broken Connections", GUILayout.Height(28)))
                if (EditorUtility.DisplayDialog("Delete Broken Connections",
                    $"Delete {broken.Count} connections with missing nodes?", "Delete", "Cancel"))
                {
                    Undo.RecordObject(nav, "Delete Broken Connections");
                    foreach (var c in broken) nav.connectionDefinitions.Remove(c);
                    nav.RefreshGraph(); EditorUtility.SetDirty(nav);
                }
            GUI.backgroundColor = Color.white;
        }

        if (duplicates.Count > 0)
        {
            Space(4);
            GUI.backgroundColor = new Color(1f, 0.9f, 0.45f, 0.5f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"⚠️  Duplicate Connections: {duplicates.Count}", Bold());
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.72f, 0.25f);
            if (GUILayout.Button($"🧹  Remove {duplicates.Count} Duplicates", GUILayout.Height(28)))
            {
                Undo.RecordObject(nav, "Remove Duplicates");
                foreach (var c in duplicates) nav.connectionDefinitions.Remove(c);
                nav.RefreshGraph(); EditorUtility.SetDirty(nav);
            }
            GUI.backgroundColor = Color.white;
        }

        Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔍 Validate & Rebuild", GUILayout.Height(24)))
        { Undo.RecordObject(nav, "Validate Graph"); nav.ValidateAndRebuildGraph(); EditorUtility.SetDirty(nav); }
        if (GUILayout.Button("📊 Print Connections", GUILayout.Height(24)))
            nav.DebugPrintAllConnections();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;
    }

    // =========================================================================
    //  3. NODE SEARCH PANEL
    // =========================================================================

    private void DrawSearchPanel(CentralizedNavigationSystem nav)
    {
        GUI.backgroundColor = new Color(0.65f, 0.88f, 1f);
        _showSearchPanel = EditorGUILayout.BeginFoldoutHeaderGroup(_showSearchPanel, "🔍  SEARCH NODE CONNECTIONS");
        GUI.backgroundColor = Color.white;

        if (!_showSearchPanel) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUILayout.BeginVertical(Box());

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Node ID:", GUILayout.Width(55));
        _searchNodeID = EditorGUILayout.IntField(_searchNodeID, GUILayout.Width(70));
        if (GUILayout.Button("🔍", GUILayout.Width(30))) Repaint();
        if (GUILayout.Button("🎯 Focus", GUILayout.Width(70))) FocusNode(nav, _searchNodeID);
        EditorGUILayout.EndHorizontal();
        Space(5);

        if (!nav.nodeMap.ContainsKey(_searchNodeID))
        {
            EditorGUILayout.HelpBox($"Node {_searchNodeID} not found.", MessageType.Warning);
        }
        else
        {
            var node = nav.nodeMap[_searchNodeID];
            GUI.backgroundColor = new Color(0.35f, 1f, 0.35f, 0.18f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"✅  Node {_searchNodeID}  —  {node.name}", Bold());
            EditorGUILayout.LabelField($"Position: {node.transform.position}");
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
            Space(6);

            var outgoing = nav.connectionDefinitions.Where(c => c.fromNodeID == _searchNodeID).ToList();
            var incoming = nav.connectionDefinitions.Where(c => c.toNodeID == _searchNodeID).ToList();
            int total = outgoing.Count + incoming.Count;

            EditorGUILayout.LabelField($"🔗 Total: {total}  (→ {outgoing.Count}  ← {incoming.Count})", Bold());
            Space(3);

            _searchScrollPos = EditorGUILayout.BeginScrollView(_searchScrollPos, GUILayout.MaxHeight(270));
            if (outgoing.Count > 0)
            {
                EditorGUILayout.LabelField("➡️ OUTGOING", Bold()); Space(2);
                foreach (var c in outgoing) DrawConnRow(nav, c, _searchNodeID, true); Space(6);
            }
            if (incoming.Count > 0)
            {
                EditorGUILayout.LabelField("⬅️ INCOMING", Bold()); Space(2);
                foreach (var c in incoming) DrawConnRow(nav, c, _searchNodeID, false);
            }
            if (total == 0) EditorGUILayout.HelpBox("No connections.", MessageType.Info);
            EditorGUILayout.EndScrollView();

            if (total > 0)
            {
                Space(6);
                GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
                if (GUILayout.Button($"🗑️  Delete ALL {total} connections for Node {_searchNodeID}", GUILayout.Height(34)))
                    if (EditorUtility.DisplayDialog("Delete All Connections",
                        $"Delete all {total} connections for Node {_searchNodeID}?", "Delete", "Cancel"))
                        DeleteAllForNode(nav, _searchNodeID);
                GUI.backgroundColor = Color.white;
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // =========================================================================
    //  4. ALL CONNECTIONS PANEL
    // =========================================================================

    private void DrawAllConnectionsPanel(CentralizedNavigationSystem nav)
    {
        GUI.backgroundColor = new Color(1f, 0.92f, 0.65f);
        _showAllConnsPanel = EditorGUILayout.BeginFoldoutHeaderGroup(_showAllConnsPanel, "📋  VIEW ALL CONNECTIONS");
        GUI.backgroundColor = Color.white;

        if (!_showAllConnsPanel) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUILayout.BeginVertical(Box());
        EditorGUILayout.LabelField($"Total: {nav.connectionDefinitions.Count}", Bold());
        Space(4);

        if (nav.connectionDefinitions.Count == 0)
        { EditorGUILayout.HelpBox("No connections.", MessageType.Info); }
        else
        {
            _allConnsScrollPos = EditorGUILayout.BeginScrollView(_allConnsScrollPos, GUILayout.MaxHeight(360));

            for (int i = 0; i < nav.connectionDefinitions.Count; i++)
            {
                var c = nav.connectionDefinitions[i];
                bool fOk = nav.nodeMap.ContainsKey(c.fromNodeID);
                bool tOk = nav.nodeMap.ContainsKey(c.toNodeID);
                string fn = fOk ? nav.nodeMap[c.fromNodeID].name : "MISSING";
                string tn = tOk ? nav.nodeMap[c.toNodeID].name : "MISSING";

                GUI.backgroundColor = c.bidirectional
                    ? new Color(0.35f, 1f, 0.35f, 0.12f)
                    : new Color(1f, 0.72f, 0.35f, 0.12f);

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((fOk && tOk) ? "✓" : "❌", GUILayout.Width(18));
                EditorGUILayout.LabelField($"{c.fromNodeID} ({fn})", GUILayout.Width(120));
                EditorGUILayout.LabelField(c.bidirectional ? "⟷" : "→", GUILayout.Width(22));
                EditorGUILayout.LabelField($"{c.toNodeID} ({tn})", GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("👁️ F", GUILayout.Width(42), GUILayout.Height(18))) FocusNode(nav, c.fromNodeID);
                if (GUILayout.Button("👁️ T", GUILayout.Width(42), GUILayout.Height(18))) FocusNode(nav, c.toNodeID);
                GUI.backgroundColor = new Color(1f, 0.28f, 0.28f);
                if (GUILayout.Button("🗑️", GUILayout.Width(28), GUILayout.Height(18)))
                    if (EditorUtility.DisplayDialog("Delete", $"Delete {c.fromNodeID} {(c.bidirectional ? "⟷" : "→")} {c.toNodeID}?", "Delete", "Cancel"))
                    { DeleteConn(nav, c); GUI.backgroundColor = Color.white; break; }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUI.backgroundColor = Color.white;
                GUILayout.Space(1);
            }
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // =========================================================================
    //  5. SNAP NODES PANEL
    // =========================================================================

    private void DrawSnapPanel(CentralizedNavigationSystem nav)
    {
        GUI.backgroundColor = new Color(0.5f, 0.88f, 0.5f);
        _showSnapPanel = EditorGUILayout.BeginFoldoutHeaderGroup(_showSnapPanel, "🏔️  SNAP NODES TO ROAD SURFACE");
        GUI.backgroundColor = Color.white;

        if (!_showSnapPanel) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUILayout.BeginVertical(Box());

        EditorGUILayout.HelpBox(
            "Fires a downward raycast from above each node to place it exactly on the road surface.\n" +
            "Set 'Snap Layer' to your Road + Terrain layers before snapping.",
            MessageType.Info);
        Space(5);

        var so = new SerializedObject(nav); so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("snapLayer"), new GUIContent("Snap Layer"));
        EditorGUILayout.PropertyField(so.FindProperty("snapRaycastOriginHeight"), new GUIContent("Raycast Origin Height"));
        EditorGUILayout.PropertyField(so.FindProperty("snapNodeHeightOffset"), new GUIContent("Surface Height Offset"));
        EditorGUILayout.PropertyField(so.FindProperty("snapAlignToSurface"), new GUIContent("Align To Surface Normal"));
        EditorGUILayout.PropertyField(so.FindProperty("autoSnapNewNodes"), new GUIContent("Auto-Snap New Nodes"));
        so.ApplyModifiedProperties();
        Space(8);

        GUI.backgroundColor = new Color(0.28f, 0.88f, 0.28f);
        if (GUILayout.Button("📍  SNAP ALL NODES TO ROAD SURFACE", BigBtn(42)))
        {
            if (nav.nodes.Count == 0) EditorUtility.DisplayDialog("No Nodes", "No nodes found.", "OK");
            else if (EditorUtility.DisplayDialog("Snap All Nodes",
                $"Snap {nav.nodes.Count} nodes downward to road surface? (Undoable)", "Snap!", "Cancel"))
            {
                Undo.SetCurrentGroupName("Snap All Nodes"); int grp = Undo.GetCurrentGroup();
                nav.SnapAllNodesToGround(); Undo.CollapseUndoOperations(grp); SceneView.RepaintAll();
            }
        }
        GUI.backgroundColor = Color.white;
        Space(3);
        GUI.backgroundColor = new Color(0.65f, 1f, 0.65f);
        if (GUILayout.Button("📍  Snap SELECTED Node Only", GUILayout.Height(28)))
        {
            var sel = Selection.activeGameObject?.GetComponent<NavNode>();
            if (sel == null || sel.parentNavSystem != nav)
                EditorUtility.DisplayDialog("No NavNode Selected", "Select a NavNode in the Hierarchy first.", "OK");
            else
            {
                Undo.SetCurrentGroupName("Snap Node"); int grp = Undo.GetCurrentGroup();
                bool hit = nav.SnapNodeToGround(sel); Undo.CollapseUndoOperations(grp);
                if (hit) Debug.Log($"[NavSystem] Snapped '{sel.name}' to ground."); SceneView.RepaintAll();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // =========================================================================
    //  6. NODE CREATION BUTTONS
    // =========================================================================

    private void DrawNodeCreationButtons(CentralizedNavigationSystem nav)
    {
        EditorGUILayout.LabelField("🚗  NODE CREATION", Bold());
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🆕 Next Node (Selected)", GUILayout.Height(34))) nav.CreateNextNodeFromSelected();
        GUILayout.Label("Select a NavNode first!", GUILayout.Width(130));
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("➡️  Forward from Last Node", GUILayout.Height(34))) nav.CreateNodeForward();
    }

    // =========================================================================
    //  7. GRAPH TOOL BUTTONS
    // =========================================================================

    private void DrawGraphToolButtons(CentralizedNavigationSystem nav)
    {
        EditorGUILayout.LabelField("🔧  GRAPH TOOLS", Bold());

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔗 Auto Connect", GUILayout.Height(28))) nav.AutoConnectNodes();
        if (GUILayout.Button("🧹 Clear Connections", GUILayout.Height(28))) nav.ClearAllConnections();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🎮 Setup Demo", GUILayout.Height(28))) nav.SetupDemo();
        if (GUILayout.Button("🎯 Test Path", GUILayout.Height(28))) nav.TestPathZeroToLast();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📂 Collect Nodes", GUILayout.Height(24))) nav.CollectAllNodes();
        if (GUILayout.Button("🔎 Route Pool", GUILayout.Height(24))) nav.DebugPrintRoutePool();
        if (GUILayout.Button("🔎 Segments", GUILayout.Height(24))) nav.DebugPrintSegmentCache();
        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    //  CONNECTION ROW  (shared between search + all-connections panels)
    // =========================================================================

    private void DrawConnRow(CentralizedNavigationSystem nav,
                             ConnectionDefinition conn, int currentNodeID, bool isOutgoing)
    {
        GUI.backgroundColor = conn.bidirectional
            ? new Color(0.35f, 1f, 0.35f, 0.12f)
            : new Color(1f, 0.72f, 0.35f, 0.12f);

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();

        int otherID = isOutgoing ? conn.toNodeID : conn.fromNodeID;
        string otherName = nav.nodeMap.ContainsKey(otherID) ? nav.nodeMap[otherID].name : "INVALID";
        string arrow = conn.bidirectional ? "⟷" : (isOutgoing ? "→" : "←");
        string dir = conn.bidirectional ? "Bidirectional" : "One-way";

        EditorGUILayout.LabelField(arrow, GUILayout.Width(22));
        EditorGUILayout.LabelField($"Node {otherID}", GUILayout.Width(70));
        EditorGUILayout.LabelField($"({otherName})", GUILayout.Width(110));
        EditorGUILayout.LabelField($"[{dir}]", GUILayout.Width(90));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("👁️", GUILayout.Width(28), GUILayout.Height(18))) FocusNode(nav, otherID);

        GUI.backgroundColor = new Color(1f, 0.28f, 0.28f);
        if (GUILayout.Button("🗑️", GUILayout.Width(28), GUILayout.Height(18)))
            if (EditorUtility.DisplayDialog("Delete Connection",
                $"Delete: {conn.fromNodeID} {arrow} {conn.toNodeID} ({dir})?", "Delete", "Cancel"))
                DeleteConn(nav, conn);
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;
        GUILayout.Space(1);
    }

    // =========================================================================
    //  HELPERS
    // =========================================================================

    private void FocusNode(CentralizedNavigationSystem nav, int id)
    {
        if (!nav.nodeMap.ContainsKey(id)) { Debug.LogWarning($"[NavSystem] Node {id} not found."); return; }
        Selection.activeGameObject = nav.nodeMap[id].gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private void DeleteConn(CentralizedNavigationSystem nav, ConnectionDefinition c)
    {
        Undo.RecordObject(nav, "Delete Connection");
        nav.connectionDefinitions.Remove(c);
        nav.RefreshGraph(); EditorUtility.SetDirty(nav);
    }

    private void DeleteAllForNode(CentralizedNavigationSystem nav, int id)
    {
        Undo.RecordObject(nav, "Delete All Connections For Node");
        int removed = nav.connectionDefinitions.RemoveAll(c => c.fromNodeID == id || c.toNodeID == id);
        nav.RefreshGraph(); EditorUtility.SetDirty(nav);
        Debug.Log($"[NavSystem] Removed {removed} connections for Node {id}.");
    }

    // ── GUIStyle helpers ──────────────────────────────────────────────────────
    private static GUIStyle Box() { var s = new GUIStyle(GUI.skin.box); s.padding = new RectOffset(10, 10, 8, 8); return s; }
    private static GUIStyle Bold() => EditorStyles.boldLabel;
    private static GUIStyle BigBtn(int h) { var s = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, fixedHeight = h }; return s; }
    private static void Space(int px) => GUILayout.Space(px);
}
#endif