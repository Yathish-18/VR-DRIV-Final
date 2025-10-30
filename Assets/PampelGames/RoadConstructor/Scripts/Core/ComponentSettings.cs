// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using PampelGames.Shared.Construction;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    [Serializable]
    public class ComponentSettings
    {
        // Integrations
        public bool integrationActive = true;
        public bool integrationDetectOverlap = true;

        // Quality
        public int resolution = 5;
        public int detailResolution = 15;
        public bool smartReduce = true;
        public AddCollider addCollider = AddCollider.None;
        public int addColliderLayer;
        public string roadTag = Constants.DefaultTag;
        public List<float> lodList = new();

        // Construction
        public float baseRoadHeight = 0.1f;
        public Vector3 grid;
        public Vector3 gridOffset;
        public float snapDistance = 1f;
        public float snapHeight = 2f;
        public float snapAngleIntersection;

        public float minAngleIntersection = 45f;
        public AnimationCurve distanceRatioAngleCurve = AnimationCurve.Linear(1.5f, 90f, 4f, 0f);

        public SplineLengthUV splineLengthUV = SplineLengthUV.Cut;

        [Tooltip("Lengths of the road curve tangents.\n" + "Higher values result in smaller corners.")]
        public float tangentLength = 0.5f;

        public float intersectionDistance = 2;

        [Tooltip("The road curves can be aligned if both the start and end are connected to existing roads or intersections.\n\n" +
                 "Otherwise the connection tangents are free.")]
        public DirectConnection directConnection = DirectConnection.Align;

        public RoadEnd roadEnd = RoadEnd.Rounded;

        // Verification
        public Vector2 roadLength = new(5f, 10000f);
        public float minOverlapDistance = 1f;
        public float maxCurvature = 110f;

        // Elevation
        public LayerMask groundLayers;
        public Vector2 heightRange = new(-4, 12);
        public float elevationStartHeight = 1f;
        public float minOverlapHeight = 3f;
        public bool elevatedIntersections = true;
        public float maxSlope = 30f;
        public bool smoothSlope;

        // Undo / Demolish
        public int undoStorageSize = 5;
        public bool undoResetsTerrain = true;
        public bool demolishResetsTerrain = true;

        // Terrain
        public bool terrainSettings;
        public Terrain terrain;
        public List<Terrain> terrains = new();
        public bool removeDetails;
        public bool removeTrees;
        public bool levelHeight;
        public int slopeTextureIndex = -1;
        public float slopeTextureStrength = 0.75f;
        public int slopeSmooth = 1;

        // Traffic System
        public bool addTrafficComponent;
        public bool updateWaypoints = true;
        public Vector2 waypointDistance = new(2f, 4f);
        public DrawGizmos waypointGizmos = DrawGizmos.None;
        public DrawGizmosColor waypointGizmosColor = DrawGizmosColor.Object;
        public float waypointGizmoSize = 1f;
        public bool waypointConnectionsOnly;


        public TerrainUpdateSettings CreateTerrainUpdateSettings()
        {
            var terrainUpdateSettings = new TerrainUpdateSettings(elevationStartHeight, groundLayers, Constants.RaycastOffset(this),
                levelHeight, slopeTextureIndex, slopeTextureStrength, slopeSmooth, removeDetails, removeTrees);

            return terrainUpdateSettings;
        }
    }
}