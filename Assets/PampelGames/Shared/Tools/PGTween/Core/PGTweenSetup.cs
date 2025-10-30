// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;

namespace PampelGames.Shared.Tools
{
    /// <summary>
    ///     Creates and sets up a new <see cref="PGTweenDescr" />.
    /// </summary>
    internal static class PGTweenSetup
    {
        internal static PGTweenDescr SetupTween(MonoBehaviour mono, object startValue, object endValue, float duration,
            bool frameTween, float startTime)
        {
            var tween = new PGTweenDescr
            {
                currentTime = startTime,
                isFrameTween = frameTween,
                startValue = startValue,
                currentValue = startValue,
                endValue = endValue,
                duration = duration
            };

            return SetupTweenInternal(mono, tween);
        }

        internal static PGTweenDescr SetupTweenShake(MonoBehaviour mono, object startValue, float duration, Vector2 fadeInOut,
            float amplitude, float frequency, object strength)
        {
            var fadeInDuration = fadeInOut.x * duration;
            var fadeOutDuration = (1 - fadeInOut.y) * duration;
            var tween = new PGTweenDescr
            {
                startValue = startValue,
                endValue = strength,
                fadeInDuration = fadeInDuration,
                duration = duration - fadeInDuration - fadeOutDuration,
                fadeOutDuration = fadeOutDuration,
                amplitude = amplitude,
                frequency = frequency,
                noiseCoordinate = Random.Range(-100, 100),
                NormalizedFadeTime = fadeInOut.x > 0 ? 0 : 1
            };

            return SetupTweenShakeInternal(mono, tween);
        }

        /********************************************************************************************************************************/

        private static PGTweenDescr SetupTweenInternal(MonoBehaviour mono, PGTweenDescr tween)
        {
            tween.easeMethod = PGTweenEase.GetEaseMethod(PGTweenEase.Ease.Linear);
            tween.active = true;

            if (tween.startValue is float value)
            {
                tween.changeValue = (float) tween.endValue - value;
                tween.SetValueAction += PGTweenSetValue.SetFloat;
            }

            else if (tween.startValue is Vector2 vector2)
            {
                tween.changeValue = (Vector2) tween.endValue - vector2;
                tween.SetValueAction += PGTweenSetValue.SetVector2;
            }

            else if (tween.startValue is Vector3 vector3)
            {
                tween.changeValue = (Vector3) tween.endValue - vector3;
                tween.SetValueAction += PGTweenSetValue.SetVector3;
            }

            else if (tween.startValue is Vector4 vector4)
            {
                tween.changeValue = (Vector4) tween.endValue - vector4;
                tween.SetValueAction += PGTweenSetValue.SetVector4;
            }

            else if (tween.startValue is Color color)
            {
                tween.changeValue = (Color) tween.endValue - color;
                tween.SetValueAction += PGTweenSetValue.SetColor;
            }

            tween.coroutine = mono.StartCoroutine(PGTweenUpdate._TweenUpdate(tween));

            return tween;
        }

        private static PGTweenDescr SetupTweenShakeInternal(MonoBehaviour mono, PGTweenDescr tween)
        {
            tween.active = true;

            switch (tween.startValue)
            {
                case float:
                    tween.SetValueAction += PGTweenSetValue.SetFloatShake;
                    break;
                case Vector2:
                    tween.SetValueAction += PGTweenSetValue.SetVector2Shake;
                    break;
                case Vector3:
                    tween.SetValueAction += PGTweenSetValue.SetVector3Shake;
                    break;
                case Vector4:
                    tween.SetValueAction += PGTweenSetValue.SetVector4Shake;
                    break;
                case Color:
                    tween.SetValueAction += PGTweenSetValue.SetColorShake;
                    break;
            }

            tween.coroutine = mono.StartCoroutine(PGTweenUpdate._TweenUpdateShake(tween));

            return tween;
        }
    }
}