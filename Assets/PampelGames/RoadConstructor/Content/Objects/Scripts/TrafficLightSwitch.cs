using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PampelGames.RoadConstructor
{
    [ExecuteAlways]
    public class TrafficLightSwitch : MonoBehaviour
    {
        public bool active;
        public List<Switcher> switchers = new();

        private int currentSwitcherIndex;
        private float timeLeft;
        private double lastTime;

        private void Awake()
        {
            foreach (var s in switchers)
            foreach (var _light in s.lights)
                _light.SetActive(false);

            if (switchers.Count > 0)
            {
                foreach (var _light in switchers[currentSwitcherIndex].lights) _light.SetActive(true);
                timeLeft = switchers[currentSwitcherIndex].duration;
                
                timeLeft = UnityEngine.Random.Range(0f, 4f); // Initial delay
            }

#if UNITY_EDITOR
            lastTime = EditorApplication.timeSinceStartup;
#endif
        }

        private void Update()
        {
            if (!active) return;

            float deltaTime;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var time = EditorApplication.timeSinceStartup;
                deltaTime = (float) (time - lastTime);
                lastTime = time;
            }
            else
            {
                deltaTime = Time.deltaTime;
            }
#else
        deltaTime = Time.deltaTime;
#endif

            timeLeft -= deltaTime;
            if (timeLeft <= 0)
            {
                if (currentSwitcherIndex >= switchers.Count) currentSwitcherIndex = 0;
                if (switchers.Count == 0) return;

                foreach (var _light in switchers[currentSwitcherIndex].lights) _light.SetActive(false);

                currentSwitcherIndex++;
                if (currentSwitcherIndex >= switchers.Count) currentSwitcherIndex = 0;

                foreach (var _light in switchers[currentSwitcherIndex].lights) _light.SetActive(true);
                timeLeft = switchers[currentSwitcherIndex].duration;
            }
        }
    }

    [Serializable]
    public class Switcher
    {
        public float duration = 1f;
        public List<GameObject> lights;
    }
}