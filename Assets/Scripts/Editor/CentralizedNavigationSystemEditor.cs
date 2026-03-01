#if UNITY_EDITOR
// ============================================================================
//  CENTRALIZED NAVIGATION SYSTEM — EDITOR  v7.0
//  ============================================================================
//  PANELS (top to bottom):
//    1. ⚡ Segment Cache Baking    — bake / clear / status of NavRouteCacheAsset
//    2. ✅ Connection Validator    — broken + duplicate connection detection
//    3. 🔍 Search Node             — inspect connections for a specific node ID
//    4. 📋 All Connections         — scrollable view of every connection
//    5. 🏔️ Snap Nodes              — raycast-snap nodes to road surface
//    6. 🚗 Node Creation           — quick-create buttons
//    7. 🔧 Graph Tools             — auto-connect, clear, demo setup, test path
//    8. Default Inspector          — all serialized fields
// ============================================================================

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(CentralizedNavigationSystem))]
public class CentralizedNavigationSystemEditor : Editor
{
    // ── Foldout state ─────────────────────────────────────────────────────────
    private bool _showBakePanel         = true;
    private bool _showValidatorPanel    = true;
    private bool _showSearchPanel       = false;
    private bool _showAllConnsPanel     = false;
    private bool _showSnapPanel         = true;

    // ── Search state ──────────────────────────────────────────────────────────
    private int     _searchNodeID              = 0;
    private Vector2 _searchScrollPos           = Vector2.zero;
    private Vector2 _allConnsScrollPos         = Vector2.zero;

    // =========================================================================
    public override void OnInspectorGUI()
    {
        CentralizedNavigationSystem nav = (CentralizedNavigationSystem)target;
        serializedObject.Update();

        DrawBakePanel(nav);
        GUILayout.Space(6);
        DrawValidatorPanel(nav);
        GUILayout.Space(6);
        DrawSearchPanel(nav);
        GUILayout.Space(6);
        DrawAllConnectionsPanel(nav);
        GUILayout.Space(6);
        DrawSnapPanel(nav);
        GUILayout.Space(6);
        DrawNodeCreationButtons(nav);
        GUILayout.Space(6);
        DrawGraphToolButtons(nav);
        GUILayout.Space(10);
        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }

    // =========================================================================
    //  1. SEGMENT CACHE BAKE PANEL
    // =========================================================================

