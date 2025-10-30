// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;

namespace PampelGames.Shared.Construction
{
    public class ConstructionFail
    {
        public readonly FailCause failCause;

        public ConstructionFail(FailCause failCause)
        {
            this.failCause = failCause;
        }
    }
}
