// ---------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ---------------------------------------------------

using UnityEngine;

namespace PampelGames.Shared.Tools
{
    /// <summary>
    ///     Creates new <see cref="PGTweenDescr" />s and animates the currentValue using the specified type.
    /// </summary>
    public static class PGTween
    {
        /// <summary>
        ///     Creates a movement tween based on a start value, end value, and duration.
        /// </summary>
        /// <param name="mono">The MonoBehaviour instance the tween will be associated with. Used to run coroutine.</param>
        /// <param name="startValue">The starting value of the movement.</param>
        /// <param name="endValue">The ending value of the movement.</param>
        /// <param name="duration">The duration of the movement in seconds.</param>
        /// <param name="startTime">Offset to the start time.</param>
        /// <returns>A tween descriptor instance that represents the movement tween.</returns>
        public static PGTweenDescr Move(MonoBehaviour mono, float startValue, float endValue, float duration, float startTime = 0f)
        {
            return PGTweenSetup.SetupTween(mono, startValue, endValue, duration, false, startTime);
        }

        public static PGTweenDescr MoveFrames(MonoBehaviour mono, float startValue, float endValue, int frames, int startFrame = 0)
        {
            return PGTweenSetup.SetupTween(mono, startValue, endValue, frames, true, startFrame);
        }

        /// <summary>
        ///     Creates a shake tween.
        /// </summary>
        /// <param name="duration">The total duration in seconds.</param>
        /// <param name="fadeInOut">Normalized 0 to 1 percent of duration fade in (x) and fade out (y).</param>
        public static PGTweenDescr Shake(MonoBehaviour mono, float startValue, float duration, Vector2 fadeInOut,
            float amplitude, float frequency, float strength)
        {
            return PGTweenSetup.SetupTweenShake(mono, startValue, duration, fadeInOut, amplitude, frequency, strength);
        }

        public static PGTweenDescr Move(MonoBehaviour mono, Vector2 startValue, Vector2 endValue, float duration, float startTime = 0f)
        {
            return PGTweenSetup.SetupTween(mono, startValue, endValue, duration, false, startTime);
        }

        public static PGTweenDescr MoveFrames(MonoBehaviour mono, Vector2 startValue, Vector2 endValue, int frames, int startFrame = 0)
        {
            return PGTweenSetup.SetupTween(mono, startValue, endValue, frames, true, startFrame);
        }

        public static PGTweenDescr Shake(MonoBehaviour mono, Vector2 startValue, float duration, Vector2 fadeInOut,
            float amplitude, float frequency, Vector2 strength)
        {
            return PGTweenSetup.SetupTweenShake(mono, startValue, duration, fadeInOut, amplitude, frequency, strength);
        }

        public static PGTweenDescr Move(MonoBehaviour mono, Vector3 startValue, Vector3 endValue, float duration, float startTime = 0f)
        {
            return PGTweenSetup.SetupTween(mono, startValue, endValue, duration, false, startTime);
        }

        public static PGTweenDescr MoveFrames(MonoBehaviour mono, Vector3 startValue, Vector3 endValue, int frames, int startFrame = 0)
        {
            return PGTweenSetup.SetupTween(mono, startValue, endValue, frames, true, startFrame);
        }

        public static PGTweenDescr Shake(MonoBehaviour mono, Vector3 startValue, float duration, Vector2 fadeInOut,
            float amplitude, float frequency, Vector3 strength)
        {
            return PGTweenSetup.SetupTweenShake(mono, startValue, duration, fadeInOut, amplitude, frequency, strength);
        }

        public static PGTweenDescr Move(MonoBehaviour mono, Vector4 startValue, Vector4 endValue, float duration, float startTime = 0f)
        {
            return PGTweenSetup.SetupTween(mono, startValue, endValue, duration, false, startTime);
        }

        public static PGTweenDescr MoveFrames(MonoBehaviour mono, Vector4 startValue, Vector4 endValue, int frames, int startFrame = 0)
        {
            return PGTweenSetup.SetupTween(mono, startValue, endValue, frames, true, startFrame);
        }

        public static PGTweenDescr Shake(MonoBehaviour mono, Vector4 startValue, float duration, Vector2 fadeInOut,
            float amplitude, float frequency, Vector4 strength)
        {
            return PGTweenSetup.SetupTweenShake(mono, startValue, duration, fadeInOut, amplitude, frequency, strength);
        }

        public static PGTweenDescr Move(MonoBehaviour mono, Color startValue, Color endValue, float duration, float startTime = 0f)
        {
            return PGTweenSetup.SetupTween(mono, startValue, endValue, duration, false, startTime);
        }

        public static PGTweenDescr MoveFrames(MonoBehaviour mono, Color startValue, Color endValue, int frames, int startFrame = 0)
        {
            return PGTweenSetup.SetupTween(mono, startValue, endValue, frames, true, startFrame);
        }

        public static PGTweenDescr Shake(MonoBehaviour mono, Color startValue, float duration, Vector2 fadeInOut,
            float amplitude, float frequency, Color strength)
        {
            return PGTweenSetup.SetupTweenShake(mono, startValue, duration, fadeInOut, amplitude, frequency, strength);
        }
    }
}