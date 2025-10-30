// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PampelGames.Shared.Utility
{
    public static class PGInformationUtility
    {
        public static string GetTimeString()
        {
            var time = DateTime.Now.TimeOfDay;
            return time.ToString(@"hh\:mm\:ss\.fff");
        }

        /// <summary>
        ///     Checks for the render pipeline that is used in the project.
        /// </summary>
        public static PGEnums.RenderPipelineEnum GetRenderPipeline()
        {
            var currentPipeline = GraphicsSettings.defaultRenderPipeline;
            if (currentPipeline == null)
                return PGEnums.RenderPipelineEnum.BuiltIn;
            if (currentPipeline.GetType().Name.Contains("UniversalRenderPipelineAsset"))
                return PGEnums.RenderPipelineEnum.URP;
            if (currentPipeline.GetType().Name.Contains("HighDefinitionRenderPipelineAsset") ||
                currentPipeline.GetType().Name.Contains("HDRenderPipelineAsset"))
                return PGEnums.RenderPipelineEnum.HDRP;
            return PGEnums.RenderPipelineEnum.BuiltIn;
        }

        /// <summary>
        ///     Get the year of the Unity version being used.
        /// </summary>
        public static string GetUnityVersionYear()
        {
            var unityVersion = CutStringAfter(Application.unityVersion, ".", true).Trim();
            return unityVersion;
        }

        private static string CutStringAfter(string value, string cutString, bool removeCutstring)
        {
            var index = value.IndexOf(cutString, StringComparison.Ordinal);
            if (index == -1) return value;
            if (!removeCutstring) return value.Substring(0, index + cutString.Length);
            return value.Substring(0, index);
        }

#if UNITY_EDITOR
        
        /********************************************************************************************************************************/
        // Instantiating GameObjects
        /********************************************************************************************************************************/

        /// <summary>
        ///     Create a primitive sphere to get visual information about a position.
        /// </summary>
        public static GameObject CreateSphere(Vector3 position, float scaleMultiplier = 0.1f)
        {
            var sphereName = "Sphere";
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
            {
                sphereName = "Position IsNAN";
                position = Vector3.zero;
            }

            if (float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            {
                sphereName = "Position IsInfinity";
                position = Vector3.zero;
            }

            var parent = GameObject.Find("SphereParent");
            if (parent == null) parent = new GameObject("SphereParent");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = position;
            sphere.transform.localScale *= scaleMultiplier;
            sphere.transform.parent = parent.transform;
            if (sphere.TryGetComponent<Collider>(out var collider))
                collider.enabled = false;
            sphere.name = sphereName;
            return sphere;
        }

        public static GameObject CreateSphere(Vector3 position, string name)
        {
            var sphere = CreateSphere(position);
            sphere.name = name;
            return sphere;
        }

        public static List<GameObject> CreateSphereBounds(Bounds bounds, string name = "")
        {
            var spheres = new List<GameObject>();

            var corners = new Vector3[8];
            corners[0] = bounds.min; // near bottom-left
            corners[1] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z); // near bottom-right
            corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z); // near top-left
            corners[3] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z); // near top-right

            corners[4] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z); // far bottom-left
            corners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z); // far bottom-right
            corners[6] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z); // far top-left
            corners[7] = bounds.max; // far top-right

            spheres.Add(CreateSphere(bounds.center, "Center _" + name));
            for (var i = 0; i < corners.Length; i++) CreateSphere(corners[i], i + "_" + name);

            return spheres;
        }

        public static void RemoveSpheres()
        {
            if (Application.isPlaying) Object.Destroy(GameObject.Find("SphereParent"));
            else Object.DestroyImmediate(GameObject.Find("SphereParent"));
        }

        public static GameObject CreateQuad(Vector3 position, Vector3 planeNormal)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.rotation = Quaternion.LookRotation(planeNormal);
            quad.transform.position = position;
            return quad;
        }

        /********************************************************************************************************************************/
        // Cast Visualizations
        /********************************************************************************************************************************/

        /// <summary>
        ///     Draws a box at the given position made of Debug.DrawLine.
        /// </summary>
        public static void DrawBox(Vector3 position, float size = 1f, Color color = default)
        {
            if (color == default) color = Color.red;

            Vector3 halfExtents = new Vector3(size * 0.5f, size * 0.5f, size * 0.5f);
            Vector3 frontTopLeft = position + new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z);
            Vector3 frontTopRight = position + new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z);
            Vector3 frontBottomLeft = position + new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
            Vector3 frontBottomRight = position + new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z);
            Vector3 backTopLeft = position + new Vector3(-halfExtents.x, halfExtents.y, halfExtents.z);
            Vector3 backTopRight = position + new Vector3(halfExtents.x, halfExtents.y, halfExtents.z);
            Vector3 backBottomLeft = position + new Vector3(-halfExtents.x, -halfExtents.y, halfExtents.z);
            Vector3 backBottomRight = position + new Vector3(halfExtents.x, -halfExtents.y, halfExtents.z);

            Debug.DrawLine(frontTopLeft, frontTopRight, color);
            Debug.DrawLine(frontTopRight, frontBottomRight, color);
            Debug.DrawLine(frontBottomRight, frontBottomLeft, color);
            Debug.DrawLine(frontBottomLeft, frontTopLeft, color);

            Debug.DrawLine(backTopLeft, backTopRight, color);
            Debug.DrawLine(backTopRight, backBottomRight, color);
            Debug.DrawLine(backBottomRight, backBottomLeft, color);
            Debug.DrawLine(backBottomLeft, backTopLeft, color);

            Debug.DrawLine(frontTopLeft, backTopLeft, color);
            Debug.DrawLine(frontTopRight, backTopRight, color);
            Debug.DrawLine(frontBottomRight, backBottomRight, color);
            Debug.DrawLine(frontBottomLeft, backBottomLeft, color);
        }
        
        /// <summary>
        ///  Draws an arrow from position to direction.
        /// </summary>
        public static void DrawArrow(Vector3 position, Vector3 direction, Color color = default)
        {
            if (color == default) color = Color.red;
    
            var length = direction.magnitude;
            if (Mathf.Approximately(length, 0f)) return;
    
            var endPoint = position + direction.normalized * length;

            Debug.DrawLine(position, endPoint, color);

            var endLineLength = length * 0.25f;
            var right = Quaternion.Euler(0, 45, 0) * direction.normalized * endLineLength;
            var left = Quaternion.Euler(0, -45, 0) * direction.normalized * endLineLength;

            Debug.DrawLine(endPoint, endPoint - right, color);
            Debug.DrawLine(endPoint, endPoint - left, color);
        }

        /// <summary>
        ///     Draws just the box at where it is currently hitting.
        /// </summary>
        public static void DrawBoxCastOnHit(Vector3 origin, Vector3 halfExtents, Vector3 direction, RaycastHit hit, Quaternion orientation, 
            Color color = default)
        {
            if (color == default) color = Color.red;
            origin = CastCenterOnCollision(origin, direction, hit.distance);
            DrawBox(origin, halfExtents, orientation, color);
        }

        /// <summary>
        ///     Draws the full box from start of cast to its end distance. Can also pass in hitInfoDistance instead of full distance.
        /// </summary>
        public static void DrawBoxCastBox(Vector3 origin, Vector3 halfExtents, Vector3 direction, float distance, Quaternion orientation, 
            Color color = default)
        {
            if (color == default) color = Color.red;
            direction.Normalize();
            var bottomBox = new Box(origin, halfExtents, orientation);
            var topBox = new Box(origin + direction * distance, halfExtents, orientation);

            Debug.DrawLine(bottomBox.backBottomLeft, topBox.backBottomLeft, color);
            Debug.DrawLine(bottomBox.backBottomRight, topBox.backBottomRight, color);
            Debug.DrawLine(bottomBox.backTopLeft, topBox.backTopLeft, color);
            Debug.DrawLine(bottomBox.backTopRight, topBox.backTopRight, color);
            Debug.DrawLine(bottomBox.frontTopLeft, topBox.frontTopLeft, color);
            Debug.DrawLine(bottomBox.frontTopRight, topBox.frontTopRight, color);
            Debug.DrawLine(bottomBox.frontBottomLeft, topBox.frontBottomLeft, color);
            Debug.DrawLine(bottomBox.frontBottomRight, topBox.frontBottomRight, color);

            DrawBox(bottomBox, color);
            DrawBox(topBox, color);
        }

        private static void DrawBox(Vector3 origin, Vector3 halfExtents, Quaternion orientation, Color color)
        {
            DrawBox(new Box(origin, halfExtents, orientation), color);
        }

        private static void DrawBox(Box box, Color color)
        {
            Debug.DrawLine(box.frontTopLeft, box.frontTopRight, color);
            Debug.DrawLine(box.frontTopRight, box.frontBottomRight, color);
            Debug.DrawLine(box.frontBottomRight, box.frontBottomLeft, color);
            Debug.DrawLine(box.frontBottomLeft, box.frontTopLeft, color);

            Debug.DrawLine(box.backTopLeft, box.backTopRight, color);
            Debug.DrawLine(box.backTopRight, box.backBottomRight, color);
            Debug.DrawLine(box.backBottomRight, box.backBottomLeft, color);
            Debug.DrawLine(box.backBottomLeft, box.backTopLeft, color);

            Debug.DrawLine(box.frontTopLeft, box.backTopLeft, color);
            Debug.DrawLine(box.frontTopRight, box.backTopRight, color);
            Debug.DrawLine(box.frontBottomRight, box.backBottomRight, color);
            Debug.DrawLine(box.frontBottomLeft, box.backBottomLeft, color);
        }

        private struct Box
        {
            private readonly Vector3 origin;

            private Vector3 localFrontTopLeft;
            private Vector3 localFrontTopRight;
            private Vector3 localFrontBottomLeft;
            private Vector3 localFrontBottomRight;
            private Vector3 localBackTopLeft => -localFrontBottomRight;
            private Vector3 localBackTopRight => -localFrontBottomLeft;
            private Vector3 localBackBottomLeft => -localFrontTopRight;
            private Vector3 localBackBottomRight => -localFrontTopLeft;

            public Vector3 frontTopLeft => localFrontTopLeft + origin;
            public Vector3 frontTopRight => localFrontTopRight + origin;
            public Vector3 frontBottomLeft => localFrontBottomLeft + origin;
            public Vector3 frontBottomRight => localFrontBottomRight + origin;
            public Vector3 backTopLeft => localBackTopLeft + origin;
            public Vector3 backTopRight => localBackTopRight + origin;
            public Vector3 backBottomLeft => localBackBottomLeft + origin;
            public Vector3 backBottomRight => localBackBottomRight + origin;


            public Box(Vector3 origin, Vector3 halfExtents, Quaternion orientation) : this(origin, halfExtents)
            {
                Rotate(orientation);
            }

            private Box(Vector3 origin, Vector3 halfExtents)
            {
                localFrontTopLeft = new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z);
                localFrontTopRight = new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z);
                localFrontBottomLeft = new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
                localFrontBottomRight = new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z);

                this.origin = origin;
            }
            private void Rotate(Quaternion orientation)
            {
                localFrontTopLeft = RotatePointAroundPivot(localFrontTopLeft, Vector3.zero, orientation);
                localFrontTopRight = RotatePointAroundPivot(localFrontTopRight, Vector3.zero, orientation);
                localFrontBottomLeft = RotatePointAroundPivot(localFrontBottomLeft, Vector3.zero, orientation);
                localFrontBottomRight = RotatePointAroundPivot(localFrontBottomRight, Vector3.zero, orientation);
            }
        }

        private static Vector3 CastCenterOnCollision(Vector3 origin, Vector3 direction, float hitInfoDistance)
        {
            return origin + direction.normalized * hitInfoDistance;
        }

        private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            var direction = point - pivot;
            return pivot + rotation * direction;
        }
#endif
    }
}