    private void DrawBakePanel(CentralizedNavigationSystem nav)
    {
        GUI.backgroundColor = new Color(0.55f, 0.95f, 0.55f);
        _showBakePanel = EditorGUILayout.BeginFoldoutHeaderGroup(_showBakePanel, "⚡  SEGMENT CACHE BAKING");
        GUI.backgroundColor = Color.white;

        if (!_showBakePanel) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        var box = BoxStyle();
        EditorGUILayout.BeginVertical(box);

        // ── How-to info ───────────────────────────────────────────────────────
        EditorGUILayout.HelpBox(
            "Bakes NavMesh road paths between every connected node pair — once, in the editor.\n\n" +
            "HOW TO USE:\n" +
            "  1. Assets → Create → Navigation → Nav Route Cache Asset\n" +
            "  2. Assign the .asset to 'Route Cache Asset' below\n" +
            "  3. Bake your scene NavMesh  (Window → AI → Navigation → Bake)\n" +
            "  4. Press 'Bake & Save Segment Cache' — wait for progress bar\n" +
            "  5. Press Play — NPCs spawn immediately with zero startup delay\n\n" +
            "RE-BAKE NEEDED:\n" +
            "  • After NavMesh is rebuilt (road geometry changed)\n" +
            "  • After changing maxWaypointSpacing or waypointHeightOffset\n\n" +
            "NOT needed when:\n" +
            "  • Moving, adding, or removing NavNodes\n" +
            "  • Adding or removing connections between nodes",
            MessageType.Info);

        GUILayout.Space(5);

        // ── Cache status ──────────────────────────────────────────────────────
        NavRouteCacheAsset asset = nav.routeCacheAsset;

        if (asset == null)
        {
            GUI.backgroundColor = new Color(1f, 0.85f, 0.4f, 0.4f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("⚠️  No cache asset assigned", BoldLabel());
            EditorGUILayout.LabelField(
                "Create via: Assets → Create → Navigation → Nav Route Cache Asset",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
        }
        else
        {
            bool valid = asset.isValid;
            GUI.backgroundColor = valid
                ? new Color(0.3f, 1f, 0.3f, 0.25f)
                : new Color(1f, 0.45f, 0.3f, 0.25f);
            EditorGUILayout.BeginVertical(GUI.skin.box);

            var hdr = new GUIStyle(EditorStyles.boldLabel);
            hdr.normal.textColor = valid ? new Color(0.1f, 0.7f, 0.1f) : new Color(0.8f, 0.2f, 0.1f);
            EditorGUILayout.LabelField(
                valid ? "✅  CACHE VALID" : "⚠️  NOT BAKED YET", hdr);

            if (valid)
            {
                EditorGUILayout.LabelField($"   Scene:      {asset.sceneName}");
                EditorGUILayout.LabelField($"   Nodes:      {asset.nodeCount}");
                EditorGUILayout.LabelField($"   Connections:{asset.connectionCount}");
                EditorGUILayout.LabelField($"   Segments:   {asset.segmentCount}");
                EditorGUILayout.LabelField($"   Baked at:   {asset.bakedAt}");

                // Stale warnings
                if (asset.nodeCount != nav.nodes.Count)
                    EditorGUILayout.HelpBox(
                        $"Node count changed (was {asset.nodeCount}, now {nav.nodes.Count}). " +
                        "Missing segments will lazy-bake at runtime. Re-bake for best performance.",
                        MessageType.Warning);

                if (!asset.SettingsMatch(nav.maxWaypointSpacing, nav.waypointHeightOffset))
                    EditorGUILayout.HelpBox(
                        "Bake settings differ (waypointSpacing or heightOffset changed). " +
                        "Re-bake recommended for correct waypoint density.",
                        MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
        }

        GUILayout.Space(8);

        // ── Bake button ───────────────────────────────────────────────────────
        bool canBake = asset != null && nav.nodes.Count >= 2;
        GUI.enabled = canBake;

        GUI.backgroundColor = new Color(0.15f, 0.85f, 0.15f);
        if (GUILayout.Button("⚡  Bake & Save Segment Cache", BigButton(48)))
            nav.EditorBakeSegmentCache();
        GUI.backgroundColor = Color.white;
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
            GUILayout.Space(3);
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("🗑️  Clear Cache (mark invalid)", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear Segment Cache",
                    "Mark the cache as invalid?\n\n" +
                    "NPCs will fall back to runtime baking until you re-bake.\n" +
                    "This does NOT delete the .asset file.",
                    "Clear", "Cancel"))
                    nav.EditorClearSegmentCache();
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // =========================================================================
    //  2. CONNECTION VALIDATOR
    // =========================================================================

    private void DrawValidatorPanel(CentralizedNavigationSystem nav)
    {
        var broken     = new List<ConnectionDefinition>();
        var duplicates = new List<ConnectionDefinition>();

        foreach (var conn in nav.connectionDefinitions)
        {
            if (!nav.nodeMap.ContainsKey(conn.fromNodeID) || !nav.nodeMap.ContainsKey(conn.toNodeID))
                broken.Add(conn);
        }
        for (int i = 0; i < nav.connectionDefinitions.Count; i++)
        {
            var a = nav.connectionDefinitions[i];
            for (int j = i + 1; j < nav.connectionDefinitions.Count; j++)
            {
                var b = nav.connectionDefinitions[j];
                if ((a.fromNodeID == b.fromNodeID && a.toNodeID == b.toNodeID) ||
                    (a.fromNodeID == b.toNodeID   && a.toNodeID == b.fromNodeID))
                    if (!duplicates.Contains(b)) duplicates.Add(b);
            }
        }

        bool hasIssues = broken.Count > 0 || duplicates.Count > 0;
        GUI.backgroundColor = hasIssues
            ? new Color(1f, 0.3f, 0.3f, 0.3f)
            : new Color(0.3f, 1f, 0.3f, 0.3f);

        var box = BoxStyle();
        EditorGUILayout.BeginVertical(box);

        var hdr = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        hdr.normal.textColor = hasIssues ? Color.red : new Color(0.1f, 0.7f, 0.1f);
        EditorGUILayout.LabelField(
            hasIssues ? "⚠️  CONNECTION VALIDATOR — ISSUES FOUND" : "✅  CONNECTION VALIDATOR — ALL GOOD",
            hdr);

        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Connections: {nav.connectionDefinitions.Count}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Nodes: {nav.nodeMap.Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // Broken connections
        if (broken.Count > 0)
        {
            GUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f, 0.5f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"❌  Broken: {broken.Count}", EditorStyles.boldLabel);
            foreach (var c in broken.Take(5))
            {
                string f = nav.nodeMap.ContainsKey(c.fromNodeID) ? $"✓{c.fromNodeID}" : $"❌{c.fromNodeID}(MISSING)";
                string t = nav.nodeMap.ContainsKey(c.toNodeID)   ? $"✓{c.toNodeID}"   : $"❌{c.toNodeID}(MISSING)";
                EditorGUILayout.LabelField($"  {f}  {(c.bidirectional ? "⟷" : "→")}  {t}", EditorStyles.miniLabel);
            }
            if (broken.Count > 5)
                EditorGUILayout.LabelField($"  …and {broken.Count - 5} more", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
            if (GUILayout.Button($"🗑️  Delete {broken.Count} Broken Connections", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Delete Broken Connections",
                    $"Delete {broken.Count} connections with missing nodes?", "Delete", "Cancel"))
                {
                    Undo.RecordObject(nav, "Delete Broken Connections");
                    foreach (var c in broken) nav.connectionDefinitions.Remove(c);
                    nav.RefreshGraph();
                    EditorUtility.SetDirty(nav);
                }
            }
            GUI.backgroundColor = Color.white;
        }

        // Duplicate connections
        if (duplicates.Count > 0)
        {
            GUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.9f, 0.5f, 0.5f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"⚠️  Duplicates: {duplicates.Count}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.75f, 0.3f);
            if (GUILayout.Button($"🧹  Remove {duplicates.Count} Duplicates", GUILayout.Height(28)))
            {
                Undo.RecordObject(nav, "Remove Duplicate Connections");
                foreach (var c in duplicates) nav.connectionDefinitions.Remove(c);
                nav.RefreshGraph();
                EditorUtility.SetDirty(nav);
            }
            GUI.backgroundColor = Color.white;
        }

        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔍 Validate & Rebuild", GUILayout.Height(24)))
        {
            Undo.RecordObject(nav, "Validate Graph");
            nav.ValidateAndRebuildGraph();
            EditorUtility.SetDirty(nav);
        }
        if (GUILayout.Button("📊 Debug Print Connections", GUILayout.Height(24)))
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
        GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
        _showSearchPanel = EditorGUILayout.BeginFoldoutHeaderGroup(_showSearchPanel, "🔍  SEARCH NODE CONNECTIONS");
        GUI.backgroundColor = Color.white;

        if (!_showSearchPanel) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUILayout.BeginVertical(BoxStyle());

        // Search input row
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Node ID:", GUILayout.Width(55));
        _searchNodeID = EditorGUILayout.IntField(_searchNodeID, GUILayout.Width(70));
        if (GUILayout.Button("🔍 Search",    GUILayout.Width(80))) Repaint();
        if (GUILayout.Button("🎯 Focus",     GUILayout.Width(70))) FocusOnNode(nav, _searchNodeID);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        if (!nav.nodeMap.ContainsKey(_searchNodeID))
        {
            EditorGUILayout.HelpBox($"Node ID {_searchNodeID} does not exist.", MessageType.Warning);
        }
        else
        {
            NavNode node = nav.nodeMap[_searchNodeID];

            GUI.backgroundColor = new Color(0.4f, 1f, 0.4f, 0.2f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"✅  Node {_searchNodeID}  —  {node.name}", BoldLabel());
            EditorGUILayout.LabelField($"Position: {node.transform.position}");
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);

            var outgoing = nav.connectionDefinitions.Where(c => c.fromNodeID == _searchNodeID).ToList();
            var incoming = nav.connectionDefinitions.Where(c => c.toNodeID   == _searchNodeID).ToList();
            int total    = outgoing.Count + incoming.Count;

            EditorGUILayout.LabelField($"🔗 Connections: {total}  (→ {outgoing.Count}  ← {incoming.Count})", BoldLabel());
            GUILayout.Space(4);

            _searchScrollPos = EditorGUILayout.BeginScrollView(_searchScrollPos, GUILayout.MaxHeight(280));

            if (outgoing.Count > 0)
            {
                EditorGUILayout.LabelField("➡️ OUTGOING", BoldLabel());
                GUILayout.Space(2);
                foreach (var c in outgoing) DrawConnectionRow(nav, c, _searchNodeID, true);
                GUILayout.Space(8);
            }
            if (incoming.Count > 0)
            {
                EditorGUILayout.LabelField("⬅️ INCOMING", BoldLabel());
                GUILayout.Space(2);
                foreach (var c in incoming) DrawConnectionRow(nav, c, _searchNodeID, false);
            }
            if (total == 0)
                EditorGUILayout.HelpBox("No connections.", MessageType.Info);

            EditorGUILayout.EndScrollView();

            if (total > 0)
            {
                GUILayout.Space(8);
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button($"🗑️  Delete ALL {total} connections for Node {_searchNodeID}", GUILayout.Height(34)))
                {
                    if (EditorUtility.DisplayDialog("Delete All Connections",
                        $"Delete all {total} connections for Node {_searchNodeID}?",
                        "Delete All", "Cancel"))
                        DeleteAllConnectionsForNode(nav, _searchNodeID);
                }
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
        GUI.backgroundColor = new Color(1f, 0.92f, 0.7f);
        _showAllConnsPanel = EditorGUILayout.BeginFoldoutHeaderGroup(_showAllConnsPanel, "📋  VIEW ALL CONNECTIONS");
        GUI.backgroundColor = Color.white;

        if (!_showAllConnsPanel) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUILayout.BeginVertical(BoxStyle());
        EditorGUILayout.LabelField($"Total: {nav.connectionDefinitions.Count}", BoldLabel());
        GUILayout.Space(4);

        if (nav.connectionDefinitions.Count == 0)
        {
            EditorGUILayout.HelpBox("No connections.", MessageType.Info);
        }
        else
        {
            _allConnsScrollPos = EditorGUILayout.BeginScrollView(_allConnsScrollPos, GUILayout.MaxHeight(380));

            for (int i = 0; i < nav.connectionDefinitions.Count; i++)
            {
                var conn      = nav.connectionDefinitions[i];
                bool fromOk   = nav.nodeMap.ContainsKey(conn.fromNodeID);
                bool toOk     = nav.nodeMap.ContainsKey(conn.toNodeID);
                string fn     = fromOk ? nav.nodeMap[conn.fromNodeID].name : "MISSING";
                string tn     = toOk   ? nav.nodeMap[conn.toNodeID].name   : "MISSING";
                string status = (fromOk && toOk) ? "✓" : "❌";
                string arrow  = conn.bidirectional ? "⟷" : "→";

                GUI.backgroundColor = conn.bidirectional
                    ? new Color(0.4f, 1f, 0.4f, 0.15f)
                    : new Color(1f, 0.75f, 0.4f, 0.15f);

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(status,                              GUILayout.Width(18));
                EditorGUILayout.LabelField($"{conn.fromNodeID} ({fn})",         GUILayout.Width(120));
                EditorGUILayout.LabelField(arrow,                               GUILayout.Width(22));
                EditorGUILayout.LabelField($"{conn.toNodeID} ({tn})",           GUILayout.Width(120));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("👁️ F", GUILayout.Width(40), GUILayout.Height(18)))
                    FocusOnNode(nav, conn.fromNodeID);
                if (GUILayout.Button("👁️ T", GUILayout.Width(40), GUILayout.Height(18)))
                    FocusOnNode(nav, conn.toNodeID);

                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                if (GUILayout.Button("🗑️", GUILayout.Width(28), GUILayout.Height(18)))
                {
                    if (EditorUtility.DisplayDialog("Delete Connection",
                        $"Delete {conn.fromNodeID} {arrow} {conn.toNodeID}?", "Delete", "Cancel"))
                    {
                        DeleteConnection(nav, conn);
                        GUI.backgroundColor = Color.white;
                        break;
                    }
                }

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
    //  5. SNAP NODES TO ROAD SURFACE
    // =========================================================================

    private void DrawSnapPanel(CentralizedNavigationSystem nav)
    {
        GUI.backgroundColor = new Color(0.55f, 0.9f, 0.55f);
        _showSnapPanel = EditorGUILayout.BeginFoldoutHeaderGroup(_showSnapPanel, "🏔️  SNAP NODES TO ROAD SURFACE");
        GUI.backgroundColor = Color.white;

        if (!_showSnapPanel) { EditorGUILayout.EndFoldoutHeaderGroup(); return; }

        EditorGUILayout.BeginVertical(BoxStyle());

        EditorGUILayout.HelpBox(
            "Fires a downward raycast from above each node to place it exactly on the road surface.\n" +
            "Ideal for hills, slopes, and uneven terrain.\n\n" +
            "1. Set 'Snap Layer' to your Road + Terrain layers.\n" +
            "2. Increase 'Raycast Origin Height' for tall hills.\n" +
            "3. Enable 'Align To Surface' to tilt nodes with banked roads.",
            MessageType.Info);

        GUILayout.Space(5);

        var so = new SerializedObject(nav);
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("snapLayer"),
            new GUIContent("Snap Layer", "Layers to raycast against."));
        EditorGUILayout.PropertyField(so.FindProperty("snapRaycastOriginHeight"),
            new GUIContent("Raycast Origin Height", "Units above node to start the ray."));
        EditorGUILayout.PropertyField(so.FindProperty("snapNodeHeightOffset"),
            new GUIContent("Surface Height Offset", "Small Y offset after landing on surface."));
        EditorGUILayout.PropertyField(so.FindProperty("snapAlignToSurface"),
            new GUIContent("Align To Surface Normal", "Tilt node to match road slope."));
        EditorGUILayout.PropertyField(so.FindProperty("autoSnapNewNodes"),
            new GUIContent("Auto-Snap New Nodes", "Snap every newly created node to ground."));
        so.ApplyModifiedProperties();

        GUILayout.Space(8);

        GUI.backgroundColor = new Color(0.3f, 0.9f, 0.3f);
        if (GUILayout.Button("📍  SNAP ALL NODES TO ROAD SURFACE", BigButton(42)))
        {
            if (nav.nodes.Count == 0)
            {
                EditorUtility.DisplayDialog("No Nodes", "No NavNodes found in the system.", "OK");
            }
            else if (EditorUtility.DisplayDialog("Snap All Nodes",
                $"Raycast {nav.nodes.Count} node(s) to road surface?\nUndoable.", "Snap!", "Cancel"))
            {
                Undo.SetCurrentGroupName("Snap All Nodes To Ground");
                int grp = Undo.GetCurrentGroup();
                nav.SnapAllNodesToGround();
                Undo.CollapseUndoOperations(grp);
                SceneView.RepaintAll();
            }
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(3);
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("📍  Snap Selected Node Only", GUILayout.Height(28)))
        {
            NavNode sel = Selection.activeGameObject?.GetComponent<NavNode>();
            if (sel == null || sel.parentNavSystem != nav)
            {
                EditorUtility.DisplayDialog("No NavNode Selected",
                    "Select a NavNode GameObject in the Hierarchy first.", "OK");
            }
            else
            {
                Undo.SetCurrentGroupName("Snap Node To Ground");
                int grp = Undo.GetCurrentGroup();
                bool hit = nav.SnapNodeToGround(sel);
                Undo.CollapseUndoOperations(grp);
                if (hit) Debug.Log($"[NavSystem] Snapped '{sel.name}' to ground.");
                SceneView.RepaintAll();
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
        EditorGUILayout.LabelField("🚗  NODE CREATION", BoldLabel());

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🆕 Next Node (Selected)", GUILayout.Height(34)))
            nav.CreateNextNodeFromSelected();
        GUILayout.Label("Select a NavNode first!", GUILayout.Width(130));
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("➡️ Forward from Last Node", GUILayout.Height(34)))
            nav.CreateNodeForward();
    }

    // =========================================================================
    //  7. GRAPH TOOL BUTTONS
    // =========================================================================

    private void DrawGraphToolButtons(CentralizedNavigationSystem nav)
    {
        EditorGUILayout.LabelField("🔧  GRAPH TOOLS", BoldLabel());

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔗 Auto Connect",      GUILayout.Height(28))) nav.AutoConnectNodes();
        if (GUILayout.Button("🧹 Clear Connections", GUILayout.Height(28))) nav.ClearAllConnections();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🎮 Setup Demo (6 nodes)", GUILayout.Height(28))) nav.SetupDemo();
        if (GUILayout.Button("🎯 Test Path",             GUILayout.Height(28))) nav.TestPathZeroToLast();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📂 Collect All Nodes",  GUILayout.Height(24)))  nav.CollectAllNodes();
        if (GUILayout.Button("🔎 Debug Route Pool",   GUILayout.Height(24)))  nav.DebugPrintRoutePool();
        if (GUILayout.Button("🔎 Debug Segments",     GUILayout.Height(24)))  nav.DebugPrintSegmentCache();
        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    //  CONNECTION ROW (shared by search and all-connections panels)
    // =========================================================================

    private void DrawConnectionRow(CentralizedNavigationSystem nav,
                                   ConnectionDefinition conn, int currentNodeID, bool isOutgoing)
    {
        GUI.backgroundColor = conn.bidirectional
            ? new Color(0.4f, 1f, 0.4f, 0.15f)
            : new Color(1f, 0.75f, 0.4f, 0.15f);

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();

        int    otherID   = isOutgoing ? conn.toNodeID : conn.fromNodeID;
        string otherName = nav.nodeMap.ContainsKey(otherID) ? nav.nodeMap[otherID].name : "INVALID";
        string arrow     = conn.bidirectional ? "⟷" : (isOutgoing ? "→" : "←");
        string direction = conn.bidirectional ? "Bidirectional" : "One-way";

        EditorGUILayout.LabelField(arrow,                      GUILayout.Width(22));
        EditorGUILayout.LabelField($"Node {otherID}",          GUILayout.Width(70));
        EditorGUILayout.LabelField($"({otherName})",           GUILayout.Width(110));
        EditorGUILayout.LabelField($"[{direction}]",           GUILayout.Width(90));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("👁️", GUILayout.Width(28), GUILayout.Height(18)))
            FocusOnNode(nav, otherID);

        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button("🗑️", GUILayout.Width(28), GUILayout.Height(18)))
        {
            if (EditorUtility.DisplayDialog("Delete Connection",
                $"Delete: {conn.fromNodeID} {arrow} {conn.toNodeID} ({direction})?",
                "Delete", "Cancel"))
                DeleteConnection(nav, conn);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;
        GUILayout.Space(1);
    }

    // =========================================================================
    //  HELPERS
    // =========================================================================

    private void FocusOnNode(CentralizedNavigationSystem nav, int nodeID)
    {
        if (!nav.nodeMap.ContainsKey(nodeID))
        { Debug.LogWarning($"[NavSystem] Node {nodeID} not found."); return; }
        Selection.activeGameObject = nav.nodeMap[nodeID].gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private void DeleteConnection(CentralizedNavigationSystem nav, ConnectionDefinition conn)
    {
        Undo.RecordObject(nav, "Delete Connection");
        nav.connectionDefinitions.Remove(conn);
        nav.RefreshGraph();
        EditorUtility.SetDirty(nav);
    }

    private void DeleteAllConnectionsForNode(CentralizedNavigationSystem nav, int nodeID)
    {
        Undo.RecordObject(nav, "Delete All Connections For Node");
        int removed = nav.connectionDefinitions.RemoveAll(c =>
            c.fromNodeID == nodeID || c.toNodeID == nodeID);
        nav.RefreshGraph();
        EditorUtility.SetDirty(nav);
        Debug.Log($"[NavSystem] Removed {removed} connections for Node {nodeID}.");
    }

    // ── GUIStyle helpers ──────────────────────────────────────────────────────

    private static GUIStyle BoxStyle()
    {
        var s = new GUIStyle(GUI.skin.box);
        s.padding = new RectOffset(10, 10, 8, 8);
        return s;
    }

    private static GUIStyle BoldLabel() => EditorStyles.boldLabel;

    private static GUIStyle BigButton(int height = 40)
    {
        var s = new GUIStyle(GUI.skin.button)
        {
            fontSize   = 13,
            fontStyle  = FontStyle.Bold,
            fixedHeight = height,
        };
        return s;
    }
}
#endif